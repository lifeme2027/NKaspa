namespace NKaspa
{
    /// <summary>
    /// Transaction output. Kaspa counterpart of NBitcoin.TxOut.
    /// <see cref="Value"/> is sompi. <see cref="ScriptPublicKey"/> carries the script version.
    /// </summary>
    public sealed class TxOut
    {
        /// <summary>Creates an output.</summary>
        public TxOut(Money value, ScriptPubKey scriptPublicKey)
        {
            Value = value;
            ScriptPublicKey = scriptPublicKey ?? throw new ArgumentNullException(nameof(scriptPublicKey));
        }

        /// <summary>Creates an output from a sompi count.</summary>
        public TxOut(ulong sompi, ScriptPubKey scriptPublicKey)
            : this(new Money(sompi), scriptPublicKey)
        {
        }

        /// <summary>Amount in sompi.</summary>
        public Money Value { get; set; }

        /// <summary>Versioned locking script.</summary>
        public ScriptPubKey ScriptPublicKey { get; set; }
    }
}
