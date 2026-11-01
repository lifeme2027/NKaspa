using System.Buffers.Binary;
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
    /// Typed wRPC failures. Node strings are rusty-kaspa master
    /// rpc/core/src/error.rs (NoUtxoIndex, ConsensusInTransitionalIbdState) and
    /// consensus/core/src/network.rs (NetworkIdError, NetworkTypeError).
    /// Borsh code 13 carries that Display text. Code 0 is workflow-rs "connection is closed".
    /// </summary>
    public class WrpcExceptionTests
    {
        [Fact]
        public async Task Json_maps_official_node_errors_and_leaves_generic_errors()
        {
            var channel = new ScriptedJson();
            var client = new KaspaWrpcJsonClient(channel);

            var utxo = await JsonError<KaspaUtxoIndexNotEnabledException>(channel, client, KaspaUtxoIndexNotEnabledException.NodeMessage);
            Assert.IsAssignableFrom<KaspaWrpcException>(utxo);
            Assert.Equal(KaspaUtxoIndexNotEnabledException.NodeMessage, utxo.Message);
            var utxoInner = Assert.IsType<KaspaRpcException>(utxo.InnerException);
            Assert.Equal(7, utxoInner.RpcCode);

            var ibd = await JsonError<KaspaNodeNotSyncedException>(channel, client, "consensus is currently in a transitional ibd state");
            Assert.Equal("consensus is currently in a transitional ibd state", ibd.Message);
            Assert.IsType<KaspaRpcException>(ibd.InnerException);

            var ibdCase = await JsonError<KaspaNodeNotSyncedException>(channel, client, "CONSENSUS IS CURRENTLY IN A TRANSITIONAL IBD STATE");
            Assert.Equal("CONSENSUS IS CURRENTLY IN A TRANSITIONAL IBD STATE", ibdCase.Message);

            var longer = await JsonError<KaspaRpcException>(channel, client, "consensus is currently in a transitional ibd state and more");
            Assert.Equal("consensus is currently in a transitional ibd state and more", longer.Message);
            Assert.Null(longer.InnerException);

            var network = await JsonError<KaspaWrongNetworkException>(channel, client, "Invalid network type: gamenet");
            Assert.Null(network.ReportedNetworkId);
            Assert.Null(network.ExpectedNetwork);
            Assert.IsType<KaspaRpcException>(network.InnerException);

            await JsonError<KaspaWrongNetworkException>(channel, client, "invalid network id: mainnet-99");
            await JsonError<KaspaWrongNetworkException>(channel, client, "Unexpected extra token: extra");
            var notAPrefix = await JsonError<KaspaRpcException>(channel, client, "Not Invalid network type: gamenet");
            Assert.Equal("Not Invalid network type: gamenet", notAPrefix.Message);

            var closed = await JsonError<KaspaNodeUnreachableException>(channel, client, "connection is closed");
            Assert.Equal("connection is closed", closed.Message);
            Assert.IsType<KaspaRpcException>(closed.InnerException);

            var timed = await JsonError<KaspaRpcException>(channel, client, "RPC call timed out");
            Assert.Equal("RPC call timed out", timed.Message);

            var nope = await JsonError<KaspaRpcException>(channel, client, "nope");
            Assert.Equal("nope", nope.Message);
            Assert.IsNotType<KaspaWrpcException>(nope);
        }

        [Fact]
        public async Task Ensure_ready_checks_network_sync_and_utxo_index()
        {
            var channel = new ScriptedJson();
            var client = new KaspaWrpcJsonClient(channel);
            UseServer(channel, "testnet-10", hasUtxoIndex: true, isSynced: false);

            var info = await client.GetServerInfoAsync();
            Assert.Equal("testnet-10", info.NetworkId);
            Assert.False(info.IsSynced);

            var unsynced = await Assert.ThrowsAsync<KaspaNodeNotSyncedException>(() => client.EnsureReadyAsync(Network.Testnet));
            Assert.Equal(KaspaNodeNotSyncedException.DefaultMessage, unsynced.Message);
            Assert.Null(unsynced.InnerException);

            UseServer(channel, "TESTNET-10", hasUtxoIndex: true, isSynced: true);
            await client.EnsureReadyAsync(Network.Testnet);

            UseServer(channel, "mainnet", hasUtxoIndex: false, isSynced: false);
            var wrong = await Assert.ThrowsAsync<KaspaWrongNetworkException>(() => client.EnsureReadyAsync(Network.Testnet));
            Assert.Equal("Node reported network 'mainnet'. Expected 'testnet'.", wrong.Message);
            Assert.Equal("mainnet", wrong.ReportedNetworkId);
            Assert.Equal("testnet", wrong.ExpectedNetwork);

            UseServer(channel, "gamenet", hasUtxoIndex: true, isSynced: true);
            var unknown = await Assert.ThrowsAsync<KaspaWrongNetworkException>(() => client.EnsureReadyAsync(Network.Mainnet));
            Assert.Equal("gamenet", unknown.ReportedNetworkId);
            Assert.Equal("mainnet", unknown.ExpectedNetwork);

            UseServer(channel, "mainnet", hasUtxoIndex: false, isSynced: true);
            var missing = await Assert.ThrowsAsync<KaspaUtxoIndexNotEnabledException>(() => client.EnsureReadyAsync(Network.Mainnet));
            Assert.Equal(KaspaUtxoIndexNotEnabledException.DefaultMessage, missing.Message);

            UseServer(channel, "mainnet", hasUtxoIndex: false, isSynced: false);
            var syncFirst = await Assert.ThrowsAsync<KaspaNodeNotSyncedException>(() => client.EnsureReadyAsync(Network.Mainnet));
            Assert.Equal(KaspaNodeNotSyncedException.DefaultMessage, syncFirst.Message);
            await client.EnsureReadyAsync(Network.Mainnet, requireSynced: false, requireUtxoIndex: false);

            await Assert.ThrowsAsync<ArgumentNullException>(() => KaspaWrpcErrors.Ensure(null!, Network.Mainnet, true, true, default));
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.EnsureReadyAsync(null!));
            Assert.Throws<ArgumentNullException>(() => new KaspaNodeUnreachableException(null!));
            Assert.Throws<ArgumentNullException>(() => new KaspaWrongNetworkException(null!, null, null));
        }

        [Fact]
        public async Task Transport_failures_wrap_and_usage_errors_do_not()
        {
            var channel = new ScriptedJson();
            var client = new KaspaWrpcJsonClient(channel);

            channel.Failure = new TimeoutException("WebSocket request timed out after 00:00:00.4000000.");
            var timeout = await Assert.ThrowsAsync<KaspaNodeUnreachableException>(() => client.GetServerInfoAsync());
            Assert.Equal(KaspaNodeUnreachableException.DefaultMessage, timeout.Message);
            Assert.IsType<TimeoutException>(timeout.InnerException);

            channel.Failure = new WebSocketException("socket");
            var socket = await Assert.ThrowsAsync<KaspaNodeUnreachableException>(() => client.GetServerInfoAsync());
            Assert.IsType<WebSocketException>(socket.InnerException);

            channel.Failure = new IOException("reset");
            var io = await Assert.ThrowsAsync<KaspaNodeUnreachableException>(() => client.GetServerInfoAsync());
            Assert.IsType<IOException>(io.InnerException);

            channel.Failure = new InvalidOperationException("WebSocket disconnected.");
            var dropped = await Assert.ThrowsAsync<KaspaNodeUnreachableException>(() => client.GetServerInfoAsync());
            Assert.Equal("WebSocket disconnected.", dropped.InnerException!.Message);

            channel.Failure = new InvalidOperationException("WebSocket is not connected. Call ConnectAsync first.");
            var usage = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetServerInfoAsync());
            Assert.Equal("WebSocket is not connected. Call ConnectAsync first.", usage.Message);

            channel.Failure = new KaspaRpcEncodingMismatchException(KaspaRpcEncoding.Json, "binary frame");
            var mismatch = await Assert.ThrowsAsync<KaspaRpcEncodingMismatchException>(() => client.GetServerInfoAsync());
            Assert.Equal("binary frame", mismatch.Message);

            channel.Failure = new OperationCanceledException();
            await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetServerInfoAsync());

            channel.Failure = new ObjectDisposedException("channel");
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetServerInfoAsync());
        }

        [Fact]
        public async Task Borsh_maps_code_13_and_a_closed_connection()
        {
            var channel = new ScriptedBorsh();
            var client = new KaspaWrpcBorshClient(channel);

            channel.Handler = request => ErrorFrame(KaspaBorshCodec.ReadRequestId(request), 13, KaspaUtxoIndexNotEnabledException.NodeMessage);
            var utxo = await Assert.ThrowsAsync<KaspaUtxoIndexNotEnabledException>(() => client.GetServerInfoAsync());
            Assert.Equal(KaspaUtxoIndexNotEnabledException.NodeMessage, utxo.Message);
            Assert.IsType<KaspaRpcException>(utxo.InnerException);

            channel.Handler = request => ErrorFrame(KaspaBorshCodec.ReadRequestId(request), 13, "consensus is currently in a transitional ibd state");
            await Assert.ThrowsAsync<KaspaNodeNotSyncedException>(() => client.GetServerInfoAsync());

            channel.Handler = request => ErrorFrame(KaspaBorshCodec.ReadRequestId(request), 13, "Invalid network suffix: 11");
            var network = await Assert.ThrowsAsync<KaspaWrongNetworkException>(() => client.GetServerInfoAsync());
            Assert.Null(network.ReportedNetworkId);
            Assert.Null(network.ExpectedNetwork);

            channel.Handler = request => ErrorFrame(KaspaBorshCodec.ReadRequestId(request), 0, null);
            var closed = await Assert.ThrowsAsync<KaspaNodeUnreachableException>(() => client.GetServerInfoAsync());
            Assert.Equal("connection is closed", closed.Message);

            channel.Handler = request => ErrorFrame(KaspaBorshCodec.ReadRequestId(request), 1, null);
            var timed = await Assert.ThrowsAsync<KaspaRpcException>(() => client.GetServerInfoAsync());
            Assert.Equal("RPC call timed out", timed.Message);

            channel.Handler = request => ErrorFrame(KaspaBorshCodec.ReadRequestId(request), 13, "nope");
            var nope = await Assert.ThrowsAsync<KaspaRpcException>(() => client.GetServerInfoAsync());
            Assert.Equal("nope", nope.Message);

            channel.Failure = new TimeoutException("WebSocket request timed out.");
            var transport = await Assert.ThrowsAsync<KaspaNodeUnreachableException>(() => client.GetFeeEstimateAsync());
            Assert.IsType<TimeoutException>(transport.InnerException);
        }

        [Fact]
        public async Task Connection_connect_timeout_is_node_unreachable()
        {
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            var accept = tcp.AcceptTcpClientAsync();
            var options = new KaspaWebSocketOptions
            {
                ConnectTimeout = TimeSpan.FromMilliseconds(400),
                AutoReconnect = false,
            };
            await using var connection = KaspaRpcClient.Create(
                "ws://127.0.0.1:" + port.ToString() + "/kaspa/mainnet/wrpc/json",
                KaspaRpcEncoding.Json,
                options);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var error = await Assert.ThrowsAsync<KaspaNodeUnreachableException>(() => connection.ConnectAsync(cancel.Token));
            Assert.Equal(KaspaNodeUnreachableException.DefaultMessage, error.Message);
            Assert.IsType<TimeoutException>(error.InnerException);
            tcp.Stop();
            try
            {
                using var client = await accept;
                client.Close();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }

        private static async Task<T> JsonError<T>(ScriptedJson channel, KaspaWrpcJsonClient client, string message)
            where T : Exception
        {
            channel.Failure = null;
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                return "{\"id\":" + id + ",\"error\":{\"code\":7,\"message\":" + JsonSerializer.Serialize(message) + "}}";
            };
            return await Assert.ThrowsAsync<T>(() => client.GetServerInfoAsync());
        }

        private static void UseServer(ScriptedJson channel, string networkId, bool hasUtxoIndex, bool isSynced)
        {
            channel.Failure = null;
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                return "{\"id\":" + id + ",\"params\":{\"rpcApiVersion\":1,\"rpcApiRevision\":1,\"serverVersion\":\"0.0.0-test\",\"networkId\":"
                    + JsonSerializer.Serialize(networkId)
                    + ",\"hasUtxoIndex\":" + (hasUtxoIndex ? "true" : "false")
                    + ",\"isSynced\":" + (isSynced ? "true" : "false")
                    + ",\"virtualDaaScore\":1}}";
            };
        }

        private static byte[] ErrorFrame(ulong id, byte code, string? text)
        {
            byte[] message = text == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(text);
            int extra = text == null ? 0 : 4 + message.Length;
            var buffer = new byte[1 + 8 + 1 + 1 + 1 + extra];
            var offset = 0;
            buffer[offset++] = 1;
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(offset), id);
            offset += 8;
            buffer[offset++] = KaspaBorshCodec.KindError;
            buffer[offset++] = 0;
            buffer[offset++] = code;
            if (text != null)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), (uint)message.Length);
                message.CopyTo(buffer.AsSpan(offset + 4));
            }

            return buffer;
        }

        private sealed class ScriptedJson : IKaspaJsonChannel
        {
            public Func<string, string> Handler { get; set; } = _ => throw new InvalidOperationException("No handler.");

            public Exception? Failure { get; set; }

            public Task<string> ExchangeAsync(string requestJson, CancellationToken cancellationToken = default)
            {
                if (Failure != null)
                {
                    throw Failure;
                }

                return Task.FromResult(Handler(requestJson));
            }
        }

        private sealed class ScriptedBorsh : IKaspaBorshChannel
        {
            public Func<byte[], byte[]> Handler { get; set; } = _ => throw new InvalidOperationException("No handler.");

            public Exception? Failure { get; set; }

            public event Action<byte[]>? NotificationReceived
            {
                add { }
                remove { }
            }

            public Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken = default)
            {
                if (Failure != null)
                {
                    throw Failure;
                }

                return Task.FromResult(Handler(request));
            }
        }
    }
}
