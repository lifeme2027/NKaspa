using System.Globalization;
using System.Net.WebSockets;
using System.Text;

namespace NKaspa.RPC
{
    /// <summary>
    /// Binary WebSocket transport for <see cref="KaspaWrpcBorshClient"/>.
    /// Timeouts, reconnect, the 16 MiB cap, <see cref="State"/>, <see cref="StateChanged"/>, and <see cref="Reconnected"/>
    /// match <see cref="KaspaWebSocketChannel"/>. Frames are workflow-rpc Borsh, not JSON text.
    /// rusty-kaspa <c>KaspaRpcHandler::handshake</c> is still a TODO and is not sent. This channel does not invent one.
    /// A text frame, including a JSON handshake, throws <see cref="KaspaRpcEncodingMismatchException"/>,
    /// fails every pending request, and does not reconnect.
    /// </summary>
    public sealed class KaspaBorshWebSocketChannel : IKaspaBorshChannel, IAsyncDisposable
    {
        private readonly Uri _uri;
        private readonly KaspaWebSocketOptions _options;
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _connectGate = new SemaphoreSlim(1, 1);
        private readonly object _gate = new object();
        private readonly Dictionary<ulong, TaskCompletionSource<byte[]>> _pending = new Dictionary<ulong, TaskCompletionSource<byte[]>>();
        private readonly Dictionary<ulong, byte[]> _early = new Dictionary<ulong, byte[]>();
        private readonly List<byte[]> _notifications = new List<byte[]>();
        private readonly List<TimeSpan> _reconnectDelays = new List<TimeSpan>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private ClientWebSocket? _socket;
        private CancellationTokenSource? _readerStop;
        private CancellationTokenSource? _readerLifetime;
        private Task? _reader;
        private Task? _reconnectTask;
        private volatile bool _hasConnected;
        private int _disposed;
        private KaspaRpcEncodingMismatchException? _terminal;

        /// <summary>Creates a channel for <paramref name="uri"/> with the default timeouts and reconnect off.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="uri"/> is null.</exception>
        public KaspaBorshWebSocketChannel(Uri uri)
            : this(uri, null)
        {
        }

        /// <summary>Creates a channel. A null <paramref name="options"/> uses <see cref="KaspaWebSocketOptions"/> defaults. The socket stays closed until <see cref="ConnectAsync"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="uri"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Timeout must be positive, or Timeout.InfiniteTimeSpan. Reconnect backoff factor must be at least 1. Max reconnect attempts must be zero or positive. Reconnect delay must be zero or positive. Reconnect max delay must be at least the initial delay. Max message size must be positive.</exception>
        public KaspaBorshWebSocketChannel(Uri uri, KaspaWebSocketOptions? options)
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
            _options = options ?? new KaspaWebSocketOptions();
            Validate(_options);
        }

        /// <summary>The options passed to the constructor, or the defaults when that argument was null.</summary>
        public KaspaWebSocketOptions Options
        {
            get { return _options; }
        }

        /// <summary>True while the current socket is open.</summary>
        public bool IsConnected
        {
            get { return IsOpen; }
        }

        /// <inheritdoc />
        public event Action<byte[]>? NotificationReceived;

        /// <summary>Raised when <see cref="State"/> changes. Do not block it on this channel.</summary>
        public event Action<KaspaWebSocketState>? StateChanged;

        /// <summary>Raised after a dropped socket is open again. The first successful <see cref="ConnectAsync"/> does not raise it.</summary>
        public event Action? Reconnected;

        /// <summary>The latest connection state. The initial value is <see cref="KaspaWebSocketState.Closed"/>.</summary>
        public KaspaWebSocketState State { get; private set; } = KaspaWebSocketState.Closed;

        /// <summary>Notification frames received so far, in arrival order. Each value includes the server header.</summary>
        public IReadOnlyList<byte[]> Notifications
        {
            get
            {
                lock (_gate)
                {
                    return _notifications.ToArray();
                }
            }
        }

        /// <summary>Delays chosen for reconnect attempts. Tests read this.</summary>
        internal IReadOnlyList<TimeSpan> ReconnectDelays
        {
            get
            {
                lock (_gate)
                {
                    return _reconnectDelays.ToArray();
                }
            }
        }

        /// <summary>Replaces <see cref="Task.Delay(TimeSpan, CancellationToken)"/> during reconnect. Tests set this.</summary>
        internal Func<TimeSpan, CancellationToken, Task>? ReconnectDelayAsync { get; set; }

        /// <summary>Opens the WebSocket and starts reading binary frames.</summary>
        /// <exception cref="System.InvalidOperationException">WebSocket is already connected.</exception>
        /// <exception cref="System.ObjectDisposedException">The KaspaBorshWebSocketChannel has been disposed.</exception>
        /// <exception cref="System.TimeoutException">WebSocket connect timed out.</exception>
        /// <exception cref="System.OperationCanceledException">The caller's token was canceled.</exception>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (IsOpen)
                {
                    throw new InvalidOperationException("WebSocket is already connected.");
                }

                await JoinReaderAsync().ConfigureAwait(false);
                SetState(KaspaWebSocketState.Connecting);
                try
                {
                    await OpenCoreAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    if (_disposed == 0)
                    {
                        SetState(KaspaWebSocketState.Closed);
                    }

                    throw;
                }
            }
            finally
            {
                Release(_connectGate);
            }
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException"><paramref name="request"/> is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first. WebSocket disconnected.</exception>
        /// <exception cref="System.TimeoutException">WebSocket request timed out.</exception>
        /// <exception cref="System.OperationCanceledException">The caller's token was canceled.</exception>
        /// <exception cref="System.ObjectDisposedException">The KaspaBorshWebSocketChannel has been disposed.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcEncodingMismatchException">Borsh wRPC client received a text WebSocket frame, or a JSON handshake frame.</exception>
        public async Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ThrowIfDisposed();
            ThrowIfTerminal();
            await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
            var id = KaspaBorshCodec.ReadRequestId(request);
            var pending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[]? early;
            lock (_gate)
            {
                if (_terminal != null)
                {
                    early = null;
                }
                else if (_early.TryGetValue(id, out early))
                {
                    _early.Remove(id);
                }
                else
                {
                    _pending[id] = pending;
                }
            }

            ThrowIfTerminal();
            if (early != null)
            {
                return early;
            }

            using var budget = LinkTimeout(_options.RequestTimeout, cancellationToken);
            try
            {
                await _sendGate.WaitAsync(budget.Token).ConfigureAwait(false);
                try
                {
                    ThrowIfTerminal();
                    var socket = _socket;
                    if (socket == null || socket.State != WebSocketState.Open)
                    {
                        ThrowIfTerminal();
                        throw new InvalidOperationException("WebSocket disconnected.");
                    }

                    try
                    {
                        await socket.SendAsync(new ArraySegment<byte>(request), WebSocketMessageType.Binary, true, budget.Token).ConfigureAwait(false);
                    }
                    catch (WebSocketException exception)
                    {
                        ScheduleReconnect();
                        throw new InvalidOperationException("WebSocket disconnected.", exception);
                    }
                }
                finally
                {
                    Release(_sendGate);
                }

                using (budget.Token.Register(() => pending.TrySetCanceled(budget.Token)))
                {
                    return await pending.Task.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !IsInfinite(_options.RequestTimeout))
            {
                throw new TimeoutException("WebSocket request timed out after " + Format(_options.RequestTimeout) + ".");
            }
            finally
            {
                lock (_gate)
                {
                    _pending.Remove(id);
                }
            }
        }

        /// <summary>Aborts and disposes the socket. Pending requests fail. Reconnect stops.</summary>
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return default;
            }

            SetState(KaspaWebSocketState.Closed);
            try
            {
                _lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            AbortSocket(_socket);
            FailPending(new ObjectDisposedException(nameof(KaspaBorshWebSocketChannel)));
            try
            {
                _socket?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            DisposeQuiet(_sendGate);
            DisposeQuiet(_connectGate);
            DisposeQuiet(_readerStop);
            DisposeQuiet(_readerLifetime);
            try
            {
                _lifetime.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            return default;
        }

        private async Task EnsureOpenAsync(CancellationToken cancellationToken)
        {
            ThrowIfTerminal();
            if (IsOpen)
            {
                return;
            }

            ThrowIfTerminal();

            if (!_hasConnected || !_options.AutoReconnect)
            {
                throw new InvalidOperationException("WebSocket is not connected. Call ConnectAsync first.");
            }

            Task job;
            lock (_gate)
            {
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                {
                    _reconnectTask = Task.Run(ReconnectAsync);
                }

                job = _reconnectTask;
            }

            await AwaitTask(job, cancellationToken).ConfigureAwait(false);
            if (!IsOpen)
            {
                throw new InvalidOperationException("WebSocket disconnected.");
            }
        }

        private async Task ReconnectAsync()
        {
            try
            {
                await _connectGate.WaitAsync(_lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var opened = false;
            try
            {
                if (_disposed != 0 || IsOpen)
                {
                    return;
                }

                await JoinReaderAsync().ConfigureAwait(false);
                SetState(KaspaWebSocketState.Reconnecting);
                var delay = _options.ReconnectInitialDelay;
                for (var attempt = 0; attempt < _options.MaxReconnectAttempts; attempt++)
                {
                    if (_disposed != 0)
                    {
                        return;
                    }

                    try
                    {
                        await WaitDelayAsync(delay).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (_disposed != 0)
                    {
                        return;
                    }

                    try
                    {
                        await OpenCoreAsync(_lifetime.Token).ConfigureAwait(false);
                        opened = true;
                        return;
                    }
                    catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || _disposed != 0)
                    {
                        return;
                    }
                    catch (Exception)
                    {
                        delay = NextDelay(delay);
                    }
                }
            }
            finally
            {
                Release(_connectGate);
                if (!opened && _disposed == 0 && !IsOpen)
                {
                    SetState(KaspaWebSocketState.Closed);
                }
            }
        }

        private async Task OpenCoreAsync(CancellationToken cancellationToken)
        {
            var reconnecting = _hasConnected;
            var socket = new ClientWebSocket();
            try
            {
                using (var budget = LinkTimeout(_options.ConnectTimeout, cancellationToken))
                {
                    try
                    {
                        await socket.ConnectAsync(_uri, budget.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !IsInfinite(_options.ConnectTimeout))
                    {
                        throw new TimeoutException("WebSocket connect timed out after " + Format(_options.ConnectTimeout) + ".");
                    }
                }

                var previous = _socket;
                if (previous != null && !ReferenceEquals(previous, socket))
                {
                    AbortSocket(previous);
                }

                var readerStop = new CancellationTokenSource();
                var readerLifetime = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, readerStop.Token);
                lock (_gate)
                {
                    _early.Clear();
                    _socket = socket;
                    _readerStop = readerStop;
                    _readerLifetime = readerLifetime;
                    _hasConnected = true;
                }

                SetState(KaspaWebSocketState.Open);
                if (reconnecting)
                {
                    RaiseReconnected();
                }

                _reader = Task.Run(() => ReadLoop(socket, readerLifetime.Token));
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private async Task ReadLoop(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            Exception? fatal = null;
            try
            {
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await CompleteCloseAsync(socket).ConfigureAwait(false);
                            return;
                        }

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            if (result.Count > 0)
                            {
                                message.Write(buffer, 0, result.Count);
                            }

                            while (!result.EndOfMessage)
                            {
                                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                                if ((long)message.Length + result.Count > _options.MaxMessageBytes)
                                {
                                    fatal = new InvalidOperationException(
                                        "WebSocket message exceeds the maximum of "
                                        + _options.MaxMessageBytes.ToString(CultureInfo.InvariantCulture)
                                        + " bytes.");
                                    return;
                                }

                                if (result.Count > 0)
                                {
                                    message.Write(buffer, 0, result.Count);
                                }
                            }

                            var text = Encoding.UTF8.GetString(message.ToArray());
                            var mismatch = KaspaRpcEncodingMismatchException.BorshReceivedText(KaspaWebSocketChannel.IsHandshakeFrame(text));
                            fatal = mismatch;
                            lock (_gate)
                            {
                                _terminal ??= mismatch;
                            }

                            return;
                        }

                        if ((long)message.Length + result.Count > _options.MaxMessageBytes)
                        {
                            fatal = new InvalidOperationException(
                                "WebSocket message exceeds the maximum of "
                                + _options.MaxMessageBytes.ToString(CultureInfo.InvariantCulture)
                                + " bytes.");
                            return;
                        }

                        if (result.Count > 0)
                        {
                            message.Write(buffer, 0, result.Count);
                        }
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Binary || message.Length > 0)
                    {
                        Dispatch(message.ToArray());
                    }
                }
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
            finally
            {
                var current = false;
                lock (_gate)
                {
                    if (ReferenceEquals(_socket, socket))
                    {
                        _socket = null;
                        current = true;
                    }
                }

                AbortSocket(socket);
                if (current)
                {
                    Exception failure;
                    if (_disposed != 0 || _lifetime.IsCancellationRequested)
                    {
                        failure = new ObjectDisposedException(nameof(KaspaBorshWebSocketChannel));
                    }
                    else if (fatal != null)
                    {
                        failure = fatal;
                    }
                    else
                    {
                        failure = new InvalidOperationException("WebSocket disconnected.");
                    }

                    FailPending(failure);
                    if (_disposed != 0 || _lifetime.IsCancellationRequested || fatal != null || !_options.AutoReconnect || !_hasConnected)
                    {
                        SetState(KaspaWebSocketState.Closed);
                    }
                    else
                    {
                        SetState(KaspaWebSocketState.Reconnecting);
                        ScheduleReconnect();
                    }
                }
            }
        }

        private void ScheduleReconnect()
        {
            if (_disposed != 0 || !_options.AutoReconnect || !_hasConnected)
            {
                return;
            }

            lock (_gate)
            {
                if (_reconnectTask != null && !_reconnectTask.IsCompleted)
                {
                    return;
                }

                _reconnectTask = Task.Run(ReconnectAsync);
            }
        }

        private async Task WaitDelayAsync(TimeSpan delay)
        {
            lock (_gate)
            {
                _reconnectDelays.Add(delay);
            }

            var hook = ReconnectDelayAsync;
            if (hook != null)
            {
                await hook(delay, _lifetime.Token).ConfigureAwait(false);
                return;
            }

            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(delay, _lifetime.Token).ConfigureAwait(false);
        }

        private TimeSpan NextDelay(TimeSpan current)
        {
            var grown = current.TotalMilliseconds * _options.ReconnectBackoffFactor;
            if (double.IsInfinity(grown) || grown >= _options.ReconnectMaxDelay.TotalMilliseconds)
            {
                return _options.ReconnectMaxDelay;
            }

            return TimeSpan.FromMilliseconds(grown);
        }

        private void Dispatch(byte[] frame)
        {
            if (!KaspaBorshCodec.TryReadResponseId(frame, out var id))
            {
                lock (_gate)
                {
                    _notifications.Add(frame);
                }

                try
                {
                    NotificationReceived?.Invoke(frame);
                }
                catch (Exception)
                {
                }

                return;
            }

            TaskCompletionSource<byte[]>? pending = null;
            lock (_gate)
            {
                if (_pending.TryGetValue(id, out pending))
                {
                    _pending.Remove(id);
                }
                else
                {
                    _early[id] = frame;
                }
            }

            pending?.TrySetResult(frame);
        }

        private async Task CompleteCloseAsync(ClientWebSocket socket)
        {
            try
            {
                if (socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (WebSocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task JoinReaderAsync()
        {
            Task? reader;
            lock (_gate)
            {
                reader = _reader;
            }

            if (reader == null)
            {
                return;
            }

            try
            {
                await reader.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private void FailPending(Exception exception)
        {
            if (exception is KaspaRpcEncodingMismatchException mismatch)
            {
                lock (_gate)
                {
                    _terminal ??= mismatch;
                }
            }

            List<TaskCompletionSource<byte[]>> waiters;
            lock (_gate)
            {
                waiters = new List<TaskCompletionSource<byte[]>>(_pending.Values);
                _pending.Clear();
            }

            for (var i = 0; i < waiters.Count; i++)
            {
                waiters[i].TrySetException(exception);
            }
        }

        private bool IsOpen
        {
            get
            {
                var socket = _socket;
                return socket != null && socket.State == WebSocketState.Open;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(KaspaBorshWebSocketChannel));
            }
        }

        private void ThrowIfTerminal()
        {
            KaspaRpcEncodingMismatchException? terminal;
            lock (_gate)
            {
                terminal = _terminal;
            }

            if (terminal != null)
            {
                throw new KaspaRpcEncodingMismatchException(terminal.ExpectedEncoding, terminal.Message, terminal.HandshakeFailed);
            }
        }

        private static async Task AwaitTask(Task task, CancellationToken cancellationToken)
        {
            var finished = await Task.WhenAny(task, Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)).ConfigureAwait(false);
            if (!ReferenceEquals(finished, task))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            await task.ConfigureAwait(false);
        }

        private static CancellationTokenSource LinkTimeout(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (!IsInfinite(timeout))
            {
                var milliseconds = timeout.TotalMilliseconds;
                if (milliseconds < 0)
                {
                    milliseconds = 0;
                }
                else if (milliseconds > int.MaxValue)
                {
                    milliseconds = int.MaxValue;
                }

                linked.CancelAfter(TimeSpan.FromMilliseconds(milliseconds));
            }

            return linked;
        }

        private static bool IsInfinite(TimeSpan timeout)
        {
            return timeout == Timeout.InfiniteTimeSpan;
        }

        private static string Format(TimeSpan timeout)
        {
            return timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";
        }

        private static void Validate(KaspaWebSocketOptions options)
        {
            RequireTimeout(options.ConnectTimeout, nameof(options.ConnectTimeout));
            RequireTimeout(options.RequestTimeout, nameof(options.RequestTimeout));
            if (options.MaxReconnectAttempts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxReconnectAttempts), "Max reconnect attempts must be zero or positive.");
            }

            if (options.ReconnectInitialDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options.ReconnectInitialDelay), "Reconnect delay must be zero or positive.");
            }

            if (options.ReconnectMaxDelay < options.ReconnectInitialDelay)
            {
                throw new ArgumentOutOfRangeException(nameof(options.ReconnectMaxDelay), "Reconnect max delay must be at least the initial delay.");
            }

            if (double.IsNaN(options.ReconnectBackoffFactor) || double.IsInfinity(options.ReconnectBackoffFactor) || options.ReconnectBackoffFactor < 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(options.ReconnectBackoffFactor), "Reconnect backoff factor must be at least 1.");
            }

            if (options.MaxMessageBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxMessageBytes), "Max message size must be positive.");
            }
        }

        private void SetState(KaspaWebSocketState state)
        {
            Action<KaspaWebSocketState>? handler;
            lock (_gate)
            {
                if (State == state)
                {
                    return;
                }

                State = state;
                handler = StateChanged;
            }

            if (handler == null)
            {
                return;
            }

            try
            {
                handler(state);
            }
            catch (Exception)
            {
            }
        }

        private void RaiseReconnected()
        {
            var handler = Reconnected;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler();
            }
            catch (Exception)
            {
            }
        }

        private static void RequireTimeout(TimeSpan timeout, string name)
        {
            if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(name, "Timeout must be positive, or Timeout.InfiniteTimeSpan.");
            }
        }

        private static void AbortSocket(ClientWebSocket? socket)
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                socket.Abort();
            }
            catch (WebSocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static void Release(SemaphoreSlim gate)
        {
            try
            {
                gate.Release();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SemaphoreFullException)
            {
            }
        }

        private static void DisposeQuiet(IDisposable? disposable)
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
