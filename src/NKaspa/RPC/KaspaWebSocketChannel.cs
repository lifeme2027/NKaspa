using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace NKaspa.RPC
{
    /// <summary>
    /// WebSocket transport for <see cref="KaspaWrpcJsonClient"/>.
    /// Call <see cref="ConnectAsync"/> before <see cref="ExchangeAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Frames are workflow-rpc JSON text. A request is <c>{id, method, params}</c>.
    /// A response carries the same <c>id</c>. A notification has <c>method</c> and <c>params</c> and no <c>id</c>.
    /// <see cref="ExchangeAsync"/> waits for the frame whose <c>id</c> matches the request.
    /// Notifications are stored and raised. They are not returned as the RPC response.
    /// </para>
    /// <para>
    /// rusty-kaspa <c>KaspaRpcHandler::handshake</c> is still a TODO and does not send a greeting.
    /// A text frame with method <c>handshake</c>, or a <c>handshake</c> property, and no request id,
    /// is recorded in <see cref="HandshakeMessage"/> so a node that starts sending one can be recognized.
    /// This channel does not invent a handshake the current node does not send.
    /// </para>
    /// </remarks>
    public sealed class KaspaWebSocketChannel : IKaspaJsonChannel, IAsyncDisposable
    {
        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private readonly Uri _uri;
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private readonly object _gate = new object();
        private readonly Dictionary<long, TaskCompletionSource<string>> _pending = new Dictionary<long, TaskCompletionSource<string>>();
        private readonly Dictionary<long, string> _early = new Dictionary<long, string>();
        private readonly List<string> _notifications = new List<string>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private Task? _reader;
        private int _disposed;

        /// <summary>Creates a channel for <paramref name="uri"/>. The socket stays closed until <see cref="ConnectAsync"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="uri"/> is null.</exception>
        public KaspaWebSocketChannel(Uri uri)
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        }

        /// <summary>The handshake frame, when the server sent one. Null until that frame arrives.</summary>
        public string? HandshakeMessage { get; private set; }

        /// <summary>Raised for a notification frame. The callback runs on the reader.</summary>
        public event Action<string>? NotificationReceived;

        /// <summary>Notification frames received so far, in arrival order.</summary>
        public IReadOnlyList<string> Notifications
        {
            get
            {
                lock (_gate)
                {
                    return _notifications.ToArray();
                }
            }
        }

        /// <summary>Opens the WebSocket and starts reading frames.</summary>
        /// <exception cref="System.InvalidOperationException">WebSocket is already connected.</exception>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_reader != null)
            {
                throw new InvalidOperationException("WebSocket is already connected.");
            }

            await _socket.ConnectAsync(_uri, cancellationToken).ConfigureAwait(false);
            _reader = Task.Run(() => ReadLoop(_lifetime.Token));
        }

        /// <summary>
        /// Sends one text frame and returns the response with the same numeric <c>id</c>.
        /// Handshake and notification frames are not returned here.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="requestJson"/> is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
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

            var id = ReadRequestId(requestJson);
            var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            string? early;
            lock (_gate)
            {
                if (_early.TryGetValue(id, out early))
                {
                    _early.Remove(id);
                }
                else
                {
                    _pending[id] = pending;
                }
            }

            if (early != null)
            {
                return early;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(requestJson);
                await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _sendGate.Release();
                }

                using (cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken)))
                {
                    return await pending.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                lock (_gate)
                {
                    _pending.Remove(id);
                }
            }
        }

        /// <summary>Aborts and disposes the socket. Pending requests fail.</summary>
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return default;
            }

            _lifetime.Cancel();
            try
            {
                _socket.Abort();
            }
            catch (WebSocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _socket.Dispose();
            FailPending(new ObjectDisposedException(nameof(KaspaWebSocketChannel)));
            _sendGate.Dispose();
            _lifetime.Dispose();
            return default;
        }

        private async Task ReadLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            try
            {
                while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            FailPending(new InvalidOperationException("WebSocket closed."));
                            await CompleteCloseAsync().ConfigureAwait(false);
                            return;
                        }

                        message.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    Dispatch(Encoding.UTF8.GetString(message.ToArray()));
                }
            }
            catch (OperationCanceledException)
            {
                FailPending(new ObjectDisposedException(nameof(KaspaWebSocketChannel)));
            }
            catch (WebSocketException exception)
            {
                FailPending(exception);
            }
            catch (ObjectDisposedException exception)
            {
                FailPending(exception);
            }
        }

        private void Dispatch(string json)
        {
            if (!TryReadId(json, out var id))
            {
                if (IsHandshake(json))
                {
                    HandshakeMessage = json;
                    return;
                }

                lock (_gate)
                {
                    _notifications.Add(json);
                }

                NotificationReceived?.Invoke(json);
                return;
            }

            TaskCompletionSource<string>? pending = null;
            lock (_gate)
            {
                if (_pending.TryGetValue(id, out pending))
                {
                    _pending.Remove(id);
                }
                else
                {
                    _early[id] = json;
                }
            }

            pending?.TrySetResult(json);
        }

        private async Task CompleteCloseAsync()
        {
            try
            {
                if (_socket.State == WebSocketState.CloseReceived)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (WebSocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void FailPending(Exception exception)
        {
            List<TaskCompletionSource<string>> waiters;
            lock (_gate)
            {
                waiters = new List<TaskCompletionSource<string>>(_pending.Values);
                _pending.Clear();
            }

            for (var i = 0; i < waiters.Count; i++)
            {
                waiters[i].TrySetException(exception);
            }
        }

        private static long ReadRequestId(string json)
        {
            if (!TryReadId(json, out var id))
            {
                throw new ArgumentException("wRPC request is missing a numeric id.", nameof(json));
            }

            return id;
        }

        private static bool TryReadId(string json, out long id)
        {
            id = 0;
            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("id", out var element) || element.ValueKind == JsonValueKind.Null)
                {
                    return false;
                }

                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out id))
                {
                    return true;
                }

                return element.ValueKind == JsonValueKind.String
                    && long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool IsHandshake(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (root.TryGetProperty("method", out var method)
                    && method.ValueKind == JsonValueKind.String
                    && string.Equals(method.GetString(), "handshake", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return root.TryGetProperty("handshake", out _);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
