using NKaspa.Samples.AspNetCheckout.Orders;

namespace NKaspa.Samples.AspNetCheckout.Payments;

/// <summary>Result of evaluating one order against the UTXOs currently seen on its address.</summary>
public readonly record struct OrderEvaluation(OrderStatus Status, ulong ReceivedSompi, ulong Confirmations, ulong? FirstSeenDaaScore);

/// <summary>
/// Pure confirmation rules. NKaspa has no "confirmations for this transaction" call, so the sample
/// computes <c>currentScore - UtxoEntry.BlockDaaScore</c> itself.
/// </summary>
public static class ConfirmationPolicy
{
    /// <summary>Score distance. Zero when the current score is not ahead of the block.</summary>
    public static ulong Confirmations(ulong currentScore, ulong blockDaaScore)
        => currentScore > blockDaaScore ? currentScore - blockDaaScore : 0;

    public static bool IsConfirmed(ulong currentScore, ulong blockDaaScore, ulong required)
        => Confirmations(currentScore, blockDaaScore) >= required;

    /// <summary>
    /// pending → seen (amount reached, not deep enough) → confirmed. Some but not enough: underpaid.
    /// Nothing received after the TTL: expired. Confirmed stays confirmed.
    /// </summary>
    public static OrderEvaluation Evaluate(
        Order order,
        IReadOnlyCollection<UtxoEntry> utxos,
        ulong currentScore,
        ulong requiredConfirmations,
        DateTimeOffset now,
        TimeSpan orderTtl)
    {
        if (order.Status == OrderStatus.Confirmed || order.Status == OrderStatus.Expired)
        {
            return new OrderEvaluation(order.Status, order.ReceivedSompi, order.Confirmations, order.FirstSeenDaaScore);
        }

        ulong received = 0;
        ulong minConf = ulong.MaxValue;
        ulong? firstSeen = order.FirstSeenDaaScore;
        foreach (var u in utxos)
        {
            received = checked(received + u.Amount.Sompi);
            minConf = Math.Min(minConf, Confirmations(currentScore, u.BlockDaaScore));
            if (firstSeen is null || u.BlockDaaScore < firstSeen)
            {
                firstSeen = u.BlockDaaScore;
            }
        }

        if (received == 0)
        {
            var status = now - order.CreatedAt >= orderTtl ? OrderStatus.Expired : OrderStatus.Pending;
            return new OrderEvaluation(status, 0, 0, order.FirstSeenDaaScore);
        }

        if (received < order.AmountSompi)
        {
            return new OrderEvaluation(OrderStatus.Underpaid, received, minConf, firstSeen);
        }

        var final = minConf >= requiredConfirmations ? OrderStatus.Confirmed : OrderStatus.Seen;
        return new OrderEvaluation(final, received, minConf, firstSeen);
    }
}
