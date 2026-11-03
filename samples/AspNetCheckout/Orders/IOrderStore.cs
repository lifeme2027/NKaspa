namespace NKaspa.Samples.AspNetCheckout.Orders;

/// <summary>Thrown when the next receive index would be hardened.</summary>
public sealed class AddressIndexExhaustedException : Exception
{
    public AddressIndexExhaustedException()
        : base("No non-hardened receive index is left.")
    {
    }
}

public interface IOrderStore
{
    /// <summary>
    /// Reserves the next receive index and stores a new pending order in one step, so an index is never reused.
    /// </summary>
    /// <exception cref="AddressIndexExhaustedException">The index would reach 0x80000000.</exception>
    Task<Order> CreateAsync(ulong amountSompi, Func<uint, string> addressForIndex, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<Order?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Orders still being watched: pending, seen, underpaid.</summary>
    Task<IReadOnlyList<Order>> ListOpenAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}
