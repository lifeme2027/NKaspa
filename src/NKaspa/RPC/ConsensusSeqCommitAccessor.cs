namespace NKaspa.RPC
{
    /// <summary>Why <see cref="ConsensusSeqCommitAccessor"/> refused to answer.</summary>
    public enum ConsensusSeqCommitFailure
    {
        /// <summary>getBlock or getBlockDagInfo omitted a field the walk needs, or returned no block.</summary>
        IncompleteNodeData,

        /// <summary>The header, verbose data, and selected-parent links disagree.</summary>
        InconsistentData,

        /// <summary>The sink changed, or the caller's chain block is no longer on the selected-parent chain.</summary>
        Reorg,

        /// <summary>The request cap was hit before the chain position was proven.</summary>
        RequestLimit,
    }

    /// <summary>
    /// Thrown when a consensus seqcommit answer cannot be proven from the node's read-only data.
    /// The accessor does not turn this into a script success or a silent null.
    /// </summary>
    public sealed class ConsensusSeqCommitException : Exception
    {
        /// <summary>Creates the failure.</summary>
        public ConsensusSeqCommitException(ConsensusSeqCommitFailure failure, string message, uint256? block = null, Exception? inner = null)
            : base(message, inner)
        {
            Failure = failure;
            Block = block;
        }

        /// <summary>Failure class.</summary>
        public ConsensusSeqCommitFailure Failure { get; }

        /// <summary>Block the failure refers to, when there is one.</summary>
        public uint256? Block { get; }
    }

    /// <summary>
    /// Read-only <see cref="ISeqCommitAccessor"/> that follows the selected-parent chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// rusty-kaspa evaluates <c>OP_CHAINBLOCKSEQCOMMIT</c> from the selected parent <c>P</c> of the chain block
    /// being validated (<c>consensus/src/model/services/seq_commit_accessor.rs</c>, KIP-21).
    /// <c>X</c> is admitted only when it is a chain ancestor of <c>P</c> and
    /// <c>seq_commit_within_threshold</c> holds: <c>X.blue_score + F &gt; P.blue_score</c>.
    /// The commitment is <c>X</c>'s <c>accepted_id_merkle_root</c>.
    /// After the Toccata cleanup on master, that accessor no longer branches on a DAA activation score.
    /// This type follows that rule.
    /// </para>
    /// <para>
    /// Post-Toccata, the first direct parent of a selected-chain block is its selected parent
    /// (<c>Header::post_toccata_chainblock_selected_parent</c>). Each step checks
    /// <c>verboseData.selectedParentHash</c> against <c>header.parents[0]</c> (or <c>parentsByLevel[0]</c>),
    /// checks that header and verbose blue scores agree, and checks that <c>isChainBlock</c> agrees with the walk.
    /// Blue score must strictly decrease along the walk. A cycle is rejected.
    /// </para>
    /// <para>
    /// The default point of view is <c>getBlockDagInfo.sink</c>, the virtual selected parent used for a block template.
    /// Set <see cref="ChainBlock"/> to chain block <c>B</c> to use <c>selected_parent(B)</c>, which is the point of view
    /// used while validating transactions inside <c>B</c>. Set <see cref="PointOfView"/> when the caller already knows <c>P</c>.
    /// A call that sees the sink change, or that names a chain block no longer on the sink's selected-parent chain, throws
    /// <see cref="ConsensusSeqCommitFailure.Reorg"/> and does not return an answer. The next call can read the new chain.
    /// rusty-kaspa defines <c>is_chain_block(hash)</c> as <c>is_chain_ancestor_of(hash, sink)</c>, so the sink itself is a chain block.
    /// A block reached by the walk with <c>isChainBlock</c> false is read against <c>getBlockDagInfo</c> again.
    /// A new sink is a reorg: at 10 blocks per second the virtual selected parent can leave the chain between two reads.
    /// The same sink is inconsistent node data.
    /// </para>
    /// <para>
    /// A missing block, a missing parent, or a missing header field throws <see cref="ConsensusSeqCommitFailure.IncompleteNodeData"/>.
    /// It is not reported as pruned. <see cref="IsChainAncestor"/> therefore returns true or false, or throws.
    /// <see cref="SeqCommitmentWithinDepth"/> returns null only after the block is proven to be an ancestor outside the window.
    /// Calls stop at <see cref="MaxRequests"/>. Cached blocks are not requested again. This type is not thread-safe.
    /// It calls <c>getBlock</c> and <c>getBlockDagInfo</c> only. It does not call <c>submitTransaction</c> and it does not open a socket.
    /// </para>
    /// <para>
    /// A light client can only approach consensus. The full finality window is hundreds of thousands of selected-parent steps,
    /// and reachability for a pruned block lives in the node. Matching consensus exactly requires running a node.
    /// </para>
    /// </remarks>
    public sealed class ConsensusSeqCommitAccessor : ISeqCommitAccessor
    {
        /// <summary>Finality depth used by the 10-blocks-per-second networks. 432000 blue-score units, about 12 hours.</summary>
        public const ulong DefaultFinalityDepth = 432000;

        /// <summary>
        /// Default cap on getBlock and getBlockDagInfo calls for one accessor.
        /// The walk stops and throws instead of scanning the whole finality window.
        /// </summary>
        public const int DefaultMaxRequests = 64;

        private readonly IKaspaRpcClient _client;
        private readonly ulong _finalityDepth;
        private readonly int _maxRequests;
        private readonly Dictionary<string, RpcChainBlock> _blocks = new Dictionary<string, RpcChainBlock>(StringComparer.Ordinal);
        private uint256? _sink;
        private int _requests;

        /// <summary>Creates an accessor. <paramref name="finalityDepth"/> is the KIP-21 window <c>F</c>.</summary>
        public ConsensusSeqCommitAccessor(IKaspaRpcClient client, ulong finalityDepth = DefaultFinalityDepth, int maxRequests = DefaultMaxRequests)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (maxRequests < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRequests), "The request limit must be at least 1.");
            }

            _finalityDepth = finalityDepth;
            _maxRequests = maxRequests;
        }

        /// <summary>KIP-21 window <c>F</c> in blue-score units.</summary>
        public ulong FinalityDepth
        {
            get { return _finalityDepth; }
        }

        /// <summary>Maximum number of getBlock and getBlockDagInfo calls.</summary>
        public int MaxRequests
        {
            get { return _maxRequests; }
        }

        /// <summary>Calls made so far. Cache hits are not counted. A reorg does not reset the count.</summary>
        public int RequestCount
        {
            get { return _requests; }
        }

        /// <summary>
        /// Selected parent <c>P</c>. Null uses <c>getBlockDagInfo.sink</c>, the virtual selected parent.
        /// Do not set this together with <see cref="ChainBlock"/>.
        /// </summary>
        public uint256? PointOfView { get; set; }

        /// <summary>
        /// Chain block <c>B</c> whose transactions are being validated.
        /// <c>P</c> becomes <c>selected_parent(B)</c>. Do not set this together with <see cref="PointOfView"/>.
        /// </summary>
        public uint256? ChainBlock { get; set; }

        /// <inheritdoc />
        public bool? IsChainAncestor(uint256 blockHash)
        {
            if (blockHash == null)
            {
                throw new ArgumentNullException(nameof(blockHash));
            }

            RejectMixedView();
            var dag = Begin();
            var pov = Resolve(dag);
            var target = RequireBlock(blockHash);
            var answer = IsAncestor(pov, target);
            Confirm(dag.Sink);
            return answer;
        }

        /// <inheritdoc />
        public uint256? SeqCommitmentWithinDepth(uint256 blockHash)
        {
            if (blockHash == null)
            {
                throw new ArgumentNullException(nameof(blockHash));
            }

            RejectMixedView();
            var dag = Begin();
            var pov = Resolve(dag);
            var target = RequireBlock(blockHash);
            var onChain = IsAncestor(pov, target);
            Confirm(dag.Sink);
            if (!onChain)
            {
                throw Fail(
                    ConsensusSeqCommitFailure.InconsistentData,
                    "seq commitment was requested for " + blockHash + ", which is not a selected-parent ancestor of " + pov.Hash + ".",
                    blockHash);
            }

            if (!WithinWindow(Score(target), Score(pov), _finalityDepth))
            {
                return null;
            }

            if (target.AcceptedIdMerkleRoot == null)
            {
                throw Fail(
                    ConsensusSeqCommitFailure.IncompleteNodeData,
                    "acceptedIdMerkleRoot is missing for " + target.Hash + ".",
                    target.Hash);
            }

            return target.AcceptedIdMerkleRoot;
        }

        private void RejectMixedView()
        {
            if (PointOfView != null && ChainBlock != null)
            {
                throw new ArgumentException("Set PointOfView or ChainBlock, not both. PointOfView is the selected parent P. ChainBlock is the chain block B whose selected parent becomes P.");
            }
        }

        private RpcBlockDagInfo Begin()
        {
            var dag = ReadDag();
            if (_sink != null && dag.Sink != _sink)
            {
                var previous = _sink;
                _blocks.Clear();
                _sink = null;
                throw Fail(
                    ConsensusSeqCommitFailure.Reorg,
                    "getBlockDagInfo.sink changed from " + previous + " to " + dag.Sink + ".",
                    dag.Sink);
            }

            _sink = dag.Sink;
            return dag;
        }

        private void Confirm(uint256 sink)
        {
            var dag = ReadDag();
            if (dag.Sink != sink)
            {
                _blocks.Clear();
                _sink = null;
                throw Fail(
                    ConsensusSeqCommitFailure.Reorg,
                    "getBlockDagInfo.sink changed from " + sink + " to " + dag.Sink + " during the selected-parent walk.",
                    dag.Sink);
            }
        }

        private RpcChainBlock Resolve(RpcBlockDagInfo dag)
        {
            var sink = RequireBlock(dag.Sink);
            RequireOnChainFlag(sink);
            Score(sink);
            if (ChainBlock != null)
            {
                var block = RequireBlock(ChainBlock);
                if (block.Hash != sink.Hash)
                {
                    EnsureOnChain(sink, block);
                }

                var parentHash = NextParent(block);
                if (parentHash == null)
                {
                    throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "chain block " + block.Hash + " has no selected parent.", block.Hash);
                }

                var parent = RequireBlock(parentHash);
                CheckLink(block, parent);
                return parent;
            }

            if (PointOfView != null)
            {
                var view = RequireBlock(PointOfView);
                if (view.Hash != sink.Hash)
                {
                    EnsureOnChain(sink, view);
                }

                RequireOnChainFlag(view);
                return view;
            }

            return sink;
        }

        private bool IsAncestor(RpcChainBlock pov, RpcChainBlock target)
        {
            Score(pov);
            Score(target);
            if (pov.Hash == target.Hash)
            {
                RequireOnChainFlag(target);
                return true;
            }

            if (Score(target) >= Score(pov))
            {
                return NotAncestor(pov, target);
            }

            var current = pov;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (current.Hash != target.Hash)
            {
                if (!seen.Add(current.Hash.ToString()))
                {
                    throw Fail(ConsensusSeqCommitFailure.InconsistentData, "selected parent chain cycled at " + current.Hash + ".", current.Hash);
                }

                if (Score(current) <= Score(target))
                {
                    return NotAncestor(pov, target);
                }

                var parentHash = NextParent(current);
                if (parentHash == null)
                {
                    return NotAncestor(pov, target);
                }

                var parent = RequireBlock(parentHash);
                CheckLink(current, parent);
                current = parent;
            }

            RequireOnChainFlag(target);
            return true;
        }

        private bool NotAncestor(RpcChainBlock pov, RpcChainBlock target)
        {
            if (!target.ChainFlagPresent)
            {
                throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "verboseData.isChainBlock is missing for " + target.Hash + ".", target.Hash);
            }

            if (!target.IsChainBlock)
            {
                return false;
            }

            if (Score(target) > Score(pov))
            {
                return false;
            }

            if (Score(target) == Score(pov))
            {
                throw Fail(
                    ConsensusSeqCommitFailure.InconsistentData,
                    "chain block " + target.Hash + " has the same blue score as " + pov.Hash + " but a different hash.",
                    target.Hash);
            }

            throw Fail(
                ConsensusSeqCommitFailure.InconsistentData,
                "isChainBlock is true for " + target.Hash + ", but the selected parent walk did not reach it.",
                target.Hash);
        }

        private void EnsureOnChain(RpcChainBlock sink, RpcChainBlock block)
        {
            var current = sink;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (current.Hash != block.Hash)
            {
                if (!seen.Add(current.Hash.ToString()))
                {
                    throw Fail(ConsensusSeqCommitFailure.InconsistentData, "selected parent chain cycled at " + current.Hash + ".", current.Hash);
                }

                if (Score(current) <= Score(block))
                {
                    throw NotOnChain(sink, block);
                }

                var parentHash = NextParent(current);
                if (parentHash == null)
                {
                    throw NotOnChain(sink, block);
                }

                var parent = RequireBlock(parentHash);
                CheckLink(current, parent);
                current = parent;
            }

            RequireOnChainFlag(block);
        }

        private uint256? NextParent(RpcChainBlock block)
        {
            RequireOnChainFlag(block);
            if (!block.ParentsPresent)
            {
                throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "header.parents is missing for " + block.Hash + ".", block.Hash);
            }

            if (block.DirectParent == null)
            {
                if (block.SelectedParentHash != null && block.SelectedParentHash != uint256.Zero)
                {
                    throw Fail(
                        ConsensusSeqCommitFailure.InconsistentData,
                        "selectedParentHash is set but header.parents is empty for " + block.Hash + ".",
                        block.Hash);
                }

                return null;
            }

            if (block.SelectedParentHash == null)
            {
                throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "verboseData.selectedParentHash is missing for " + block.Hash + ".", block.Hash);
            }

            if (block.SelectedParentHash != block.DirectParent)
            {
                throw Fail(
                    ConsensusSeqCommitFailure.InconsistentData,
                    "selectedParentHash " + block.SelectedParentHash + " does not match the first direct parent " + block.DirectParent + " of " + block.Hash + ".",
                    block.Hash);
            }

            return block.SelectedParentHash;
        }

        private void CheckLink(RpcChainBlock child, RpcChainBlock parent)
        {
            RequireOnChainFlag(parent);
            var childScore = Score(child);
            var parentScore = Score(parent);
            if (parentScore >= childScore)
            {
                throw Fail(
                    ConsensusSeqCommitFailure.InconsistentData,
                    "blue score does not decrease from " + child.Hash + " (" + childScore.ToString(System.Globalization.CultureInfo.InvariantCulture) + ") to selected parent " + parent.Hash + " (" + parentScore.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").",
                    child.Hash);
            }
        }

        private void RequireOnChainFlag(RpcChainBlock block)
        {
            if (!block.ChainFlagPresent)
            {
                throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "verboseData.isChainBlock is missing for " + block.Hash + ".", block.Hash);
            }

            if (block.IsChainBlock)
            {
                return;
            }

            // is_chain_block is measured against the sink at the time of getBlock.
            // A false flag on a block this walk reached means the sink moved, or the node disagrees with itself.
            var again = ReadDag();
            if (_sink != null && again.Sink != _sink)
            {
                var previous = _sink;
                _blocks.Clear();
                _sink = null;
                throw Fail(
                    ConsensusSeqCommitFailure.Reorg,
                    "getBlockDagInfo.sink changed from " + previous + " to " + again.Sink + " while checking " + block.Hash + ".",
                    again.Sink);
            }

            throw Fail(
                ConsensusSeqCommitFailure.InconsistentData,
                "selected parent walk reached " + block.Hash + " but isChainBlock is false.",
                block.Hash);
        }

        private ulong Score(RpcChainBlock block)
        {
            if (block.HeaderBlueScore == null && block.VerboseBlueScore == null)
            {
                throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "blueScore is missing for " + block.Hash + ".", block.Hash);
            }

            if (block.HeaderBlueScore != null && block.VerboseBlueScore != null && block.HeaderBlueScore.Value != block.VerboseBlueScore.Value)
            {
                throw Fail(
                    ConsensusSeqCommitFailure.InconsistentData,
                    "header blueScore does not match verboseData blueScore for " + block.Hash + ".",
                    block.Hash);
            }

            if (block.HeaderBlueScore != null)
            {
                return block.HeaderBlueScore.Value;
            }

            return block.VerboseBlueScore!.Value;
        }

        private void EnsureIdentity(RpcChainBlock block)
        {
            if (block.HeaderHash != null && block.HeaderHash != block.Hash)
            {
                throw Fail(
                    ConsensusSeqCommitFailure.InconsistentData,
                    "header.hash " + block.HeaderHash + " does not match the requested block " + block.Hash + ".",
                    block.Hash);
            }

            if (block.VerboseHash != null && block.VerboseHash != block.Hash)
            {
                throw Fail(
                    ConsensusSeqCommitFailure.InconsistentData,
                    "verboseData.hash " + block.VerboseHash + " does not match the requested block " + block.Hash + ".",
                    block.Hash);
            }
        }

        private RpcChainBlock RequireBlock(uint256 hash)
        {
            var block = Find(hash);
            if (block == null)
            {
                throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "getBlock did not return " + hash + ". The node data is incomplete.", hash);
            }

            EnsureIdentity(block);
            return block;
        }

        private RpcChainBlock? Find(uint256 hash)
        {
            var key = hash.ToString();
            if (_blocks.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Charge();
            var block = _client.GetBlockAsync(hash, includeTransactions: false).GetAwaiter().GetResult();
            if (block != null)
            {
                _blocks[key] = block;
            }

            return block;
        }

        private RpcBlockDagInfo ReadDag()
        {
            Charge();
            try
            {
                return _client.GetBlockDagInfoAsync().GetAwaiter().GetResult();
            }
            catch (KaspaRpcException ex)
            {
                throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "getBlockDagInfo failed: " + ex.Message, null, ex);
            }
        }

        private void Charge()
        {
            if (_requests >= _maxRequests)
            {
                throw Fail(
                    ConsensusSeqCommitFailure.RequestLimit,
                    "Request limit of " + _maxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture) + " was reached before the selected parent chain could be checked.");
            }

            _requests++;
        }

        private static ConsensusSeqCommitException NotOnChain(RpcChainBlock sink, RpcChainBlock block)
        {
            return Fail(
                ConsensusSeqCommitFailure.Reorg,
                block.Hash + " is not on the selected parent chain of sink " + sink.Hash + ".",
                block.Hash);
        }

        private static ConsensusSeqCommitException Fail(ConsensusSeqCommitFailure failure, string message, uint256? block = null, Exception? inner = null)
        {
            return new ConsensusSeqCommitException(failure, message, block, inner);
        }

        /// <summary>KIP-21 <c>X.blue_score + F &gt; P.blue_score</c>. A sum that would overflow is inside the window.</summary>
        private static bool WithinWindow(ulong blockScore, ulong pointOfViewScore, ulong finalityDepth)
        {
            if (blockScore > ulong.MaxValue - finalityDepth)
            {
                return true;
            }

            return blockScore + finalityDepth > pointOfViewScore;
        }
    }
}
