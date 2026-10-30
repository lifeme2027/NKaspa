namespace NKaspa
{
    /// <summary>
    /// Block-chain view used by <c>OP_CHAINBLOCKSEQCOMMIT</c>.
    /// Without an accessor the opcode is rejected as undefined. The engine does not query a node.
    /// </summary>
    public interface ISeqCommitAccessor
    {
        /// <summary>
        /// Whether <paramref name="blockHash"/> is a chain ancestor of the current point of view.
        /// Null means the block is already pruned.
        /// </summary>
        bool? IsChainAncestor(uint256 blockHash);

        /// <summary>
        /// Sequence commitment of <paramref name="blockHash"/> when it is still inside the depth window.
        /// Null means the ancestor is too deep.
        /// </summary>
        uint256? SeqCommitmentWithinDepth(uint256 blockHash);
    }
}
