using System.Threading.Channels;
using Microsoft.Extensions.Options;
using NKaspa.Samples.AspNetCheckout.Orders;

namespace NKaspa.Samples.AspNetCheckout.Payments;

/// <summary>The node is reachable but cannot serve this sample (wrong network, no UTXO index, not synced).</summary>
public sealed class NodeNotUsableException : Exception
{
    public NodeNotUsableException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Watches every open order address. Notifications are the fast path, a periodic
/// getBalancesByAddresses/getUtxosByAddresses poll is the fallback.
/// NKaspa callbacks are synchronous <c>Action&lt;T&gt;</c>: they only enqueue into a channel, and this
/// single consumer does all async work. Watch-only: nothing here signs or submits.
/// </summary>
public sealed class PaymentMonitor : BackgroundService
{
    private sealed record NewAddress(string Address, string OrderId);

    private sealed record ReconnectedSignal;

    private sealed record ClosedSignal;

    private readonly IRpcFactory _factory;
    private readonly IOrderStore _store;
    private readonly CheckoutOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<PaymentMonitor> _log;
    private readonly IHostApplicationLifetime? _lifetime;
    private readonly Channel<object> _events = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });

    // Owned by the consumer loop (single reader); not touched from callbacks.
    private readonly Dictionary<string, string> _orderByAddress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<OutPoint, UtxoEntry>> _utxos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, KaspaSubscription> _subscriptions = new(StringComparer.Ordinal);
    private KaspaSubscription? _scoreSubscription;
    private ICheckoutRpc? _rpc;
    private ulong _currentScore;
    private volatile bool _connected;

    public PaymentMonitor(
        IRpcFactory factory,
        IOrderStore store,
        IOptions<CheckoutOptions> options,
        TimeProvider time,
        ILogger<PaymentMonitor> log,
        IHostApplicationLifetime? lifetime = null)
    {
        _factory = factory;
        _store = store;
        _options = options.Value;
        _time = time;
        _log = log;
        _lifetime = lifetime;
    }

    /// <summary>Connection state for /health.</summary>
    public string RpcState => _rpc is null ? "NotCreated" : _rpc.State.ToString();

    public bool IsConnected => _connected;

    public ulong CurrentScore => Interlocked.Read(ref _currentScore);

    /// <summary>Start watching a newly created order. Safe to call from any thread.</summary>
    public void Watch(Order order) => _events.Writer.TryWrite(new NewAddress(order.Address, order.Id));

    /// <summary>Connects, checks the node, subscribes all open addresses, and runs one poll.</summary>
    /// <exception cref="NodeNotUsableException">The node is on the wrong network or lacks the UTXO index or sync.</exception>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await DropConnectionAsync().ConfigureAwait(false);
        var rpc = _factory.Create();
        rpc.Reconnected += () => _events.Writer.TryWrite(new ReconnectedSignal());
        rpc.StateChanged += s =>
        {
            if (s == KaspaWebSocketState.Closed)
            {
                _events.Writer.TryWrite(new ClosedSignal());
            }
        };
        _rpc = rpc;
        await rpc.ConnectAsync(cancellationToken).ConfigureAwait(false);

        var info = await rpc.GetServerInfoAsync(cancellationToken).ConfigureAwait(false);
        if (!info.NetworkId.StartsWith("testnet", StringComparison.OrdinalIgnoreCase))
        {
            throw new NodeNotUsableException($"Node reports network '{info.NetworkId}'. This sample only runs against testnet.");
        }

        if (!info.HasUtxoIndex)
        {
            throw new NodeNotUsableException("Node does not have the UTXO index enabled (start kaspad with --utxoindex).");
        }

        if (!info.IsSynced)
        {
            throw new NodeNotUsableException("Node is not synced yet.");
        }

        SetScore(info.VirtualDaaScore);
        _connected = true;
        await LoadOpenOrdersAsync(cancellationToken).ConfigureAwait(false);
        await SubscribeAllAsync(cancellationToken).ConfigureAwait(false);
        await PollOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Processes every queued event. Returns false when the connection was reported closed.</summary>
    public async Task<bool> DrainAsync(CancellationToken cancellationToken)
    {
        var alive = true;
        while (_events.Reader.TryRead(out var item))
        {
            alive &= await HandleAsync(item, cancellationToken).ConfigureAwait(false);
        }

        return alive;
    }

    /// <summary>Fallback: refresh balances and UTXOs for all open addresses, then re-evaluate every open order.</summary>
    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        await LoadOpenOrdersAsync(cancellationToken).ConfigureAwait(false);
        var rpc = _rpc;
        if (_connected && rpc is not null && _orderByAddress.Count > 0)
        {
            if (_options.ConfirmationSource == ConfirmationSource.SinkBlueScore)
            {
                SetScore(await rpc.GetSinkBlueScoreAsync(cancellationToken).ConfigureAwait(false));
            }

            var addresses = _orderByAddress.Keys.ToList();
            var balances = await rpc.GetBalancesByAddressesAsync(addresses, cancellationToken).ConfigureAwait(false);
            var funded = new List<string>();
            foreach (var b in balances)
            {
                if (!_utxos.ContainsKey(b.Address))
                {
                    continue;
                }

                if ((b.Balance?.Sompi ?? 0) == 0)
                {
                    _utxos[b.Address].Clear();
                }
                else
                {
                    funded.Add(b.Address);
                }
            }

            if (funded.Count > 0)
            {
                var utxos = await rpc.GetUtxosByAddressesAsync(funded, cancellationToken).ConfigureAwait(false);
                foreach (var a in funded)
                {
                    _utxos[a].Clear();
                }

                foreach (var u in utxos)
                {
                    if (_utxos.TryGetValue(u.Address, out var set))
                    {
                        set[u.Outpoint] = u.Entry;
                    }
                }
            }
        }

        foreach (var address in _orderByAddress.Keys.ToList())
        {
            await EvaluateAsync(address, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(stoppingToken).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(1);
                _log.LogInformation("Connected to {Url}; watching {Count} open orders.", _options.NodeUrl, _orderByAddress.Count);
                await RunConnectedAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (NodeNotUsableException ex)
            {
                _log.LogCritical("{Message} Stopping.", ex.Message);
                _lifetime?.StopApplication();
                return;
            }
            catch (Exception ex)
            {
                _connected = false;
                _log.LogWarning("Node connection failed: {Message}. Retrying in {Delay}.", ex.Message, delay);
                try
                {
                    // Keep expiring orders while the node is away.
                    await PollOnceAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception pollEx) when (pollEx is not OperationCanceledException)
                {
                    _log.LogDebug(pollEx, "Offline poll failed.");
                }

                await Task.Delay(delay, _time, stoppingToken).ConfigureAwait(false);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, TimeSpan.FromMinutes(1).Ticks));
            }
        }

        await DropConnectionAsync().ConfigureAwait(false);
    }

    private async Task RunConnectedAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            wait.CancelAfter(_options.PollInterval);
            bool hasItem;
            try
            {
                hasItem = await _events.Reader.WaitToReadAsync(wait.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                hasItem = false;
            }

            if (hasItem)
            {
                if (!await DrainAsync(stoppingToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("Connection closed after reconnect attempts were exhausted.");
                }
            }
            else
            {
                try
                {
                    await PollOnceAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogWarning("Poll failed: {Message}", ex.Message);
                }
            }
        }
    }

    private async Task<bool> HandleAsync(object item, CancellationToken ct)
    {
        switch (item)
        {
            case RpcUtxosChanged changed:
                await OnUtxosChangedAsync(changed, ct).ConfigureAwait(false);
                return true;
            case RpcVirtualDaaScoreChanged daa:
                if (_options.ConfirmationSource == ConfirmationSource.DaaScore)
                {
                    SetScore(daa.VirtualDaaScore);
                    foreach (var address in _utxos.Where(p => p.Value.Count > 0).Select(p => p.Key).ToList())
                    {
                        await EvaluateAsync(address, ct).ConfigureAwait(false);
                    }
                }

                return true;
            case NewAddress added:
                if (!_orderByAddress.ContainsKey(added.Address))
                {
                    _orderByAddress[added.Address] = added.OrderId;
                    _utxos[added.Address] = new Dictionary<OutPoint, UtxoEntry>();
                    if (_connected)
                    {
                        await SubscribeAddressAsync(added.Address, ct).ConfigureAwait(false);
                    }
                }

                return true;
            case ReconnectedSignal:
                // The library reconnects the socket but does not resend subscriptions.
                _log.LogInformation("Reconnected; resubscribing {Count} addresses and polling.", _orderByAddress.Count);
                await ForgetSubscriptionsAsync().ConfigureAwait(false);
                _connected = true;
                await SubscribeAllAsync(ct).ConfigureAwait(false);
                await PollOnceAsync(ct).ConfigureAwait(false);
                return true;
            case ClosedSignal:
                _connected = false;
                return false;
            default:
                return true;
        }
    }

    private async Task OnUtxosChangedAsync(RpcUtxosChanged changed, CancellationToken ct)
    {
        var touched = new HashSet<string>(StringComparer.Ordinal);
        var unknownAddress = false;
        foreach (var u in changed.Removed)
        {
            if (string.IsNullOrEmpty(u.Address))
            {
                unknownAddress = true;
            }
            else if (_utxos.TryGetValue(u.Address, out var set) && set.Remove(u.Outpoint))
            {
                touched.Add(u.Address);
            }
        }

        foreach (var u in changed.Added)
        {
            if (string.IsNullOrEmpty(u.Address))
            {
                unknownAddress = true;
            }
            else if (_utxos.TryGetValue(u.Address, out var set))
            {
                set[u.Outpoint] = u.Entry;
                touched.Add(u.Address);
            }
        }

        if (unknownAddress)
        {
            // Some Borsh entries omit the address: fall back to a full poll.
            await PollOnceAsync(ct).ConfigureAwait(false);
            return;
        }

        foreach (var address in touched)
        {
            await EvaluateAsync(address, ct).ConfigureAwait(false);
        }
    }

    private async Task EvaluateAsync(string address, CancellationToken ct)
    {
        if (!_orderByAddress.TryGetValue(address, out var id))
        {
            return;
        }

        var order = await _store.GetAsync(id, ct).ConfigureAwait(false);
        if (order is null)
        {
            await StopWatchingAsync(address).ConfigureAwait(false);
            return;
        }

        var result = ConfirmationPolicy.Evaluate(
            order,
            _utxos.TryGetValue(address, out var set) ? set.Values : Array.Empty<UtxoEntry>(),
            CurrentScore,
            _options.RequiredConfirmations,
            _time.GetUtcNow(),
            _options.OrderTtl);

        if (result.Status != order.Status || result.ReceivedSompi != order.ReceivedSompi
            || result.Confirmations != order.Confirmations || result.FirstSeenDaaScore != order.FirstSeenDaaScore)
        {
            if (result.Status != order.Status)
            {
                _log.LogInformation("Order {Id}: {From} -> {To}", order.Id, order.Status.ToText(), result.Status.ToText());
            }

            order.Status = result.Status;
            order.ReceivedSompi = result.ReceivedSompi;
            order.Confirmations = result.Confirmations;
            order.FirstSeenDaaScore = result.FirstSeenDaaScore;
            await _store.UpdateAsync(order, ct).ConfigureAwait(false);
        }

        if (!order.IsOpen)
        {
            await StopWatchingAsync(address).ConfigureAwait(false);
        }
    }

    private async Task LoadOpenOrdersAsync(CancellationToken ct)
    {
        foreach (var o in await _store.ListOpenAsync(ct).ConfigureAwait(false))
        {
            if (!_orderByAddress.ContainsKey(o.Address))
            {
                _orderByAddress[o.Address] = o.Id;
                _utxos[o.Address] = new Dictionary<OutPoint, UtxoEntry>();
                if (_connected && _rpc is not null)
                {
                    await SubscribeAddressAsync(o.Address, ct).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task SubscribeAllAsync(CancellationToken ct)
    {
        var rpc = _rpc!;
        _scoreSubscription = await rpc.NotifyVirtualDaaScoreChangedAsync(e => _events.Writer.TryWrite(e), ct).ConfigureAwait(false);
        foreach (var address in _orderByAddress.Keys.ToList())
        {
            if (!_subscriptions.ContainsKey(address))
            {
                await SubscribeAddressAsync(address, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task SubscribeAddressAsync(string address, CancellationToken ct)
    {
        if (_rpc is null || _subscriptions.ContainsKey(address))
        {
            return;
        }

        _subscriptions[address] = await _rpc.NotifyUtxosChangedAsync(new[] { address }, e => _events.Writer.TryWrite(e), ct).ConfigureAwait(false);
    }

    private async Task StopWatchingAsync(string address)
    {
        _orderByAddress.Remove(address);
        _utxos.Remove(address);
        if (_subscriptions.Remove(address, out var sub))
        {
            await SafeDisposeAsync(sub).ConfigureAwait(false);
        }
    }

    private async Task ForgetSubscriptionsAsync()
    {
        var old = _subscriptions.Values.ToList();
        _subscriptions.Clear();
        var score = _scoreSubscription;
        _scoreSubscription = null;
        foreach (var s in old)
        {
            await SafeDisposeAsync(s).ConfigureAwait(false);
        }

        if (score is not null)
        {
            await SafeDisposeAsync(score).ConfigureAwait(false);
        }
    }

    private async Task DropConnectionAsync()
    {
        _connected = false;
        await ForgetSubscriptionsAsync().ConfigureAwait(false);
        var rpc = _rpc;
        _rpc = null;
        if (rpc is not null)
        {
            try
            {
                await rpc.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Dispose failed.");
            }
        }
    }

    private async Task SafeDisposeAsync(KaspaSubscription sub)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await sub.UnsubscribeAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The old socket may already be gone; the subscription died with it.
            _log.LogDebug(ex, "Unsubscribe failed.");
        }
    }

    private void SetScore(ulong value)
    {
        ulong seen;
        do
        {
            seen = Interlocked.Read(ref _currentScore);
            if (value <= seen && _options.ConfirmationSource == ConfirmationSource.DaaScore)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _currentScore, value, seen) != seen);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await DropConnectionAsync().ConfigureAwait(false);
    }
}
