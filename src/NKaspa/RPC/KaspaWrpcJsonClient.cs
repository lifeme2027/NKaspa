using System.Text;
using System.Text.Json;

namespace NKaspa.RPC
{
    /// <summary>
    /// wRPC JSON client. Kaspa counterpart of a small slice of NBitcoin.RPC.RPCClient.
    /// Methods are the camelCase names of rusty-kaspa <c>RpcApiOps</c>.
    /// <see cref="KaspaWebSocketChannel"/> matches responses by request id and keeps handshake and notification frames off that path.
    /// This type does not open a socket by itself. Pass a channel.
    /// </summary>
    public sealed class KaspaWrpcJsonClient : IKaspaRpcClient
    {
        private readonly IKaspaJsonChannel _channel;
        private readonly object _gate = new object();
        private readonly List<Action<string>> _listeners = new List<Action<string>>();
        private long _nextId;
        private bool _hooked;

        /// <summary>Creates a client over <paramref name="channel"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="channel"/> is null.</exception>
        public KaspaWrpcJsonClient(IKaspaJsonChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <inheritdoc />
        public Task EnsureReadyAsync(Network network, bool requireSynced = true, bool requireUtxoIndex = true, CancellationToken cancellationToken = default)
        {
            return KaspaWrpcErrors.Ensure(this, network, requireSynced, requireUtxoIndex, cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a boolean for {name}. Missing JSON string {name}. Missing JSON property {name}. networkId
        /// is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        public async Task<RpcServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default)
        {
            var body = await InvokeAsync("getServerInfo", writer => { }, cancellationToken).ConfigureAwait(false);
            var network = body.TryGetProperty("networkId", out var networkElement)
                ? KaspaRpcJson.ReadNetworkId(networkElement)
                : string.Empty;
            return new RpcServerInfo(
                (int)KaspaRpcJson.ReadU64(Require(body, "rpcApiVersion")),
                (int)KaspaRpcJson.ReadU64(Require(body, "rpcApiRevision")),
                StringOf(body, "serverVersion"),
                network,
                BoolOf(body, "hasUtxoIndex"),
                BoolOf(body, "isSynced"),
                body.TryGetProperty("virtualDaaScore", out var score) ? KaspaRpcJson.ReadU64(score) : 0UL);
        }

        /// <inheritdoc />
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
        public async Task<RpcBlockDagInfo> GetBlockDagInfoAsync(CancellationToken cancellationToken = default)
        {
            var body = await InvokeAsync("getBlockDagInfo", writer => { }, cancellationToken).ConfigureAwait(false);
            var network = body.TryGetProperty("network", out var networkElement)
                ? KaspaRpcJson.ReadNetworkId(networkElement)
                : body.TryGetProperty("networkName", out var alias) ? KaspaRpcJson.ReadNetworkId(alias) : string.Empty;
            return new RpcBlockDagInfo(
                network,
                KaspaRpcJson.ReadU64(Require(body, "blockCount")),
                KaspaRpcJson.ReadU64(Require(body, "headerCount")),
                KaspaRpcJson.ReadHashes(Require(body, "tipHashes")),
                Require(body, "difficulty").GetDouble(),
                KaspaRpcJson.ReadU64(Require(body, "pastMedianTime")),
                KaspaRpcJson.ReadHashes(Require(body, "virtualParentHashes")),
                uint256.Parse(StringOf(body, "pruningPointHash")),
                KaspaRpcJson.ReadU64(Require(body, "virtualDaaScore")),
                uint256.Parse(StringOf(body, "sink")),
                body.TryGetProperty("blueScore", out var blueScore) && blueScore.ValueKind != JsonValueKind.Null
                    ? KaspaRpcJson.ReadU64(blueScore)
                    : 0UL);
        }

        /// <inheritdoc />
        public async Task<ulong> GetSinkBlueScoreAsync(CancellationToken cancellationToken = default)
        {
            var body = await InvokeAsync("getSinkBlueScore", writer => { }, cancellationToken).ConfigureAwait(false);
            return KaspaRpcJson.ReadU64(Require(body, "blueScore"));
        }

        /// <inheritdoc />
        public async Task<RpcVirtualChain> GetVirtualChainFromBlockAsync(uint256 startHash, bool includeAcceptedTransactionIds = false, ulong? minConfirmationCount = null, CancellationToken cancellationToken = default)
        {
            if (startHash == null)
            {
                throw new ArgumentNullException(nameof(startHash));
            }

            var body = await InvokeAsync("getVirtualChainFromBlock", writer =>
            {
                writer.WriteString("startHash", startHash.ToString());
                writer.WriteBoolean("includeAcceptedTransactionIds", includeAcceptedTransactionIds);
                if (minConfirmationCount.HasValue)
                {
                    writer.WriteNumber("minConfirmationCount", minConfirmationCount.Value);
                }
                else
                {
                    writer.WriteNull("minConfirmationCount");
                }
            }, cancellationToken).ConfigureAwait(false);

            return new RpcVirtualChain(
                ReadHashList(body, "removedChainBlockHashes"),
                ReadHashList(body, "addedChainBlockHashes"),
                ReadAcceptedIds(body));
        }

        /// <inheritdoc />
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
        public async Task<IReadOnlyList<RpcUtxo>> GetUtxosByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
        {
            if (addresses == null)
            {
                throw new ArgumentNullException(nameof(addresses));
            }

            var body = await InvokeAsync("getUtxosByAddresses", writer =>
            {
                writer.WritePropertyName("addresses");
                writer.WriteStartArray();
                for (var i = 0; i < addresses.Count; i++)
                {
                    writer.WriteStringValue(addresses[i]);
                }

                writer.WriteEndArray();
            }, cancellationToken).ConfigureAwait(false);

            var entries = new List<RpcUtxo>();
            if (!body.TryGetProperty("entries", out var array))
            {
                return entries;
            }

            foreach (var item in array.EnumerateArray())
            {
                var address = StringOf(item, "address");
                var outpoint = KaspaRpcJson.ReadOutPoint(Require(item, "outpoint"));
                var entry = KaspaRpcJson.ReadUtxoEntry(Require(item, "utxoEntry"));
                entries.Add(new RpcUtxo(address, outpoint, entry));
            }

            return entries;
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException"><paramref name="address"/> is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer. Missing JSON property {name}.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        public async Task<Money> GetBalanceByAddressAsync(string address, CancellationToken cancellationToken = default)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            var body = await InvokeAsync("getBalanceByAddress", writer =>
            {
                writer.WriteString("address", address);
            }, cancellationToken).ConfigureAwait(false);
            return new Money(KaspaRpcJson.ReadU64(Require(body, "balance")));
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.FormatException">
        /// uint256 hex must be 32 bytes. uint256 requires 32 bytes. Hex string contains a non-hex character.
        /// Hex string length must be even. Missing JSON string {name}.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        public async Task<uint256> SubmitTransactionAsync(Transaction transaction, bool allowOrphan = false, CancellationToken cancellationToken = default)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            var body = await InvokeAsync("submitTransaction", writer =>
            {
                writer.WritePropertyName("transaction");
                KaspaRpcJson.WriteTransaction(writer, transaction);
                writer.WriteBoolean("allowOrphan", allowOrphan);
            }, cancellationToken).ConfigureAwait(false);
            return uint256.Parse(StringOf(body, "transactionId"));
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        public async Task<RpcFeeEstimate> GetFeeEstimateAsync(CancellationToken cancellationToken = default)
        {
            var body = await InvokeAsync("getFeeEstimate", writer => { }, cancellationToken).ConfigureAwait(false);
            var estimate = body.TryGetProperty("estimate", out var wrapped) ? wrapped : body;
            RpcFeeBucket? priority = null;
            if (estimate.TryGetProperty("priorityBucket", out var priorityElement))
            {
                priority = KaspaRpcJson.ReadBucket(priorityElement);
            }

            var normal = estimate.TryGetProperty("normalBuckets", out var normalElement)
                ? KaspaRpcJson.ReadBuckets(normalElement)
                : new List<RpcFeeBucket>();
            var low = estimate.TryGetProperty("lowBuckets", out var lowElement)
                ? KaspaRpcJson.ReadBuckets(lowElement)
                : new List<RpcFeeBucket>();
            return new RpcFeeEstimate(priority, normal, low);
        }

        /// <inheritdoc />
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
        public async Task<RpcChainBlock?> GetBlockAsync(uint256 hash, bool includeTransactions = false, CancellationToken cancellationToken = default)
        {
            if (hash == null)
            {
                throw new ArgumentNullException(nameof(hash));
            }

            JsonElement body;
            try
            {
                body = await InvokeAsync("getBlock", writer =>
                {
                    writer.WriteString("hash", hash.ToString());
                    writer.WriteBoolean("includeTransactions", includeTransactions);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (KaspaRpcException)
            {
                return null;
            }

            if (!body.TryGetProperty("block", out var block))
            {
                return null;
            }

            var header = block.TryGetProperty("header", out var headerElement) ? headerElement : block;
            var verbose = block.TryGetProperty("verboseData", out var verboseElement) ? verboseElement : default;
            var blueScore = ReadScore(header, verbose);
            uint256? root = null;
            if (header.ValueKind == JsonValueKind.Object && header.TryGetProperty("acceptedIdMerkleRoot", out var rootElement) && rootElement.ValueKind == JsonValueKind.String)
            {
                var text = rootElement.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    root = uint256.Parse(text);
                }
            }

            var chainFlagPresent = false;
            var chain = false;
            if (verbose.ValueKind == JsonValueKind.Object && verbose.TryGetProperty("isChainBlock", out var chainElement))
            {
                chainFlagPresent = true;
                if (chainElement.ValueKind == JsonValueKind.True)
                {
                    chain = true;
                }
                else if (chainElement.ValueKind == JsonValueKind.False)
                {
                    chain = false;
                }
                else
                {
                    throw new FormatException("Expected a boolean for isChainBlock.");
                }
            }

            ReadDirectParent(header, out var parentsPresent, out var directParent);
            return new RpcChainBlock(
                hash,
                blueScore,
                root,
                chain,
                ReadOptionalHash(verbose, "selectedParentHash"),
                directParent,
                parentsPresent,
                chainFlagPresent,
                ReadOptionalScore(header, "blueScore"),
                ReadOptionalScore(verbose, "blueScore"),
                ReadOptionalHash(header, "hash"),
                ReadOptionalHash(verbose, "hash"),
                ReadBlueWork(header));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<RpcAddressBalance>> GetBalancesByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
        {
            if (addresses == null)
            {
                throw new ArgumentNullException(nameof(addresses));
            }

            var body = await InvokeAsync("getBalancesByAddresses", writer =>
            {
                writer.WritePropertyName("addresses");
                writer.WriteStartArray();
                for (var i = 0; i < addresses.Count; i++)
                {
                    writer.WriteStringValue(addresses[i]);
                }

                writer.WriteEndArray();
            }, cancellationToken).ConfigureAwait(false);

            var rows = new List<RpcAddressBalance>();
            if (!body.TryGetProperty("entries", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return rows;
            }

            foreach (var item in array.EnumerateArray())
            {
                Money? balance = null;
                if (item.TryGetProperty("balance", out var balanceElement) && balanceElement.ValueKind != JsonValueKind.Null)
                {
                    balance = new Money(KaspaRpcJson.ReadU64(balanceElement));
                }

                rows.Add(new RpcAddressBalance(StringOf(item, "address"), balance));
            }

            return rows;
        }

        /// <inheritdoc />
        public Task<KaspaSubscription> NotifyUtxosChangedAsync(IReadOnlyList<string> addresses, Action<RpcUtxosChanged> onChanged, CancellationToken cancellationToken = default)
        {
            if (addresses == null)
            {
                throw new ArgumentNullException(nameof(addresses));
            }

            if (onChanged == null)
            {
                throw new ArgumentNullException(nameof(onChanged));
            }

            return SubscribeAsync(writer =>
            {
                writer.WritePropertyName("UtxosChanged");
                writer.WriteStartObject();
                writer.WritePropertyName("addresses");
                writer.WriteStartArray();
                for (var i = 0; i < addresses.Count; i++)
                {
                    writer.WriteStringValue(addresses[i]);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }, json =>
            {
                if (TryNotificationBody(json, "utxosChangedNotification", "UtxosChanged", out var body))
                {
                    onChanged(ReadUtxosChanged(body));
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public Task<KaspaSubscription> NotifyVirtualDaaScoreChangedAsync(Action<RpcVirtualDaaScoreChanged> onChanged, CancellationToken cancellationToken = default)
        {
            if (onChanged == null)
            {
                throw new ArgumentNullException(nameof(onChanged));
            }

            return SubscribeAsync(writer =>
            {
                writer.WritePropertyName("VirtualDaaScoreChanged");
                writer.WriteStartObject();
                writer.WriteEndObject();
            }, json =>
            {
                if (TryNotificationBody(json, "virtualDaaScoreChangedNotification", "VirtualDaaScoreChanged", out var body)
                    && body.TryGetProperty("virtualDaaScore", out var score))
                {
                    onChanged(new RpcVirtualDaaScoreChanged(KaspaRpcJson.ReadU64(score)));
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public Task<KaspaSubscription> NotifyBlockAddedAsync(Action<RpcBlockAdded> onAdded, CancellationToken cancellationToken = default)
        {
            if (onAdded == null)
            {
                throw new ArgumentNullException(nameof(onAdded));
            }

            return SubscribeAsync(writer =>
            {
                writer.WritePropertyName("BlockAdded");
                writer.WriteStartObject();
                writer.WriteEndObject();
            }, json =>
            {
                if (!TryNotificationBody(json, "blockAddedNotification", "BlockAdded", out var body))
                {
                    return;
                }

                var block = body.TryGetProperty("block", out var wrapped) ? wrapped : body;
                onAdded(ReadBlockAdded(block));
            }, cancellationToken);
        }

        private async Task<KaspaSubscription> SubscribeAsync(Action<Utf8JsonWriter> writeScope, Action<string> handler, CancellationToken cancellationToken)
        {
            // rusty-kaspa router.rs registers Subscribe and Unsubscribe, not NotifyBlockAdded / NotifyUtxosChanged / NotifyVirtualDaaScoreChanged.
            // JSON params are serde of Scope: an externally tagged object, which this writer already opens as "params".
            var socket = _channel as KaspaWebSocketChannel;
            if (socket == null)
            {
                throw new InvalidOperationException("Subscriptions require KaspaWebSocketChannel.");
            }

            EnsureHook(socket);
            lock (_gate)
            {
                _listeners.Add(handler);
            }

            try
            {
                await InvokeAsync("subscribe", writeScope, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (_gate)
                {
                    _listeners.Remove(handler);
                }

                throw;
            }

            return new KaspaSubscription(token =>
            {
                lock (_gate)
                {
                    _listeners.Remove(handler);
                }

                return InvokeAsync("unsubscribe", writeScope, token);
            });
        }

        private void EnsureHook(KaspaWebSocketChannel socket)
        {
            lock (_gate)
            {
                if (_hooked)
                {
                    return;
                }

                socket.NotificationReceived += OnNotification;
                _hooked = true;
            }
        }

        private void OnNotification(string json)
        {
            Action<string>[] listeners;
            lock (_gate)
            {
                listeners = _listeners.ToArray();
            }

            for (var i = 0; i < listeners.Length; i++)
            {
                try
                {
                    listeners[i](json);
                }
                catch (Exception)
                {
                }
            }
        }

        private static bool TryNotificationBody(string json, string method, string tag, out JsonElement body)
        {
            body = default;
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("method", out var methodElement) || methodElement.GetString() != method)
            {
                return false;
            }

            if (!root.TryGetProperty("params", out var parameters) || parameters.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (parameters.TryGetProperty(tag, out var tagged) && tagged.ValueKind == JsonValueKind.Object)
            {
                body = tagged.Clone();
                return true;
            }

            body = parameters.Clone();
            return true;
        }

        private static RpcUtxosChanged ReadUtxosChanged(JsonElement body)
        {
            return new RpcUtxosChanged(ReadUtxoArray(body, "added"), ReadUtxoArray(body, "removed"));
        }

        private static List<RpcUtxo> ReadUtxoArray(JsonElement body, string name)
        {
            var entries = new List<RpcUtxo>();
            if (!body.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return entries;
            }

            foreach (var item in array.EnumerateArray())
            {
                var address = item.TryGetProperty("address", out var addressElement) && addressElement.ValueKind == JsonValueKind.String
                    ? addressElement.GetString() ?? string.Empty
                    : string.Empty;
                var outpoint = KaspaRpcJson.ReadOutPoint(Require(item, "outpoint"));
                var entry = KaspaRpcJson.ReadUtxoEntry(Require(item, "utxoEntry"));
                entries.Add(new RpcUtxo(address, outpoint, entry));
            }

            return entries;
        }

        private static RpcBlockAdded ReadBlockAdded(JsonElement block)
        {
            var header = block.TryGetProperty("header", out var headerElement) ? headerElement : block;
            var verbose = block.TryGetProperty("verboseData", out var verboseElement) ? verboseElement : default;
            var hash = ReadOptionalHash(header, "hash") ?? throw new FormatException("Missing JSON string hash.");
            var pruning = ReadOptionalHash(header, "pruningPoint") ?? throw new FormatException("Missing JSON string pruningPoint.");
            ReadDirectParent(header, out _, out var directParent);
            var transactions = new List<Transaction>();
            if (block.TryGetProperty("transactions", out var transactionElement) && transactionElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in transactionElement.EnumerateArray())
                {
                    transactions.Add(KaspaRpcJson.ReadTransaction(item));
                }
            }

            var chain = false;
            if (verbose.ValueKind == JsonValueKind.Object && verbose.TryGetProperty("isChainBlock", out var chainElement))
            {
                if (chainElement.ValueKind == JsonValueKind.True)
                {
                    chain = true;
                }
                else if (chainElement.ValueKind == JsonValueKind.False)
                {
                    chain = false;
                }
                else
                {
                    throw new FormatException("Expected a boolean for isChainBlock.");
                }
            }

            return new RpcBlockAdded(
                hash,
                header.TryGetProperty("version", out var versionElement) ? (ushort)KaspaRpcJson.ReadU64(versionElement) : (ushort)0,
                header.TryGetProperty("daaScore", out var daaElement) ? KaspaRpcJson.ReadU64(daaElement) : 0UL,
                ReadScore(header, verbose),
                header.TryGetProperty("timestamp", out var timeElement) ? KaspaRpcJson.ReadU64(timeElement) : 0UL,
                pruning,
                ReadOptionalHash(header, "acceptedIdMerkleRoot"),
                directParent,
                chain,
                ReadOptionalHash(verbose, "selectedParentHash"),
                transactions,
                ReadBlueWork(header));
        }

        private static IReadOnlyList<uint256> ReadHashList(JsonElement body, string name)
        {
            if (!body.TryGetProperty(name, out var array) || array.ValueKind == JsonValueKind.Null)
            {
                return new List<uint256>();
            }

            if (array.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("Expected a hex hash.");
            }

            var hashes = new List<uint256>();
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new FormatException("Expected a hex hash.");
                }

                var text = item.GetString();
                if (string.IsNullOrEmpty(text))
                {
                    throw new FormatException("Expected a hex hash.");
                }

                hashes.Add(uint256.Parse(text));
            }

            return hashes;
        }

        private static IReadOnlyList<RpcAcceptedTransactionIds> ReadAcceptedIds(JsonElement body)
        {
            if (!body.TryGetProperty("acceptedTransactionIds", out var array) || array.ValueKind == JsonValueKind.Null)
            {
                return new List<RpcAcceptedTransactionIds>();
            }

            if (array.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("Expected a hex hash.");
            }

            var rows = new List<RpcAcceptedTransactionIds>();
            foreach (var item in array.EnumerateArray())
            {
                var accepting = ReadOptionalHash(item, "acceptingBlockHash") ?? throw new FormatException("Expected a hex hash.");
                rows.Add(new RpcAcceptedTransactionIds(accepting, ReadHashList(item, "acceptedTransactionIds")));
            }

            return rows;
        }

        private static byte[]? ReadBlueWork(JsonElement header)
        {
            if (header.ValueKind != JsonValueKind.Object || !header.TryGetProperty("blueWork", out var element) || element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                throw new FormatException("Expected a hex string for blueWork.");
            }

            var text = element.GetString();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return KaspaRpcJson.ReadBlueWork(text);
        }

        private static ulong? ReadOptionalScore(JsonElement obj, string name)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return KaspaRpcJson.ReadU64(element);
        }

        private static uint256? ReadOptionalHash(JsonElement obj, string name)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                throw new FormatException("Expected a hex hash for " + name + ".");
            }

            var text = element.GetString();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return uint256.Parse(text);
        }

        private static void ReadDirectParent(JsonElement header, out bool parentsPresent, out uint256? directParent)
        {
            parentsPresent = false;
            directParent = null;
            if (header.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            JsonElement levels;
            if (header.TryGetProperty("parents", out var parents))
            {
                levels = parents;
            }
            else if (header.TryGetProperty("parentsByLevel", out var byLevel))
            {
                levels = byLevel;
            }
            else
            {
                return;
            }

            if (levels.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("Expected an array for parents.");
            }

            parentsPresent = true;
            var levelEnumerator = levels.EnumerateArray();
            if (!levelEnumerator.MoveNext())
            {
                return;
            }

            var firstLevel = levelEnumerator.Current;
            JsonElement hashes;
            if (firstLevel.ValueKind == JsonValueKind.Array)
            {
                hashes = firstLevel;
            }
            else if (firstLevel.ValueKind == JsonValueKind.Object && firstLevel.TryGetProperty("parentHashes", out var parentHashes))
            {
                hashes = parentHashes;
            }
            else
            {
                throw new FormatException("Expected parent hashes.");
            }

            if (hashes.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("Expected parent hashes.");
            }

            var hashEnumerator = hashes.EnumerateArray();
            if (!hashEnumerator.MoveNext())
            {
                return;
            }

            if (hashEnumerator.Current.ValueKind != JsonValueKind.String)
            {
                throw new FormatException("Expected a parent hash.");
            }

            var text = hashEnumerator.Current.GetString();
            if (string.IsNullOrEmpty(text))
            {
                throw new FormatException("Expected a parent hash.");
            }

            directParent = uint256.Parse(text);
        }

        private static ulong ReadScore(JsonElement header, JsonElement verbose)
        {
            if (header.ValueKind == JsonValueKind.Object && header.TryGetProperty("blueScore", out var headerScore))
            {
                return KaspaRpcJson.ReadU64(headerScore);
            }

            if (verbose.ValueKind == JsonValueKind.Object && verbose.TryGetProperty("blueScore", out var verboseScore))
            {
                return KaspaRpcJson.ReadU64(verboseScore);
            }

            return 0;
        }

        private async Task<JsonElement> InvokeAsync(string method, Action<Utf8JsonWriter> writeParams, CancellationToken cancellationToken)
        {
            var id = Interlocked.Increment(ref _nextId);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", id);
                writer.WriteString("method", method);
                writer.WritePropertyName("params");
                writer.WriteStartObject();
                writeParams(writer);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            var request = Encoding.UTF8.GetString(stream.ToArray());
            string responseText;
            try
            {
                responseText = await _channel.ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                var mapped = KaspaWrpcErrors.Transport(exception);
                if (ReferenceEquals(mapped, exception))
                {
                    throw;
                }

                throw mapped;
            }
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                var code = error.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var codeValue) ? codeValue : 0;
                var message = error.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "RPC error";
                throw KaspaWrpcErrors.FromRpc(new KaspaRpcException(code, message ?? "RPC error"));
            }

            if (!root.TryGetProperty("id", out var responseId) || !responseId.TryGetInt64(out var responseNumber) || responseNumber != id)
            {
                throw new KaspaRpcException(0, "RPC response id does not match the request.");
            }

            JsonElement body;
            if (root.TryGetProperty("params", out var parameters))
            {
                body = parameters;
            }
            else if (root.TryGetProperty("result", out var result))
            {
                body = result;
            }
            else
            {
                throw new KaspaRpcException(0, "RPC response has no params.");
            }

            return body.Clone();
        }

        private static JsonElement Require(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                throw new FormatException("Missing JSON property " + name + ".");
            }

            return value;
        }

        private static string StringOf(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException("Missing JSON string " + name + ".");
            }

            return value.GetString() ?? string.Empty;
        }

        private static bool BoolOf(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            throw new FormatException("Expected a boolean for " + name + ".");
        }
    }
}
