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
        /// <exception cref="System.ArgumentNullException"><paramref name="requestJson"/> is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
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
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a boolean for {name}. Missing JSON string {name}. Missing JSON property {name}. networkId
        /// is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        Task<RpcServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>getBlockDagInfo.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">
        /// uint256 hex must be 32 bytes. uint256 requires 32 bytes. Hex string contains a non-hex character.
        /// Hex string length must be even. Missing JSON string {name}. Expected an unsigned 64-bit integer.
        /// JSON number does not fit in an unsigned 64-bit integer. Missing JSON property {name}. Hash is
        /// missing. networkId is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        Task<RpcBlockDagInfo> GetBlockDagInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>getUtxosByAddresses.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="addresses"/> is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isCoinbase. uint256 hex must be 32 bytes. uint256 requires 32 bytes. Hex
        /// string contains a non-hex character. Hex string length must be even. Expected an unsigned 64-bit
        /// integer. JSON number does not fit in an unsigned 64-bit integer. Script public key is missing script
        /// bytes. Script public key is not an object or a hex string. Expected hex string. Script public key
        /// hex must start with a 2-byte version. Missing JSON property {name}. Outpoint is not an object.
        /// Outpoint index is not an unsigned 32-bit integer. Outpoint string must be transactionId:index. JSON
        /// number does not fit in an unsigned 32-bit integer. Missing JSON string {name}.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        Task<IReadOnlyList<RpcUtxo>> GetUtxosByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default);

        /// <summary>getBalanceByAddress.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="address"/> is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer. Missing JSON property {name}.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        Task<Money> GetBalanceByAddressAsync(string address, CancellationToken cancellationToken = default);

        /// <summary>submitTransaction.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">
        /// uint256 hex must be 32 bytes. uint256 requires 32 bytes. Hex string contains a non-hex character.
        /// Hex string length must be even. Missing JSON string {name}.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        Task<uint256> SubmitTransactionAsync(Transaction transaction, bool allowOrphan = false, CancellationToken cancellationToken = default);

        /// <summary>getFeeEstimate.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        Task<RpcFeeEstimate> GetFeeEstimateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// getBlock. <paramref name="includeTransactions"/> false asks for the header only.
        /// A missing block returns null. This method does not submit a transaction.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="hash"/> is null.</exception>
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
        Task<RpcChainBlock?> GetBlockAsync(uint256 hash, bool includeTransactions = false, CancellationToken cancellationToken = default);

        /// <summary>getBalancesByAddresses. A missing balance is null.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="addresses"/> is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer. Missing JSON property {name}. Borsh message is truncated.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        Task<IReadOnlyList<RpcAddressBalance>> GetBalancesByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts UTXO-change notifications for <paramref name="addresses"/>.
        /// An empty list asks for every address. Dispose the result, or call <see cref="KaspaSubscription.UnsubscribeAsync"/>, to stop.
        /// Borsh uses workflow-rpc <c>Subscribe</c> with <c>Scope::UtxosChanged</c>. The current node does not register <c>NotifyUtxosChanged</c>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="addresses"/> is null. <paramref name="onChanged"/> is null.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first. Subscriptions require KaspaWebSocketChannel.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC error.</exception>
        Task<KaspaSubscription> NotifyUtxosChangedAsync(IReadOnlyList<string> addresses, Action<RpcUtxosChanged> onChanged, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts virtual DAA score notifications.
        /// Dispose the result, or call <see cref="KaspaSubscription.UnsubscribeAsync"/>, to stop.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="onChanged"/> is null.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first. Subscriptions require KaspaWebSocketChannel.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC error.</exception>
        Task<KaspaSubscription> NotifyVirtualDaaScoreChangedAsync(Action<RpcVirtualDaaScoreChanged> onChanged, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts block-added notifications.
        /// Dispose the result, or call <see cref="KaspaSubscription.UnsubscribeAsync"/>, to stop.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="onAdded"/> is null.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first. Subscriptions require KaspaWebSocketChannel.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC error.</exception>
        Task<KaspaSubscription> NotifyBlockAddedAsync(Action<RpcBlockAdded> onAdded, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Binary wRPC transport. <see cref="KaspaWrpcBorshClient"/> speaks this.
    /// A frame is a workflow-rpc Borsh header plus a payload. Tests can supply an in-memory implementation.
    /// </summary>
    public interface IKaspaBorshChannel
    {
        /// <summary>Sends one request frame and returns the response frame, including its header.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="request"/> is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken = default);

        /// <summary>Raised for a notification frame. The callback runs on the reader. The bytes include the server header.</summary>
        event Action<byte[]>? NotificationReceived;
    }

    /// <summary>Stops one wRPC subscription. Disposing it sends the matching unsubscribe.</summary>
    public sealed class KaspaSubscription : IAsyncDisposable
    {
        private readonly Func<CancellationToken, Task> _unsubscribe;
        private int _done;

        /// <summary>Creates a subscription whose stop action is <paramref name="unsubscribe"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="unsubscribe"/> is null.</exception>
        public KaspaSubscription(Func<CancellationToken, Task> unsubscribe)
        {
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
        }

        /// <summary>Sends unsubscribe. A second call does nothing.</summary>
        public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _done, 1) != 0)
            {
                return;
            }

            await _unsubscribe(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Sends unsubscribe.</summary>
        public ValueTask DisposeAsync()
        {
            return new ValueTask(UnsubscribeAsync());
        }
    }

    /// <summary>
    /// Header fields <c>OP_CHAINBLOCKSEQCOMMIT</c> reads from getBlock.
    /// <see cref="AcceptedIdMerkleRoot"/> is the sequence commitment after KIP-21.
    /// </summary>
    public sealed class RpcChainBlock
    {
        /// <summary>Creates the header view. Omitted consensus fields stay null or false.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="hash"/> is null.</exception>
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
        /// <exception cref="System.ArgumentNullException"><paramref name="entry"/> is null. <paramref name="outpoint"/> is null. <paramref name="address"/> is null.</exception>
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
        /// <exception cref="System.ArgumentNullException"><paramref name="lowBuckets"/> is null. <paramref name="normalBuckets"/> is null.</exception>
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

    /// <summary>One row from getBalancesByAddresses. <see cref="Balance"/> is null when the node has no value for that address.</summary>
    public sealed class RpcAddressBalance
    {
        /// <summary>Creates a row.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="address"/> is null.</exception>
        public RpcAddressBalance(string address, Money? balance)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Balance = balance;
        }

        /// <summary>Address string the node returned.</summary>
        public string Address { get; }

        /// <summary>Balance in sompi, or null when the entry omitted it.</summary>
        public Money? Balance { get; }
    }

    /// <summary>utxosChanged notification. Address is empty when the Borsh entry omitted it.</summary>
    public sealed class RpcUtxosChanged
    {
        /// <summary>Creates the notification. A null list is empty.</summary>
        public RpcUtxosChanged(IReadOnlyList<RpcUtxo>? added, IReadOnlyList<RpcUtxo>? removed)
        {
            Added = added ?? new List<RpcUtxo>();
            Removed = removed ?? new List<RpcUtxo>();
        }

        /// <summary>UTXOs added to the index.</summary>
        public IReadOnlyList<RpcUtxo> Added { get; }

        /// <summary>UTXOs removed from the index.</summary>
        public IReadOnlyList<RpcUtxo> Removed { get; }
    }

    /// <summary>virtualDaaScoreChanged notification.</summary>
    public sealed class RpcVirtualDaaScoreChanged
    {
        /// <summary>Creates the notification.</summary>
        public RpcVirtualDaaScoreChanged(ulong virtualDaaScore)
        {
            VirtualDaaScore = virtualDaaScore;
        }

        /// <summary>virtualDaaScore.</summary>
        public ulong VirtualDaaScore { get; }
    }

    /// <summary>blockAdded notification. Transactions are empty when the node sent none.</summary>
    public sealed class RpcBlockAdded
    {
        /// <summary>Creates the notification.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="hash"/> is null. <paramref name="pruningPoint"/> is null. <paramref name="transactions"/> is null.</exception>
        public RpcBlockAdded(
            uint256 hash,
            ushort version,
            ulong daaScore,
            ulong blueScore,
            ulong timestamp,
            uint256 pruningPoint,
            uint256? acceptedIdMerkleRoot,
            uint256? directParent,
            bool isChainBlock,
            uint256? selectedParentHash,
            IReadOnlyList<Transaction> transactions)
        {
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Version = version;
            DaaScore = daaScore;
            BlueScore = blueScore;
            Timestamp = timestamp;
            PruningPoint = pruningPoint ?? throw new ArgumentNullException(nameof(pruningPoint));
            AcceptedIdMerkleRoot = acceptedIdMerkleRoot;
            DirectParent = directParent;
            IsChainBlock = isChainBlock;
            SelectedParentHash = selectedParentHash;
            Transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        }

        /// <summary>header.hash.</summary>
        public uint256 Hash { get; }

        /// <summary>header.version.</summary>
        public ushort Version { get; }

        /// <summary>header.daaScore.</summary>
        public ulong DaaScore { get; }

        /// <summary>header.blueScore.</summary>
        public ulong BlueScore { get; }

        /// <summary>header.timestamp, milliseconds.</summary>
        public ulong Timestamp { get; }

        /// <summary>header.pruningPoint.</summary>
        public uint256 PruningPoint { get; }

        /// <summary>header.acceptedIdMerkleRoot.</summary>
        public uint256? AcceptedIdMerkleRoot { get; }

        /// <summary>First parent in parentsByLevel[0]. Null when that list is empty.</summary>
        public uint256? DirectParent { get; }

        /// <summary>verboseData.isChainBlock. False when verbose data is absent.</summary>
        public bool IsChainBlock { get; }

        /// <summary>verboseData.selectedParentHash. Null when verbose data is absent.</summary>
        public uint256? SelectedParentHash { get; }

        /// <summary>Block transactions. Empty when the node included none.</summary>
        public IReadOnlyList<Transaction> Transactions { get; }
    }
}
