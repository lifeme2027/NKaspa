namespace NKaspa
{
    /// <summary>
    /// Result of running one Kaspa input. Kaspa counterpart of NBitcoin.ScriptEvaluationContext.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Verify(Script, ScriptPubKey, Transaction, int, IReadOnlyList{UtxoEntry})"/> enforces the input's
    /// sigop count or compute budget. A null <see cref="Error"/> means the input is valid.
    /// </para>
    /// <para>
    /// The script public key passed here replaces the spent output's script for both execution and the signature hash.
    /// Amount, DAA score, coinbase flag, and covenant id stay on the <see cref="UtxoEntry"/>.
    /// </para>
    /// </remarks>
    public sealed class ScriptEvaluationContext
    {
        private ScriptEvaluationContext(TxScriptEngine engine)
        {
            Error = engine.Error;
            UsedSigOps = engine.UsedSigOps;
            UsedScriptUnits = engine.UsedScriptUnits;
        }

        /// <summary>Failure from the script, or null when the input is valid.</summary>
        public ScriptError? Error { get; }

        /// <summary>True when <see cref="Error"/> is null.</summary>
        public bool Result
        {
            get { return Error == null; }
        }

        /// <summary>Signature checks charged, including failed attempts until a later attempt matched.</summary>
        public ushort UsedSigOps { get; }

        /// <summary>Script units charged. A charge that did not fit is not included.</summary>
        public ulong UsedScriptUnits { get; }

        /// <summary>Executes <paramref name="scriptSig"/> against <paramref name="scriptPubKey"/>.</summary>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utxos"/> is null. <paramref name="scriptPubKey"/> is null. <paramref
        /// name="scriptSig"/> is null. <paramref name="tx"/> is null.
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
        public static ScriptEvaluationContext Verify(
            Script scriptSig,
            ScriptPubKey scriptPubKey,
            Transaction tx,
            int inputIndex,
            IReadOnlyList<UtxoEntry> utxos)
        {
            if (scriptSig == null)
            {
                throw new ArgumentNullException(nameof(scriptSig));
            }

            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            if (utxos == null)
            {
                throw new ArgumentNullException(nameof(utxos));
            }

            var copy = new UtxoEntry?[utxos.Count];
            for (var i = 0; i < utxos.Count; i++)
            {
                copy[i] = utxos[i];
            }

            var engine = TxScriptEngine.Run(scriptSig.ToBytes(), scriptPubKey, tx, inputIndex, copy);
            return new ScriptEvaluationContext(engine);
        }

        /// <summary>Executes <paramref name="scriptSig"/> against a version-0 <paramref name="scriptPubKey"/>.</summary>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="scriptPubKey"/> is null. <paramref name="utxos"/> is null. <paramref
        /// name="scriptSig"/> is null. <paramref name="tx"/> is null.
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
        public static ScriptEvaluationContext Verify(
            Script scriptSig,
            Script scriptPubKey,
            Transaction tx,
            int inputIndex,
            IReadOnlyList<UtxoEntry> utxos)
        {
            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            return Verify(scriptSig, new ScriptPubKey(0, scriptPubKey), tx, inputIndex, utxos);
        }
    }
}
