namespace NKaspa
{
    /// <summary>
    /// The output an input spends, as sighash needs it.
    /// Amount, script, DAA score, and the coinbase flag match rusty-kaspa <c>UtxoEntry</c>.
    /// </summary>
    /// <remarks>
    /// Sighash commits to the amount and the script public key.
    /// <see cref="BlockDaaScore"/> and <see cref="IsCoinbase"/> are part of the UTXO record.
    /// Version-0 sighash does not write them into the preimage. Introspection opcodes read them at execution time.
    /// </remarks>
    public sealed class UtxoEntry
    {
        /// <summary>Creates a UTXO entry.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPublicKey"/> is null.</exception>
        public UtxoEntry(Money amount, ScriptPubKey scriptPublicKey, ulong blockDaaScore, bool isCoinbase, uint256? covenantId = null)
        {
            Amount = amount;
            ScriptPublicKey = scriptPublicKey ?? throw new ArgumentNullException(nameof(scriptPublicKey));
            BlockDaaScore = blockDaaScore;
            IsCoinbase = isCoinbase;
            CovenantId = covenantId;
        }

        /// <summary>Creates a UTXO entry from a sompi count.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPublicKey"/> is null.</exception>
        public UtxoEntry(ulong sompi, ScriptPubKey scriptPublicKey, ulong blockDaaScore, bool isCoinbase, uint256? covenantId = null)
            : this(new Money(sompi), scriptPublicKey, blockDaaScore, isCoinbase, covenantId)
        {
        }

        /// <summary>Value of the spent output, in sompi.</summary>
        public Money Amount { get; }

        /// <summary>Locking script of the spent output.</summary>
        public ScriptPubKey ScriptPublicKey { get; }

        /// <summary>DAA score of the block that created the output.</summary>
        public ulong BlockDaaScore { get; }

        /// <summary>True when the output was created by a coinbase transaction.</summary>
        public bool IsCoinbase { get; }

        /// <summary>
        /// Covenant id stored on this UTXO, when the spent output had one.
        /// Storage plurality counts an extra 32 bytes when this is set. Sighash does not write it.
        /// </summary>
        public uint256? CovenantId { get; }
    }
}
