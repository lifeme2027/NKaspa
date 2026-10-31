using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Loopback WebSocket. The fake server pushes a handshake and a notification before the response,
    /// which is what a workflow-rpc peer can do. rusty-kaspa's handler handshake is still a stub,
    /// so the handshake frame here is the shape this client recognizes, not a captured node greeting.
    /// </summary>
    public class WebSocketChannelTests
    {
        [Fact]
        public async Task Response_is_matched_by_id_after_handshake_and_notification()
        {
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString() + "/");
            listener.Start();
            var server = Task.Run(() => ServeAsync(listener));

            await using (var channel = new KaspaWebSocketChannel(new Uri("ws://127.0.0.1:" + port.ToString() + "/")))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await channel.ConnectAsync(timeout.Token);
                var dag = await new KaspaWrpcJsonClient(channel).GetBlockDagInfoAsync(timeout.Token);

                Assert.Equal("testnet-10", dag.Network);
                Assert.Equal(4UL, dag.BlockCount);
                Assert.NotNull(channel.HandshakeMessage);
                Assert.Contains("handshake", channel.HandshakeMessage, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(channel.Notifications, note => note.Contains("blockAdded", StringComparison.Ordinal));
            }

            await server.WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task Connect_times_out_when_the_handshake_never_finishes()
        {
            var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            var accept = tcp.AcceptTcpClientAsync();
            var options = new KaspaWebSocketOptions
            {
                ConnectTimeout = TimeSpan.FromMilliseconds(400),
                AutoReconnect = false,
            };
            await using var channel = new KaspaWebSocketChannel(new Uri("ws://127.0.0.1:" + port.ToString() + "/"), options);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var error = await Assert.ThrowsAsync<TimeoutException>(() => channel.ConnectAsync(cancel.Token));
            Assert.Contains("timed out", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(KaspaWebSocketState.Closed, channel.State);
            tcp.Stop();
            try
            {
                using var client = await accept;
                client.Close();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (System.Net.Sockets.SocketException)
            {
            }
        }

        [Fact]
        public async Task Request_times_out_when_the_server_does_not_answer()
        {
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString() + "/");
            listener.Start();
            using var serverStop = new CancellationTokenSource();
            var server = Task.Run(() => ServeSilentAsync(listener, serverStop.Token));
            var options = new KaspaWebSocketOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(2),
                RequestTimeout = TimeSpan.FromMilliseconds(400),
                AutoReconnect = false,
            };

            try
            {
                await using var channel = new KaspaWebSocketChannel(new Uri("ws://127.0.0.1:" + port.ToString() + "/"), options);
                await channel.ConnectAsync();
                var error = await Assert.ThrowsAsync<TimeoutException>(() => channel.ExchangeAsync(Request(1)));
                Assert.Contains("timed out", error.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                serverStop.Cancel();
                listener.Stop();
                await server.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task Caller_cancellation_is_not_reported_as_a_timeout()
        {
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString() + "/");
            listener.Start();
            using var serverStop = new CancellationTokenSource();
            var server = Task.Run(() => ServeSilentAsync(listener, serverStop.Token));
            var options = new KaspaWebSocketOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(2),
                RequestTimeout = TimeSpan.FromSeconds(10),
                AutoReconnect = false,
            };

            try
            {
                await using var channel = new KaspaWebSocketChannel(new Uri("ws://127.0.0.1:" + port.ToString() + "/"), options);
                await channel.ConnectAsync();
                using var cancel = new CancellationTokenSource();
                var pending = channel.ExchangeAsync(Request(2), cancel.Token);
                await Task.Delay(150);
                cancel.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            finally
            {
                serverStop.Cancel();
                listener.Stop();
                await server.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task Pending_request_fails_when_the_peer_disconnects()
        {
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString() + "/");
            listener.Start();
            var server = Task.Run(() => ServeAbortAfterRequestAsync(listener));
            var options = new KaspaWebSocketOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(2),
                RequestTimeout = TimeSpan.FromSeconds(5),
                AutoReconnect = false,
            };

            try
            {
                await using var channel = new KaspaWebSocketChannel(new Uri("ws://127.0.0.1:" + port.ToString() + "/"), options);
                await channel.ConnectAsync();
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => channel.ExchangeAsync(Request(3)));
                Assert.Contains("disconnected", error.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                listener.Stop();
                await server.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task Next_request_reconnects_with_backoff_after_disconnect()
        {
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString() + "/");
            listener.Start();
            var server = Task.Run(() => ServeReconnectAsync(listener));
            var options = new KaspaWebSocketOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(2),
                RequestTimeout = TimeSpan.FromSeconds(3),
                AutoReconnect = true,
                MaxReconnectAttempts = 3,
                ReconnectInitialDelay = TimeSpan.FromMilliseconds(100),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(400),
                ReconnectBackoffFactor = 2,
            };

            try
            {
                await using var channel = new KaspaWebSocketChannel(new Uri("ws://127.0.0.1:" + port.ToString() + "/"), options);
                var reconnects = 0;
                channel.Reconnected += () => Interlocked.Increment(ref reconnects);
                channel.ReconnectDelayAsync = (_, _) => Task.CompletedTask;
                await channel.ConnectAsync();
                Assert.Equal(KaspaWebSocketState.Open, channel.State);
                Assert.Equal(0, Volatile.Read(ref reconnects));
                var first = await channel.ExchangeAsync(Request(1));
                Assert.Contains("\"id\":1", first, StringComparison.Ordinal);
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (channel.IsConnected && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(15);
                }

                var second = await channel.ExchangeAsync(Request(7));
                Assert.Contains("\"id\":7", second, StringComparison.Ordinal);
                var delays = channel.ReconnectDelays;
                Assert.Equal(3, delays.Count);
                Assert.Equal(TimeSpan.FromMilliseconds(100), delays[0]);
                Assert.Equal(TimeSpan.FromMilliseconds(200), delays[1]);
                Assert.Equal(TimeSpan.FromMilliseconds(400), delays[2]);
                Assert.Equal(1, Volatile.Read(ref reconnects));
                Assert.Equal(KaspaWebSocketState.Open, channel.State);
            }
            finally
            {
                listener.Stop();
                await server.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public void Options_reject_a_negative_timeout_and_a_shrinking_backoff()
        {
            var uri = new Uri("ws://127.0.0.1:1/");
            // TimeSpan.FromMilliseconds(-1) is Timeout.InfiniteTimeSpan and means "no timeout".
            Assert.Throws<ArgumentOutOfRangeException>(() => new KaspaWebSocketChannel(uri, new KaspaWebSocketOptions { ConnectTimeout = TimeSpan.FromMilliseconds(-2) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KaspaWebSocketChannel(uri, new KaspaWebSocketOptions { ReconnectBackoffFactor = 0.5 }));
            var zero = Assert.Throws<ArgumentOutOfRangeException>(() => new KaspaWebSocketChannel(uri, new KaspaWebSocketOptions { MaxMessageBytes = 0 }));
            Assert.Contains("Max message size must be positive.", zero.Message, StringComparison.Ordinal);
            Assert.Throws<ArgumentOutOfRangeException>(() => new KaspaWebSocketChannel(uri, new KaspaWebSocketOptions { MaxMessageBytes = -1 }));
            var channel = new KaspaWebSocketChannel(uri, new KaspaWebSocketOptions { ConnectTimeout = Timeout.InfiniteTimeSpan, RequestTimeout = Timeout.InfiniteTimeSpan });
            Assert.Equal(Timeout.InfiniteTimeSpan, channel.Options.ConnectTimeout);
            Assert.False(channel.Options.AutoReconnect);
            Assert.Equal(KaspaWebSocketOptions.DefaultMaxMessageBytes, channel.Options.MaxMessageBytes);
            Assert.Equal(16 * 1024 * 1024, KaspaWebSocketOptions.DefaultMaxMessageBytes);
            Assert.Equal(KaspaWebSocketState.Closed, channel.State);
        }

        [Fact]
        public async Task Message_at_the_byte_limit_is_accepted()
        {
            var payload = Encoding.UTF8.GetBytes(ResponseFor(Request(1)));
            await AssertPayloadAsync(payload, payload.Length, fragment: false, expectSuccess: true);
            await AssertPayloadAsync(payload, payload.Length, fragment: true, expectSuccess: true);
        }

        [Fact]
        public async Task Message_one_byte_over_the_limit_fails_the_pending_request()
        {
            var marker = "PAYLOADMARKER";
            var body = ResponseFor(Request(4)) + marker;
            var payload = Encoding.UTF8.GetBytes(body);
            await AssertPayloadAsync(payload, payload.Length - 1, fragment: false, expectSuccess: false);
            await AssertPayloadAsync(payload, payload.Length - 1, fragment: true, expectSuccess: false);

            var wide = new byte[8193];
            Encoding.UTF8.GetBytes("{\"id\":9,").CopyTo(wide, 0);
            for (var i = 8; i < wide.Length - 1; i++)
            {
                wide[i] = (byte)'x';
            }

            wide[wide.Length - 1] = (byte)'}';
            await AssertPayloadAsync(wide, 8192, fragment: false, expectSuccess: false);
        }

        private static async Task AssertPayloadAsync(byte[] payload, int maxMessageBytes, bool fragment, bool expectSuccess)
        {
            var port = FreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + port.ToString() + "/");
            listener.Start();
            var server = Task.Run(() => ServePayloadAsync(listener, payload, fragment));
            var options = new KaspaWebSocketOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(2),
                RequestTimeout = TimeSpan.FromSeconds(3),
                AutoReconnect = false,
                MaxMessageBytes = maxMessageBytes,
            };

            try
            {
                await using var channel = new KaspaWebSocketChannel(new Uri("ws://127.0.0.1:" + port.ToString() + "/"), options);
                var reconnects = 0;
                channel.Reconnected += () => Interlocked.Increment(ref reconnects);
                await channel.ConnectAsync();
                Assert.Equal(KaspaWebSocketState.Open, channel.State);
                Assert.Equal(0, Volatile.Read(ref reconnects));
                if (expectSuccess)
                {
                    var text = await channel.ExchangeAsync(Request(1));
                    Assert.Equal(Encoding.UTF8.GetString(payload), text);
                    Assert.Equal(KaspaWebSocketState.Open, channel.State);
                }
                else
                {
                    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => channel.ExchangeAsync(Request(1)));
                    Assert.Contains(maxMessageBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
                    Assert.Contains("maximum", error.Message, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("PAYLOADMARKER", error.Message, StringComparison.Ordinal);
                    Assert.DoesNotContain("xxxxxxxx", error.Message, StringComparison.Ordinal);
                    Assert.Equal(KaspaWebSocketState.Closed, channel.State);
                }
            }
            finally
            {
                listener.Stop();
                await server.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        private static async Task ServePayloadAsync(HttpListener listener, byte[] payload, bool fragment)
        {
            var context = await listener.GetContextAsync();
            var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
            try
            {
                await ReceiveAsync(socket);
                if (!fragment || payload.Length < 2)
                {
                    await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    var mid = payload.Length / 2;
                    await socket.SendAsync(new ArraySegment<byte>(payload, 0, mid), WebSocketMessageType.Text, false, CancellationToken.None);
                    await socket.SendAsync(new ArraySegment<byte>(payload, mid, payload.Length - mid), WebSocketMessageType.Text, true, CancellationToken.None);
                }

                await Task.Delay(200);
            }
            finally
            {
                socket.Abort();
                socket.Dispose();
            }
        }

        private static async Task ServeAsync(HttpListener listener)
        {
            var context = await listener.GetContextAsync();
            var socket = await context.AcceptWebSocketAsync(subProtocol: null);
            var webSocket = socket.WebSocket;
            try
            {
                await SendAsync(webSocket, "{\"method\":\"handshake\",\"params\":{}}");
                await SendAsync(webSocket, "{\"method\":\"blockAdded\",\"params\":{\"block\":1}}");
                var request = await ReceiveAsync(webSocket);
                using var document = JsonDocument.Parse(request);
                var id = document.RootElement.GetProperty("id").GetInt64();
                await SendAsync(
                    webSocket,
                    "{\"id\":" + id.ToString() + ",\"method\":\"getBlockDagInfo\",\"params\":{\"network\":{\"networkType\":\"testnet\",\"suffix\":10},\"blockCount\":4,\"headerCount\":5,\"tipHashes\":[\"" + uint256.Zero + "\"],\"difficulty\":1.5,\"pastMedianTime\":8,\"virtualParentHashes\":[\"" + uint256.Zero + "\"],\"pruningPointHash\":\"" + uint256.Zero + "\",\"virtualDaaScore\":9,\"sink\":\"" + uint256.Zero + "\"}}");
                await Task.Delay(200);
            }
            finally
            {
                webSocket.Abort();
                webSocket.Dispose();
            }
        }

        private static async Task ServeSilentAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            var context = await listener.GetContextAsync();
            var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
            try
            {
                await ReceiveAsync(socket);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            }
            finally
            {
                socket.Abort();
                socket.Dispose();
            }
        }

        private static async Task ServeAbortAfterRequestAsync(HttpListener listener)
        {
            var context = await listener.GetContextAsync();
            var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
            try
            {
                await ReceiveAsync(socket);
            }
            finally
            {
                socket.Abort();
                socket.Dispose();
            }
        }

        private static async Task ServeReconnectAsync(HttpListener listener)
        {
            var seen = 0;
            while (seen < 4)
            {
                var context = await listener.GetContextAsync();
                seen++;
                if (seen == 1 || seen == 4)
                {
                    var socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
                    try
                    {
                        var request = await ReceiveAsync(socket);
                        await SendAsync(socket, ResponseFor(request));
                        if (seen == 1)
                        {
                            await Task.Delay(30);
                        }
                        else
                        {
                            await Task.Delay(200);
                        }
                    }
                    finally
                    {
                        socket.Abort();
                        socket.Dispose();
                    }
                }
                else
                {
                    context.Response.StatusCode = 503;
                    context.Response.Close();
                }
            }
        }

        private static string Request(long id)
        {
            return "{\"id\":" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"method\":\"ping\",\"params\":{}}";
        }

        private static string ResponseFor(string request)
        {
            using var document = JsonDocument.Parse(request);
            var id = document.RootElement.GetProperty("id").GetInt64();
            return "{\"id\":" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"params\":{\"ok\":true}}";
        }

        private static async Task SendAsync(WebSocket socket, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task<string> ReceiveAsync(WebSocket socket)
        {
            var buffer = new byte[8192];
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(message.ToArray());
        }

        private static int FreePort()
        {
            var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
            return port;
        }
    }
}
