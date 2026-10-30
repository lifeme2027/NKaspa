namespace NKaspa
{
    /// <summary>
    /// Why a Kaspa script stopped. Kaspa counterpart of NBitcoin.ScriptError.
    /// Names follow rusty-kaspa <c>TxScriptError</c>. The official script vectors map several of these onto the same historic code.
    /// </summary>
    public enum ScriptErrorCode
    {
        /// <summary>The script finished with a true top element. Not stored on a failed <see cref="ScriptError"/>.</summary>
        Ok = 0,

        /// <summary>A number or arithmetic result does not fit the Kaspa width.</summary>
        NumberTooBig,

        /// <summary>A script number could not be serialized into the requested width.</summary>
        Serialization,

        /// <summary>An ECDSA public key is not 33 bytes.</summary>
        PubKeyFormat,

        /// <summary>The script left a false value on the stack.</summary>
        EvalFalse,

        /// <summary>The stack had no element where one was required at the end of execution.</summary>
        EmptyStack,

        /// <summary>A failed multisig still had a non-empty signature.</summary>
        NullFail,

        /// <summary>The sighash byte is not one of the six Kaspa values.</summary>
        InvalidSigHashType,

        /// <summary>A transaction input's signature script was not push-only.</summary>
        SignatureScriptNotPushOnly,

        /// <summary>The final stack had more than one element.</summary>
        CleanStack,

        /// <summary><c>OP_RESERVED</c>, <c>OP_VER</c>, <c>OP_VERIF</c>, <c>OP_VERNOTIF</c>, or a reserved opcode.</summary>
        OpcodeReserved,

        /// <summary>A push opcode did not have the bytes it declared.</summary>
        MalformedPush,

        /// <summary>A push length prefix was truncated.</summary>
        MalformedPushSize,

        /// <summary>The opcode is not defined, or <c>OP_CHAINBLOCKSEQCOMMIT</c> has no block accessor.</summary>
        InvalidOpcode,

        /// <summary><c>OP_ZKPRECOMPILE</c> is not implemented. The engine returns an error instead of accepting the proof.</summary>
        Unimplemented,

        /// <summary>An <c>OP_IF</c> was still open at the end of a script.</summary>
        UnbalancedConditional,

        /// <summary>The script reached a state the opcode rejects. <see cref="ScriptError.Message"/> distinguishes the case.</summary>
        InvalidState,

        /// <summary><c>OP_RETURN</c>.</summary>
        EarlyReturn,

        /// <summary>A verify opcode saw a false value.</summary>
        VerifyError,

        /// <summary>An opcode needed more stack elements than were present, or an index passed to pick or roll was out of range after the pop.</summary>
        InvalidStackOperation,

        /// <summary>A substring range was outside the value.</summary>
        OutOfBoundsSubstring,

        /// <summary>A substring bound was negative.</summary>
        InvalidIndex,

        /// <summary>A substring end was below its start.</summary>
        InvalidRange,

        /// <summary>One of the six opcodes Kaspa still disables.</summary>
        OpcodeDisabled,

        /// <summary>A stack element or BLAKE2b key was longer than its limit.</summary>
        ElementTooBig,

        /// <summary>The script exceeded <see cref="ScriptLimits.MaxOpsPerScript"/>.</summary>
        TooManyOperations,

        /// <summary>The combined stacks exceeded <see cref="ScriptLimits.MaxStackSize"/>.</summary>
        StackSizeExceeded,

        /// <summary>A multisig public-key count was negative or above 20.</summary>
        InvalidPubKeyCount,

        /// <summary>A multisig signature count was negative or above the public-key count.</summary>
        InvalidSignatureCount,

        /// <summary><c>OP_NUM2BIN</c> was asked for more than 8 bytes.</summary>
        NotMinimalData,

        /// <summary>A lock-time or sequence check failed.</summary>
        UnsatisfiedLockTime,

        /// <summary>A script was longer than <see cref="ScriptLimits.MaxScriptSize"/>.</summary>
        ScriptSize,

        /// <summary>A covenant index or genesis id check failed.</summary>
        CovenantsError,

        /// <summary>A signature could not be parsed.</summary>
        InvalidSignature,

        /// <summary>A Schnorr or ECDSA public key could not be parsed.</summary>
        InvalidPubkey,

        /// <summary>No script was supplied.</summary>
        NoScripts,

        /// <summary>The opcode needs a transaction input and the engine was not given one.</summary>
        InvalidSource,

        /// <summary>An introspection opcode named an input that does not exist.</summary>
        InvalidInputIndex,

        /// <summary>An introspection opcode named an output that does not exist.</summary>
        InvalidOutputIndex,

        /// <summary>A value used as a 32-byte hash had a different length.</summary>
        InvalidLengthOfBlockHash,

        /// <summary>The input spent more script units than its sigop count or compute budget allows.</summary>
        ExceededCommittedScriptUnits,

        /// <summary>The block named by <c>OP_CHAINBLOCKSEQCOMMIT</c> is already pruned.</summary>
        BlockAlreadyPruned,

        /// <summary>The block named by <c>OP_CHAINBLOCKSEQCOMMIT</c> is not a chain ancestor.</summary>
        BlockNotSelected,

        /// <summary>The block is an ancestor but its sequence commitment is deeper than the window.</summary>
        BlockIsTooDeep,

        /// <summary>The short-form test parser did not recognize a token.</summary>
        BadToken,
    }

    /// <summary>
    /// One script failure. Kaspa counterpart of NBitcoin.ScriptError.
    /// </summary>
    public sealed class ScriptError
    {
        internal ScriptError(ScriptErrorCode code, string message)
        {
            Code = code;
            Message = message;
        }

        /// <summary>Failure kind.</summary>
        public ScriptErrorCode Code { get; }

        /// <summary>Detail that matches the rusty-kaspa messages the official vectors distinguish, when those messages matter.</summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Code + ": " + Message;
        }

        internal static ScriptError NumberTooBig(string message)
        {
            return new ScriptError(ScriptErrorCode.NumberTooBig, message);
        }

        internal static ScriptError Serialization(string message)
        {
            return new ScriptError(ScriptErrorCode.Serialization, message);
        }

        internal static readonly ScriptError PubKeyFormat = new ScriptError(ScriptErrorCode.PubKeyFormat, "public key format");

        internal static readonly ScriptError EvalFalse = new ScriptError(ScriptErrorCode.EvalFalse, "false stack entry at end of script");

        internal static readonly ScriptError EmptyStack = new ScriptError(ScriptErrorCode.EmptyStack, "empty stack");

        internal static readonly ScriptError NullFail = new ScriptError(ScriptErrorCode.NullFail, "null fail");

        internal static ScriptError InvalidSigHashType(byte value)
        {
            return new ScriptError(ScriptErrorCode.InvalidSigHashType, "invalid sighash type " + value.ToString("x2"));
        }

        internal static readonly ScriptError SignatureScriptNotPushOnly = new ScriptError(ScriptErrorCode.SignatureScriptNotPushOnly, "signature script is not push only");

        internal static ScriptError CleanStack(int extra)
        {
            return new ScriptError(ScriptErrorCode.CleanStack, "clean stack extra " + extra.ToString());
        }

        internal static ScriptError OpcodeReserved(byte code)
        {
            return new ScriptError(ScriptErrorCode.OpcodeReserved, "reserved opcode " + code.ToString("x2"));
        }

        internal static ScriptError MalformedPush(long expected, long actual)
        {
            return new ScriptError(ScriptErrorCode.MalformedPush, "malformed push expected " + expected.ToString() + " got " + actual.ToString());
        }

        internal static readonly ScriptError MalformedPushSize = new ScriptError(ScriptErrorCode.MalformedPushSize, "malformed push size");

        internal static ScriptError InvalidOpcode(byte code)
        {
            return new ScriptError(ScriptErrorCode.InvalidOpcode, "invalid opcode " + code.ToString("x2"));
        }

        internal static ScriptError Unimplemented(string name)
        {
            return new ScriptError(ScriptErrorCode.Unimplemented, name + " is not implemented");
        }

        internal static readonly ScriptError UnbalancedConditional = new ScriptError(ScriptErrorCode.UnbalancedConditional, "unbalanced conditional");

        internal static ScriptError InvalidState(string message)
        {
            return new ScriptError(ScriptErrorCode.InvalidState, message);
        }

        internal static readonly ScriptError EarlyReturn = new ScriptError(ScriptErrorCode.EarlyReturn, "early return");

        internal static readonly ScriptError VerifyError = new ScriptError(ScriptErrorCode.VerifyError, "verify failed");

        internal static ScriptError InvalidStackOperation(int needed, int available)
        {
            return new ScriptError(ScriptErrorCode.InvalidStackOperation, "stack operation needed " + needed.ToString() + " available " + available.ToString());
        }

        internal static ScriptError OutOfBoundsSubstring(int start, int end, int length)
        {
            return new ScriptError(ScriptErrorCode.OutOfBoundsSubstring, "substring " + start.ToString() + ".." + end.ToString() + " length " + length.ToString());
        }

        internal static ScriptError InvalidIndex(int value)
        {
            return new ScriptError(ScriptErrorCode.InvalidIndex, "invalid index " + value.ToString());
        }

        internal static ScriptError InvalidRange(int start, int end)
        {
            return new ScriptError(ScriptErrorCode.InvalidRange, "invalid range " + start.ToString() + ".." + end.ToString());
        }

        internal static ScriptError OpcodeDisabled(byte code)
        {
            return new ScriptError(ScriptErrorCode.OpcodeDisabled, "disabled opcode " + code.ToString("x2"));
        }

        internal static ScriptError ElementTooBig(int length, int limit)
        {
            return new ScriptError(ScriptErrorCode.ElementTooBig, "element " + length.ToString() + " exceeds " + limit.ToString());
        }

        internal static readonly ScriptError TooManyOperations = new ScriptError(ScriptErrorCode.TooManyOperations, "too many operations");

        internal static ScriptError StackSizeExceeded(int size)
        {
            return new ScriptError(ScriptErrorCode.StackSizeExceeded, "stack size " + size.ToString());
        }

        internal static ScriptError InvalidPubKeyCount(string message)
        {
            return new ScriptError(ScriptErrorCode.InvalidPubKeyCount, message);
        }

        internal static ScriptError InvalidSignatureCount(string message)
        {
            return new ScriptError(ScriptErrorCode.InvalidSignatureCount, message);
        }

        internal static ScriptError NotMinimalData(string message)
        {
            return new ScriptError(ScriptErrorCode.NotMinimalData, message);
        }

        internal static ScriptError UnsatisfiedLockTime(string message)
        {
            return new ScriptError(ScriptErrorCode.UnsatisfiedLockTime, message);
        }

        internal static ScriptError ScriptSize(int length)
        {
            return new ScriptError(ScriptErrorCode.ScriptSize, "script size " + length.ToString());
        }

        internal static ScriptError Covenants(string message)
        {
            return new ScriptError(ScriptErrorCode.CovenantsError, message);
        }

        internal static ScriptError InvalidSignature(string message)
        {
            return new ScriptError(ScriptErrorCode.InvalidSignature, message);
        }

        internal static ScriptError InvalidPubkey(string message)
        {
            return new ScriptError(ScriptErrorCode.InvalidPubkey, message);
        }

        internal static readonly ScriptError NoScripts = new ScriptError(ScriptErrorCode.NoScripts, "no scripts");

        internal static ScriptError InvalidSource(string message)
        {
            return new ScriptError(ScriptErrorCode.InvalidSource, message);
        }

        internal static ScriptError InvalidInputIndex(int index, int count)
        {
            return new ScriptError(ScriptErrorCode.InvalidInputIndex, "input " + index.ToString() + " of " + count.ToString());
        }

        internal static ScriptError InvalidOutputIndex(int index, int count)
        {
            return new ScriptError(ScriptErrorCode.InvalidOutputIndex, "output " + index.ToString() + " of " + count.ToString());
        }

        internal static ScriptError InvalidLengthOfBlockHash(int length)
        {
            return new ScriptError(ScriptErrorCode.InvalidLengthOfBlockHash, "hash length " + length.ToString());
        }

        internal static ScriptError Exceeded(ulong used, ulong limit)
        {
            return new ScriptError(ScriptErrorCode.ExceededCommittedScriptUnits, "used " + used.ToString() + " limit " + limit.ToString());
        }

        internal static ScriptError BlockAlreadyPruned()
        {
            return new ScriptError(ScriptErrorCode.BlockAlreadyPruned, "block already pruned");
        }

        internal static ScriptError BlockNotSelected()
        {
            return new ScriptError(ScriptErrorCode.BlockNotSelected, "block not selected");
        }

        internal static ScriptError BlockIsTooDeep()
        {
            return new ScriptError(ScriptErrorCode.BlockIsTooDeep, "block is too deep");
        }

        internal static ScriptError BadToken(string token)
        {
            return new ScriptError(ScriptErrorCode.BadToken, "cannot parse " + token);
        }
    }
}
