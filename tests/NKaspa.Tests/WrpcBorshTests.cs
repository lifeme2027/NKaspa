using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Byte-exact Borsh vectors and loopback wRPC.
    /// Cargo is not installed here, so the vectors are hand-built from the rusty-kaspa layout
    /// (commit 4edcbbccb449f84832afb1ca157904b35f37d006) and workflow-rpc 0.19.0
    /// (workflow-rs 7e2958098bbe0647adcfcc0dfc174e438a60699e).
    /// Expected bytes come from <see cref="BorshGolden"/>, not from <see cref="KaspaBorshCodec"/>.
    /// </summary>
    public class WrpcBorshTests
    {
        private const string ServerInfoRequestHex = "01010000000000000072020000000100";

        private const string ServerInfoResponseHex = "01010000000000000000017201170000000100010001000100000076000001000700000000000000";

        [Fact]
        public void Literal_hex_matches_the_hand_layout_for_server_info_and_balance()
        {
            var zero = new KaspaPubKeyAddress(new byte[32], Network.Mainnet).ToString();
            Assert.Equal(ServerInfoRequestHex, BorshGolden.Hex(KaspaBorshCodec.EmptyRequest(1, KaspaBorshCodec.OpGetServerInfo)));
            Assert.Equal(ServerInfoRequestHex, BorshGolden.Hex(BorshGolden.Request(1, KaspaBorshCodec.OpGetServerInfo, BorshGolden.Wrap(BorshGolden.VersionOnly()))));
            var balance = ZeroBalanceRequest();
            Assert.Equal(54, balance.Length);
            Assert.Equal(BorshGolden.Hex(balance), BorshGolden.Hex(KaspaBorshCodec.BalanceRequest(1, zero)));
            Assert.Equal(BorshGolden.Hex(balance), BorshGolden.Hex(BorshGolden.BalanceRequest(1, zero)));

            var response = BorshGolden.ServerInfoResponse(1, 1, 1, "v", 0, null, true, false, 7);
            Assert.Equal(ServerInfoResponseHex, BorshGolden.Hex(response));
            var info = KaspaBorshCodec.ReadServerInfo(KaspaBorshCodec.DecodeFrame(BorshGolden.FromHex(ServerInfoResponseHex)).Body);
            Assert.Equal(1, info.RpcApiVersion);
            Assert.Equal(1, info.RpcApiRevision);
            Assert.Equal("v", info.ServerVersion);
            Assert.Equal("mainnet", info.NetworkId);
            Assert.True(info.HasUtxoIndex);
            Assert.False(info.IsSynced);
            Assert.Equal(7UL, info.VirtualDaaScore);
        }

        [Fact]
        public void Requests_match_the_independent_spec_and_responses_decode()
        {
            var address = new KaspaPubKeyAddress(Sequence(1), Network.Mainnet).ToString();
            var other = new KaspaPubKeyAddress(Sequence(2), Network.Testnet).ToString();
            var addresses = new[] { address, other };

            AssertBytes(BorshGolden.Request(4, KaspaBorshCodec.OpGetBlockDagInfo, BorshGolden.Wrap(BorshGolden.VersionOnly())), KaspaBorshCodec.EmptyRequest(4, KaspaBorshCodec.OpGetBlockDagInfo));
            AssertBytes(BorshGolden.Request(5, KaspaBorshCodec.OpGetFeeEstimate, BorshGolden.Wrap(BorshGolden.VersionOnly())), KaspaBorshCodec.EmptyRequest(5, KaspaBorshCodec.OpGetFeeEstimate));
            AssertBytes(BorshGolden.BalanceRequest(2, address), KaspaBorshCodec.BalanceRequest(2, address));
            AssertBytes(BorshGolden.AddressesRequest(3, KaspaBorshCodec.OpGetBalancesByAddresses, addresses), KaspaBorshCodec.AddressesRequest(3, KaspaBorshCodec.OpGetBalancesByAddresses, addresses));
            AssertBytes(BorshGolden.AddressesRequest(6, KaspaBorshCodec.OpGetUtxosByAddresses, addresses), KaspaBorshCodec.AddressesRequest(6, KaspaBorshCodec.OpGetUtxosByAddresses, addresses));
            AssertBytes(BorshGolden.Scope(KaspaBorshCodec.ScopeUtxosChanged, addresses), KaspaBorshCodec.ScopePayload(KaspaBorshCodec.ScopeUtxosChanged, addresses));
            AssertBytes(BorshGolden.Scope(KaspaBorshCodec.ScopeBlockAdded, null), KaspaBorshCodec.ScopePayload(KaspaBorshCodec.ScopeBlockAdded, null));
            AssertBytes(BorshGolden.Scope(KaspaBorshCodec.ScopeVirtualDaaScoreChanged, null), KaspaBorshCodec.ScopePayload(KaspaBorshCodec.ScopeVirtualDaaScoreChanged, null));

            var tx = SampleTransaction();
            AssertBytes(BorshGolden.SubmitRequest(8, tx, true), KaspaBorshCodec.SubmitRequest(8, tx, true));
            var blockHash = new uint256(Sequence(3));
            AssertBytes(BorshGolden.GetBlockRequest(9, blockHash, false), KaspaBorshCodec.GetBlockRequest(9, blockHash, false));

            var dag = KaspaBorshCodec.ReadBlockDagInfo(Body(BorshGolden.DagResponse(1)));
            Assert.Equal("testnet-10", dag.Network);
            Assert.Equal(10UL, dag.BlockCount);
            Assert.Equal(11UL, dag.HeaderCount);
            Assert.Equal(new uint256(1), dag.TipHashes[0]);
            Assert.Equal(1.5, dag.Difficulty);
            Assert.Equal(99UL, dag.PastMedianTime);
            Assert.Equal(new uint256(2), dag.VirtualParentHashes[0]);
            Assert.Equal(new uint256(3), dag.PruningPointHash);
            Assert.Equal(50UL, dag.VirtualDaaScore);
            Assert.Equal(new uint256(4), dag.Sink);
            Assert.Equal(0UL, dag.BlueScore);

            Assert.Equal(9007199254740993UL, KaspaBorshCodec.ReadBalance(Body(BorshGolden.BalanceResponse(1, 9007199254740993UL))).Sompi);

            var balances = KaspaBorshCodec.ReadBalances(Body(BorshGolden.BalancesResponse(1, address, 5, other)));
            Assert.Equal(address, balances[0].Address);
            Assert.Equal(5UL, balances[0].Balance.Value.Sompi);
            Assert.Equal(other, balances[1].Address);
            Assert.Null(balances[1].Balance);

            var utxos = KaspaBorshCodec.ReadUtxos(Body(BorshGolden.UtxosResponse(1, address)));
            Assert.Equal(address, utxos[0].Address);
            Assert.Equal(new uint256(9), utxos[0].Outpoint.TransactionId);
            Assert.Equal(3U, utxos[0].Outpoint.Index);
            Assert.Equal(5UL, utxos[0].Entry.Amount.Sompi);
            Assert.Equal(7UL, utxos[0].Entry.BlockDaaScore);
            Assert.True(utxos[0].Entry.IsCoinbase);
            Assert.Equal(new uint256(8), utxos[0].Entry.CovenantId);
            Assert.Equal(new byte[] { 0x51 }, utxos[0].Entry.ScriptPublicKey.Script);

            Assert.Equal(new uint256(9), KaspaBorshCodec.ReadTransactionId(Body(BorshGolden.SubmitResponse(1, new uint256(9)))));

            var fees = KaspaBorshCodec.ReadFeeEstimate(Body(BorshGolden.FeeResponse(1)));
            Assert.Equal(1.0, fees.PriorityBucket!.Feerate);
            Assert.Equal(2.0, fees.PriorityBucket.EstimatedSeconds);
            Assert.Equal(4.0, fees.NormalBuckets[0].EstimatedSeconds);
            Assert.Equal(5.0, fees.LowBuckets[0].Feerate);

            var block = KaspaBorshCodec.ReadGetBlock(Body(BorshGolden.GetBlockResponse(1, blockHash)), blockHash);
            Assert.NotNull(block);
            Assert.Equal(blockHash, block!.Hash);
            Assert.Equal(4UL, block.BlueScore);
            Assert.Equal(4UL, block.HeaderBlueScore);
            Assert.Equal(new uint256(6), block.AcceptedIdMerkleRoot);
            Assert.Equal(new uint256(5), block.DirectParent);
            Assert.True(block.ParentsPresent);
            Assert.True(block.IsChainBlock);
            Assert.True(block.ChainFlagPresent);
            Assert.Equal(new uint256(5), block.SelectedParentHash);

            var note = KaspaBorshCodec.TryReadUtxosChanged(KaspaBorshCodec.DecodeFrame(BorshGolden.UtxosNotification(address)).Body);
            Assert.NotNull(note);
            Assert.Single(note!.Added);
            Assert.Empty(note.Removed);
            Assert.Equal(5UL, note.Added[0].Entry.Amount.Sompi);

            Assert.Equal(42UL, KaspaBorshCodec.TryReadVirtualDaa(KaspaBorshCodec.DecodeFrame(BorshGolden.DaaNotification(42)).Body));

            var added = KaspaBorshCodec.TryReadBlockAdded(KaspaBorshCodec.DecodeFrame(BorshGolden.BlockNotification(blockHash)).Body);
            Assert.NotNull(added);
            Assert.Equal(blockHash, added!.Hash);
            Assert.Equal((ushort)1, added.Version);
            Assert.Equal(9UL, added.DaaScore);
            Assert.Equal(4UL, added.BlueScore);
            Assert.Equal(8UL, added.Timestamp);
            Assert.Equal(new uint256(7), added.PruningPoint);
            Assert.Equal(new uint256(5), added.DirectParent);
            Assert.True(added.IsChainBlock);
            Assert.Empty(added.Transactions);

            var missing = KaspaBorshCodec.DecodeFrame(BorshGolden.ProtocolError(1, 3));
            Assert.Equal(3, missing.Error!.RpcCode);
            Assert.Equal("RPC method not found", missing.Error.Message);

            var inner = KaspaBorshCodec.DecodeFrame(BorshGolden.ResultError(1, KaspaBorshCodec.OpGetServerInfo, 10, "x"));
            Assert.Equal(10, inner.Error!.RpcCode);
            Assert.Equal("request deserialization error", inner.Error.Message);
        }

        [Fact]
        public async Task Client_round_trips_queries_through_a_fake_borsh_socket()
        {
            var address = new KaspaPubKeyAddress(Sequence(1), Network.Mainnet).ToString();
            var tx = SampleTransaction();
            var blockHash = new uint256(Sequence(3));
            var ops = new List<byte>();
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/");
            listener.Start();
            var server = Task.Run(() => ServeBorshAsync(listener, ops, address, blockHash));

            await using var connection = KaspaRpcClient.Create("ws://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/", KaspaRpcEncoding.Borsh);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await connection.ConnectAsync(timeout.Token);
            var info = await connection.GetServerInfoAsync(timeout.Token);
            var dag = await connection.GetBlockDagInfoAsync(timeout.Token);
            var balance = await connection.GetBalanceByAddressAsync(address, timeout.Token);
            var balances = await connection.GetBalancesByAddressesAsync(new[] { address }, timeout.Token);
            var utxos = await connection.GetUtxosByAddressesAsync(new[] { address }, timeout.Token);
            var fees = await connection.GetFeeEstimateAsync(timeout.Token);
            var id = await connection.SubmitTransactionAsync(tx, true, timeout.Token);
            var block = await connection.GetBlockAsync(blockHash, false, timeout.Token);

            Assert.Equal("mainnet", info.NetworkId);
            Assert.Equal("testnet-10", dag.Network);
            Assert.Equal(9007199254740993UL, balance.Sompi);
            Assert.Equal(5UL, balances[0].Balance.Value.Sompi);
            Assert.Equal(5UL, utxos[0].Entry.Amount.Sompi);
            Assert.Equal(1.0, fees.PriorityBucket!.Feerate);
            Assert.Equal(tx.GetId(), id);
            Assert.Equal(blockHash, block!.Hash);
            Assert.Equal(4UL, block.BlueScore);
            Assert.Equal(new byte[24], block.BlueWork);
            await connection.DisposeAsync();
            await server.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new byte[] { 114, 131, 136, 137, 135, 147, 125, 126 }, ops);
        }

        [Fact]
        public async Task Subscription_notification_arrives_and_unsubscribe_uses_op_4()
        {
            var address = new KaspaPubKeyAddress(Sequence(1), Network.Mainnet).ToString();
            var channel = new MemoryBorsh();
            byte[]? unsubscribe = null;
            var unsubCount = 0;
            channel.Responder = request =>
            {
                var op = request[9];
                var id = BinaryPrimitives.ReadUInt64LittleEndian(request.AsSpan(1, 8));
                if (op == KaspaBorshCodec.OpSubscribe)
                {
                    AssertBytes(BorshGolden.Scope(KaspaBorshCodec.ScopeUtxosChanged, new[] { address }), request.AsSpan(10).ToArray());
                    return BorshGolden.Success(id, op, BorshGolden.SubscribeAck(7));
                }

                if (op == KaspaBorshCodec.OpUnsubscribe)
                {
                    unsubCount++;
                    unsubscribe = request;
                    return BorshGolden.Success(id, op, BorshGolden.VersionOnly());
                }

                throw new InvalidOperationException(op.ToString(CultureInfo.InvariantCulture));
            };

            var client = new KaspaWrpcBorshClient(channel);
            RpcUtxosChanged? seen = null;
            var subscription = await client.NotifyUtxosChangedAsync(new[] { address }, note => seen = note);
            channel.Raise(BorshGolden.UtxosNotification(address));
            Assert.NotNull(seen);
            Assert.Equal(5UL, seen!.Added[0].Entry.Amount.Sompi);
            await subscription.DisposeAsync();
            await subscription.UnsubscribeAsync();
            Assert.Equal(1, unsubCount);
            Assert.NotNull(unsubscribe);
            Assert.Equal(KaspaBorshCodec.OpUnsubscribe, unsubscribe![9]);
            AssertBytes(BorshGolden.Scope(KaspaBorshCodec.ScopeUtxosChanged, new[] { address }), unsubscribe.AsSpan(10).ToArray());

            ulong? score = null;
            channel.Responder = request =>
            {
                var id = BinaryPrimitives.ReadUInt64LittleEndian(request.AsSpan(1, 8));
                return BorshGolden.Success(id, request[9], BorshGolden.SubscribeAck(8));
            };
            var daa = await client.NotifyVirtualDaaScoreChangedAsync(note => score = note.VirtualDaaScore);
            channel.Raise(BorshGolden.DaaNotification(42));
            Assert.Equal(42UL, score);

            RpcBlockAdded? added = null;
            var hash = new uint256(Sequence(3));
            var blockSub = await client.NotifyBlockAddedAsync(note => added = note);
            channel.Raise(BorshGolden.BlockNotification(hash));
            Assert.Equal(hash, added!.Hash);

            await daa.DisposeAsync();
            await blockSub.DisposeAsync();

            var missing = new MemoryBorsh
            {
                Responder = request =>
                {
                    var id = BinaryPrimitives.ReadUInt64LittleEndian(request.AsSpan(1, 8));
                    return BorshGolden.ProtocolError(id, 3);
                },
            };
            Assert.Null(await new KaspaWrpcBorshClient(missing).GetBlockAsync(hash));
        }

        [Fact]
        public async Task Borsh_socket_delivers_a_utxo_notification()
        {
            var address = new KaspaPubKeyAddress(Sequence(1), Network.Mainnet).ToString();
            byte[]? unsubscribe = null;
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/");
            listener.Start();
            var server = Task.Run(() => ServeSubscribeAsync(listener, address, frame => unsubscribe = frame));

            await using var connection = KaspaRpcClient.Create("ws://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/", KaspaRpcEncoding.Borsh);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await connection.ConnectAsync(timeout.Token);
            var seen = new TaskCompletionSource<RpcUtxosChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
            var subscription = await connection.NotifyUtxosChangedAsync(new[] { address }, note => seen.TrySetResult(note), timeout.Token);
            var note = await seen.Task.WaitAsync(timeout.Token);
            Assert.Equal(address, note.Added[0].Address);
            await subscription.DisposeAsync();
            await server.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(unsubscribe);
            Assert.Equal(KaspaBorshCodec.OpUnsubscribe, unsubscribe![9]);
        }

        [Fact]
        public async Task Auto_selects_borsh_and_json_ports_and_rejects_17310()
        {
            await using (var mainnet = KaspaRpcClient.Create("ws://127.0.0.1:17110/"))
            await using (var testnet = KaspaRpcClient.Create("ws://example.test:17210/"))
            await using (var simnet = KaspaRpcClient.Create("wss://127.0.0.1:17510/"))
            await using (var devnet = KaspaRpcClient.Create("ws://127.0.0.1:17610/"))
            {
                Assert.Equal(KaspaRpcEncoding.Borsh, mainnet.Encoding);
                Assert.Equal(KaspaRpcEncoding.Borsh, testnet.Encoding);
                Assert.Equal(KaspaRpcEncoding.Borsh, simnet.Encoding);
                Assert.Equal(KaspaRpcEncoding.Borsh, devnet.Encoding);
                Assert.Equal(KaspaWebSocketState.Closed, mainnet.State);
                Assert.False(mainnet.IsConnected);
            }

            await using (var mainnet = KaspaRpcClient.Create("ws://127.0.0.1:18110/"))
            await using (var testnet = KaspaRpcClient.Create("ws://127.0.0.1:18210/"))
            await using (var simnet = KaspaRpcClient.Create("ws://127.0.0.1:18510/"))
            await using (var devnet = KaspaRpcClient.Create("ws://127.0.0.1:18610/"))
            {
                Assert.Equal(KaspaRpcEncoding.Json, mainnet.Encoding);
                Assert.Equal(KaspaRpcEncoding.Json, testnet.Encoding);
                Assert.Equal(KaspaRpcEncoding.Json, simnet.Encoding);
                Assert.Equal(KaspaRpcEncoding.Json, devnet.Encoding);
                Assert.Equal(KaspaWebSocketState.Closed, simnet.State);
            }

            var simnetBorsh = Assert.Throws<ArgumentException>(() => KaspaRpcClient.Create("ws://127.0.0.1:17310/"));
            Assert.Contains("17510", simnetBorsh.Message, StringComparison.Ordinal);
            var simnetJson = Assert.Throws<ArgumentException>(() => KaspaRpcClient.Create("ws://127.0.0.1:18310/"));
            Assert.Contains("18510", simnetJson.Message, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => KaspaRpcClient.Create("http://127.0.0.1:17110/"));
            Assert.Throws<ArgumentNullException>(() => KaspaRpcClient.Create(null!));

            await using var forced = KaspaRpcClient.Create("ws://127.0.0.1:18110/", KaspaRpcEncoding.Borsh);
            Assert.Equal(KaspaRpcEncoding.Borsh, forced.Encoding);
            Assert.Equal(16 * 1024 * 1024, forced.Options.MaxMessageBytes);
            await using var forcedJson = KaspaRpcClient.Create("ws://127.0.0.1:17110/", KaspaRpcEncoding.Json);
            Assert.Equal(KaspaRpcEncoding.Json, forcedJson.Encoding);
        }

        [Fact]
        public async Task Json_balances_and_subscribe_use_the_serde_scope_shape()
        {
            var channel = new ScriptedJson();
            var client = new KaspaWrpcJsonClient(channel);
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                Assert.Equal("getBalancesByAddresses", doc.RootElement.GetProperty("method").GetString());
                return "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"method\":\"getBalancesByAddresses\",\"params\":{\"entries\":[{\"address\":\"kaspa:qq\",\"balance\":5},{\"address\":\"kaspa:qr\",\"balance\":null}]}}";
            };

            var rows = await client.GetBalancesByAddressesAsync(new[] { "kaspa:qq", "kaspa:qr" });
            Assert.Equal(5UL, rows[0].Balance.Value.Sompi);
            Assert.Null(rows[1].Balance);
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.NotifyUtxosChangedAsync(new[] { "kaspa:qq" }, _ => { }));

            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/");
            listener.Start();
            string? subscribe = null;
            string? unsubscribe = null;
            var server = Task.Run(() => ServeJsonSubscribeAsync(listener, text => subscribe = text, text => unsubscribe = text));
            await using var connection = KaspaRpcClient.Create("ws://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/", KaspaRpcEncoding.Json);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await connection.ConnectAsync(timeout.Token);
            var seen = new TaskCompletionSource<RpcUtxosChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
            var daaSeen = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
            var blockSeen = new TaskCompletionSource<RpcBlockAdded>(TaskCreationOptions.RunContinuationsAsynchronously);
            var utxos = await connection.NotifyUtxosChangedAsync(new[] { "kaspa:qq" }, note => seen.TrySetResult(note), timeout.Token);
            var daa = await connection.NotifyVirtualDaaScoreChangedAsync(note => daaSeen.TrySetResult(note.VirtualDaaScore), timeout.Token);
            var blocks = await connection.NotifyBlockAddedAsync(note => blockSeen.TrySetResult(note), timeout.Token);
            var note = await seen.Task.WaitAsync(timeout.Token);
            Assert.Equal(5UL, note.Added[0].Entry.Amount.Sompi);
            Assert.Equal(42UL, await daaSeen.Task.WaitAsync(timeout.Token));
            Assert.Equal(uint256.Zero, (await blockSeen.Task.WaitAsync(timeout.Token)).Hash);
            await utxos.DisposeAsync();
            await daa.DisposeAsync();
            await blocks.DisposeAsync();
            await server.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains("\"UtxosChanged\"", subscribe, StringComparison.Ordinal);
            Assert.Contains("kaspa:qq", subscribe, StringComparison.Ordinal);
            Assert.Contains("\"method\":\"unsubscribe\"", unsubscribe, StringComparison.Ordinal);
        }

        private static byte[] ZeroBalanceRequest()
        {
            // id=1, op=136, Serializable body is u16 version 1 plus a mainnet pubkey address of 32 zero bytes.
            var header = new byte[]
            {
                0x01,
                0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x88,
                0x28, 0x00, 0x00, 0x00,
                0x01, 0x00,
                0x00, 0x00,
                0x20, 0x00, 0x00, 0x00,
            };
            var frame = new byte[header.Length + 32];
            Buffer.BlockCopy(header, 0, frame, 0, header.Length);
            return frame;
        }

        private static Transaction SampleTransaction()
        {
            var output = new TxOut(5UL, new ScriptPubKey(0, new byte[] { 0x51 }));
            output.Covenant = new CovenantBinding(2, new uint256(8));
            var tx = new Transaction(
                0,
                new[] { new TxIn(new OutPoint(new uint256(9), 3), Script.Empty, 4, 1) },
                new[] { output },
                8,
                SubnetworkId.Native,
                0,
                new byte[] { 1, 2 });
            tx.StorageMass = 11;
            return tx;
        }

        private static void AssertBytes(byte[] expected, byte[] actual)
        {
            Assert.Equal(BorshGolden.Hex(expected), BorshGolden.Hex(actual));
        }

        private static byte[] Body(byte[] frame)
        {
            var incoming = KaspaBorshCodec.DecodeFrame(frame);
            Assert.Null(incoming.Error);
            return incoming.Body;
        }

        private static byte[] Sequence(byte seed)
        {
            var bytes = new byte[32];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(seed + i);
            }

            return bytes;
        }

        private static int FreePort()
        {
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
            return port;
        }

        private static async Task ServeBorshAsync(HttpListener listener, List<byte> ops, string address, uint256 blockHash)
        {
            var context = await listener.GetContextAsync();
            var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
            try
            {
                for (var i = 0; i < 8; i++)
                {
                    var request = await ReceiveBinaryAsync(socket);
                    if (request == null)
                    {
                        return;
                    }

                    var op = request[9];
                    lock (ops)
                    {
                        ops.Add(op);
                    }

                    var id = BinaryPrimitives.ReadUInt64LittleEndian(request.AsSpan(1, 8));
                    byte[] response;
                    if (op == KaspaBorshCodec.OpGetServerInfo)
                    {
                        response = BorshGolden.ServerInfoResponse(id, 1, 1, "v", 0, null, true, false, 7);
                    }
                    else if (op == KaspaBorshCodec.OpGetBlockDagInfo)
                    {
                        response = BorshGolden.DagResponse(id);
                    }
                    else if (op == KaspaBorshCodec.OpGetBalanceByAddress)
                    {
                        response = BorshGolden.BalanceResponse(id, 9007199254740993UL);
                    }
                    else if (op == KaspaBorshCodec.OpGetBalancesByAddresses)
                    {
                        response = BorshGolden.BalancesResponse(id, address, 5, null);
                    }
                    else if (op == KaspaBorshCodec.OpGetUtxosByAddresses)
                    {
                        response = BorshGolden.UtxosResponse(id, address);
                    }
                    else if (op == KaspaBorshCodec.OpGetFeeEstimate)
                    {
                        response = BorshGolden.FeeResponse(id);
                    }
                    else if (op == KaspaBorshCodec.OpSubmitTransaction)
                    {
                        response = BorshGolden.SubmitResponse(id, SampleTransaction().GetId());
                    }
                    else if (op == KaspaBorshCodec.OpGetBlock)
                    {
                        response = BorshGolden.GetBlockResponse(id, blockHash);
                    }
                    else
                    {
                        response = BorshGolden.ProtocolError(id, 3);
                    }

                    await socket.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Binary, true, CancellationToken.None);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task ServeSubscribeAsync(HttpListener listener, string address, Action<byte[]> onUnsubscribe)
        {
            var context = await listener.GetContextAsync();
            var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
            try
            {
                var request = await ReceiveBinaryAsync(socket);
                var id = BinaryPrimitives.ReadUInt64LittleEndian(request!.AsSpan(1, 8));
                var ack = BorshGolden.Success(id, KaspaBorshCodec.OpSubscribe, BorshGolden.SubscribeAck(7));
                await socket.SendAsync(new ArraySegment<byte>(ack), WebSocketMessageType.Binary, true, CancellationToken.None);
                var note = BorshGolden.UtxosNotification(address);
                await socket.SendAsync(new ArraySegment<byte>(note), WebSocketMessageType.Binary, true, CancellationToken.None);
                var unsubscribe = await ReceiveBinaryAsync(socket);
                onUnsubscribe(unsubscribe!);
                var done = BorshGolden.Success(BinaryPrimitives.ReadUInt64LittleEndian(unsubscribe.AsSpan(1, 8)), KaspaBorshCodec.OpUnsubscribe, BorshGolden.VersionOnly());
                await socket.SendAsync(new ArraySegment<byte>(done), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task ServeJsonSubscribeAsync(HttpListener listener, Action<string> onSubscribe, Action<string> onUnsubscribe)
        {
            var context = await listener.GetContextAsync();
            var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
            try
            {
                string? first = null;
                for (var i = 0; i < 3; i++)
                {
                    var request = await ReceiveTextAsync(socket);
                    if (i == 0)
                    {
                        first = request;
                    }

                    using var doc = JsonDocument.Parse(request!);
                    var id = doc.RootElement.GetProperty("id").GetInt64();
                    var body = "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"method\":\"subscribe\",\"params\":{\"id\":7}}";
                    var bytes = Encoding.UTF8.GetBytes(body);
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }

                onSubscribe(first!);
                await SendTextAsync(socket, "{\"method\":\"utxosChangedNotification\",\"params\":{\"UtxosChanged\":{\"added\":[{\"address\":\"kaspa:qq\",\"outpoint\":{\"transactionId\":\"" + uint256.Zero + "\",\"index\":1},\"utxoEntry\":{\"amount\":5,\"scriptPublicKey\":{\"version\":0,\"script\":\"51\"},\"blockDaaScore\":7,\"isCoinbase\":false}}],\"removed\":[]}}}");
                await SendTextAsync(socket, "{\"method\":\"virtualDaaScoreChangedNotification\",\"params\":{\"VirtualDaaScoreChanged\":{\"virtualDaaScore\":42}}}");
                await SendTextAsync(socket, "{\"method\":\"blockAddedNotification\",\"params\":{\"BlockAdded\":{\"block\":{\"header\":{\"hash\":\"" + uint256.Zero + "\",\"version\":1,\"parentsByLevel\":[[\"" + uint256.Zero + "\"]],\"acceptedIdMerkleRoot\":\"" + uint256.Zero + "\",\"timestamp\":9,\"daaScore\":3,\"blueScore\":4,\"pruningPoint\":\"" + uint256.Zero + "\"},\"transactions\":[],\"verboseData\":{\"isChainBlock\":true,\"selectedParentHash\":\"" + uint256.Zero + "\"}}}}}");
                string? unsubscribe = null;
                for (var i = 0; i < 3; i++)
                {
                    var request = await ReceiveTextAsync(socket);
                    if (i == 0)
                    {
                        unsubscribe = request;
                    }

                    using var doc = JsonDocument.Parse(request!);
                    var id = doc.RootElement.GetProperty("id").GetInt64();
                    var body = "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"method\":\"unsubscribe\",\"params\":{}}";
                    var bytes = Encoding.UTF8.GetBytes(body);
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }

                onUnsubscribe(unsubscribe!);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task SendTextAsync(WebSocket socket, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task<byte[]?> ReceiveBinaryAsync(WebSocket socket)
        {
            var buffer = new byte[8192];
            using var message = new MemoryStream();
            while (true)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                message.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    return message.ToArray();
                }
            }
        }

        private static async Task<string?> ReceiveTextAsync(WebSocket socket)
        {
            var bytes = await ReceiveBinaryAsync(socket);
            return bytes == null ? null : Encoding.UTF8.GetString(bytes);
        }

        private sealed class MemoryBorsh : IKaspaBorshChannel
        {
            public Func<byte[], byte[]> Responder { get; set; } = _ => Array.Empty<byte>();

            public event Action<byte[]>? NotificationReceived;

            public void Raise(byte[] frame)
            {
                NotificationReceived?.Invoke(frame);
            }

            public Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Responder(request));
            }
        }

        private sealed class ScriptedJson : IKaspaJsonChannel
        {
            public Func<string, string> Handler { get; set; } = _ => "{}";

            public Task<string> ExchangeAsync(string requestJson, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Handler(requestJson));
            }
        }
    }

    /// <summary>
    /// Independent Borsh writer for rusty-kaspa rpc-core Serializer bodies.
    /// store! is raw Borsh. serialize! is a u32 length around Serializer::serialize.
    /// serialize!(Vec) is that length around (u32 count + a payload per item).
    /// serialize!(Option) None is a payload of one 0 byte. Some is 1 plus a payload of the inner serializer.
    /// A wRPC success payload is Borsh Result::Ok (1u8 in borsh 1.8.1; Err is 0u8) plus Serializable, which is another u32 length.
    /// </summary>
    internal static class BorshGolden
    {
        internal static byte[] Request(ulong id, byte op, byte[] payload)
        {
            var writer = new Writer();
            writer.U8(1);
            writer.U64(id);
            writer.U8(op);
            writer.Raw(payload);
            return writer.ToArray();
        }

        internal static byte[] Success(ulong id, byte op, byte[] serializerBody)
        {
            var writer = new Writer();
            writer.U8(1);
            writer.U64(id);
            writer.U8(0);
            writer.U8(1);
            writer.U8(op);
            writer.U8(1);
            writer.Vec(serializerBody);
            return writer.ToArray();
        }

        internal static byte[] Notification(byte op, byte[] serializerBody)
        {
            var writer = new Writer();
            writer.U8(0);
            writer.U8(0xff);
            writer.U8(1);
            writer.U8(op);
            writer.Vec(serializerBody);
            return writer.ToArray();
        }

        internal static byte[] ProtocolError(ulong id, byte code)
        {
            var writer = new Writer();
            writer.U8(1);
            writer.U64(id);
            writer.U8(1);
            writer.U8(0);
            writer.U8(code);
            return writer.ToArray();
        }

        internal static byte[] ResultError(ulong id, byte op, byte code, string text)
        {
            var writer = new Writer();
            writer.U8(1);
            writer.U64(id);
            writer.U8(0);
            writer.U8(1);
            writer.U8(op);
            writer.U8(0);
            writer.U8(code);
            writer.String(text);
            return writer.ToArray();
        }

        internal static byte[] VersionOnly()
        {
            var writer = new Writer();
            writer.U16(1);
            return writer.ToArray();
        }

        internal static byte[] Wrap(byte[] body)
        {
            var writer = new Writer();
            writer.Vec(body);
            return writer.ToArray();
        }

        internal static byte[] BalanceRequest(ulong id, string address)
        {
            var body = new Writer();
            body.U16(1);
            Address(body, address);
            return Request(id, 136, Wrap(body.ToArray()));
        }

        internal static byte[] AddressesRequest(ulong id, byte op, IReadOnlyList<string> addresses)
        {
            var body = new Writer();
            body.U16(1);
            AddressList(body, addresses);
            return Request(id, op, Wrap(body.ToArray()));
        }

        internal static byte[] Scope(ushort scope, IReadOnlyList<string>? addresses)
        {
            var body = new Writer();
            body.U16(1);
            body.U8((byte)scope);
            if (scope == 4)
            {
                AddressList(body, addresses ?? Array.Empty<string>());
            }

            return Wrap(body.ToArray());
        }

        internal static byte[] SubmitRequest(ulong id, Transaction transaction, bool allowOrphan)
        {
            var body = new Writer();
            body.U16(1);
            body.Payload(writer => TransactionBody(writer, transaction));
            body.U8(allowOrphan ? (byte)1 : (byte)0);
            return Request(id, 125, Wrap(body.ToArray()));
        }

        internal static byte[] GetBlockRequest(ulong id, uint256 hash, bool includeTransactions)
        {
            var body = new Writer();
            body.U16(1);
            body.Raw(hash.ToBytes());
            body.U8(includeTransactions ? (byte)1 : (byte)0);
            return Request(id, 126, Wrap(body.ToArray()));
        }

        internal static byte[] ServerInfoResponse(ulong id, ushort api, ushort revision, string version, byte networkType, uint? suffix, bool hasUtxo, bool synced, ulong score)
        {
            var body = new Writer();
            body.U16(1);
            body.U16(api);
            body.U16(revision);
            body.String(version);
            WriteNetworkId(body, networkType, suffix);
            body.U8(hasUtxo ? (byte)1 : (byte)0);
            body.U8(synced ? (byte)1 : (byte)0);
            body.U64(score);
            return Success(id, 114, body.ToArray());
        }

        internal static byte[] DagResponse(ulong id)
        {
            var body = new Writer();
            body.U16(1);
            WriteNetworkId(body, 1, 10);
            body.U64(10);
            body.U64(11);
            HashList(body, new uint256(1));
            body.F64(1.5);
            body.U64(99);
            HashList(body, new uint256(2));
            body.Raw(new uint256(3).ToBytes());
            body.U64(50);
            body.Raw(new uint256(4).ToBytes());
            return Success(id, 131, body.ToArray());
        }

        internal static byte[] BalanceResponse(ulong id, ulong balance)
        {
            var body = new Writer();
            body.U16(1);
            body.U64(balance);
            return Success(id, 136, body.ToArray());
        }

        internal static byte[] BalancesResponse(ulong id, string first, ulong firstBalance, string? second)
        {
            var entry = new Writer();
            entry.U8(1);
            Address(entry, first);
            entry.U8(1);
            entry.U64(firstBalance);
            var items = new Writer();
            items.U32(second == null ? 1U : 2U);
            items.Vec(entry.ToArray());
            if (second != null)
            {
                var empty = new Writer();
                empty.U8(1);
                Address(empty, second);
                empty.U8(0);
                items.Vec(empty.ToArray());
            }

            var body = new Writer();
            body.U16(1);
            body.Vec(items.ToArray());
            return Success(id, 137, body.ToArray());
        }

        internal static byte[] UtxosResponse(ulong id, string address)
        {
            var row = UtxoRow(address);
            var list = new Writer();
            list.U32(1);
            list.Vec(row);
            var body = new Writer();
            body.U16(1);
            body.Vec(list.ToArray());
            return Success(id, 135, body.ToArray());
        }

        internal static byte[] SubmitResponse(ulong id, uint256 transactionId)
        {
            var body = new Writer();
            body.U16(1);
            body.Raw(transactionId.ToBytes());
            return Success(id, 125, body.ToArray());
        }

        internal static byte[] FeeResponse(ulong id)
        {
            var estimate = new Writer();
            estimate.U16(1);
            estimate.F64(1);
            estimate.F64(2);
            estimate.U32(1);
            estimate.F64(3);
            estimate.F64(4);
            estimate.U32(1);
            estimate.F64(5);
            estimate.F64(6);
            var body = new Writer();
            body.U16(1);
            body.Vec(estimate.ToArray());
            return Success(id, 147, body.ToArray());
        }

        internal static byte[] GetBlockResponse(ulong id, uint256 hash)
        {
            var body = new Writer();
            body.U16(1);
            body.Vec(BlockSerializer(hash, true));
            return Success(id, 126, body.ToArray());
        }

        internal static byte[] SubscribeAck(ulong listenerId)
        {
            var body = new Writer();
            body.U16(1);
            body.U64(listenerId);
            return body.ToArray();
        }

        internal static byte[] UtxosNotification(string address)
        {
            var added = new Writer();
            added.U32(1);
            added.Vec(UtxoRow(address));
            var removed = new Writer();
            removed.U32(0);
            var inner = new Writer();
            inner.U16(1);
            inner.Vec(added.ToArray());
            inner.Vec(removed.ToArray());
            return Notification(64, NotificationEnum(4, inner.ToArray()));
        }

        internal static byte[] DaaNotification(ulong score)
        {
            var inner = new Writer();
            inner.U16(1);
            inner.U64(score);
            return Notification(66, NotificationEnum(6, inner.ToArray()));
        }

        internal static byte[] BlockNotification(uint256 hash)
        {
            var inner = new Writer();
            inner.U16(1);
            inner.Vec(BlockSerializer(hash, true));
            return Notification(60, NotificationEnum(0, inner.ToArray()));
        }

        internal static string Hex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
            {
                text.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        internal static byte[] FromHex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static byte[] NotificationEnum(ushort variant, byte[] inner)
        {
            var writer = new Writer();
            writer.U16(1);
            writer.U16(variant);
            writer.Vec(inner);
            return writer.ToArray();
        }

        private static byte[] UtxoRow(string address)
        {
            var outpoint = new Writer();
            outpoint.U8(1);
            outpoint.Raw(new uint256(9).ToBytes());
            outpoint.U32(3);
            var entry = new Writer();
            entry.U8(2);
            entry.U64(5);
            entry.U16(0);
            entry.Vec(new byte[] { 0x51 });
            entry.U64(7);
            entry.U8(1);
            entry.U8(1);
            entry.Raw(new uint256(8).ToBytes());
            var row = new Writer();
            row.U8(1);
            row.U8(1);
            Address(row, address);
            row.Vec(outpoint.ToArray());
            row.Vec(entry.ToArray());
            return row.ToArray();
        }

        private static byte[] BlockSerializer(uint256 hash, bool verbose)
        {
            var header = Header(hash, null);
            var transactions = new Writer();
            transactions.U32(0);
            var block = new Writer();
            block.U16(1);
            block.Vec(header);
            block.Vec(transactions.ToArray());
            if (!verbose)
            {
                var none = new Writer();
                none.U8(0);
                block.Vec(none.ToArray());
                return block.ToArray();
            }

            var data = new Writer();
            data.U8(1);
            data.Raw(hash.ToBytes());
            data.F64(1.5);
            data.Raw(new uint256(5).ToBytes());
            data.U32(0);
            data.U8(0);
            data.U64(4);
            data.U32(0);
            data.U32(0);
            data.U32(0);
            data.U8(1);
            var some = new Writer();
            some.U8(1);
            some.Vec(data.ToArray());
            block.Vec(some.ToArray());
            return block.ToArray();
        }

        private static byte[] Header(uint256 hash, byte[]? blueWork)
        {
            var writer = new Writer();
            writer.U16(1);
            writer.Raw(hash.ToBytes());
            writer.U16(1);
            writer.U32(1);
            writer.U32(1);
            writer.Raw(new uint256(5).ToBytes());
            writer.Raw(new uint256(1).ToBytes());
            writer.Raw(new uint256(6).ToBytes());
            writer.Raw(new uint256(2).ToBytes());
            writer.U64(8);
            writer.U32(0);
            writer.U64(0);
            writer.U64(9);
            writer.Raw(blueWork ?? new byte[24]);
            writer.U64(4);
            writer.Raw(new uint256(7).ToBytes());
            return writer.ToArray();
        }

        internal static byte[] GetBlockResponse(ulong id, uint256 hash, byte[] blueWork)
        {
            var header = Header(hash, blueWork);
            var transactions = new Writer();
            transactions.U32(0);
            var block = new Writer();
            block.U16(1);
            block.Vec(header);
            block.Vec(transactions.ToArray());
            var none = new Writer();
            none.U8(0);
            block.Vec(none.ToArray());
            var body = new Writer();
            body.U16(1);
            body.Vec(block.ToArray());
            return Success(id, 126, body.ToArray());
        }

        internal static byte[] SinkResponse(ulong id, ulong blueScore)
        {
            var body = new Writer();
            body.U16(1);
            body.U64(blueScore);
            return Success(id, 138, body.ToArray());
        }

        internal static byte[] VirtualChainResponse(ulong id, uint256 removed, uint256 added, uint256 accepting, uint256 transactionId)
        {
            var body = new Writer();
            body.U16(1);
            HashList(body, removed);
            HashList(body, added);
            body.U32(1);
            body.Raw(accepting.ToBytes());
            HashList(body, transactionId);
            return Success(id, 128, body.ToArray());
        }

        internal static byte[] VirtualChainRequest(uint256 startHash, bool includeAccepted, ulong? minConfirmation)
        {
            var body = new Writer();
            body.U16(2);
            body.Raw(startHash.ToBytes());
            body.U8(includeAccepted ? (byte)1 : (byte)0);
            if (minConfirmation.HasValue)
            {
                body.U8(1);
                body.U64(minConfirmation.Value);
            }
            else
            {
                body.U8(0);
            }

            return Wrap(body.ToArray());
        }

        private static void TransactionBody(Writer writer, Transaction transaction)
        {
            writer.U16(1);
            writer.U16(transaction.Version);
            writer.Payload(inputs =>
            {
                inputs.U32((uint)transaction.Inputs.Count);
                for (var i = 0; i < transaction.Inputs.Count; i++)
                {
                    var input = transaction.Inputs[i];
                    inputs.Payload(item =>
                    {
                        item.U8(2);
                        item.Payload(outpoint =>
                        {
                            outpoint.U8(1);
                            outpoint.Raw(input.PreviousOutpoint.TransactionId.ToBytes());
                            outpoint.U32(input.PreviousOutpoint.Index);
                        });
                        item.Vec(input.SignatureScript.ToBytes());
                        item.U64(input.Sequence);
                        item.U8(input.SigOpCount);
                        item.Payload(option => option.U8(0));
                        item.U16(input.ComputeBudget ?? 0);
                    });
                }
            });
            writer.Payload(outputs =>
            {
                outputs.U32((uint)transaction.Outputs.Count);
                for (var i = 0; i < transaction.Outputs.Count; i++)
                {
                    var output = transaction.Outputs[i];
                    outputs.Payload(item =>
                    {
                        item.U8(2);
                        item.U64(output.Value.Sompi);
                        item.U16(output.ScriptPublicKey.Version);
                        item.Vec(output.ScriptPublicKey.Script);
                        item.Payload(option => option.U8(0));
                        if (output.Covenant == null)
                        {
                            item.Payload(option => option.U8(0));
                            return;
                        }

                        var covenant = output.Covenant;
                        item.Payload(option =>
                        {
                            option.U8(1);
                            option.Payload(binding =>
                            {
                                binding.U8(1);
                                binding.U16(covenant.AuthorizingInput);
                                binding.Raw(covenant.CovenantId.ToBytes());
                            });
                        });
                    });
                }
            });
            writer.U64(transaction.LockTime);
            writer.Raw(transaction.SubnetworkId.ToBytes());
            writer.U64(transaction.Gas);
            writer.Vec(transaction.Payload);
            writer.U64(transaction.StorageMass);
            writer.Payload(option => option.U8(0));
        }

        private static void AddressList(Writer writer, IReadOnlyList<string> addresses)
        {
            writer.U32((uint)addresses.Count);
            for (var i = 0; i < addresses.Count; i++)
            {
                Address(writer, addresses[i]);
            }
        }

        private static void Address(Writer writer, string text)
        {
            var parsed = KaspaAddress.Parse(text);
            byte prefix;
            var name = parsed.Network.AddressPrefix;
            if (name == Network.Mainnet.AddressPrefix)
            {
                prefix = 0;
            }
            else if (name == Network.Testnet.AddressPrefix)
            {
                prefix = 1;
            }
            else if (name == Network.Simnet.AddressPrefix)
            {
                prefix = 2;
            }
            else if (name == Network.Devnet.AddressPrefix)
            {
                prefix = 3;
            }
            else
            {
                throw new FormatException("Unknown Kaspa address prefix.");
            }

            writer.U8(prefix);
            writer.U8((byte)parsed.Version);
            writer.Vec(parsed.Payload);
        }

        private static void WriteNetworkId(Writer writer, byte type, uint? suffix)
        {
            writer.U8(type);
            if (suffix.HasValue)
            {
                writer.U8(1);
                writer.U32(suffix.Value);
            }
            else
            {
                writer.U8(0);
            }
        }

        private static void HashList(Writer writer, params uint256[] hashes)
        {
            writer.U32((uint)hashes.Length);
            for (var i = 0; i < hashes.Length; i++)
            {
                writer.Raw(hashes[i].ToBytes());
            }
        }

        private sealed class Writer
        {
            private readonly MemoryStream _stream = new MemoryStream();

            public void U8(byte value)
            {
                _stream.WriteByte(value);
            }

            public void U16(ushort value)
            {
                var bytes = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
                Raw(bytes);
            }

            public void U32(uint value)
            {
                var bytes = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
                Raw(bytes);
            }

            public void U64(ulong value)
            {
                var bytes = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
                Raw(bytes);
            }

            public void F64(double value)
            {
                var bytes = new byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(value));
                Raw(bytes);
            }

            public void Raw(byte[] bytes)
            {
                _stream.Write(bytes, 0, bytes.Length);
            }

            public void Vec(byte[] bytes)
            {
                U32((uint)bytes.Length);
                Raw(bytes);
            }

            public void String(string value)
            {
                Vec(Encoding.UTF8.GetBytes(value));
            }

            public void Payload(Action<Writer> write)
            {
                var inner = new Writer();
                write(inner);
                Vec(inner.ToArray());
            }

            public byte[] ToArray()
            {
                return _stream.ToArray();
            }
        }
    }
}
