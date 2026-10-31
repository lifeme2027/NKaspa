namespace NKaspa.RPC
{
    /// <summary>Why <see cref="ConsensusSeqCommitAccessor"/> refused to answer.</summary>
    public enum ConsensusSeqCommitFailure
    {
        /// <summary>getBlock or getBlockDagInfo omitted a field the walk needs, or returned no block.</summary>
        IncompleteNodeData,

        /// <summary>The header, verbose data, and selected-parent links disagree.</summary>
        InconsistentData,

        /// <summary>
        /// The snapshot viewpoint, or a block on the walk, left the selected chain.
        /// Forward growth of the snapshot is not a reorg. Retries are used before this is thrown.
        /// </summary>
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
    /// Each call freezes that viewpoint and its blue score. The walk and the KIP-21 window use only the snapshot.
    /// At 10 blocks per second the sink hash changes during the walk. A later sink whose selected-parent chain still
    /// contains the snapshot is forward growth: the call returns the snapshot answer and does not throw.
    /// <see cref="ConsensusSeqCommitFailure.Reorg"/> is reserved for a snapshot viewpoint, or a block on the walk,
    /// that left the selected chain (<c>isChainBlock</c> false, or the new chain does not pass through it),
    /// and for a caller chain block that is not on the chain. Real reorgs are retried
    /// <see cref="MaxReorgRetries"/> times, then thrown. The request count is not reset.
    /// A sink that is already off the selected chain is replaced before the snapshot is frozen.
    /// <see cref="StableDepth"/> then steps the viewpoint back from that sink. The default is 20 steps,
    /// about two seconds at 10 blocks per second. Zero keeps the locked sink.
    /// A newer sink is walked for at most 32 selected parents. Farther growth, or a walk the request
    /// budget cannot finish, is accepted when a fresh <c>isChainBlock</c> is still true for the snapshot
    /// viewpoint and for every guarded block. That flag is the node's selected-chain test, so the call
    /// does not chase every block a 10-per-second tip adds during the snapshot walk.
    /// rusty-kaspa defines <c>is_chain_block(hash)</c> as <c>is_chain_ancestor_of(hash, sink)</c>, so the sink itself is a chain block.
    /// A block reached by the walk with <c>isChainBlock</c> false is read against <c>getBlockDagInfo</c> again.
    /// The same sink with that flag clear is inconsistent node data. A moved sink means the block left the chain.
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
        /// Blocks added after the snapshot are part of this cap.
        /// </summary>
        public const int DefaultMaxRequests = 256;

        /// <summary>Default number of extra attempts after a real reorg. The request count is kept.</summary>
        public const int DefaultReorgRetries = 3;

        /// <summary>
        /// Default selected-parent steps to step back from the sink. About two seconds at 10 blocks per second.
        /// Zero uses the sink itself.
        /// </summary>
        public const int DefaultStableDepth = 20;

        /// <summary>How many newer sinks to follow when the dag tip is already off the selected chain.</summary>
        private const int CatchUpHops = 8;

        /// <summary>
        /// Selected-parent steps to follow past the snapshot before trusting a fresh isChainBlock.
        /// At 10 blocks per second the tip moves while the snapshot walk is in flight.
        /// </summary>
        private const int MaxGrowthWalk = 32;

        private readonly IKaspaRpcClient _client;
        private readonly ulong _finalityDepth;
        private readonly int _maxRequests;
        private readonly Dictionary<string, RpcChainBlock> _blocks = new Dictionary<string, RpcChainBlock>(StringComparer.Ordinal);
        private readonly List<uint256> _guards = new List<uint256>();
        private uint256? _sink;
        private uint256? _snapshotSink;
        private uint256? _anchor;
        private ulong _anchorScore;
        private int _requests;
        private int _maxReorgRetries = DefaultReorgRetries;
        private int _stableDepth = DefaultStableDepth;
        private bool _retryReorg;

        /// <summary>Creates an accessor. <paramref name="finalityDepth"/> is the KIP-21 window <c>F</c>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="client"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">The request limit must be at least 1.</exception>
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

        /// <summary>Calls made so far. Cache hits are not counted. A reorg retry does not reset the count.</summary>
        public int RequestCount
        {
            get { return _requests; }
        }

        /// <summary>
        /// Extra attempts after a real reorg. Zero throws on the first one.
        /// The count is not reset between attempts.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Reorg retries cannot be negative.</exception>
        public int MaxReorgRetries
        {
            get { return _maxReorgRetries; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Reorg retries cannot be negative.");
                }

                _maxReorgRetries = value;
            }
        }

        /// <summary>
        /// Selected-parent steps to step back from the snapshot sink before using it as <c>P</c>.
        /// The default is <see cref="DefaultStableDepth"/>. Zero keeps the sink.
        /// Ignored when <see cref="PointOfView"/> or <see cref="ChainBlock"/> is set.
        /// The steps count toward <see cref="MaxRequests"/>.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Stable depth cannot be negative.</exception>
        public int StableDepth
        {
            get { return _stableDepth; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Stable depth cannot be negative.");
                }

                _stableDepth = value;
            }
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
        public bool? IsChainAncestor(uint256 blockHash)
        {
            if (blockHash == null)
            {
                throw new ArgumentNullException(nameof(blockHash));
            }

            RejectMixedView();
            return Attempt(() =>
            {
                var dag = Begin();
                var pov = Resolve(dag);
                var target = RequireBlock(blockHash);
                var answer = IsAncestor(pov, target);
                Confirm(pov);
                return answer;
            });
        }

        /// <inheritdoc />
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
        public uint256? SeqCommitmentWithinDepth(uint256 blockHash)
        {
            if (blockHash == null)
            {
                throw new ArgumentNullException(nameof(blockHash));
            }

            RejectMixedView();
            return Attempt(() =>
            {
                var dag = Begin();
                var pov = Resolve(dag);
                var target = RequireBlock(blockHash);
                var onChain = IsAncestor(pov, target);
                Confirm(pov);
                return Commitment(pov, target, onChain);
            });
        }

        private uint256? Commitment(RpcChainBlock pov, RpcChainBlock target, bool onChain)
        {
            if (!onChain)
            {
                throw Fail(
                    ConsensusSeqCommitFailure.InconsistentData,
                    "seq commitment was requested for " + target.Hash + ", which is not a selected-parent ancestor of " + pov.Hash + ".",
                    target.Hash);
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

        private T Attempt<T>(Func<T> body)
        {
            for (var attempt = 0; ; attempt++)
            {
                _retryReorg = false;
                try
                {
                    return body();
                }
                catch (ConsensusSeqCommitException ex) when (ex.Failure == ConsensusSeqCommitFailure.Reorg && _retryReorg && attempt < _maxReorgRetries)
                {
                    _blocks.Clear();
                    _sink = null;
                    _snapshotSink = null;
                    _anchor = null;
                    _guards.Clear();
                }
            }
        }

        private RpcBlockDagInfo Begin()
        {
            var dag = ReadDag();
            if (_sink != null && dag.Sink != _sink)
            {
                // The previous call's chain flags were proved against an older tip.
                // Header links are immutable. isChainBlock is not, so drop the cache.
                // A new call takes a new snapshot. Growth is not a reorg.
                _blocks.Clear();
            }

            _sink = dag.Sink;
            _snapshotSink = dag.Sink;
            _anchor = null;
            _guards.Clear();
            return dag;
        }

        private void Confirm(RpcChainBlock pov)
        {
            var anchor = _anchor ?? pov.Hash;
            var anchorScore = _anchor == null ? Score(pov) : _anchorScore;
            var snapshot = _snapshotSink;
            var dag = ReadDag();
            if (snapshot != null && dag.Sink == snapshot)
            {
                return;
            }

            if (!SelectedChainContains(dag.Sink, anchor, anchorScore))
            {
                var previous = snapshot;
                _blocks.Clear();
                _sink = null;
                _snapshotSink = null;
                _anchor = null;
                _guards.Clear();
                throw Reorg(
                    "getBlockDagInfo.sink changed from " + previous + " to " + dag.Sink + " and the new selected chain does not contain " + anchor + ".",
                    dag.Sink,
                    retry: true);
            }

            // Growth. Later calls may keep immutable headers. The answer stays on the snapshot.
            _sink = dag.Sink;
        }

        private bool SelectedChainContains(uint256 tip, uint256 anchor, ulong anchorScore)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = RequireBlock(tip);
            var walked = 0;
            while (current.Hash != anchor)
            {
                if (!seen.Add(current.Hash.ToString()))
                {
                    throw Fail(ConsensusSeqCommitFailure.InconsistentData, "selected parent chain cycled at " + current.Hash + ".", current.Hash);
                }

                // The new chain passed this blue score without hitting the snapshot.
                if (Score(current) <= anchorScore)
                {
                    return false;
                }

                var guardReads = _guards.Count == 0 ? 1 : _guards.Count;
                if (walked >= MaxGrowthWalk || _maxRequests - _requests < guardReads + 1)
                {
                    // The tip ran farther than this call should chase at 10 blocks per second.
                    // A fresh isChainBlock is rusty-kaspa is_chain_ancestor_of(hash, sink).
                    return GuardsStillOnChain();
                }

                if (!current.ChainFlagPresent)
                {
                    throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "verboseData.isChainBlock is missing for " + current.Hash + ".", current.Hash);
                }

                if (!current.IsChainBlock)
                {
                    // The dag named a sink that was already off the chain. Follow the newer one.
                    current = FollowMovedTip(current.Hash);
                    continue;
                }

                var parentHash = NextParent(current);
                if (parentHash == null)
                {
                    return false;
                }

                var parent = RequireBlock(parentHash);
                CheckLink(current, parent);
                current = parent;
                walked++;
            }

            if (seen.Count > 0)
            {
                // The anchor was loaded before the tip moved. Read its chain flag again.
                current = Refresh(anchor);
            }

            NoteChainFlag(current);
            seen.Add(current.Hash.ToString());
            for (var i = 0; i < _guards.Count; i++)
            {
                if (!seen.Contains(_guards[i].ToString()))
                {
                    return false;
                }
            }

            return true;
        }

        private bool GuardsStillOnChain()
        {
            if (_guards.Count == 0)
            {
                if (_anchor == null)
                {
                    return true;
                }

                NoteChainFlag(Refresh(_anchor));
                return true;
            }

            for (var i = 0; i < _guards.Count; i++)
            {
                NoteChainFlag(Refresh(_guards[i]));
            }

            return true;
        }

        private RpcChainBlock FollowMovedTip(uint256 rejected)
        {
            var hash = rejected;
            for (var hop = 0; hop < CatchUpHops; hop++)
            {
                var again = ReadDag();
                if (again.Sink == hash)
                {
                    throw Fail(
                        ConsensusSeqCommitFailure.InconsistentData,
                        "selected parent walk reached " + hash + " but isChainBlock is false.",
                        hash);
                }

                var sink = RequireBlock(again.Sink);
                if (!sink.ChainFlagPresent)
                {
                    throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "verboseData.isChainBlock is missing for " + sink.Hash + ".", sink.Hash);
                }

                if (sink.IsChainBlock)
                {
                    return sink;
                }

                hash = sink.Hash;
            }

            throw Reorg(
                "getBlockDagInfo.sink did not stay a chain block long enough to take a snapshot.",
                hash,
                retry: true);
        }

        private RpcChainBlock Refresh(uint256 hash)
        {
            _blocks.Remove(hash.ToString());
            return RequireBlock(hash);
        }

        private void NoteChainFlag(RpcChainBlock block)
        {
            if (!block.ChainFlagPresent)
            {
                throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "verboseData.isChainBlock is missing for " + block.Hash + ".", block.Hash);
            }

            if (!block.IsChainBlock)
            {
                throw Reorg("isChainBlock is false for " + block.Hash + " on the selected chain.", block.Hash, retry: true);
            }
        }

        private RpcChainBlock CatchUpSink(RpcBlockDagInfo dag)
        {
            var hash = dag.Sink;
            for (var hop = 0; hop < CatchUpHops; hop++)
            {
                var sink = RequireBlock(hash);
                if (!sink.ChainFlagPresent)
                {
                    throw Fail(ConsensusSeqCommitFailure.IncompleteNodeData, "verboseData.isChainBlock is missing for " + sink.Hash + ".", sink.Hash);
                }

                if (sink.IsChainBlock)
                {
                    Score(sink);
                    _snapshotSink = sink.Hash;
                    _sink = sink.Hash;
                    return sink;
                }

                if (hop == CatchUpHops - 1)
                {
                    break;
                }

                var again = ReadDag();
                if (again.Sink == hash)
                {
                    throw Fail(
                        ConsensusSeqCommitFailure.InconsistentData,
                        "selected parent walk reached " + hash + " but isChainBlock is false.",
                        hash);
                }

                hash = again.Sink;
            }

            throw Reorg(
                "getBlockDagInfo.sink did not stay a chain block long enough to take a snapshot.",
                hash,
                retry: true);
        }

        private RpcChainBlock Resolve(RpcBlockDagInfo dag)
        {
            var sink = CatchUpSink(dag);
            RpcChainBlock pov;
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
                pov = parent;
            }
            else if (PointOfView != null)
            {
                var view = RequireBlock(PointOfView);
                if (view.Hash != sink.Hash)
                {
                    EnsureOnChain(sink, view);
                }

                RequireOnChainFlag(view);
                pov = view;
            }
            else
            {
                pov = Retreat(sink);
            }

            _anchor = pov.Hash;
            _anchorScore = Score(pov);
            _guards.Clear();
            _guards.Add(pov.Hash);
            if (ChainBlock != null && ChainBlock != pov.Hash)
            {
                _guards.Add(ChainBlock);
            }

            return pov;
        }

        private RpcChainBlock Retreat(RpcChainBlock sink)
        {
            var current = sink;
            for (var step = 0; step < _stableDepth; step++)
            {
                var parentHash = NextParent(current);
                if (parentHash == null)
                {
                    return current;
                }

                var parent = RequireBlock(parentHash);
                CheckLink(current, parent);
                current = parent;
            }

            return current;
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
            if (_snapshotSink != null && again.Sink != _snapshotSink)
            {
                var previous = _snapshotSink;
                _blocks.Clear();
                _sink = null;
                _snapshotSink = null;
                _anchor = null;
                _guards.Clear();
                throw Reorg(
                    "isChainBlock is false for " + block.Hash + " and getBlockDagInfo.sink changed from " + previous + " to " + again.Sink + ".",
                    again.Sink,
                    retry: true);
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

        private ConsensusSeqCommitException NotOnChain(RpcChainBlock sink, RpcChainBlock block)
        {
            // The sink did not move. Retrying cannot put this block on the chain.
            _retryReorg = false;
            return Fail(
                ConsensusSeqCommitFailure.Reorg,
                block.Hash + " is not on the selected parent chain of sink " + sink.Hash + ".",
                block.Hash);
        }

        private ConsensusSeqCommitException Reorg(string message, uint256? block, bool retry)
        {
            _retryReorg = retry;
            return Fail(ConsensusSeqCommitFailure.Reorg, message, block);
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
