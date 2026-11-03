using System.Globalization;
using Microsoft.Data.Sqlite;

namespace NKaspa.Samples.AspNetCheckout.Orders;

/// <summary>SQLite store, hand-written SQL. The receive index is reserved inside the same transaction as the insert.</summary>
public sealed class SqliteOrderStore : IOrderStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SqliteOrderStore(string path, uint startIndex = 0)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS orders(
              id TEXT PRIMARY KEY,
              idx INTEGER NOT NULL UNIQUE,
              address TEXT NOT NULL UNIQUE,
              amount_sompi INTEGER NOT NULL,
              received_sompi INTEGER NOT NULL,
              status TEXT NOT NULL,
              first_seen_daa INTEGER NULL,
              confirmations INTEGER NOT NULL DEFAULT 0,
              created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            INSERT OR IGNORE INTO meta(key, value) VALUES('next_index', $start);
            """;
        cmd.Parameters.AddWithValue("$start", startIndex.ToString(CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    public async Task<Order> CreateAsync(ulong amountSompi, Func<uint, string> addressForIndex, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var c = Open();
            using var tx = c.BeginTransaction();
            long next;
            using (var read = c.CreateCommand())
            {
                read.Transaction = tx;
                read.CommandText = "SELECT value FROM meta WHERE key = 'next_index'";
                next = long.Parse((string)read.ExecuteScalar()!, CultureInfo.InvariantCulture);
            }

            if (next > AddressDeriverLimits.MaxIndex)
            {
                throw new AddressIndexExhaustedException();
            }

            var index = (uint)next;
            var order = new Order
            {
                Id = Guid.NewGuid().ToString("N"),
                Index = index,
                Address = addressForIndex(index),
                AmountSompi = amountSompi,
                CreatedAt = now,
            };

            using (var bump = c.CreateCommand())
            {
                bump.Transaction = tx;
                bump.CommandText = "UPDATE meta SET value = $v WHERE key = 'next_index'";
                bump.Parameters.AddWithValue("$v", (next + 1).ToString(CultureInfo.InvariantCulture));
                bump.ExecuteNonQuery();
            }

            using (var ins = c.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = """
                    INSERT INTO orders(id, idx, address, amount_sompi, received_sompi, status, first_seen_daa, confirmations, created_at)
                    VALUES($id, $idx, $addr, $amt, 0, $st, NULL, 0, $at)
                    """;
                ins.Parameters.AddWithValue("$id", order.Id);
                ins.Parameters.AddWithValue("$idx", (long)index);
                ins.Parameters.AddWithValue("$addr", order.Address);
                ins.Parameters.AddWithValue("$amt", checked((long)amountSompi));
                ins.Parameters.AddWithValue("$st", order.Status.ToText());
                ins.Parameters.AddWithValue("$at", now.ToString("O", CultureInfo.InvariantCulture));
                ins.ExecuteNonQuery();
            }

            tx.Commit();
            return order;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task<Order?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, idx, address, amount_sompi, received_sompi, status, first_seen_daa, confirmations, created_at FROM orders WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return Task.FromResult(r.Read() ? ReadOrder(r) : null);
    }

    public Task<IReadOnlyList<Order>> ListOpenAsync(CancellationToken cancellationToken = default)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, idx, address, amount_sompi, received_sompi, status, first_seen_daa, confirmations, created_at FROM orders WHERE status IN ('pending','seen','underpaid') ORDER BY idx";
        using var r = cmd.ExecuteReader();
        var list = new List<Order>();
        while (r.Read())
        {
            list.Add(ReadOrder(r));
        }

        return Task.FromResult<IReadOnlyList<Order>>(list);
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE orders SET received_sompi = $rec, status = $st, first_seen_daa = $fs, confirmations = $conf WHERE id = $id";
            cmd.Parameters.AddWithValue("$rec", checked((long)order.ReceivedSompi));
            cmd.Parameters.AddWithValue("$st", order.Status.ToText());
            cmd.Parameters.AddWithValue("$fs", order.FirstSeenDaaScore.HasValue ? checked((long)order.FirstSeenDaaScore.Value) : DBNull.Value);
            cmd.Parameters.AddWithValue("$conf", checked((long)Math.Min(order.Confirmations, long.MaxValue)));
            cmd.Parameters.AddWithValue("$id", order.Id);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static Order ReadOrder(SqliteDataReader r) => new()
    {
        Id = r.GetString(0),
        Index = (uint)r.GetInt64(1),
        Address = r.GetString(2),
        AmountSompi = (ulong)r.GetInt64(3),
        ReceivedSompi = (ulong)r.GetInt64(4),
        Status = OrderStatusText.Parse(r.GetString(5)),
        FirstSeenDaaScore = r.IsDBNull(6) ? null : (ulong)r.GetInt64(6),
        Confirmations = (ulong)r.GetInt64(7),
        CreatedAt = DateTimeOffset.Parse(r.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
    };
}
