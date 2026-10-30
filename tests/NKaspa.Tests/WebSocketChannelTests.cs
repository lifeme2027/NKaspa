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
