namespace NKaspa.Samples.AspNetCheckout.Orders;

/// <summary>In-memory store for tests and diskless demos. Everything is lost on restart.</summary>
public sealed class InMemoryOrderStore : IOrderStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Order> _orders = new(StringComparer.Ordinal);
    private long _nextIndex;

    public InMemoryOrderStore(uint startIndex = 0)
    {
        _nextIndex = startIndex;
    }

    public Task<Order> CreateAsync(ulong amountSompi, Func<uint, string> addressForIndex, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_nextIndex > AddressDeriverLimits.MaxIndex)
            {
                throw new AddressIndexExhaustedException();
            }

            var index = (uint)_nextIndex;
            var order = new Order
            {
                Id = Guid.NewGuid().ToString("N"),
                Index = index,
                Address = addressForIndex(index),
                AmountSompi = amountSompi,
                CreatedAt = now,
            };
            _nextIndex++;
            _orders[order.Id] = order;
            return Task.FromResult(order.Clone());
        }
    }

    public Task<Order?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_orders.TryGetValue(id, out var o) ? o.Clone() : null);
        }
    }

    public Task<IReadOnlyList<Order>> ListOpenAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Order> list = _orders.Values.Where(o => o.IsOpen).Select(o => o.Clone()).ToList();
            return Task.FromResult(list);
        }
    }

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _orders[order.Id] = order.Clone();
        }

        return Task.CompletedTask;
    }
}
