using NKaspa.Samples.AspNetCheckout.Addresses;
using NKaspa.Samples.AspNetCheckout.Orders;
using Xunit;

namespace NKaspa.Samples.AspNetCheckout.Tests;

public class OrderStoreTests
{
    private static readonly AddressDeriver Deriver = new(Vectors.Ktub);

    public static IEnumerable<object[]> Stores()
    {
        yield return new object[] { "memory" };
        yield return new object[] { "sqlite" };
    }

    private static IOrderStore Make(string kind, uint start = 0) => kind == "memory"
        ? new InMemoryOrderStore(start)
        : new SqliteOrderStore(Path.Combine(Path.GetTempPath(), "nkaspa-checkout-" + Guid.NewGuid().ToString("N") + ".db"), start);

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task Fifty_concurrent_creates_never_reuse_an_index(string kind)
    {
        var store = Make(kind);
        var orders = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => store.CreateAsync(1, Deriver.Derive, DateTimeOffset.UtcNow))));
        Assert.Equal(50, orders.Select(o => o.Index).Distinct().Count());
        Assert.Equal(50, orders.Select(o => o.Address).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 50).Select(i => (uint)i), orders.Select(o => o.Index).OrderBy(i => i));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task Round_trips_and_lists_open_only(string kind)
    {
        var store = Make(kind, 5);
        var a = await store.CreateAsync(123, Deriver.Derive, DateTimeOffset.UtcNow);
        var b = await store.CreateAsync(456, Deriver.Derive, DateTimeOffset.UtcNow);
        Assert.Equal(5u, a.Index);
        Assert.Equal(Vectors.Address5, a.Address);

        b.Status = OrderStatus.Confirmed;
        b.ReceivedSompi = 456;
        b.FirstSeenDaaScore = 77;
        b.Confirmations = 100;
        await store.UpdateAsync(b);

        var got = (await store.GetAsync(b.Id))!;
        Assert.Equal(OrderStatus.Confirmed, got.Status);
        Assert.Equal(77UL, got.FirstSeenDaaScore);
        Assert.Equal(100UL, got.Confirmations);
        Assert.Equal(a.Id, Assert.Single(await store.ListOpenAsync()).Id);
        Assert.Null(await store.GetAsync("missing"));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task Stops_at_hardened_boundary(string kind)
    {
        var store = Make(kind, 0x7FFFFFFF);
        await store.CreateAsync(1, Deriver.Derive, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<AddressIndexExhaustedException>(() => store.CreateAsync(1, Deriver.Derive, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Sqlite_index_survives_restart()
    {
        var path = Path.Combine(Path.GetTempPath(), "nkaspa-checkout-" + Guid.NewGuid().ToString("N") + ".db");
        var first = await new SqliteOrderStore(path).CreateAsync(1, Deriver.Derive, DateTimeOffset.UtcNow);
        var second = await new SqliteOrderStore(path).CreateAsync(1, Deriver.Derive, DateTimeOffset.UtcNow);
        Assert.Equal(first.Index + 1, second.Index);
    }
}
