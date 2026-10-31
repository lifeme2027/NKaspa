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
    /// <see cref="NKaspa.RPC.ConsensusSeqCommitAccessor"/> walks that selected parent chain over the same read-only calls
    /// and throws when the node data is incomplete, inconsistent, or reorged. A light client can only approach consensus.
    /// </para>
    /// </remarks>
    public interface ISeqCommitAccessor
    {
        /// <summary>
        /// Whether <paramref name="blockHash"/> is a chain ancestor of the current point of view.
        /// Null means the block is already pruned or unknown.
        /// </summary>
        bool? IsChainAncestor(uint256 blockHash);

        /// <summary>
        /// <c>accepted_id_merkle_root</c> of <paramref name="blockHash"/> when it is still inside the blue-score window
        /// <c>X.blue_score + F &gt; P.blue_score</c>.
        /// Null means the ancestor is too deep.
        /// </summary>
        uint256? SeqCommitmentWithinDepth(uint256 blockHash);
    }
}
