namespace NKaspa.RPC
{
    /// <summary>
    /// Error returned by a Kaspa wRPC response.
    /// </summary>
    public sealed class KaspaRpcException : Exception
    {
        /// <summary>Creates an exception from a wRPC error object.</summary>
        public KaspaRpcException(int code, string message)
            : base(message)
        {
            RpcCode = code;
        }

        /// <summary>Numeric code from the error object. Workflow-rpc often uses 0.</summary>
        public int RpcCode { get; }
    }

    /// <summary>
    /// Sends one wRPC JSON request and returns the response text.
    /// Tests use an in-memory implementation. <see cref="KaspaWebSocketChannel"/> speaks WebSocket.
    /// </summary>
    public interface IKaspaJsonChannel
    {
        /// <summary>Sends <paramref name="requestJson"/> and returns the raw response text.</summary>
        Task<string> ExchangeAsync(string requestJson, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Minimal Kaspa wRPC client.
    /// The JSON envelope is workflow-rpc: <c>id</c>, <c>method</c>, and <c>params</c>.
    /// Success responses carry the body in <c>params</c>. A <c>result</c> field is accepted when reading.
    /// </summary>
    public interface IKaspaRpcClient
    {
        /// <summary>getServerInfo.</summary>
        Task<RpcServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>getBlockDagInfo.</summary>
        Task<RpcBlockDagInfo> GetBlockDagInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>getUtxosByAddresses.</summary>
        Task<IReadOnlyList<RpcUtxo>> GetUtxosByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default);

        /// <summary>getBalanceByAddress.</summary>
        Task<Money> GetBalanceByAddressAsync(string address, CancellationToken cancellationToken = default);

        /// <summary>submitTransaction.</summary>
        Task<uint256> SubmitTransactionAsync(Transaction transaction, bool allowOrphan = false, CancellationToken cancellationToken = default);

        /// <summary>getFeeEstimate.</summary>
        Task<RpcFeeEstimate> GetFeeEstimateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// getBlock. <paramref name="includeTransactions"/> false asks for the header only.
        /// A missing block returns null. This method does not submit a transaction.
        /// </summary>
        Task<RpcChainBlock?> GetBlockAsync(uint256 hash, bool includeTransactions = false, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Header fields <c>OP_CHAINBLOCKSEQCOMMIT</c> reads from getBlock.
    /// <see cref="AcceptedIdMerkleRoot"/> is the sequence commitment after KIP-21.
    /// </summary>
    public sealed class RpcChainBlock
    {
        /// <summary>Creates the header view. Omitted consensus fields stay null or false.</summary>
        public RpcChainBlock(
            uint256 hash,
            ulong blueScore,
            uint256? acceptedIdMerkleRoot,
            bool isChainBlock,
            uint256? selectedParentHash = null,
            uint256? directParent = null,
            bool parentsPresent = false,
            bool chainFlagPresent = false,
            ulong? headerBlueScore = null,
            ulong? verboseBlueScore = null,
            uint256? headerHash = null,
            uint256? verboseHash = null)
        {
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            BlueScore = blueScore;
            AcceptedIdMerkleRoot = acceptedIdMerkleRoot;
            IsChainBlock = isChainBlock;
            SelectedParentHash = selectedParentHash;
            DirectParent = directParent;
            ParentsPresent = parentsPresent;
            ChainFlagPresent = chainFlagPresent;
            HeaderBlueScore = headerBlueScore;
            VerboseBlueScore = verboseBlueScore;
            HeaderHash = headerHash;
            VerboseHash = verboseHash;
        }

        /// <summary>Block hash requested from getBlock.</summary>
        public uint256 Hash { get; }

        /// <summary>header blueScore, or verboseData blueScore when the header omits it. Zero when both are missing.</summary>
        public ulong BlueScore { get; }

        /// <summary>header acceptedIdMerkleRoot. Null when the field is missing.</summary>
        public uint256? AcceptedIdMerkleRoot { get; }

        /// <summary>verboseData.isChainBlock. False when the node omits verbose data.</summary>
        public bool IsChainBlock { get; }

        /// <summary>verboseData.selectedParentHash. Null when the field is missing.</summary>
        public uint256? SelectedParentHash { get; }

        /// <summary>First direct parent, header.parents[0] (or parentsByLevel[0]). Null when that list is empty.</summary>
        public uint256? DirectParent { get; }

        /// <summary>True when the header contained <c>parents</c> or <c>parentsByLevel</c>.</summary>
        public bool ParentsPresent { get; }

        /// <summary>True when verboseData contained <c>isChainBlock</c>.</summary>
        public bool ChainFlagPresent { get; }

        /// <summary>header.blueScore. Null when the header omits it.</summary>
        public ulong? HeaderBlueScore { get; }

        /// <summary>verboseData.blueScore. Null when verbose data omits it.</summary>
        public ulong? VerboseBlueScore { get; }

        /// <summary>header.hash. Null when the header omits it.</summary>
        public uint256? HeaderHash { get; }

        /// <summary>verboseData.hash. Null when verbose data omits it.</summary>
        public uint256? VerboseHash { get; }
    }

    /// <summary>getServerInfo body.</summary>
    public sealed class RpcServerInfo
    {
        /// <summary>Creates the info record.</summary>
        public RpcServerInfo(int rpcApiVersion, int rpcApiRevision, string serverVersion, string networkId, bool hasUtxoIndex, bool isSynced, ulong virtualDaaScore)
        {
            RpcApiVersion = rpcApiVersion;
            RpcApiRevision = rpcApiRevision;
            ServerVersion = serverVersion;
            NetworkId = networkId;
            HasUtxoIndex = hasUtxoIndex;
            IsSynced = isSynced;
            VirtualDaaScore = virtualDaaScore;
        }

        /// <summary>rpcApiVersion.</summary>
        public int RpcApiVersion { get; }

        /// <summary>rpcApiRevision.</summary>
        public int RpcApiRevision { get; }

        /// <summary>serverVersion.</summary>
        public string ServerVersion { get; }

        /// <summary>Network id string such as <c>mainnet</c> or <c>testnet-10</c>.</summary>
        public string NetworkId { get; }

        /// <summary>hasUtxoIndex.</summary>
        public bool HasUtxoIndex { get; }

        /// <summary>isSynced.</summary>
        public bool IsSynced { get; }

        /// <summary>virtualDaaScore.</summary>
        public ulong VirtualDaaScore { get; }
    }

    /// <summary>getBlockDagInfo body.</summary>
    public sealed class RpcBlockDagInfo
    {
        /// <summary>Creates the DAG info record.</summary>
        public RpcBlockDagInfo(
            string network,
            ulong blockCount,
            ulong headerCount,
            IReadOnlyList<uint256> tipHashes,
            double difficulty,
            ulong pastMedianTime,
            IReadOnlyList<uint256> virtualParentHashes,
            uint256 pruningPointHash,
            ulong virtualDaaScore,
            uint256 sink)
        {
            Network = network;
            BlockCount = blockCount;
            HeaderCount = headerCount;
            TipHashes = tipHashes;
            Difficulty = difficulty;
            PastMedianTime = pastMedianTime;
            VirtualParentHashes = virtualParentHashes;
            PruningPointHash = pruningPointHash;
            VirtualDaaScore = virtualDaaScore;
            Sink = sink;
        }

        /// <summary>Network name. The wRPC field is <c>network</c>.</summary>
        public string Network { get; }

        /// <summary>blockCount.</summary>
        public ulong BlockCount { get; }

        /// <summary>headerCount.</summary>
        public ulong HeaderCount { get; }

        /// <summary>tipHashes.</summary>
        public IReadOnlyList<uint256> TipHashes { get; }

        /// <summary>difficulty.</summary>
        public double Difficulty { get; }

        /// <summary>pastMedianTime, milliseconds.</summary>
        public ulong PastMedianTime { get; }

        /// <summary>virtualParentHashes.</summary>
        public IReadOnlyList<uint256> VirtualParentHashes { get; }

        /// <summary>pruningPointHash.</summary>
        public uint256 PruningPointHash { get; }

        /// <summary>virtualDaaScore.</summary>
        public ulong VirtualDaaScore { get; }

        /// <summary>sink.</summary>
        public uint256 Sink { get; }
    }

    /// <summary>One entry from getUtxosByAddresses.</summary>
    public sealed class RpcUtxo
    {
        /// <summary>Creates an entry.</summary>
        public RpcUtxo(string address, OutPoint outpoint, UtxoEntry entry)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Outpoint = outpoint ?? throw new ArgumentNullException(nameof(outpoint));
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        /// <summary>Address that can spend the output, as reported by the node.</summary>
        public string Address { get; }

        /// <summary>Outpoint.</summary>
        public OutPoint Outpoint { get; }

        /// <summary>UTXO entry.</summary>
        public UtxoEntry Entry { get; }
    }

    /// <summary>One fee bucket. <see cref="Feerate"/> is sompi per gram.</summary>
    public sealed class RpcFeeBucket
    {
        /// <summary>Creates a bucket.</summary>
        public RpcFeeBucket(double feerate, double estimatedSeconds)
        {
            Feerate = feerate;
            EstimatedSeconds = estimatedSeconds;
        }

        /// <summary>feerate, sompi per gram.</summary>
        public double Feerate { get; }

        /// <summary>estimatedSeconds.</summary>
        public double EstimatedSeconds { get; }
    }

    /// <summary>
    /// getFeeEstimate body.
    /// The bucket names follow the public Kaspa WASM <c>IFeeEstimate</c> shape:
    /// <c>priorityBucket</c>, <c>normalBuckets</c>, and <c>lowBuckets</c>.
    /// </summary>
    public sealed class RpcFeeEstimate
    {
        /// <summary>Creates an estimate. A missing bucket list is empty.</summary>
        public RpcFeeEstimate(RpcFeeBucket? priorityBucket, IReadOnlyList<RpcFeeBucket> normalBuckets, IReadOnlyList<RpcFeeBucket> lowBuckets)
        {
            PriorityBucket = priorityBucket;
            NormalBuckets = normalBuckets ?? throw new ArgumentNullException(nameof(normalBuckets));
            LowBuckets = lowBuckets ?? throw new ArgumentNullException(nameof(lowBuckets));
        }

        /// <summary>priorityBucket. Null when the node omitted it.</summary>
        public RpcFeeBucket? PriorityBucket { get; }

        /// <summary>normalBuckets.</summary>
        public IReadOnlyList<RpcFeeBucket> NormalBuckets { get; }

        /// <summary>lowBuckets.</summary>
        public IReadOnlyList<RpcFeeBucket> LowBuckets { get; }
    }
}
