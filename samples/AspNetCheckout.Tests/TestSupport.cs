using NKaspa;
using NKaspa.RPC;
using NKaspa.Samples.AspNetCheckout.Payments;

namespace NKaspa.Samples.AspNetCheckout.Tests;

/// <summary>Public, watch-only test vector. Generated from throwaway random entropy that was discarded; only the account public key is kept.</summary>
internal static class Vectors
{
    public const string Ktub = "ktub23ATyNPJw3DLg8b9KayCqZvPkYc5VfzkdGfYPvChH9w4MGwZxKEm4Y3HDxbEFqjdcWAGtTFUrd5w5BB6ck8osqsfuksSiSx8xDBA5kHpRBx";

    /// <summary>The same public key encoded with the mainnet version.</summary>
    public const string Kpub = "kpub2JUYHdXdy4VRW5NHVpnKWp7SVtirbRFik2NosktP7Mn5eyvAHUK7VKBFSXVEAtN9miXovgJtqXw1N6gY6QeAQohJuMRj29ejNTzbUsBMaV9";

    public const string Address0 = "kaspatest:qzq6f6glw4xlauwnewmq7t0ymud4ulkxm703ln8esg34h52de9kh6xqeas53y";
    public const string Address1 = "kaspatest:qq5uhryps58rqtpkgvcaqeqsml7pldtlfu9yt8qaska7y67r2a58ztyk7qpxe";
    public const string Address5 = "kaspatest:qzrrx94k225lrmjt8vv39dvfmc5s6kr0ad92e29r7y8wjp6c70gh2hshqusjr";
}

internal sealed class ManualTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Offline fake node. Captures callbacks so tests can raise notifications by hand.</summary>
internal sealed class FakeRpc : ICheckoutRpc
{
    public string NetworkId { get; set; } = "testnet-10";

    public bool HasUtxoIndex { get; set; } = true;

    public bool IsSynced { get; set; } = true;

    public ulong VirtualDaa { get; set; } = 1_000;

    /// <summary>What getUtxosByAddresses / getBalancesByAddresses return.</summary>
    public List<RpcUtxo> NodeUtxos { get; } = new();

    public List<(IReadOnlyList<string> Addresses, Action<RpcUtxosChanged> Callback)> UtxoSubscriptions { get; } = new();

    public List<Action<RpcVirtualDaaScoreChanged>> ScoreSubscriptions { get; } = new();

    public int Unsubscribes;

    public KaspaWebSocketState State { get; set; } = KaspaWebSocketState.Closed;

    public event Action<KaspaWebSocketState>? StateChanged;

    public event Action? Reconnected;

    public void RaiseReconnected() => Reconnected?.Invoke();

    public void RaiseClosed()
    {
        State = KaspaWebSocketState.Closed;
        StateChanged?.Invoke(State);
    }

    public void RaiseUtxos(RpcUtxosChanged e)
    {
        foreach (var s in UtxoSubscriptions.ToList())
        {
            s.Callback(e);
        }
    }

    public void RaiseScore(ulong score)
    {
        VirtualDaa = score;
        foreach (var s in ScoreSubscriptions.ToList())
        {
            s(new RpcVirtualDaaScoreChanged(score));
        }
    }

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        State = KaspaWebSocketState.Open;
        return Task.CompletedTask;
    }

    public Task<RpcServerInfo> GetServerInfoAsync(CancellationToken cancellationToken)
        => Task.FromResult(new RpcServerInfo(1, 0, "fake", NetworkId, HasUtxoIndex, IsSynced, VirtualDaa));

    public Task<ulong> GetSinkBlueScoreAsync(CancellationToken cancellationToken) => Task.FromResult(VirtualDaa);

    public Task<IReadOnlyList<RpcUtxo>> GetUtxosByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RpcUtxo>>(NodeUtxos.Where(u => addresses.Contains(u.Address)).ToList());

    public Task<IReadOnlyList<RpcAddressBalance>> GetBalancesByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RpcAddressBalance>>(addresses
            .Select(a => new RpcAddressBalance(a, NodeUtxos.Any(u => u.Address == a) ? Money.FromSompi(NodeUtxos.Where(u => u.Address == a).Aggregate(0UL, (s, u) => s + u.Entry.Amount.Sompi)) : null))
            .ToList());

    public Task<KaspaSubscription> NotifyUtxosChangedAsync(IReadOnlyList<string> addresses, Action<RpcUtxosChanged> onChanged, CancellationToken cancellationToken)
    {
        var entry = (addresses, onChanged);
        UtxoSubscriptions.Add(entry);
        return Task.FromResult(new KaspaSubscription(_ =>
        {
            UtxoSubscriptions.Remove(entry);
            Interlocked.Increment(ref Unsubscribes);
            return Task.CompletedTask;
        }));
    }

    public Task<KaspaSubscription> NotifyVirtualDaaScoreChangedAsync(Action<RpcVirtualDaaScoreChanged> onChanged, CancellationToken cancellationToken)
    {
        ScoreSubscriptions.Add(onChanged);
        return Task.FromResult(new KaspaSubscription(_ =>
        {
            ScoreSubscriptions.Remove(onChanged);
            return Task.CompletedTask;
        }));
    }

    /// <summary>Drops every subscription without unsubscribing, as a dropped socket would.</summary>
    public void LoseSubscriptions()
    {
        UtxoSubscriptions.Clear();
        ScoreSubscriptions.Clear();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeRpcFactory : IRpcFactory
{
    public FakeRpc Rpc { get; set; } = new();

    public int Created;

    public ICheckoutRpc Create()
    {
        Created++;
        return Rpc;
    }
}

internal static class Utxo
{
    private static uint _n;

    public static RpcUtxo Make(string address, ulong sompi, ulong blockDaa)
    {
        var id = Interlocked.Increment(ref _n);
        var entry = new UtxoEntry(sompi, new ScriptPubKey(0, new byte[34]), blockDaa, false);
        return new RpcUtxo(address, new OutPoint(new uint256(id), 0), entry);
    }
}
