using System.Text.Json;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Blue score and blue work on the wRPC models.
    /// Field names follow rusty-kaspa: virtualDaaScore, blueScore, blueWork.
    /// </summary>
    public class BlueScoreTests
    {
        [Fact]
        public void Blue_work_hex_is_a_24_byte_little_endian_integer()
        {
            // api-tn10.kaspa.org header blueWork for sink df743fe8… on 2026-11-01, unpadded hex.
            var work = KaspaRpcJson.ReadBlueWork("7cdcc9528ef3b");
            Assert.Equal(24, work.Length);
            Assert.Equal(new byte[] { 0x3b, 0xef, 0x28, 0x95, 0xcc, 0xcd, 0x07 }, work.Take(7).ToArray());
            Assert.All(work.Skip(7), value => Assert.Equal(0, value));
            Assert.Equal(work, KaspaRpcJson.ReadBlueWork("0x07cdcc9528ef3b"));
        }

        [Fact]
        public async Task Json_dag_reads_optional_blue_score_and_omits_it_as_zero()
        {
            var withScore = await DagAsync("\"blueScore\":600042329,");
            Assert.Equal(600042329UL, withScore.BlueScore);
            Assert.Equal(50UL, withScore.VirtualDaaScore);

            var without = await DagAsync(string.Empty);
            Assert.Equal(0UL, without.BlueScore);
        }

        [Fact]
        public async Task Json_sink_blue_score_and_virtual_chain_use_upstream_names()
        {
            var channel = new ScriptedJson();
            var client = new KaspaWrpcJsonClient(channel);
            var start = new uint256(4);
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                var method = doc.RootElement.GetProperty("method").GetString();
                var parameters = doc.RootElement.GetProperty("params");
                if (method == "getSinkBlueScore")
                {
                    Assert.Equal(JsonValueKind.Object, parameters.ValueKind);
                    return "{\"id\":" + id + ",\"params\":{\"blueScore\":600042329}}";
                }

                Assert.Equal("getVirtualChainFromBlock", method);
                Assert.Equal(start.ToString(), parameters.GetProperty("startHash").GetString());
                Assert.True(parameters.GetProperty("includeAcceptedTransactionIds").GetBoolean());
                Assert.Equal(3UL, parameters.GetProperty("minConfirmationCount").GetUInt64());
                return "{\"id\":" + id + ",\"params\":{\"removedChainBlockHashes\":[\"" + new uint256(1) + "\"],\"addedChainBlockHashes\":[\"" + new uint256(2) + "\"],\"acceptedTransactionIds\":[{\"acceptingBlockHash\":\"" + new uint256(3) + "\",\"acceptedTransactionIds\":[\"" + new uint256(5) + "\"]}]}}";
            };

            Assert.Equal(600042329UL, await client.GetSinkBlueScoreAsync());
            var chain = await client.GetVirtualChainFromBlockAsync(start, true, 3);
            Assert.Equal(new uint256(1), chain.RemovedChainBlockHashes[0]);
            Assert.Equal(new uint256(2), chain.AddedChainBlockHashes[0]);
            Assert.Equal(new uint256(3), chain.AcceptedTransactionIds[0].AcceptingBlockHash);
            Assert.Equal(new uint256(5), chain.AcceptedTransactionIds[0].AcceptedTransactionIds[0]);
        }

        [Fact]
        public async Task Json_block_header_reads_blue_work_then_blue_score()
        {
            var hash = new uint256(9);
            var channel = new ScriptedJson();
            var client = new KaspaWrpcJsonClient(channel);
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                Assert.Equal("getBlock", doc.RootElement.GetProperty("method").GetString());
                var header = "{\"hash\":\"" + hash + "\",\"daaScore\":611675901,\"blueWork\":\"7cdcc9528ef3b\",\"blueScore\":600042329,\"pruningPoint\":\"" + new uint256(7) + "\"}";
                return "{\"id\":" + id + ",\"params\":{\"block\":{\"header\":" + header + ",\"verboseData\":{\"hash\":\"" + hash + "\",\"blueScore\":600042329,\"isChainBlock\":false}}}}";
            };

            var block = await client.GetBlockAsync(hash);
            Assert.NotNull(block);
            Assert.Equal(600042329UL, block!.BlueScore);
            Assert.Equal(600042329UL, block.HeaderBlueScore);
            Assert.Equal(KaspaRpcJson.ReadBlueWork("7cdcc9528ef3b"), block.BlueWork);
        }

        [Fact]
        public void Borsh_dag_has_no_blue_score_and_header_keeps_blue_work_before_blue_score()
        {
            var body = KaspaBorshCodec.DecodeFrame(BorshGolden.DagResponse(1)).Body;
            var extra = new byte[body.Length + 8];
            Buffer.BlockCopy(body, 0, extra, 0, body.Length);
            for (var i = 0; i < 8; i++)
            {
                extra[body.Length + i] = 0xff;
            }

            var dag = KaspaBorshCodec.ReadBlockDagInfo(extra);
            Assert.Equal(0UL, dag.BlueScore);
            Assert.Equal(50UL, dag.VirtualDaaScore);
            Assert.Equal(new uint256(4), dag.Sink);

            var work = new byte[24];
            for (var i = 0; i < work.Length; i++)
            {
                work[i] = (byte)(i + 1);
            }

            var hash = new uint256(9);
            var block = KaspaBorshCodec.ReadGetBlock(KaspaBorshCodec.DecodeFrame(BorshGolden.GetBlockResponse(1, hash, work)).Body, hash);
            Assert.NotNull(block);
            Assert.Equal(work, block!.BlueWork);
            Assert.Equal(4UL, block.BlueScore);
            Assert.Equal(4UL, block.HeaderBlueScore);
        }

        [Fact]
        public void Borsh_sink_blue_score_and_virtual_chain_match_message_rs_order()
        {
            var sink = KaspaBorshCodec.ReadSinkBlueScore(KaspaBorshCodec.DecodeFrame(BorshGolden.SinkResponse(4, 600042329UL)).Body);
            Assert.Equal(600042329UL, sink);

            var start = new uint256(8);
            var payload = KaspaBorshCodec.VirtualChainPayload(start, true, 3UL);
            Assert.Equal(BorshGolden.VirtualChainRequest(start, true, 3UL), payload);
            Assert.Equal(KaspaBorshCodec.OpGetVirtualChainFromBlock, (byte)128);
            Assert.Equal(KaspaBorshCodec.OpGetSinkBlueScore, (byte)138);

            var removed = new uint256(1);
            var added = new uint256(2);
            var accepting = new uint256(3);
            var transaction = new uint256(5);
            var chain = KaspaBorshCodec.ReadVirtualChain(KaspaBorshCodec.DecodeFrame(BorshGolden.VirtualChainResponse(4, removed, added, accepting, transaction)).Body);
            Assert.Equal(removed, chain.RemovedChainBlockHashes[0]);
            Assert.Equal(added, chain.AddedChainBlockHashes[0]);
            Assert.Equal(accepting, chain.AcceptedTransactionIds[0].AcceptingBlockHash);
            Assert.Equal(transaction, chain.AcceptedTransactionIds[0].AcceptedTransactionIds[0]);
        }

        [Fact]
        public async Task Borsh_client_reads_sink_blue_score_on_op_138()
        {
            var channel = new ScriptedBorsh(request =>
            {
                var id = KaspaBorshCodec.ReadRequestId(request);
                Assert.Equal(KaspaBorshCodec.OpGetSinkBlueScore, request[9]);
                return BorshGolden.SinkResponse(id, 42);
            });
            var score = await new KaspaWrpcBorshClient(channel).GetSinkBlueScoreAsync();
            Assert.Equal(42UL, score);
        }

        private static async Task<RpcBlockDagInfo> DagAsync(string blueScoreField)
        {
            var channel = new ScriptedJson();
            var client = new KaspaWrpcJsonClient(channel);
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                return "{\"id\":" + id + ",\"params\":{" + blueScoreField + "\"network\":\"testnet-10\",\"blockCount\":10,\"headerCount\":11,\"tipHashes\":[\"" + new uint256(1) + "\"],\"difficulty\":1.5,\"pastMedianTime\":99,\"virtualParentHashes\":[\"" + new uint256(2) + "\"],\"pruningPointHash\":\"" + new uint256(3) + "\",\"virtualDaaScore\":50,\"sink\":\"" + new uint256(4) + "\"}}";
            };
            return await client.GetBlockDagInfoAsync();
        }

        private sealed class ScriptedJson : IKaspaJsonChannel
        {
            public Func<string, string> Handler { get; set; } = _ => throw new InvalidOperationException("No handler.");

            public Task<string> ExchangeAsync(string requestJson, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Handler(requestJson));
            }
        }

        private sealed class ScriptedBorsh : IKaspaBorshChannel
        {
            private readonly Func<byte[], byte[]> _handler;

            public ScriptedBorsh(Func<byte[], byte[]> handler)
            {
                _handler = handler;
            }

            public event Action<byte[]>? NotificationReceived
            {
                add { }
                remove { }
            }

            public Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_handler(request));
            }
        }
    }
}
