namespace NKaspa.RPC
{
    /// <summary>
    /// Read-only <see cref="ISeqCommitAccessor"/> over <see cref="IKaspaRpcClient.GetBlockAsync"/> and <see cref="IKaspaRpcClient.GetBlockDagInfoAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The point of view is <c>getBlockDagInfo.sink</c>, then that header's blue score.
    /// Consensus uses the selected parent of the chain block being validated. This helper is for off-chain script checks.
    /// </para>
    /// <para>
    /// <see cref="FinalityDepth"/> defaults to 432000, the 10-blocks-per-second finality depth
    /// (<c>finality_depth</c> in rusty-kaspa <c>params.rs</c>, 12 hours at 100 ms).
    /// Another block rate must pass its own depth. The window is KIP-21 <c>X.blue_score + F &gt; P.blue_score</c>.
    /// </para>
    /// <para>
    /// An unknown block or an RPC error for that hash is treated as pruned.
    /// A header that omits <c>isChainBlock</c> is not a chain block. A non-boolean <c>isChainBlock</c> throws <see cref="FormatException"/>.
    /// A chain block whose header has no <c>acceptedIdMerkleRoot</c> returns null from
    /// <see cref="SeqCommitmentWithinDepth"/>, which the engine reports as too deep.
    /// This type calls <c>getBlock</c> and <c>getBlockDagInfo</c> only. It does not call <c>submitTransaction</c> and it does not open a socket.
    /// The interface is synchronous, so each call blocks on the client.
    /// </para>
    /// </remarks>
    public sealed class WrpcSeqCommitAccessor : ISeqCommitAccessor
    {
        /// <summary>Finality depth used by the 10-blocks-per-second networks. 432000 blue-score units, about 12 hours.</summary>
        public const ulong DefaultFinalityDepth = 432000;

        private readonly IKaspaRpcClient _client;
        private readonly ulong _finalityDepth;
        private readonly Dictionary<string, RpcChainBlock?> _blocks = new Dictionary<string, RpcChainBlock?>(StringComparer.Ordinal);

        /// <summary>Creates an accessor. <paramref name="finalityDepth"/> is the KIP-21 window <c>F</c>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="client"/> is null.</exception>
        public WrpcSeqCommitAccessor(IKaspaRpcClient client, ulong finalityDepth = DefaultFinalityDepth)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _finalityDepth = finalityDepth;
        }

        /// <summary>KIP-21 window <c>F</c> in blue-score units.</summary>
        public ulong FinalityDepth
        {
            get { return _finalityDepth; }
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException"><paramref name="blockHash"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isChainBlock. Expected a hex hash for {name}. uint256 hex must be 32 bytes.
        /// uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a parent hash. Expected parent hashes. Expected an array for parents.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        public bool? IsChainAncestor(uint256 blockHash)
        {
            if (blockHash == null)
            {
                throw new ArgumentNullException(nameof(blockHash));
            }

            var block = Find(blockHash);
            if (block == null)
            {
                return null;
            }

            return block.IsChainBlock;
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException"><paramref name="blockHash"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isChainBlock. Expected a hex hash for {name}. uint256 hex must be 32 bytes.
        /// uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a parent hash. Expected parent hashes. Expected an array for parents. Missing JSON string
        /// {name}. Missing JSON property {name}. Hash is missing. networkId is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        public uint256? SeqCommitmentWithinDepth(uint256 blockHash)
        {
            if (blockHash == null)
            {
                throw new ArgumentNullException(nameof(blockHash));
            }

            var block = Find(blockHash);
            if (block == null || !block.IsChainBlock || block.AcceptedIdMerkleRoot == null)
            {
                return null;
            }

            var sink = PointOfView();
            if (sink == null)
            {
                return null;
            }

            if (!WithinWindow(block.BlueScore, sink.BlueScore))
            {
                return null;
            }

            return block.AcceptedIdMerkleRoot;
        }

        private bool WithinWindow(ulong blockScore, ulong pointOfViewScore)
        {
            if (blockScore > ulong.MaxValue - _finalityDepth)
            {
                return true;
            }

            return blockScore + _finalityDepth > pointOfViewScore;
        }

        private RpcChainBlock? PointOfView()
        {
            var dag = _client.GetBlockDagInfoAsync().GetAwaiter().GetResult();
            return Find(dag.Sink);
        }

        private RpcChainBlock? Find(uint256 hash)
        {
            var key = hash.ToString();
            if (_blocks.TryGetValue(key, out var cached))
            {
                return cached;
            }

            RpcChainBlock? block;
            try
            {
                block = _client.GetBlockAsync(hash).GetAwaiter().GetResult();
            }
            catch (KaspaRpcException)
            {
                block = null;
            }

            _blocks[key] = block;
            return block;
        }
    }
}
