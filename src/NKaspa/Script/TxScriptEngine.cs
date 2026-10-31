using NBitcoin.Secp256k1;

namespace NKaspa
{
    /// <summary>
    /// Kaspa script virtual machine. Kaspa counterpart of NBitcoin.ScriptEvaluationContext's executor,
    /// aligned with rusty-kaspa <c>TxScriptEngine</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A version-0 input may spend <c>sigOpCount * 100000 + 9999</c> script units.
    /// A version-1 input may spend <c>computeBudget * 10000 + 9999</c>.
    /// <see cref="Script.VerifyScript(Script, ScriptPubKey, Transaction, int, IReadOnlyList{UtxoEntry})"/> applies that limit.
    /// The official script vectors run with an unlimited budget, matching <c>from_transaction_input</c>.
    /// </para>
    /// <para>
    /// <c>OP_ZKPRECOMPILE</c> enforces the finalized tag, cost, and structural checks.
    /// The BN254 pairing and the RISC0 STARK return <see cref="ScriptErrorCode.Unimplemented"/> and do not push true.
    /// <c>OP_CHAINBLOCKSEQCOMMIT</c> returns <see cref="ScriptErrorCode.InvalidOpcode"/> unless an <see cref="ISeqCommitAccessor"/> is supplied.
    /// </para>
    /// </remarks>
    public sealed partial class TxScriptEngine
    {
        private readonly Transaction _tx;
        private readonly int _inputIndex;
        private readonly UtxoEntry?[] _utxos;
        private readonly byte[] _scriptSig;
        private readonly byte[] _scriptPubKey;
        private readonly bool _hasInput;
        private readonly bool _isP2Sh;
        private readonly CovenantsContext _covenants;
        private readonly ISeqCommitAccessor? _seq;
        private readonly RuntimeResourceMeter _meter;
        private readonly SigCache _cache;
        private readonly ScriptError? _contextError;
        private readonly ScriptStack _dstack = new ScriptStack();
        private readonly ScriptStack _astack = new ScriptStack();
        private readonly List<Cond> _cond = new List<Cond>();
        private int _numOps;

        private TxScriptEngine(
            Transaction tx,
            int inputIndex,
            UtxoEntry?[] utxos,
            byte[] scriptSig,
            byte[] scriptPubKey,
            ushort scriptVersion,
            ulong sigOpUnits,
            ulong scriptUnitsLimit,
            CovenantsContext covenants,
            ScriptError? contextError,
            ISeqCommitAccessor? seq,
            SigCache cache)
        {
            _tx = tx;
            _inputIndex = inputIndex;
            _utxos = utxos;
            _scriptSig = scriptSig;
            _scriptPubKey = scriptPubKey;
            _hasInput = true;
            _isP2Sh = scriptVersion <= ScriptLimits.MaxScriptPublicKeyVersion && IsPayToScriptHash(scriptPubKey);
            _covenants = covenants;
            _contextError = contextError;
            _seq = seq;
            _meter = new RuntimeResourceMeter(sigOpUnits, scriptUnitsLimit);
            _cache = cache;
            ScriptVersion = scriptVersion;
        }

        /// <summary>Script public key version being executed.</summary>
        public ushort ScriptVersion { get; }

        /// <summary>Signature checks charged so far. Failed attempts count until a later attempt matches.</summary>
        public ushort UsedSigOps { get; private set; }

        /// <summary>Script units charged so far. A charge that does not fit is not included.</summary>
        public ulong UsedScriptUnits { get; private set; }

        /// <summary>Failure from the last <see cref="Execute"/>, or null when the script succeeded.</summary>
        public ScriptError? Error { get; private set; }

        /// <summary>
        /// Runs one input. <paramref name="scriptUnitsLimit"/> null uses <see cref="ScriptLimits.AllowedScriptUnits"/>.
        /// Pass <see cref="ulong.MaxValue"/> to match the unlimited official vectors.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utxos"/> is null. <paramref name="tx"/> is null. <paramref name="scriptPubKey"/> is
        /// null. <paramref name="scriptSig"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="inputIndex"/> is outside the allowed range. BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">
        /// The spent output for this input is missing. BLAKE3 key must be 32 bytes. wRPC request is missing a
        /// numeric id. Set PointOfView or ChainBlock, not both. PointOfView is the selected parent P.
        /// ChainBlock is the chain block B whose selected parent becomes P. Sighash type is not one of the six
        /// Kaspa values. BLAKE2b key must be at most 64 bytes.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isChainBlock. Expected a hex hash for {name}. uint256 hex must be 32 bytes.
        /// uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a parent hash. Expected parent hashes. Expected an array for parents. Missing JSON string
        /// {name}. Missing JSON property {name}. Hash is missing. networkId is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// WebSocket is not connected. Call ConnectAsync first. Transaction input is missing an outpoint.
        /// BLAKE2b hasher is already finished. Transaction output is missing a script. SHA256 did not produce a
        /// digest.
        /// </exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        /// <exception cref="NKaspa.RPC.ConsensusSeqCommitException">
        /// acceptedIdMerkleRoot is missing for {target.Hash}. seq commitment was requested for {target.Hash},
        /// which is not a selected-parent ancestor of {pov.Hash}. header blueScore does not match verboseData
        /// blueScore for {block.Hash}. blueScore is missing for {block.Hash}. getBlockDagInfo.sink changed from
        /// {previous} to {dag.Sink} and the new selected chain does not contain {anchor}.
        /// verboseData.isChainBlock is missing for {current.Hash}. selected parent chain cycled at
        /// {current.Hash}. isChainBlock is false for {block.Hash} on the selected chain.
        /// verboseData.isChainBlock is missing for {block.Hash}. getBlock did not return {hash}. The node data
        /// is incomplete. verboseData.hash {block.VerboseHash} does not match the requested block {block.Hash}.
        /// header.hash {block.HeaderHash} does not match the requested block {block.Hash}. Request limit of
        /// {_maxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)} was reached before the
        /// selected parent chain could be checked. blue score does not decrease from {child.Hash}
        /// ({childScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}) to selected parent
        /// {parent.Hash} ({parentScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}). selected
        /// parent walk reached {block.Hash} but isChainBlock is false. isChainBlock is false for {block.Hash}
        /// and getBlockDagInfo.sink changed from {previous} to {again.Sink}. getBlockDagInfo failed:
        /// {ex.Message}. selectedParentHash {block.SelectedParentHash} does not match the first direct parent
        /// {block.DirectParent} of {block.Hash}. Further failures of this type use the same exception.
        /// </exception>
        public static TxScriptEngine Run(
            byte[] scriptSig,
            ScriptPubKey scriptPubKey,
            Transaction tx,
            int inputIndex,
            IReadOnlyList<UtxoEntry?> utxos,
            ulong? scriptUnitsLimit = null,
            ISeqCommitAccessor? seqCommit = null,
            CovenantsContext? covenants = null)
        {
            if (scriptSig == null)
            {
                throw new ArgumentNullException(nameof(scriptSig));
            }

            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (utxos == null)
            {
                throw new ArgumentNullException(nameof(utxos));
            }

            if ((uint)inputIndex >= (uint)tx.Inputs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(inputIndex));
            }

            var limit = scriptUnitsLimit ?? ScriptLimits.AllowedScriptUnits(tx, inputIndex);
            var overlay = new UtxoEntry?[utxos.Count];
            for (var i = 0; i < utxos.Count; i++)
            {
                overlay[i] = utxos[i];
            }

            UtxoEntry? current = inputIndex < overlay.Length ? overlay[inputIndex] : null;
            if (current == null)
            {
                throw new ArgumentException("The spent output for this input is missing.", nameof(utxos));
            }

            overlay[inputIndex] = new UtxoEntry(current.Amount, scriptPubKey, current.BlockDaaScore, current.IsCoinbase, current.CovenantId);
            ScriptError? contextError = null;
            var context = covenants;
            if (context == null)
            {
                contextError = CovenantsContext.TryFromTransaction(tx, overlay, out var built);
                context = contextError == null ? built : CovenantsContext.Empty;
            }

            var engine = new TxScriptEngine(
                tx,
                inputIndex,
                overlay,
                scriptSig,
                scriptPubKey.Script,
                scriptPubKey.Version,
                ScriptLimits.ScriptUnitsPerSigOpCountUnit,
                limit,
                context!,
                contextError,
                seqCommit,
                new SigCache());
            engine.Execute();
            return engine;
        }

        /// <summary>Executes the signature script and the script public key. Null means the input is valid.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">
        /// BLAKE3 key must be 32 bytes. wRPC request is missing a numeric id. Set PointOfView or ChainBlock,
        /// not both. PointOfView is the selected parent P. ChainBlock is the chain block B whose selected
        /// parent becomes P. Sighash type is not one of the six Kaspa values. BLAKE2b key must be at most 64
        /// bytes.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isChainBlock. Expected a hex hash for {name}. uint256 hex must be 32 bytes.
        /// uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a parent hash. Expected parent hashes. Expected an array for parents. Missing JSON string
        /// {name}. Missing JSON property {name}. Hash is missing. networkId is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// WebSocket is not connected. Call ConnectAsync first. Transaction input is missing an outpoint.
        /// BLAKE2b hasher is already finished. Transaction output is missing a script. SHA256 did not produce a
        /// digest.
        /// </exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        /// <exception cref="NKaspa.RPC.ConsensusSeqCommitException">
        /// acceptedIdMerkleRoot is missing for {target.Hash}. seq commitment was requested for {target.Hash},
        /// which is not a selected-parent ancestor of {pov.Hash}. header blueScore does not match verboseData
        /// blueScore for {block.Hash}. blueScore is missing for {block.Hash}. getBlockDagInfo.sink changed from
        /// {previous} to {dag.Sink} and the new selected chain does not contain {anchor}.
        /// verboseData.isChainBlock is missing for {current.Hash}. selected parent chain cycled at
        /// {current.Hash}. isChainBlock is false for {block.Hash} on the selected chain.
        /// verboseData.isChainBlock is missing for {block.Hash}. getBlock did not return {hash}. The node data
        /// is incomplete. verboseData.hash {block.VerboseHash} does not match the requested block {block.Hash}.
        /// header.hash {block.HeaderHash} does not match the requested block {block.Hash}. Request limit of
        /// {_maxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)} was reached before the
        /// selected parent chain could be checked. blue score does not decrease from {child.Hash}
        /// ({childScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}) to selected parent
        /// {parent.Hash} ({parentScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}). selected
        /// parent walk reached {block.Hash} but isChainBlock is false. isChainBlock is false for {block.Hash}
        /// and getBlockDagInfo.sink changed from {previous} to {again.Sink}. getBlockDagInfo failed:
        /// {ex.Message}. selectedParentHash {block.SelectedParentHash} does not match the first direct parent
        /// {block.DirectParent} of {block.Hash}. Further failures of this type use the same exception.
        /// </exception>
        public ScriptError? Execute()
        {
            Error = ExecuteInner();
            UsedSigOps = _meter.UsedSigOps;
            UsedScriptUnits = _meter.UsedScriptUnits;
            return Error;
        }

        /// <summary>True when a parse error occurs or the first opcode is <c>OP_RETURN</c>.</summary>
        public static bool IsUnspendable(ReadOnlySpan<byte> script)
        {
            var ops = new List<ParsedOp>();
            var error = Parse(script.ToArray(), ops);
            if (error != null)
            {
                return true;
            }

            return ops.Count > 0 && ops[0].Code == (byte)OpcodeType.OP_RETURN;
        }

        /// <summary>
        /// Upper bound of CHECKSIG, CHECKSIGVERIFY, CHECKSIGECDSA, and multisig opcodes.
        /// A multisig preceded by <c>OP_1</c> through <c>OP_16</c> counts as that integer. Otherwise it counts as 20.
        /// Pay-to-script-hash uses the last push of the signature script. A malformed signature script counts as 0.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPubKey"/> is null. <paramref name="signatureScript"/> is null.</exception>
        public static ulong GetSigOpCountUpperBound(byte[] signatureScript, byte[] scriptPubKey)
        {
            if (signatureScript == null)
            {
                throw new ArgumentNullException(nameof(signatureScript));
            }

            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            if (!IsPayToScriptHash(scriptPubKey))
            {
                return CountUpper(scriptPubKey);
            }

            var pushes = new List<ParsedOp>();
            if (signatureScript.Length == 0 || Parse(signatureScript, pushes) != null || pushes.Count == 0)
            {
                return 0;
            }

            return CountUpper(pushes[pushes.Count - 1].Data);
        }

        /// <summary>
        /// Upper bound of <c>OP_ZKPRECOMPILE</c> script units.
        /// A precompile whose previous push starts with a known tag uses that tag's cost.
        /// Anything else, including a precompile in the first position, uses <see cref="ZkPrecompile.R0SuccinctScriptUnits"/>.
        /// Pay-to-script-hash reads the last push of the signature script. A missing or malformed signature script counts as 0.
        /// Parsing stops at the first malformed opcode and keeps the units counted before it.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPubKey"/> is null. <paramref name="signatureScript"/> is null.</exception>
        public static ulong GetZkScriptUnitsUpperBound(byte[] signatureScript, byte[] scriptPubKey)
        {
            if (signatureScript == null)
            {
                throw new ArgumentNullException(nameof(signatureScript));
            }

            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            if (!IsPayToScriptHash(scriptPubKey))
            {
                return ZkUnitsOf(scriptPubKey);
            }

            var pushes = new List<ParsedOp>();
            if (signatureScript.Length == 0 || Parse(signatureScript, pushes) != null || pushes.Count == 0)
            {
                return 0;
            }

            return ZkUnitsOf(pushes[pushes.Count - 1].Data);
        }

        /// <summary>
        /// Runs one opcode against a preset data stack. Index 0 of <paramref name="stack"/> is the bottom.
        /// Used by the opcodes.rs table tests. The meter limit is <see cref="ulong.MaxValue"/>.
        /// </summary>
        internal static ScriptError? ExecuteOpcodeForTest(byte code, byte[] data, IReadOnlyList<byte[]> stack, out byte[][] result)
        {
            return ExecuteOpcodeForTest(code, data, stack, null, out result);
        }

        /// <summary>
        /// Runs one opcode against a preset data stack and <paramref name="transaction"/> when the opcode reads the transaction.
        /// The transaction needs one input when the opcode indexes it. A null transaction is an empty version-0 transaction with one input.
        /// </summary>
        internal static ScriptError? ExecuteOpcodeForTest(byte code, byte[] data, IReadOnlyList<byte[]> stack, Transaction? transaction, out byte[][] result)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (stack == null)
            {
                throw new ArgumentNullException(nameof(stack));
            }

            var tx = transaction ?? TestTransaction();
            var engine = new TxScriptEngine(
                tx,
                0,
                new UtxoEntry?[Math.Max(tx.Inputs.Count, 1)],
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                0,
                ScriptLimits.ScriptUnitsPerSigOpCountUnit,
                ulong.MaxValue,
                CovenantsContext.Empty,
                null,
                null,
                new SigCache());
            for (var i = 0; i < stack.Count; i++)
            {
                var pushed = engine._dstack.Push((byte[])stack[i].Clone(), meter: false);
                if (pushed != null)
                {
                    result = Array.Empty<byte[]>();
                    return pushed;
                }
            }

            var error = engine.ExecuteOpcode(new ParsedOp(code, data));
            result = new byte[engine._dstack.Count][];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = (byte[])engine._dstack[i].Clone();
            }

            return error;
        }

        private static Transaction TestTransaction()
        {
            var input = new TxIn(new OutPoint(uint256.Zero, 0), Script.Empty, 1, 0);
            var output = new TxOut(0UL, new ScriptPubKey(0, Script.Empty));
            return new Transaction(0, new[] { input }, new[] { output }, 0, SubnetworkId.Native, 0, Array.Empty<byte>());
        }

        private static ulong ZkUnitsOf(byte[] script)
        {
            var ops = new List<ParsedOp>();
            Parse(script, ops);
            ulong units = 0;
            for (var i = 0; i < ops.Count; i++)
            {
                if (ops[i].Code != (byte)OpcodeType.OP_ZKPRECOMPILE)
                {
                    continue;
                }

                var cost = i == 0 || ops[i - 1].Data.Length == 0
                    ? ZkPrecompile.R0SuccinctScriptUnits
                    : ZkPrecompile.Cost(ops[i - 1].Data[0]);
                units = ScriptLimits.SaturatingAdd(units, cost);
            }

            return units;
        }

        /// <summary>
        /// Sigops in a pay-to-script-hash redeem script, including from-stack checks and non-minimal numeric pubkey counts.
        /// Returns 0 when the script public key is not pay-to-script-hash or either script is malformed.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPubKey"/> is null. <paramref name="signatureScript"/> is null.</exception>
        public static ulong CountP2shSigOps(byte[] signatureScript, byte[] scriptPubKey)
        {
            if (signatureScript == null)
            {
                throw new ArgumentNullException(nameof(signatureScript));
            }

            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            if (!IsPayToScriptHash(scriptPubKey))
            {
                return 0;
            }

            var pushes = new List<ParsedOp>();
            if (Parse(signatureScript, pushes) != null || pushes.Count == 0)
            {
                return 0;
            }

            var redeem = new List<ParsedOp>();
            if (Parse(pushes[pushes.Count - 1].Data, redeem) != null)
            {
                return 0;
            }

            ulong sigops = 0;
            ulong? pending = null;
            for (var i = 0; i < redeem.Count; i++)
            {
                var count = pending;
                pending = null;
                var code = redeem[i].Code;
                if (IsSingleSigOp(code, includeFromStack: true))
                {
                    sigops++;
                }
                else if (IsMultiSigOp(code))
                {
                    sigops += count ?? (ulong)ScriptLimits.MaxPubKeysPerMultisig;
                }
                else if (code >= (byte)OpcodeType.OP_1 && code <= (byte)OpcodeType.OP_16)
                {
                    pending = (ulong)(code - (byte)OpcodeType.OP_1 + 1);
                }
                else if (code <= (byte)OpcodeType.OP_PUSHDATA4)
                {
                    if (ScriptNum.TryDeserializeI64(redeem[i].Data, out var number) == null
                        && number >= 1
                        && number <= ScriptLimits.MaxPubKeysPerMultisig)
                    {
                        pending = (ulong)number;
                    }
                }
            }

            return sigops;
        }

        private ScriptError? ExecuteInner()
        {
            if (_contextError != null)
            {
                return _contextError;
            }

            if (ScriptVersion > ScriptLimits.MaxScriptPublicKeyVersion)
            {
                return null;
            }

            var extra = _scriptPubKey.Length > ScriptLimits.StandardScriptPubKeyMaxSize
                ? (ulong)(_scriptPubKey.Length - ScriptLimits.StandardScriptPubKeyMaxSize)
                : 0UL;
            var charge = _meter.Consume(extra * ScriptLimits.ScriptUnitsPerGram);
            if (charge != null)
            {
                return charge;
            }

            if (_scriptSig.Length == 0 && _scriptPubKey.Length == 0)
            {
                return ScriptError.EvalFalse;
            }

            if (_scriptSig.Length > ScriptLimits.MaxScriptSize)
            {
                return ScriptError.ScriptSize(_scriptSig.Length);
            }

            if (_scriptPubKey.Length > ScriptLimits.MaxScriptSize)
            {
                return ScriptError.ScriptSize(_scriptPubKey.Length);
            }

            ScriptStack? saved = null;
            if (_scriptSig.Length != 0)
            {
                var sigError = ExecuteScript(_scriptSig, pushOnly: true);
                if (sigError != null)
                {
                    return sigError;
                }
            }

            if (_scriptPubKey.Length != 0)
            {
                if (_isP2Sh)
                {
                    saved = _dstack.CloneStack();
                }

                var pubError = ExecuteScript(_scriptPubKey, pushOnly: false);
                if (pubError != null)
                {
                    return pubError;
                }
            }

            if (_isP2Sh)
            {
                var condition = CheckSuccess(finalScript: false);
                if (condition != null)
                {
                    return condition;
                }

                if (saved == null)
                {
                    return ScriptError.EmptyStack;
                }

                _dstack.Clear();
                var restored = saved.Pop(out var redeem);
                if (restored != null)
                {
                    return restored;
                }

                for (var i = 0; i < saved.Count; i++)
                {
                    var moved = _dstack.Push(saved[i], meter: false);
                    if (moved != null)
                    {
                        return moved;
                    }
                }

                // Pop removed the redeem script from the clone. The items pushed above are the signature-script stack.
                var redeemError = ExecuteScript(redeem, pushOnly: false);
                if (redeemError != null)
                {
                    return redeemError;
                }

                return CheckSuccess(finalScript: true);
            }

            return CheckSuccess(finalScript: true);
        }

        private ScriptError? ExecuteScript(byte[] script, bool pushOnly)
        {
            var ops = new List<ParsedOp>();
            var parsed = Parse(script, ops);
            ScriptError? result = parsed;
            if (result == null)
            {
                for (var i = 0; i < ops.Count; i++)
                {
                    result = Step(ops[i], pushOnly);
                    if (result != null)
                    {
                        break;
                    }
                }
            }

            if (result == null && _cond.Count != 0)
            {
                return ScriptError.UnbalancedConditional;
            }

            _astack.Clear();
            _numOps = 0;
            return result;
        }

        private ScriptError? Step(ParsedOp op, bool pushOnly)
        {
            if (IsDisabled(op.Code))
            {
                return ScriptError.OpcodeDisabled(op.Code);
            }

            if (op.Code == (byte)OpcodeType.OP_VERIF || op.Code == (byte)OpcodeType.OP_VERNOTIF)
            {
                return ScriptError.OpcodeReserved(op.Code);
            }

            if (pushOnly && op.Code > ScriptLimits.NoCostOpcode)
            {
                return ScriptError.SignatureScriptNotPushOnly;
            }

            if (op.Code > ScriptLimits.NoCostOpcode)
            {
                _numOps++;
                if (_numOps > ScriptLimits.MaxOpsPerScript)
                {
                    return ScriptError.TooManyOperations;
                }
            }
            else if (op.Data.Length > ScriptLimits.MaxScriptElementSize)
            {
                return ScriptError.ElementTooBig(op.Data.Length, ScriptLimits.MaxScriptElementSize);
            }

            var conditional = op.Code >= (byte)OpcodeType.OP_IF && op.Code <= (byte)OpcodeType.OP_ENDIF;
            if (IsExecuting || conditional)
            {
                var executed = ExecuteOpcode(op);
                if (executed != null)
                {
                    return executed;
                }

                var pushed = ScriptLimits.SaturatingAdd(_dstack.PopPushedBytes(), _astack.PopPushedBytes());
                var charged = _meter.ChargePushed(pushed);
                if (charged != null)
                {
                    return charged;
                }
            }

            var depth = _astack.Count + _dstack.Count;
            if (depth > ScriptLimits.MaxStackSize)
            {
                return ScriptError.StackSizeExceeded(depth);
            }

            return null;
        }

        private ScriptError? CheckSuccess(bool finalScript)
        {
            if (finalScript)
            {
                if (_dstack.Count > 1)
                {
                    return ScriptError.CleanStack(_dstack.Count - 1);
                }

                if (_dstack.Count == 0)
                {
                    return ScriptError.EmptyStack;
                }
            }

            var error = PopBool(out var value);
            if (error != null)
            {
                return error;
            }

            return value ? null : ScriptError.EvalFalse;
        }

        private bool IsExecuting
        {
            get { return _cond.Count == 0 || _cond[_cond.Count - 1] == Cond.True; }
        }

        private static ulong CountUpper(byte[] script)
        {
            var ops = new List<ParsedOp>();
            var error = Parse(script, ops);
            ulong count = 0;
            var limit = error == null ? ops.Count : ops.Count;
            for (var i = 0; i < ops.Count; i++)
            {
                var code = ops[i].Code;
                if (IsSingleSigOp(code, includeFromStack: false))
                {
                    count++;
                }
                else if (IsMultiSigOp(code))
                {
                    if (i == 0)
                    {
                        count += (ulong)ScriptLimits.MaxPubKeysPerMultisig;
                    }
                    else
                    {
                        var previous = ops[i - 1].Code;
                        if (previous >= (byte)OpcodeType.OP_1 && previous <= (byte)OpcodeType.OP_16)
                        {
                            count += (ulong)(previous - (byte)OpcodeType.OP_1 + 1);
                        }
                        else
                        {
                            count += (ulong)ScriptLimits.MaxPubKeysPerMultisig;
                        }
                    }
                }
            }

            if (error != null)
            {
                return count;
            }

            return count;
        }

        private static bool IsSingleSigOp(byte code, bool includeFromStack)
        {
            if (code == (byte)OpcodeType.OP_CHECKSIG
                || code == (byte)OpcodeType.OP_CHECKSIGVERIFY
                || code == (byte)OpcodeType.OP_CHECKSIGECDSA)
            {
                return true;
            }

            return includeFromStack
                && (code == (byte)OpcodeType.OP_CHECKSIGFROMSTACK || code == (byte)OpcodeType.OP_CHECKSIGFROMSTACKECDSA);
        }

        private static bool IsMultiSigOp(byte code)
        {
            return code == (byte)OpcodeType.OP_CHECKMULTISIG
                || code == (byte)OpcodeType.OP_CHECKMULTISIGVERIFY
                || code == (byte)OpcodeType.OP_CHECKMULTISIGECDSA;
        }

        internal static bool IsPayToScriptHash(byte[] script)
        {
            return script.Length == 35
                && script[0] == (byte)OpcodeType.OP_BLAKE2B
                && script[1] == (byte)OpcodeType.OP_DATA_32
                && script[34] == (byte)OpcodeType.OP_EQUAL;
        }

        internal static bool IsDisabled(byte code)
        {
            return code == (byte)OpcodeType.OP_LEFT
                || code == (byte)OpcodeType.OP_RIGHT
                || code == (byte)OpcodeType.OP_2MUL
                || code == (byte)OpcodeType.OP_2DIV
                || code == (byte)OpcodeType.OP_LSHIFT
                || code == (byte)OpcodeType.OP_RSHIFT;
        }

        internal static ScriptError? Parse(byte[] script, List<ParsedOp> ops)
        {
            var offset = 0;
            while (offset < script.Length)
            {
                var code = script[offset++];
                if (code >= 1 && code <= 75)
                {
                    var error = Take(script, ref offset, code, out var data);
                    if (error != null)
                    {
                        return error;
                    }

                    ops.Add(new ParsedOp(code, data));
                    continue;
                }

                if (code == (byte)OpcodeType.OP_PUSHDATA1 || code == (byte)OpcodeType.OP_PUSHDATA2 || code == (byte)OpcodeType.OP_PUSHDATA4)
                {
                    var width = code == (byte)OpcodeType.OP_PUSHDATA1 ? 1 : code == (byte)OpcodeType.OP_PUSHDATA2 ? 2 : 4;
                    if (script.Length - offset < width)
                    {
                        return ScriptError.MalformedPushSize;
                    }

                    long length = 0;
                    for (var i = 0; i < width; i++)
                    {
                        length |= (long)script[offset++] << (8 * i);
                    }

                    var error = Take(script, ref offset, length, out var data);
                    if (error != null)
                    {
                        return error;
                    }

                    ops.Add(new ParsedOp(code, data));
                    continue;
                }

                ops.Add(new ParsedOp(code, Array.Empty<byte>()));
            }

            return null;
        }

        private static ScriptError? Take(byte[] script, ref int offset, long length, out byte[] data)
        {
            var available = script.Length - offset;
            if (length < 0 || length > available)
            {
                data = Array.Empty<byte>();
                return ScriptError.MalformedPush(length, available);
            }

            data = new byte[length];
            if (length > 0)
            {
                Buffer.BlockCopy(script, offset, data, 0, (int)length);
                offset += (int)length;
            }

            return null;
        }

        private Check CheckSchnorr(SigHashType hashType, byte[] key, byte[] signature)
        {
            if (!_hasInput)
            {
                return Check.Fail(ScriptError.InvalidSource("checksig requires a transaction input"));
            }

            var utxo = _utxos[_inputIndex];
            if (utxo == null)
            {
                return Check.Fail(ScriptError.InvalidSource("checksig requires a transaction input"));
            }

            var hash = _tx.GetSignatureHash(_inputIndex, utxo, hashType).ToBytes();
            return CheckSchnorrMessage(key, signature, hash);
        }

        private Check CheckEcdsa(SigHashType hashType, byte[] key, byte[] signature)
        {
            if (!_hasInput)
            {
                return Check.Fail(ScriptError.InvalidSource("checksig requires a transaction input"));
            }

            var utxo = _utxos[_inputIndex];
            if (utxo == null)
            {
                return Check.Fail(ScriptError.InvalidSource("checksig requires a transaction input"));
            }

            var hash = _tx.GetEcdsaSignatureHash(_inputIndex, utxo, hashType).ToBytes();
            return CheckEcdsaMessage(key, signature, hash);
        }

        private Check CheckSchnorrMessage(byte[] key, byte[] signature, byte[] message)
        {
            var charged = _meter.ConsumeSigOp(1);
            if (charged != null)
            {
                return Check.Fail(charged);
            }

            if (!ECXOnlyPubKey.TryCreate(key, out var publicKey) || publicKey == null)
            {
                return Check.Fail(ScriptError.InvalidPubkey("schnorr public key"));
            }

            if (!SecpSchnorrSignature.TryCreate(signature, out var parsed) || parsed == null)
            {
                return Check.Fail(ScriptError.InvalidSignature("schnorr signature"));
            }

            if (_cache.TryGet(ecdsa: false, key, signature, message, out var cached))
            {
                return Check.From(cached);
            }

            var valid = publicKey.SigVerifyBIP340(parsed, message);
            _cache.Store(ecdsa: false, key, signature, message, valid);
            return Check.From(valid);
        }

        private Check CheckEcdsaMessage(byte[] key, byte[] signature, byte[] message)
        {
            var charged = _meter.ConsumeSigOp(1);
            if (charged != null)
            {
                return Check.Fail(charged);
            }

            if (key.Length != 33)
            {
                return Check.Fail(ScriptError.PubKeyFormat);
            }

            if (!ECPubKey.TryCreate(key, null, out _, out var publicKey) || publicKey == null)
            {
                return Check.Fail(ScriptError.InvalidPubkey("ecdsa public key"));
            }

            if (!SecpECDSASignature.TryCreateFromCompact(signature, out var parsed) || parsed == null)
            {
                return Check.Fail(ScriptError.InvalidSignature("ecdsa signature"));
            }

            if (_cache.TryGet(ecdsa: true, key, signature, message, out var cached))
            {
                return Check.From(cached);
            }

            var valid = publicKey.SigVerify(parsed, message);
            _cache.Store(ecdsa: true, key, signature, message, valid);
            return Check.From(valid);
        }

        private readonly struct Check
        {
            private Check(ScriptError? error, bool valid)
            {
                Error = error;
                Valid = valid;
            }

            internal ScriptError? Error { get; }

            internal bool Valid { get; }

            internal static Check From(bool valid)
            {
                return new Check(null, valid);
            }

            internal static Check Fail(ScriptError error)
            {
                return new Check(error, false);
            }
        }

        private enum Cond
        {
            Skip,
            True,
            False,
        }

        internal readonly struct ParsedOp
        {
            internal ParsedOp(byte code, byte[] data)
            {
                Code = code;
                Data = data;
            }

            internal byte Code { get; }

            internal byte[] Data { get; }
        }

        private sealed class SigCache
        {
            private readonly Dictionary<SigCacheKey, bool> _items = new Dictionary<SigCacheKey, bool>();

            internal bool TryGet(bool ecdsa, byte[] key, byte[] signature, byte[] message, out bool valid)
            {
                return _items.TryGetValue(new SigCacheKey(ecdsa, key, signature, message), out valid);
            }

            internal void Store(bool ecdsa, byte[] key, byte[] signature, byte[] message, bool valid)
            {
                _items[new SigCacheKey(ecdsa, key, signature, message)] = valid;
            }
        }

        private readonly struct SigCacheKey : IEquatable<SigCacheKey>
        {
            private readonly bool _ecdsa;
            private readonly byte[] _key;
            private readonly byte[] _signature;
            private readonly byte[] _message;

            internal SigCacheKey(bool ecdsa, byte[] key, byte[] signature, byte[] message)
            {
                _ecdsa = ecdsa;
                _key = key;
                _signature = signature;
                _message = message;
            }

            public bool Equals(SigCacheKey other)
            {
                return _ecdsa == other._ecdsa
                    && _key.AsSpan().SequenceEqual(other._key)
                    && _signature.AsSpan().SequenceEqual(other._signature)
                    && _message.AsSpan().SequenceEqual(other._message);
            }

            public override bool Equals(object? obj)
            {
                return obj is SigCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                var hash = _ecdsa ? 17 : 31;
                hash = Fold(hash, _key);
                hash = Fold(hash, _signature);
                hash = Fold(hash, _message);
                return hash;
            }

            private static int Fold(int hash, byte[] bytes)
            {
                for (var i = 0; i < bytes.Length; i++)
                {
                    hash = unchecked((hash * 31) + bytes[i]);
                }

                return hash;
            }
        }
    }
}
