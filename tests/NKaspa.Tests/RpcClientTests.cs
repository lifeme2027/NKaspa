using System.Text.Json;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Offline wRPC JSON samples. Field names follow rusty-kaspa rpc/core model structs
    /// (camelCase) and the workflow-rpc envelope (id, method, params). No socket is opened.
    /// </summary>
    public class RpcClientTests
    {
        [Fact]
        public async Task Server_dag_balance_utxo_and_fee_estimate_parse_canned_json()
        {
            var channel = new ScriptedChannel();
            var client = new KaspaWrpcJsonClient(channel);
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                var method = doc.RootElement.GetProperty("method").GetString();
                Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("params").ValueKind);
                return method switch
                {
                    "getServerInfo" => "{\"id\":" + id + ",\"method\":\"getServerInfo\",\"params\":{\"rpcApiVersion\":1,\"rpcApiRevision\":1,\"serverVersion\":\"0.0.0-test\",\"networkId\":{\"networkType\":\"testnet\",\"suffix\":10},\"hasUtxoIndex\":true,\"isSynced\":false,\"virtualDaaScore\":123}}",
                    "getBlockDagInfo" => "{\"id\":" + id + ",\"method\":\"getBlockDagInfo\",\"params\":{\"network\":\"mainnet\",\"blockCount\":10,\"headerCount\":11,\"tipHashes\":[\"" + new uint256(1) + "\"],\"difficulty\":2.5,\"pastMedianTime\":99,\"virtualParentHashes\":[\"" + new uint256(2) + "\"],\"pruningPointHash\":\"" + new uint256(3) + "\",\"virtualDaaScore\":50,\"sink\":\"" + new uint256(4) + "\"}}",
                    "getBalanceByAddress" => "{\"id\":" + id + ",\"method\":\"getBalanceByAddress\",\"params\":{\"balance\":9007199254740993}}",
                    "getUtxosByAddresses" => UtxoResponse(id),
                    "getFeeEstimate" => "{\"id\":" + id + ",\"method\":\"getFeeEstimate\",\"params\":{\"estimate\":{\"priorityBucket\":{\"feerate\":5.5,\"estimatedSeconds\":1},\"normalBuckets\":[{\"feerate\":1,\"estimatedSeconds\":10}],\"lowBuckets\":[{\"feeRate\":0.5,\"estimatedSeconds\":60}]}}}",
                    _ => throw new InvalidOperationException(method),
                };
            };

            var info = await client.GetServerInfoAsync();
            Assert.Equal("getServerInfo", channel.LastMethod);
            Assert.Equal(1, info.RpcApiVersion);
            Assert.Equal("testnet-10", info.NetworkId);
            Assert.True(info.HasUtxoIndex);
            Assert.False(info.IsSynced);
            Assert.Equal(123UL, info.VirtualDaaScore);

            var dag = await client.GetBlockDagInfoAsync();
            Assert.Equal("mainnet", dag.Network);
            Assert.Equal(10UL, dag.BlockCount);
            Assert.Equal(2.5, dag.Difficulty);
            Assert.Equal(new uint256(4), dag.Sink);
            Assert.Equal(0UL, dag.BlueScore);

            var balance = await client.GetBalanceByAddressAsync("kaspa:qq");
            Assert.Equal(9007199254740993UL, balance.Sompi);
            Assert.Contains("kaspa:qq", channel.LastRequest, StringComparison.Ordinal);

            var utxos = await client.GetUtxosByAddressesAsync(new[] { "kaspa:qq" });
            Assert.Single(utxos);
            Assert.Equal(9007199254740993UL, utxos[0].Entry.Amount.Sompi);
            Assert.Equal(7UL, utxos[0].Entry.BlockDaaScore);
            Assert.True(utxos[0].Entry.IsCoinbase);
            Assert.Equal((uint)2, utxos[0].Outpoint.Index);

            var fees = await client.GetFeeEstimateAsync();
            Assert.NotNull(fees.PriorityBucket);
            Assert.Equal(5.5, fees.PriorityBucket!.Feerate);
            Assert.Equal(10, fees.NormalBuckets[0].EstimatedSeconds);
            Assert.Equal(0.5, fees.LowBuckets[0].Feerate);
        }

        [Fact]
        public async Task Submit_roundtrips_transaction_json_and_errors_use_the_error_object()
        {
            using var key = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
            var tx = new Transaction(
                0,
                new[] { new TxIn(new OutPoint(new uint256(9), 3), Script.Empty, 4, 1) },
                new[] { new TxOut(9007199254740993UL, key.PubKey.GetScriptPubKey()) },
                8,
                SubnetworkId.Native,
                0,
                new byte[] { 1, 2, 3 });
            tx.StorageMass = 2017;

            var channel = new ScriptedChannel();
            var client = new KaspaWrpcJsonClient(channel);
            string? submitted = null;
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                Assert.Equal("submitTransaction", doc.RootElement.GetProperty("method").GetString());
                var parameters = doc.RootElement.GetProperty("params");
                Assert.True(parameters.GetProperty("allowOrphan").GetBoolean());
                submitted = parameters.GetProperty("transaction").GetRawText();
                return "{\"id\":" + id + ",\"method\":\"submitTransaction\",\"params\":{\"transactionId\":\"" + tx.GetId() + "\"}}";
            };

            var idReturned = await client.SubmitTransactionAsync(tx, allowOrphan: true);
            Assert.Equal(tx.GetId(), idReturned);
            var parsed = KaspaRpcJson.ReadTransaction(submitted!);
            Assert.Equal(tx.GetId(), parsed.GetId());
            Assert.Equal(9007199254740993UL, parsed.Outputs[0].Value.Sompi);
            Assert.Equal(tx.Outputs[0].ScriptPublicKey, parsed.Outputs[0].ScriptPublicKey);
            Assert.Equal(2017UL, parsed.StorageMass);
            Assert.Equal(new byte[] { 1, 2, 3 }, parsed.Payload);
            Assert.Equal(4UL, parsed.Inputs[0].Sequence);
            Assert.Equal(1, parsed.Inputs[0].SigOpCount);
            Assert.Contains("\"verboseData\":null", submitted, StringComparison.Ordinal);
            Assert.Contains("\"covenant\":null", submitted, StringComparison.Ordinal);

            var alias = submitted!.Replace("\"value\":", "\"amount\":").Replace("\"script\":", "\"scriptPublicKey\":");
            var fromAlias = KaspaRpcJson.ReadTransaction(alias);
            Assert.Equal(parsed.Outputs[0].Value.Sompi, fromAlias.Outputs[0].Value.Sompi);
            Assert.Equal(parsed.Outputs[0].ScriptPublicKey, fromAlias.Outputs[0].ScriptPublicKey);

            var mismatched = submitted.Replace("\"storageMass\":2017", "\"storageMass\":1");
            Assert.Throws<FormatException>(() => KaspaRpcJson.ReadTransaction(mismatched));

            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                return "{\"id\":" + id + ",\"method\":\"getServerInfo\",\"error\":{\"code\":0,\"message\":\"nope\"}}";
            };
            var error = await Assert.ThrowsAsync<KaspaRpcException>(() => client.GetServerInfoAsync());
            Assert.Equal("nope", error.Message);
            Assert.Equal(0, error.RpcCode);
        }

        [Fact]
        public async Task Version1_submit_json_keeps_sig_op_count_and_compute_budget()
        {
            using var key = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
            var tx = new Transaction(
                1,
                new[] { TxIn.WithComputeBudget(new OutPoint(new uint256(9), 3), Script.Empty, 4, 10) },
                new[] { new TxOut(10000000UL, key.PubKey.GetScriptPubKey()) },
                0,
                SubnetworkId.Native,
                0,
                Array.Empty<byte>());
            tx.StorageMass = 152080;

            var channel = new ScriptedChannel();
            var client = new KaspaWrpcJsonClient(channel);
            string? submitted = null;
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                submitted = doc.RootElement.GetProperty("params").GetProperty("transaction").GetRawText();
                return "{\"id\":" + id + ",\"method\":\"submitTransaction\",\"params\":{\"transactionId\":\"" + tx.GetId() + "\"}}";
            };

            Assert.Equal(tx.GetId(), await client.SubmitTransactionAsync(tx, allowOrphan: false));
            using var body = JsonDocument.Parse(submitted!);
            var input = body.RootElement.GetProperty("inputs")[0];
            Assert.Equal(0, input.GetProperty("sigOpCount").GetInt32());
            Assert.Equal(10, input.GetProperty("computeBudget").GetInt32());
            Assert.Equal(152080UL, body.RootElement.GetProperty("mass").GetUInt64());
            Assert.Equal(152080UL, body.RootElement.GetProperty("storageMass").GetUInt64());

            var parsed = KaspaRpcJson.ReadTransaction(submitted!);
            Assert.Equal(tx.GetId(), parsed.GetId());
            Assert.Equal(tx.GetHash(), parsed.GetHash());
            Assert.Equal((ushort)10, parsed.Inputs[0].ComputeBudget);
            Assert.Equal(0, parsed.Inputs[0].SigOpCount);
            Assert.Equal(152080UL, parsed.StorageMass);
        }

        [Fact]
        public async Task Block_dag_network_accepts_a_string_and_an_object()
        {
            var channel = new ScriptedChannel();
            var client = new KaspaWrpcJsonClient(channel);
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                return "{\"id\":" + id + ",\"method\":\"getBlockDagInfo\",\"params\":{\"network\":{\"networkType\":\"testnet\",\"suffix\":10},\"blockCount\":10,\"headerCount\":11,\"tipHashes\":[\"" + new uint256(1) + "\"],\"difficulty\":2.5,\"pastMedianTime\":99,\"virtualParentHashes\":[\"" + new uint256(2) + "\"],\"pruningPointHash\":\"" + new uint256(3) + "\",\"virtualDaaScore\":50,\"sink\":\"" + new uint256(4) + "\"}}";
            };

            var dag = await client.GetBlockDagInfoAsync();
            Assert.Equal("testnet-10", dag.Network);

            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                return "{\"id\":" + id + ",\"method\":\"getBlockDagInfo\",\"params\":{\"network\":\"mainnet\",\"blockCount\":1,\"headerCount\":1,\"tipHashes\":[\"" + uint256.Zero + "\"],\"difficulty\":1,\"pastMedianTime\":1,\"virtualParentHashes\":[\"" + uint256.Zero + "\"],\"pruningPointHash\":\"" + uint256.Zero + "\",\"virtualDaaScore\":1,\"sink\":\"" + uint256.Zero + "\"}}";
            };
            Assert.Equal("mainnet", (await client.GetBlockDagInfoAsync()).Network);
        }

        [Fact]
        public async Task Utxo_script_accepts_the_human_readable_version_prefixed_hex()
        {
            // rusty-kaspa ScriptPublicKey JSON is one hex string: big-endian version, then the script.
            // The vector is the upstream test: version 0xc0de and script bytes 0x00..=0x22.
            var script = new byte[35];
            for (var i = 0; i < script.Length; i++)
            {
                script[i] = (byte)i;
            }

            var prefixed = "c0de" + NKaspa.DataEncoders.Encoders.Hex.EncodeData(script);
            var channel = new ScriptedChannel();
            var client = new KaspaWrpcJsonClient(channel);
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                return "{\"id\":" + id + ",\"method\":\"getUtxosByAddresses\",\"params\":{\"entries\":[{\"address\":\"kaspa:qq\",\"outpoint\":{\"transactionId\":\"" + new uint256(5) + "\",\"index\":2},\"utxoEntry\":{\"amount\":35000000,\"scriptPublicKey\":\"" + prefixed + "\",\"blockDaaScore\":541054755,\"covenantId\":null,\"isCoinbase\":false}}]}}";
            };

            var utxos = await client.GetUtxosByAddressesAsync(new[] { "kaspa:qq" });
            Assert.Single(utxos);
            Assert.Equal((ushort)0xc0de, utxos[0].Entry.ScriptPublicKey.Version);
            Assert.Equal(script, utxos[0].Entry.ScriptPublicKey.Script);
            Assert.Equal(35000000UL, utxos[0].Entry.Amount.Sompi);
            Assert.Equal(541054755UL, utxos[0].Entry.BlockDaaScore);
            Assert.False(utxos[0].Entry.IsCoinbase);

            using (var zero = JsonDocument.Parse("\"000020ac\""))
            {
                var versionZero = KaspaRpcJson.ReadScript(zero.RootElement);
                Assert.Equal((ushort)0, versionZero.Version);
                Assert.Equal(new byte[] { 0x20, 0xac }, versionZero.Script);
            }

            using (var tooShort = JsonDocument.Parse("\"00\""))
            {
                Assert.Throws<FormatException>(() => KaspaRpcJson.ReadScript(tooShort.RootElement));
            }
        }

        private static string UtxoResponse(long id)
        {
            var script = new Key(new byte[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3,
            }).PubKey.GetScriptPubKey();
            var hex = NKaspa.DataEncoders.Encoders.Hex.EncodeData(script.Script);
            return "{\"id\":" + id + ",\"method\":\"getUtxosByAddresses\",\"params\":{\"entries\":[{\"address\":\"kaspa:qq\",\"outpoint\":{\"transactionId\":\"" + new uint256(5) + "\",\"index\":2},\"utxoEntry\":{\"amount\":\"9007199254740993\",\"scriptPublicKey\":{\"version\":0,\"scriptPublicKey\":\"" + hex + "\"},\"blockDaaScore\":7,\"isCoinbase\":true}}]}}";
        }

        private sealed class ScriptedChannel : IKaspaJsonChannel
        {
            public string LastRequest { get; private set; } = string.Empty;

            public string? LastMethod { get; private set; }

            public Func<string, string> Handler { get; set; } = _ => throw new InvalidOperationException("No handler.");

            public Task<string> ExchangeAsync(string requestJson, CancellationToken cancellationToken = default)
            {
                LastRequest = requestJson;
                using var doc = JsonDocument.Parse(requestJson);
                LastMethod = doc.RootElement.GetProperty("method").GetString();
                return Task.FromResult(Handler(requestJson));
            }
        }
    }
}
