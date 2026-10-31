namespace NKaspa.RPC
{
    /// <summary>
    /// Timeouts and optional reconnect for <see cref="KaspaWebSocketChannel"/>.
    /// <see cref="Timeout.InfiniteTimeSpan"/> turns a timeout off.
    /// A caller's <see cref="System.Threading.CancellationToken"/> still cancels the same call.
    /// </summary>
    public sealed class KaspaWebSocketOptions
    {
        /// <summary>Default <see cref="ConnectTimeout"/>, 15 seconds.</summary>
        public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(15);

        /// <summary>Default <see cref="RequestTimeout"/>, 30 seconds.</summary>
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long <see cref="KaspaWebSocketChannel.ConnectAsync"/> may wait for the WebSocket handshake.
        /// The default is 15 seconds. <see cref="Timeout.InfiniteTimeSpan"/> waits until the caller's token cancels.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = DefaultConnectTimeout;

        /// <summary>
        /// How long <see cref="KaspaWebSocketChannel.ExchangeAsync"/> may wait to send and receive one response.
        /// The default is 30 seconds. <see cref="Timeout.InfiniteTimeSpan"/> waits until the caller's token cancels.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = DefaultRequestTimeout;

        /// <summary>
        /// When true, a drop after a successful connect starts another handshake with exponential backoff.
        /// Requests already waiting fail. They are not sent again. The default is false.
        /// </summary>
        public bool AutoReconnect { get; set; }

        /// <summary>How many reconnect handshakes to try after a drop. The default is 5. Zero disables retries.</summary>
        public int MaxReconnectAttempts { get; set; } = 5;

        /// <summary>Delay before the first reconnect attempt. The default is 200 milliseconds.</summary>
        public TimeSpan ReconnectInitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>Upper bound on the reconnect delay. The default is 10 seconds.</summary>
        public TimeSpan ReconnectMaxDelay { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>Multiplier applied after each failed reconnect. The default is 2. Values below 1 are rejected.</summary>
        public double ReconnectBackoffFactor { get; set; } = 2;
    }
}
