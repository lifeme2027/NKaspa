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
        private long _nextId;

        /// <summary>Creates a client over <paramref name="channel"/>.</summary>
        public KaspaWrpcJsonClient(IKaspaJsonChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <inheritdoc />
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
                uint256.Parse(StringOf(body, "sink")));
        }

        /// <inheritdoc />
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
                ReadOptionalHash(verbose, "hash"));
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
            var responseText = await _channel.ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                var code = error.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var codeValue) ? codeValue : 0;
                var message = error.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "RPC error";
                throw new KaspaRpcException(code, message ?? "RPC error");
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
