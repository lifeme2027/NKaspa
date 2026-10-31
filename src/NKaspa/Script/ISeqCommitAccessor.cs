namespace NKaspa
{
    /// <summary>
    /// Block-chain view used by <c>OP_CHAINBLOCKSEQCOMMIT</c> (KIP-21).
    /// Without an accessor the opcode is rejected as undefined. The engine does not query a node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Consensus evaluates the opcode from the selected parent of the chain block being validated.
    /// Let <c>P</c> be that point of view and <c>X</c> the block named on the stack.
    /// The opcode pushes <c>X</c>'s <c>accepted_id_merkle_root</c> only when all of these hold:
    /// <c>X</c> is known, <c>X</c> is on the selected-parent chain of <c>P</c>,
    /// and <c>X.blue_score + F &gt; P.blue_score</c>.
    /// <c>F</c> is the finality depth. At 10 blocks per second that depth is 432000, about 12 hours.
    /// </para>
    /// <para>
    /// <see cref="IsChainAncestor"/> answers the chain test.
    /// Null means the block is already pruned or unknown, and the engine returns <see cref="ScriptErrorCode.BlockAlreadyPruned"/>.
    /// False means the block is known and is not a chain ancestor, and the engine returns <see cref="ScriptErrorCode.BlockNotSelected"/>.
    /// </para>
    /// <para>
    /// <see cref="SeqCommitmentWithinDepth"/> answers the window and returns the 32-byte commitment.
    /// Null means the ancestor is deeper than <c>F</c>, and the engine returns <see cref="ScriptErrorCode.BlockIsTooDeep"/>.
    /// The engine calls this method only after <see cref="IsChainAncestor"/> returned true.
    /// </para>
    /// <para>
    /// <see cref="NKaspa.RPC.WrpcSeqCommitAccessor"/> is an optional read-only helper. Its point of view is the DAG sink,
    /// not the selected parent of a block under validation, so it is not a consensus implementation.
    /// <see cref="NKaspa.RPC.ConsensusSeqCommitAccessor"/> walks that selected parent chain over the same read-only calls.
    /// It freezes a sink snapshot. Forward growth is not a reorg. It throws when the node data is incomplete,
    /// inconsistent, or still reorged after retries. A light client can only approach consensus.
    /// </para>
    /// </remarks>
    public interface ISeqCommitAccessor
    {
        /// <summary>
        /// Whether <paramref name="blockHash"/> is a chain ancestor of the current point of view.
        /// Null means the block is already pruned or unknown.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="blockHash"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">
        /// wRPC request is missing a numeric id. Set PointOfView or ChainBlock, not both. PointOfView is the
        /// selected parent P. ChainBlock is the chain block B whose selected parent becomes P.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isChainBlock. Expected a hex hash for {name}. uint256 hex must be 32 bytes.
        /// uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a parent hash. Expected parent hashes. Expected an array for parents. Missing JSON string
        /// {name}. Missing JSON property {name}. Hash is missing. networkId is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        /// <exception cref="NKaspa.RPC.ConsensusSeqCommitException">
        /// getBlockDagInfo.sink changed from {previous} to {dag.Sink} and the new selected chain does not
        /// contain {anchor}. verboseData.isChainBlock is missing for {current.Hash}. selected parent chain
        /// cycled at {current.Hash}. isChainBlock is false for {block.Hash} on the selected chain.
        /// verboseData.isChainBlock is missing for {block.Hash}. getBlock did not return {hash}. The node data
        /// is incomplete. verboseData.hash {block.VerboseHash} does not match the requested block {block.Hash}.
        /// header.hash {block.HeaderHash} does not match the requested block {block.Hash}. Request limit of
        /// {_maxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)} was reached before the
        /// selected parent chain could be checked. blue score does not decrease from {child.Hash}
        /// ({childScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}) to selected parent
        /// {parent.Hash} ({parentScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}). header
        /// blueScore does not match verboseData blueScore for {block.Hash}. blueScore is missing for
        /// {block.Hash}. selected parent walk reached {block.Hash} but isChainBlock is false. isChainBlock is
        /// false for {block.Hash} and getBlockDagInfo.sink changed from {previous} to {again.Sink}.
        /// getBlockDagInfo failed: {ex.Message}. selectedParentHash {block.SelectedParentHash} does not match
        /// the first direct parent {block.DirectParent} of {block.Hash}. verboseData.selectedParentHash is
        /// missing for {block.Hash}. selectedParentHash is set but header.parents is empty for {block.Hash}.
        /// Further failures of this type use the same exception.
        /// </exception>
        bool? IsChainAncestor(uint256 blockHash);

        /// <summary>
        /// <c>accepted_id_merkle_root</c> of <paramref name="blockHash"/> when it is still inside the blue-score window
        /// <c>X.blue_score + F &gt; P.blue_score</c>.
        /// Null means the ancestor is too deep.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="blockHash"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">
        /// wRPC request is missing a numeric id. Set PointOfView or ChainBlock, not both. PointOfView is the
        /// selected parent P. ChainBlock is the chain block B whose selected parent becomes P.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isChainBlock. Expected a hex hash for {name}. uint256 hex must be 32 bytes.
        /// uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a parent hash. Expected parent hashes. Expected an array for parents. Missing JSON string
        /// {name}. Missing JSON property {name}. Hash is missing. networkId is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
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
        uint256? SeqCommitmentWithinDepth(uint256 blockHash);
    }
}
