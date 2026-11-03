namespace NKaspa.Samples.AspNetCheckout.Payments;

/// <summary>
/// The slice of the node connection this sample uses. <c>IKaspaRpcClient</c> does not carry
/// <c>ConnectAsync</c>, <c>State</c>, <c>StateChanged</c> or <c>Reconnected</c> (they live on
/// <c>KaspaRpcConnection</c>), so the sample wraps them here and tests can fake all of it.
/// No transaction submission is exposed: this service is watch-only.
/// </summary>
public interface ICheckoutRpc : IAsyncDisposable
{
    KaspaWebSocketState State { get; }

    event Action<KaspaWebSocketState>? StateChanged;

    /// <summary>Raised after an automatic reconnect. Subscriptions are not restored by the library.</summary>
    event Action? Reconnected;

    Task ConnectAsync(CancellationToken cancellationToken);

    Task<RpcServerInfo> GetServerInfoAsync(CancellationToken cancellationToken);

    Task<ulong> GetSinkBlueScoreAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RpcUtxo>> GetUtxosByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken);

    Task<IReadOnlyList<RpcAddressBalance>> GetBalancesByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken);

    Task<KaspaSubscription> NotifyUtxosChangedAsync(IReadOnlyList<string> addresses, Action<RpcUtxosChanged> onChanged, CancellationToken cancellationToken);

    Task<KaspaSubscription> NotifyVirtualDaaScoreChangedAsync(Action<RpcVirtualDaaScoreChanged> onChanged, CancellationToken cancellationToken);
}

/// <summary>Creates a fresh connection. Called again after the library gives up reconnecting.</summary>
public interface IRpcFactory
{
    ICheckoutRpc Create();
}

/// <summary>Real factory: <c>KaspaRpcClient.Create</c> returns a <c>KaspaRpcConnection</c>, not a "KaspaRpcClient" instance.</summary>
public sealed class KaspaRpcFactory : IRpcFactory
{
    private readonly string _url;

    public KaspaRpcFactory(string url)
    {
        _url = url;
    }

    public ICheckoutRpc Create()
    {
        var options = new KaspaWebSocketOptions
        {
            AutoReconnect = true,
            MaxReconnectAttempts = 10,
            ReconnectInitialDelay = TimeSpan.FromMilliseconds(500),
            ReconnectMaxDelay = TimeSpan.FromSeconds(15),
        };
        return new KaspaCheckoutRpc(KaspaRpcClient.Create(_url, KaspaRpcEncoding.Auto, options));
    }
}

/// <summary>Adapter over <see cref="KaspaRpcConnection"/>.</summary>
public sealed class KaspaCheckoutRpc : ICheckoutRpc
{
    private readonly KaspaRpcConnection _connection;

    public KaspaCheckoutRpc(KaspaRpcConnection connection)
    {
        _connection = connection;
        _connection.StateChanged += s => StateChanged?.Invoke(s);
        _connection.Reconnected += () => Reconnected?.Invoke();
    }

    public KaspaWebSocketState State => _connection.State;

    public event Action<KaspaWebSocketState>? StateChanged;

    public event Action? Reconnected;

    public Task ConnectAsync(CancellationToken cancellationToken) => _connection.ConnectAsync(cancellationToken);

    public Task<RpcServerInfo> GetServerInfoAsync(CancellationToken cancellationToken) => _connection.GetServerInfoAsync(cancellationToken);

    public Task<ulong> GetSinkBlueScoreAsync(CancellationToken cancellationToken) => _connection.GetSinkBlueScoreAsync(cancellationToken);

    public Task<IReadOnlyList<RpcUtxo>> GetUtxosByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken)
        => _connection.GetUtxosByAddressesAsync(addresses, cancellationToken);

    public Task<IReadOnlyList<RpcAddressBalance>> GetBalancesByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken)
        => _connection.GetBalancesByAddressesAsync(addresses, cancellationToken);

    public Task<KaspaSubscription> NotifyUtxosChangedAsync(IReadOnlyList<string> addresses, Action<RpcUtxosChanged> onChanged, CancellationToken cancellationToken)
        => _connection.NotifyUtxosChangedAsync(addresses, onChanged, cancellationToken);

    public Task<KaspaSubscription> NotifyVirtualDaaScoreChangedAsync(Action<RpcVirtualDaaScoreChanged> onChanged, CancellationToken cancellationToken)
        => _connection.NotifyVirtualDaaScoreChangedAsync(onChanged, cancellationToken);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
