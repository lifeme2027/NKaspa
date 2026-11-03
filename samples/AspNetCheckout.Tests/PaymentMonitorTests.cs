using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NKaspa.RPC;
using NKaspa.Samples.AspNetCheckout.Addresses;
using NKaspa.Samples.AspNetCheckout.Orders;
using NKaspa.Samples.AspNetCheckout.Payments;
using Xunit;

namespace NKaspa.Samples.AspNetCheckout.Tests;

public class PaymentMonitorTests
{
    private readonly FakeRpcFactory _factory = new();
    private readonly InMemoryOrderStore _store = new();
    private readonly ManualTime _time = new();
    private readonly AddressDeriver _deriver = new(Vectors.Ktub);
    private readonly CheckoutOptions _options = new() { NodeUrl = "ws://127.0.0.1:17210", Ktub = Vectors.Ktub, RequiredConfirmations = 100 };

    private FakeRpc Rpc => _factory.Rpc;

    private PaymentMonitor NewMonitor() =>
        new(_factory, _store, Options.Create(_options), _time, NullLogger<PaymentMonitor>.Instance);

    private Task<Order> NewOrderAsync(ulong sompi = 100_000_000) => _store.CreateAsync(sompi, _deriver.Derive, _time.GetUtcNow());

    private async Task<OrderStatus> StatusAsync(Order o) => (await _store.GetAsync(o.Id))!.Status;

    [Fact]
    public async Task Notification_marks_seen_then_score_marks_confirmed()
    {
        var order = await NewOrderAsync();
        var m = NewMonitor();
        await m.ConnectAsync(default);
        Assert.Single(Rpc.UtxoSubscriptions);
        Assert.Equal(order.Address, Rpc.UtxoSubscriptions[0].Addresses.Single());

        Rpc.RaiseUtxos(new RpcUtxosChanged(new[] { Utxo.Make(order.Address, 100_000_000, 1000) }, null));
        await m.DrainAsync(default);
        Assert.Equal(OrderStatus.Seen, await StatusAsync(order));

        Rpc.RaiseScore(1099);
        await m.DrainAsync(default);
        Assert.Equal(OrderStatus.Seen, await StatusAsync(order));

        Rpc.RaiseScore(1100);
        await m.DrainAsync(default);
        var done = (await _store.GetAsync(order.Id))!;
        Assert.Equal(OrderStatus.Confirmed, done.Status);
        Assert.Equal(100UL, done.Confirmations);
        Assert.Empty(Rpc.UtxoSubscriptions); // terminal order is unsubscribed
    }

    [Fact]
    public async Task Short_payment_is_underpaid_and_topup_completes()
    {
        var order = await NewOrderAsync();
        var m = NewMonitor();
        await m.ConnectAsync(default);
        Rpc.RaiseUtxos(new RpcUtxosChanged(new[] { Utxo.Make(order.Address, 40_000_000, 1000) }, null));
        await m.DrainAsync(default);
        Assert.Equal(OrderStatus.Underpaid, await StatusAsync(order));

        Rpc.RaiseUtxos(new RpcUtxosChanged(new[] { Utxo.Make(order.Address, 60_000_000, 1001) }, null));
        await m.DrainAsync(default);
        Assert.Equal(OrderStatus.Seen, await StatusAsync(order));
    }

    [Fact]
    public async Task Removed_utxo_rolls_back()
    {
        var order = await NewOrderAsync();
        var m = NewMonitor();
        await m.ConnectAsync(default);
        var u = Utxo.Make(order.Address, 100_000_000, 1000);
        Rpc.RaiseUtxos(new RpcUtxosChanged(new[] { u }, null));
        await m.DrainAsync(default);
        Assert.Equal(OrderStatus.Seen, await StatusAsync(order));

        Rpc.RaiseUtxos(new RpcUtxosChanged(null, new[] { u }));
        await m.DrainAsync(default);
        var o = (await _store.GetAsync(order.Id))!;
        Assert.Equal(OrderStatus.Pending, o.Status);
        Assert.Equal(0UL, o.ReceivedSompi);
    }

    [Fact]
    public async Task Poll_confirms_without_any_notification()
    {
        var order = await NewOrderAsync();
        var m = NewMonitor();
        await m.ConnectAsync(default);
        Rpc.NodeUtxos.Add(Utxo.Make(order.Address, 100_000_000, 500));
        await m.PollOnceAsync(default); // current score is still 1000 from getServerInfo
        Assert.Equal(OrderStatus.Confirmed, await StatusAsync(order));
    }

    [Fact]
    public async Task Ignores_other_addresses()
    {
        var order = await NewOrderAsync();
        var m = NewMonitor();
        await m.ConnectAsync(default);
        Rpc.RaiseUtxos(new RpcUtxosChanged(new[] { Utxo.Make(Vectors.Address5, 100_000_000, 1000) }, null));
        await m.DrainAsync(default);
        Assert.Equal(OrderStatus.Pending, await StatusAsync(order));
    }

    [Fact]
    public async Task New_order_after_connect_gets_its_own_subscription()
    {
        var m = NewMonitor();
        await m.ConnectAsync(default);
        Assert.Empty(Rpc.UtxoSubscriptions);
        var order = await NewOrderAsync();
        m.Watch(order);
        await m.DrainAsync(default);
        Assert.Single(Rpc.UtxoSubscriptions);
        Assert.Equal(order.Address, Rpc.UtxoSubscriptions[0].Addresses.Single());
    }

    [Fact]
    public async Task Reconnect_resubscribes_all_open_addresses_and_catches_up()
    {
        var a = await NewOrderAsync();
        var b = await NewOrderAsync();
        var m = NewMonitor();
        await m.ConnectAsync(default);
        Assert.Equal(2, Rpc.UtxoSubscriptions.Count);

        // Socket drops: the node forgets our subscriptions. A payment lands while we are away.
        Rpc.LoseSubscriptions();
        Rpc.NodeUtxos.Add(Utxo.Make(b.Address, 100_000_000, 1000));
        Rpc.RaiseReconnected();
        await m.DrainAsync(default);

        Assert.Equal(2, Rpc.UtxoSubscriptions.Count);
        Assert.Equal(new[] { a.Address, b.Address }.OrderBy(x => x), Rpc.UtxoSubscriptions.Select(s => s.Addresses.Single()).OrderBy(x => x));
        Assert.Single(Rpc.ScoreSubscriptions);
        Assert.Equal(OrderStatus.Seen, await StatusAsync(b));
        Assert.Equal(OrderStatus.Pending, await StatusAsync(a));
    }

    [Fact]
    public async Task Closed_state_is_reported()
    {
        var m = NewMonitor();
        await m.ConnectAsync(default);
        Rpc.RaiseClosed();
        Assert.False(await m.DrainAsync(default));
        Assert.False(m.IsConnected);
    }

    [Fact]
    public async Task Unpaid_order_expires_on_poll()
    {
        var order = await NewOrderAsync();
        var m = NewMonitor();
        await m.ConnectAsync(default);
        _time.Now = _time.Now.AddMinutes(31);
        await m.PollOnceAsync(default);
        Assert.Equal(OrderStatus.Expired, await StatusAsync(order));
        Assert.Empty(Rpc.UtxoSubscriptions);
    }

    [Theory]
    [InlineData("mainnet", true, true)]
    [InlineData("testnet-10", false, true)]
    [InlineData("testnet-10", true, false)]
    public async Task Refuses_unusable_node(string network, bool utxoIndex, bool synced)
    {
        Rpc.NetworkId = network;
        Rpc.HasUtxoIndex = utxoIndex;
        Rpc.IsSynced = synced;
        await Assert.ThrowsAsync<NodeNotUsableException>(() => NewMonitor().ConnectAsync(default));
    }

    [Fact]
    public async Task Sink_blue_score_mode_uses_sink_score_on_poll()
    {
        _options.ConfirmationSource = ConfirmationSource.SinkBlueScore;
        var order = await NewOrderAsync();
        var m = NewMonitor();
        await m.ConnectAsync(default);
        Rpc.NodeUtxos.Add(Utxo.Make(order.Address, 100_000_000, 1000));
        Rpc.VirtualDaa = 1200;
        await m.PollOnceAsync(default);
        Assert.Equal(OrderStatus.Confirmed, await StatusAsync(order));
    }
}
