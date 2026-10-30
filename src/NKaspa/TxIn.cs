namespace NKaspa
{
    /// <summary>
    /// Transaction input. Kaspa counterpart of NBitcoin.TxIn.
    /// </summary>
    /// <remarks>
    /// <see cref="Sequence"/> is a u64. Bitcoin stores sequence as a u32.
    /// <see cref="SigOpCount"/> is the byte committed by a version-0 signature hash.
    /// <see cref="ComputeBudget"/> is the u16 committed by a version-1 transaction hash.
    /// <see cref="SignatureScript"/> is the sigScript, not a Bitcoin scriptSig witness.
    /// </remarks>
    public sealed class TxIn
    {
        private Script _signatureScript;

        /// <summary>Creates an input. The signature script starts empty and the sequence is 0.</summary>
        public TxIn(OutPoint previousOutpoint)
            : this(previousOutpoint, Script.Empty, 0, 0)
        {
        }

        /// <summary>Creates an input.</summary>
        public TxIn(OutPoint previousOutpoint, Script signatureScript, ulong sequence, byte sigOpCount)
        {
            PreviousOutpoint = previousOutpoint ?? throw new ArgumentNullException(nameof(previousOutpoint));
            _signatureScript = signatureScript ?? throw new ArgumentNullException(nameof(signatureScript));
            Sequence = sequence;
            SigOpCount = sigOpCount;
        }

        /// <summary>
        /// Creates a version-1 input that commits a compute budget.
        /// <see cref="SigOpCount"/> stays 0. One budget unit is 100 compute grams.
        /// </summary>
        public static TxIn WithComputeBudget(OutPoint previousOutpoint, Script signatureScript, ulong sequence, ushort computeBudget)
        {
            var input = new TxIn(previousOutpoint, signatureScript, sequence, 0);
            input.ComputeBudget = computeBudget;
            return input;
        }

        /// <summary>Output spent by this input.</summary>
        public OutPoint PreviousOutpoint { get; set; }

        /// <summary>Signature script. Empty until the input is signed.</summary>
        public Script SignatureScript
        {
            get { return _signatureScript; }
            set { _signatureScript = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>Sequence, as a u64.</summary>
        public ulong Sequence { get; set; }

        /// <summary>Signature-operation count committed by a version-0 sighash. A standard pubkey input uses 1.</summary>
        public byte SigOpCount { get; set; }

        /// <summary>
        /// Toccata compute budget. Null means this input carries <see cref="SigOpCount"/> instead.
        /// Version-1 hashing writes this value, or 0 when it is null.
        /// The script engine allows <c>computeBudget * 10000 + 9999</c> script units. A null budget allows 9999.
        /// </summary>
        public ushort? ComputeBudget { get; set; }
    }
}
