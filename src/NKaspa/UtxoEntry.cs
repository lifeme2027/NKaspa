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
        public UtxoEntry(Money amount, ScriptPubKey scriptPublicKey, ulong blockDaaScore, bool isCoinbase)
        {
            Amount = amount;
            ScriptPublicKey = scriptPublicKey ?? throw new ArgumentNullException(nameof(scriptPublicKey));
            BlockDaaScore = blockDaaScore;
            IsCoinbase = isCoinbase;
        }

        /// <summary>Creates a UTXO entry from a sompi count.</summary>
        public UtxoEntry(ulong sompi, ScriptPubKey scriptPublicKey, ulong blockDaaScore, bool isCoinbase)
            : this(new Money(sompi), scriptPublicKey, blockDaaScore, isCoinbase)
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
    }
}
