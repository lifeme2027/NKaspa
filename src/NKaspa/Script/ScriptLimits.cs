namespace NKaspa
{
    /// <summary>
    /// Consensus limits for the Kaspa script engine.
    /// Values match rusty-kaspa <c>crypto/txscript/src/lib.rs</c> and <c>consensus/core/src/mass/units.rs</c>.
    /// </summary>
    public static class ScriptLimits
    {
        /// <summary>Highest script public key version this engine executes. A higher version is accepted without running the script.</summary>
        public const int MaxScriptPublicKeyVersion = 0;

        /// <summary>Combined data-stack and alt-stack depth.</summary>
        public const int MaxStackSize = 244;

        /// <summary>Maximum length of one script, in bytes.</summary>
        public const int MaxScriptSize = 1_000_000;

        /// <summary>Maximum length of one stack element, in bytes.</summary>
        public const int MaxScriptElementSize = 1_000_000;

        /// <summary>Maximum non-push opcodes in one script.</summary>
        public const int MaxOpsPerScript = 1_000_000;

        /// <summary>Public keys accepted by one <c>OP_CHECKMULTISIG</c> or <c>OP_CHECKMULTISIGECDSA</c>.</summary>
        public const int MaxPubKeysPerMultisig = 20;

        /// <summary>Opcodes at or below this value are pushes. They do not increment the operation counter. <c>OP_RESERVED</c> is included.</summary>
        public const byte NoCostOpcode = 0x60;

        /// <summary>A standard script public key is at most this many bytes. Only the excess is charged as script units.</summary>
        public const int StandardScriptPubKeyMaxSize = 35;

        /// <summary>Lock time at or above this value is a unix timestamp. Below it, the value is a DAA score.</summary>
        public const ulong LockTimeThreshold = 500_000_000_000;

        /// <summary>Bit 63 of a sequence. When set on the stack operand, <c>OP_CHECKSEQUENCEVERIFY</c> is a no-op.</summary>
        public const ulong SequenceLockTimeDisabled = 1UL << 63;

        /// <summary>Low 32 bits of a sequence, which <c>OP_CHECKSEQUENCEVERIFY</c> compares.</summary>
        public const ulong SequenceLockTimeMask = 0x00000000ffffffffUL;

        /// <summary>Sequence value that marks an input as finalized for <c>OP_CHECKLOCKTIMEVERIFY</c>.</summary>
        public const ulong MaxTxInSequenceNum = ulong.MaxValue;

        /// <summary>Largest BLAKE2b key accepted by <c>OP_BLAKE2BWITHKEY</c>.</summary>
        public const int Blake2bMaxKeySize = 64;

        /// <summary>BLAKE3 key length required by <c>OP_BLAKE3WITHKEY</c>.</summary>
        public const int Blake3KeySize = 32;

        /// <summary>Script units in one gram. A non-standard script public key is charged this many units per byte above <see cref="StandardScriptPubKeyMaxSize"/>.</summary>
        public const ulong ScriptUnitsPerGram = 100;

        /// <summary>Script units granted by one version-1 compute-budget unit.</summary>
        public const ulong ScriptUnitsPerComputeBudgetUnit = 10_000;

        /// <summary>Script units charged for one signature check, and granted by one version-0 sigop.</summary>
        public const ulong ScriptUnitsPerSigOpCountUnit = 100_000;

        /// <summary>Free script units added to every input's committed budget. One less than a single compute-budget unit.</summary>
        public const ulong FreeScriptUnitsPerInput = 9_999;

        /// <summary>BLAKE2b hashes cost two script units per hashed byte. SHA256 and BLAKE3 cost one.</summary>
        public const ulong Blake2bUnitsPerByte = 2;

        /// <summary>
        /// Script units a transaction input may spend.
        /// Version 0 uses <see cref="TxIn.SigOpCount"/>. Version 1 uses <see cref="TxIn.ComputeBudget"/>, or 0 when it is unset.
        /// Both include <see cref="FreeScriptUnitsPerInput"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="tx"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="inputIndex"/> is outside the allowed range.</exception>
        public static ulong AllowedScriptUnits(Transaction tx, int inputIndex)
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if ((uint)inputIndex >= (uint)tx.Inputs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(inputIndex));
            }

            var input = tx.Inputs[inputIndex];
            ulong committed = tx.Version >= 1
                ? (ulong)(input.ComputeBudget ?? 0) * ScriptUnitsPerComputeBudgetUnit
                : (ulong)input.SigOpCount * ScriptUnitsPerSigOpCountUnit;
            return SaturatingAdd(committed, FreeScriptUnitsPerInput);
        }

        internal static ulong SaturatingAdd(ulong left, ulong right)
        {
            return left > ulong.MaxValue - right ? ulong.MaxValue : left + right;
        }
    }
}
