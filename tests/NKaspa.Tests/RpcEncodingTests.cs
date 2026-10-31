using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Auto encoding follows the URL path, then the port. gRPC ports are rejected.
    /// A frame of the other encoding fails pending calls with <see cref="KaspaRpcEncodingMismatchException"/>.
    /// </summary>
    public class RpcEncodingTests
    {
        [Theory]
        [InlineData("ws://127.0.0.1:17110/", KaspaRpcEncoding.Borsh)]
        [InlineData("ws://127.0.0.1:17210/", KaspaRpcEncoding.Borsh)]
        [InlineData("wss://127.0.0.1:17510/", KaspaRpcEncoding.Borsh)]
        [InlineData("ws://127.0.0.1:17610/", KaspaRpcEncoding.Borsh)]
        [InlineData("ws://127.0.0.1:18110/", KaspaRpcEncoding.Json)]
        [InlineData("ws://127.0.0.1:18210/", KaspaRpcEncoding.Json)]
        [InlineData("ws://127.0.0.1:18510/", KaspaRpcEncoding.Json)]
        [InlineData("ws://127.0.0.1:18610/", KaspaRpcEncoding.Json)]
        [InlineData("wss://eva.kaspa.green/kaspa/mainnet/wrpc/json", KaspaRpcEncoding.Json)]
        [InlineData("wss://eva.kaspa.green/kaspa/mainnet/wrpc/json/", KaspaRpcEncoding.Json)]
        [InlineData("wss://eva.kaspa.green/kaspa/mainnet/wrpc/borsh", KaspaRpcEncoding.Borsh)]
        [InlineData("ws://127.0.0.1:17110/wrpc/json", KaspaRpcEncoding.Json)]
        [InlineData("ws://127.0.0.1:18110/kaspa/testnet/wrpc/borsh", KaspaRpcEncoding.Borsh)]
        [InlineData("ws://127.0.0.1:17110/json", KaspaRpcEncoding.Json)]
        [InlineData("wss://node.example/borsh", KaspaRpcEncoding.Borsh)]
        [InlineData("wss://node.example/kaspa/mainnet/WRPC/JSON", KaspaRpcEncoding.Json)]
        [InlineData("ws://127.0.0.1:9/rpc/borsh/extra", KaspaRpcEncoding.Borsh)]
        [InlineData("ws://127.0.0.1:18110/json/borsh", KaspaRpcEncoding.Borsh)]
        public async Task Auto_selects_encoding_from_path_then_port(string url, KaspaRpcEncoding expected)
        {
            await using var connection = KaspaRpcClient.Create(url);
            Assert.Equal(expected, connection.Encoding);
            Assert.False(connection.IsConnected);
        }

        [Theory]
        [InlineData("wss://node.example/")]
        [InlineData("wss://node.example/kaspa/mainnet/wrpc")]
        [InlineData("ws://127.0.0.1:443/")]
        [InlineData("ws://127.0.0.1:80/rpc")]
        [InlineData("ws://127.0.0.1:17310/")]
        [InlineData("ws://127.0.0.1:18310/")]
        public void Auto_rejects_an_unknown_port_and_path(string url)
        {
            var error = Assert.Throws<ArgumentException>(() => KaspaRpcClient.Create(url));
            Assert.Contains("does not default", error.Message, StringComparison.Ordinal);
            Assert.Contains("17110", error.Message, StringComparison.Ordinal);
            Assert.Contains("18110", error.Message, StringComparison.Ordinal);
            Assert.Contains("443", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(16110)]
        [InlineData(16210)]
        [InlineData(16510)]
        [InlineData(16610)]
        public void Grpc_ports_are_rejected_even_when_encoding_is_explicit(int port)
        {
            var url = "ws://127.0.0.1:" + port.ToString() + "/";
            var error = Assert.Throws<ArgumentException>(() => KaspaRpcClient.Create(url));
            Assert.Contains("gRPC", error.Message, StringComparison.Ordinal);
            Assert.Contains("wRPC", error.Message, StringComparison.Ordinal);
            Assert.Contains(port.ToString(), error.Message, StringComparison.Ordinal);
            Assert.Contains("17110", error.Message, StringComparison.Ordinal);
            Assert.Contains("18110", error.Message, StringComparison.Ordinal);

            var forced = Assert.Throws<ArgumentException>(() => KaspaRpcClient.Create(url + "wrpc/json", KaspaRpcEncoding.Json));
            Assert.Contains("gRPC", forced.Message, StringComparison.Ordinal);
            Assert.True(KaspaWrpcPorts.IsGrpcPort(port));
        }

        [Fact]
        public async Task Explicit_encoding_overrides_path_and_unknown_port()
        {
            await using var borsh = KaspaRpcClient.Create("ws://127.0.0.1:17110/wrpc/json", KaspaRpcEncoding.Borsh);
            await using var json = KaspaRpcClient.Create("wss://node.example/", KaspaRpcEncoding.Json);
            Assert.Equal(KaspaRpcEncoding.Borsh, borsh.Encoding);
            Assert.Equal(KaspaRpcEncoding.Json, json.Encoding);
            Assert.Throws<ArgumentNullException>(() => KaspaWrpcPorts.TrySelectPath(null!, out _));
            Assert.False(KaspaWrpcPorts.TrySelectPath("/kaspa/mainnet/wrpc", out _));
        }

        [Fact]
        public async Task Json_client_fails_pending_requests_when_a_binary_frame_arrives()
        {
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString() + "/");
            listener.Start();
            var server = Task.Run(() => ServeAsync(listener, WebSocketMessageType.Binary, new byte[] { 1, 2, 3 }, true));
            var options = new KaspaWebSocketOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(20),
                AutoReconnect = true,
                ReconnectInitialDelay = TimeSpan.FromSeconds(10),
            };
            await using var connection = KaspaRpcClient.Create("ws://127.0.0.1:" + port.ToString() + "/wrpc/json", KaspaRpcEncoding.Auto, options);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await connection.ConnectAsync(timeout.Token);
            var watch = Stopwatch.StartNew();
            var first = connection.GetServerInfoAsync(timeout.Token);
            var second = connection.GetBlockDagInfoAsync(timeout.Token);
            var firstError = await Assert.ThrowsAsync<KaspaRpcEncodingMismatchException>(() => first);
            var secondError = await Assert.ThrowsAsync<KaspaRpcEncodingMismatchException>(() => second);
            watch.Stop();
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
            Assert.Equal(KaspaRpcEncoding.Json, firstError.ExpectedEncoding);
            Assert.Equal(KaspaRpcEncoding.Json, secondError.ExpectedEncoding);
            Assert.False(firstError.HandshakeFailed);
            Assert.Contains("binary", firstError.Message, StringComparison.OrdinalIgnoreCase);
            var again = await Assert.ThrowsAsync<KaspaRpcEncodingMismatchException>(() => connection.GetFeeEstimateAsync(timeout.Token));
            Assert.Equal(KaspaRpcEncoding.Json, again.ExpectedEncoding);
            await Task.Delay(50);
            Assert.Equal(KaspaWebSocketState.Closed, connection.State);
            await server.WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task Borsh_client_fails_pending_requests_on_a_text_frame_and_on_a_json_handshake()
        {
            await AssertBorshMismatch("{\"id\":1,\"method\":\"getServerInfo\",\"params\":{}}", false, true);
            await AssertBorshMismatch("{\"method\":\"handshake\",\"params\":{}}", true, false);
        }

        private static async Task AssertBorshMismatch(string text, bool handshake, bool waitForRequest)
        {
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString() + "/");
            listener.Start();
            var server = Task.Run(() => ServeAsync(listener, WebSocketMessageType.Text, Encoding.UTF8.GetBytes(text), waitForRequest));
            var options = new KaspaWebSocketOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(20),
                AutoReconnect = false,
            };
            await using var connection = KaspaRpcClient.Create("ws://127.0.0.1:" + port.ToString() + "/wrpc/borsh", KaspaRpcEncoding.Auto, options);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await connection.ConnectAsync(timeout.Token);
            var watch = Stopwatch.StartNew();
            KaspaRpcEncodingMismatchException error;
            if (waitForRequest)
            {
                var pending = connection.GetServerInfoAsync(timeout.Token);
                error = await Assert.ThrowsAsync<KaspaRpcEncodingMismatchException>(() => pending);
            }
            else
            {
                error = await Assert.ThrowsAsync<KaspaRpcEncodingMismatchException>(() => connection.GetServerInfoAsync(timeout.Token));
            }

            watch.Stop();
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
            Assert.Equal(KaspaRpcEncoding.Borsh, error.ExpectedEncoding);
            Assert.Equal(handshake, error.HandshakeFailed);
            Assert.Contains(handshake ? "handshake" : "text", error.Message, StringComparison.OrdinalIgnoreCase);
            var again = await Assert.ThrowsAsync<KaspaRpcEncodingMismatchException>(() => connection.GetServerInfoAsync(timeout.Token));
            Assert.True(again.HandshakeFailed == handshake);
            await server.WaitAsync(TimeSpan.FromSeconds(5));
        }

        private static async Task ServeAsync(HttpListener listener, WebSocketMessageType type, byte[] payload, bool waitForRequest)
        {
            try
            {
                var context = await listener.GetContextAsync();
                var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
                using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                if (waitForRequest)
                {
                    var buffer = new byte[8192];
                    await socket.ReceiveAsync(new ArraySegment<byte>(buffer), budget.Token);
                }

                await socket.SendAsync(new ArraySegment<byte>(payload), type, true, budget.Token);
                await Task.Delay(300, budget.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (HttpListenerException)
            {
            }
            finally
            {
                try
                {
                    listener.Stop();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private static int FreePort()
        {
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
            return port;
        }
    }
}
