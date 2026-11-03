namespace NKaspa.Samples.AspNetCheckout.Orders;

public enum OrderStatus
{
    Pending,
    Seen,
    Confirmed,
    Underpaid,
    Expired,
}

/// <summary>One checkout order. One fresh address per order; addresses are never reused.</summary>
public sealed class Order
{
    public required string Id { get; init; }

    public required uint Index { get; init; }

    public required string Address { get; init; }

    public required ulong AmountSompi { get; init; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public ulong ReceivedSompi { get; set; }

    public ulong? FirstSeenDaaScore { get; set; }

    public ulong Confirmations { get; set; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Confirmed and expired orders are no longer watched.</summary>
    public bool IsOpen => Status is OrderStatus.Pending or OrderStatus.Seen or OrderStatus.Underpaid;

    public Order Clone() => (Order)MemberwiseClone();
}

public static class OrderStatusText
{
    public static string ToText(this OrderStatus status) => status.ToString().ToLowerInvariant();

    public static OrderStatus Parse(string text) => Enum.Parse<OrderStatus>(text, ignoreCase: true);
}
