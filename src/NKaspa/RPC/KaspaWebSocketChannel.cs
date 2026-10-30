using System.Net.WebSockets;
using System.Text;

namespace NKaspa.RPC
{
    /// <summary>
    /// WebSocket transport for <see cref="KaspaWrpcJsonClient"/>.
    /// Call <see cref="ConnectAsync"/> before <see cref="ExchangeAsync"/>.
    /// The workflow-rpc subscription handshake is not sent. The channel exchanges complete JSON text frames.
    /// </summary>
    public sealed class KaspaWebSocketChannel : IKaspaJsonChannel, IAsyncDisposable
    {
        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private readonly Uri _uri;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        /// <summary>Creates a channel for <paramref name="uri"/>. The socket stays closed until <see cref="ConnectAsync"/>.</summary>
        public KaspaWebSocketChannel(Uri uri)
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        }

        /// <summary>Opens the WebSocket.</summary>
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return _socket.ConnectAsync(_uri, cancellationToken);
        }

        /// <summary>Sends one text frame and reads one complete text message.</summary>
        public async Task<string> ExchangeAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            if (requestJson == null)
            {
                throw new ArgumentNullException(nameof(requestJson));
            }

            if (_socket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not connected. Call ConnectAsync first.");
            }

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(requestJson);
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
                var buffer = new byte[8192];
                using var message = new MemoryStream();
                while (true)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        throw new InvalidOperationException("WebSocket closed.");
                    }

                    message.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        break;
                    }
                }

                return Encoding.UTF8.GetString(message.ToArray());
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Closes the socket when it is open.</summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (WebSocketException)
            {
            }

            _socket.Dispose();
            _gate.Dispose();
        }
    }
}
