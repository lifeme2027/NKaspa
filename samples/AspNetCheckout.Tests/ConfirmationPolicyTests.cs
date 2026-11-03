using NKaspa.Samples.AspNetCheckout.Orders;
using NKaspa.Samples.AspNetCheckout.Payments;
using Xunit;

namespace NKaspa.Samples.AspNetCheckout.Tests;

public class ConfirmationPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private static Order NewOrder(ulong amount = 100_000_000) => new()
    {
        Id = "o1",
        Index = 0,
        Address = Vectors.Address0,
        AmountSompi = amount,
        CreatedAt = T0,
    };

    private static UtxoEntry Entry(ulong sompi, ulong daa) => Utxo.Make(Vectors.Address0, sompi, daa).Entry;

    [Theory]
    [InlineData(1099UL, 1000UL, 100UL, false)] // threshold - 1
    [InlineData(1100UL, 1000UL, 100UL, true)]  // threshold
    [InlineData(1500UL, 1000UL, 100UL, true)]
    [InlineData(900UL, 1000UL, 100UL, false)]  // score behind block: 0
    public void Boundary(ulong current, ulong block, ulong required, bool expected)
    {
        Assert.Equal(expected, ConfirmationPolicy.IsConfirmed(current, block, required));
    }

    [Fact]
    public void Nothing_seen_is_pending_then_expired()
    {
        var o = NewOrder();
        Assert.Equal(OrderStatus.Pending, ConfirmationPolicy.Evaluate(o, Array.Empty<UtxoEntry>(), 5000, 100, T0.AddMinutes(29), Ttl).Status);
        Assert.Equal(OrderStatus.Expired, ConfirmationPolicy.Evaluate(o, Array.Empty<UtxoEntry>(), 5000, 100, T0.AddMinutes(30), Ttl).Status);
    }

    [Fact]
    public void Full_amount_shallow_is_seen_deep_is_confirmed()
    {
        var o = NewOrder();
        var u = new[] { Entry(100_000_000, 1000) };
        var seen = ConfirmationPolicy.Evaluate(o, u, 1099, 100, T0, Ttl);
        Assert.Equal(OrderStatus.Seen, seen.Status);
        Assert.Equal(99UL, seen.Confirmations);
        Assert.Equal(1000UL, seen.FirstSeenDaaScore);
        Assert.Equal(OrderStatus.Confirmed, ConfirmationPolicy.Evaluate(o, u, 1100, 100, T0, Ttl).Status);
    }

    [Fact]
    public void All_counted_utxos_must_be_deep_enough()
    {
        var o = NewOrder();
        var u = new[] { Entry(50_000_000, 1000), Entry(50_000_000, 1050) };
        var r = ConfirmationPolicy.Evaluate(o, u, 1120, 100, T0, Ttl);
        Assert.Equal(OrderStatus.Seen, r.Status);
        Assert.Equal(70UL, r.Confirmations);
    }

    [Fact]
    public void Partial_payment_is_underpaid_even_after_ttl()
    {
        var o = NewOrder();
        var r = ConfirmationPolicy.Evaluate(o, new[] { Entry(10, 1000) }, 9999, 100, T0.AddHours(2), Ttl);
        Assert.Equal(OrderStatus.Underpaid, r.Status);
        Assert.Equal(10UL, r.ReceivedSompi);
    }

    [Fact]
    public void Confirmed_is_terminal()
    {
        var o = NewOrder();
        o.Status = OrderStatus.Confirmed;
        o.ReceivedSompi = 100_000_000;
        Assert.Equal(OrderStatus.Confirmed, ConfirmationPolicy.Evaluate(o, Array.Empty<UtxoEntry>(), 0, 100, T0.AddDays(1), Ttl).Status);
    }
}
