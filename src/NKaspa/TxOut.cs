namespace NKaspa
{
    /// <summary>
    /// Optional covenant bound to a version-1 output.
    /// Serialization and sighash write these bytes. The script engine reads them through the covenant opcodes.
    /// </summary>
    public sealed class CovenantBinding
    {
        /// <summary>Creates a binding. <paramref name="covenantId"/> is the 32-byte covenant id.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="covenantId"/> is null.</exception>
        public CovenantBinding(ushort authorizingInput, uint256 covenantId)
        {
            AuthorizingInput = authorizingInput;
            CovenantId = covenantId ?? throw new ArgumentNullException(nameof(covenantId));
        }

        /// <summary>Index of the input allowed to authorize this covenant.</summary>
        public ushort AuthorizingInput { get; }

        /// <summary>32-byte covenant id, in Kaspa wire order.</summary>
        public uint256 CovenantId { get; }
    }

    /// <summary>
    /// Transaction output. Kaspa counterpart of NBitcoin.TxOut.
    /// <see cref="Value"/> is sompi. <see cref="ScriptPublicKey"/> carries the script version.
    /// <see cref="Covenant"/> is written only by version-1 transactions.
    /// </summary>
    public sealed class TxOut
    {
        /// <summary>Creates an output.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPublicKey"/> is null.</exception>
        public TxOut(Money value, ScriptPubKey scriptPublicKey)
        {
            Value = value;
            ScriptPublicKey = scriptPublicKey ?? throw new ArgumentNullException(nameof(scriptPublicKey));
        }

        /// <summary>Creates an output from a sompi count.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPublicKey"/> is null.</exception>
        public TxOut(ulong sompi, ScriptPubKey scriptPublicKey)
            : this(new Money(sompi), scriptPublicKey)
        {
        }

        /// <summary>Amount in sompi.</summary>
        public Money Value { get; set; }

        /// <summary>Versioned locking script.</summary>
        public ScriptPubKey ScriptPublicKey { get; set; }

        /// <summary>
        /// Covenant bound to this output. Null when the output has none.
        /// Version-0 bytes ignore it. Version-1 bytes write a presence flag and, when set, the authorizing input and the covenant id.
        /// </summary>
        public CovenantBinding? Covenant { get; set; }
    }
}
