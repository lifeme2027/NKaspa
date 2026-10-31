namespace NKaspa
{
    /// <summary>
    /// Previous output a transaction input spends.
    /// Kaspa counterpart of NBitcoin.OutPoint. The index is a u32.
    /// </summary>
    public sealed class OutPoint : IEquatable<OutPoint>
    {
        /// <summary>Creates an outpoint. <paramref name="transactionId"/> is the Kaspa transaction id in wire order.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="transactionId"/> is null.</exception>
        public OutPoint(uint256 transactionId, uint index)
        {
            TransactionId = transactionId ?? throw new ArgumentNullException(nameof(transactionId));
            Index = index;
        }

        /// <summary>Transaction id of the output being spent.</summary>
        public uint256 TransactionId { get; }

        /// <summary>Index of the output being spent.</summary>
        public uint Index { get; }

        /// <summary>Wire-order id, a colon, and the index.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public override string ToString()
        {
            return TransactionId.ToString() + ":" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <inheritdoc />
        public bool Equals(OutPoint? other)
        {
            return other is not null && Index == other.Index && TransactionId.Equals(other.TransactionId);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as OutPoint);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (TransactionId.GetHashCode() * 31) + (int)Index;
        }
    }
}
