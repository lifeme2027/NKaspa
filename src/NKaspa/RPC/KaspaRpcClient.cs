namespace NKaspa.RPC
{
    /// <summary>
    /// The WebSocket frame type does not match the wRPC client.
    /// A JSON client received a binary frame, or a Borsh client received a text frame.
    /// A JSON handshake text frame on a Borsh client sets <see cref="HandshakeFailed"/>.
    /// Pending requests fail with this exception. The client does not wait for the request timeout and does not reconnect.
    /// </summary>
    public sealed class KaspaRpcEncodingMismatchException : Exception
    {
        /// <summary>Creates an exception for <paramref name="expectedEncoding"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaRpcEncodingMismatchException(KaspaRpcEncoding expectedEncoding, string message)
            : this(expectedEncoding, message, false)
        {
        }

        /// <summary>Creates an exception for <paramref name="expectedEncoding"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaRpcEncodingMismatchException(KaspaRpcEncoding expectedEncoding, string message, bool handshakeFailed)
            : base(message ?? throw new ArgumentNullException(nameof(message)))
        {
            ExpectedEncoding = expectedEncoding;
            HandshakeFailed = handshakeFailed;
        }

        /// <summary>The encoding this client was built for.</summary>
        public KaspaRpcEncoding ExpectedEncoding { get; }

        /// <summary>True when a Borsh client received a JSON handshake text frame.</summary>
        public bool HandshakeFailed { get; }

        internal static KaspaRpcEncodingMismatchException JsonReceivedBinary()
        {
            return new KaspaRpcEncodingMismatchException(
                KaspaRpcEncoding.Json,
                "JSON wRPC client received a binary WebSocket frame. The peer is speaking Borsh, not JSON. Pending requests fail with KaspaRpcEncodingMismatchException. NKaspa does not wait for the request timeout.");
        }

        internal static KaspaRpcEncodingMismatchException BorshReceivedText(bool handshakeFailed)
        {
            var message = handshakeFailed
                ? "Borsh wRPC client received a JSON handshake text frame. The handshake failed because the peer is speaking JSON, not Borsh. Pending requests fail with KaspaRpcEncodingMismatchException."
                : "Borsh wRPC client received a text WebSocket frame. The peer is speaking JSON, not Borsh. Pending requests fail with KaspaRpcEncodingMismatchException. NKaspa does not wait for the request timeout.";
            return new KaspaRpcEncodingMismatchException(KaspaRpcEncoding.Borsh, message, handshakeFailed);
        }
    }

    /// <summary>Which wRPC encoding <see cref="KaspaRpcClient.Create"/> should open.</summary>
    public enum KaspaRpcEncoding
    {
        /// <summary>
        /// Pick Borsh or JSON from the URL path, then from the port. The path wins over the port.
        /// A path ending in <c>/wrpc/borsh</c> or containing <c>/borsh</c> is Borsh.
        /// A path ending in <c>/wrpc/json</c> or containing <c>/json</c> is JSON.
        /// Borsh is tested first. The comparison is ordinal and case-insensitive, and trailing slashes are ignored for the ending check.
        /// When the path has no hint and the port is not a default wRPC port, <see cref="KaspaRpcClient.Create"/> throws.
        /// NKaspa does not default to either encoding. <c>wss</c> on port 443 with no path hint is that case.
        /// gRPC ports 16110, 16210, 16510, and 16610 are rejected.
        /// </summary>
        Auto = 0,

        /// <summary>workflow-rpc Borsh on a binary WebSocket.</summary>
        Borsh = 1,

        /// <summary>workflow-rpc JSON text on a WebSocket.</summary>
        Json = 2,
    }

    /// <summary>
    /// Default wRPC ports from rusty-kaspa <c>4edcbbccb449f84832afb1ca157904b35f37d006</c>
    /// <c>consensus/core/src/network.rs</c> <c>NetworkType::default_borsh_rpc_port</c> and <c>default_json_rpc_port</c>.
    /// Simnet Borsh is 17510 and simnet JSON is 18510. They are not 17310 or 18310.
    /// A testnet suffix does not change these ports.
    /// </summary>
    public static class KaspaWrpcPorts
    {
        /// <summary>Mainnet Borsh. rusty-kaspa default 17110.</summary>
        public const int MainnetBorsh = 17110;

        /// <summary>Testnet Borsh. rusty-kaspa default 17210.</summary>
        public const int TestnetBorsh = 17210;

        /// <summary>Simnet Borsh. rusty-kaspa default 17510.</summary>
        public const int SimnetBorsh = 17510;

        /// <summary>Devnet Borsh. rusty-kaspa default 17610.</summary>
        public const int DevnetBorsh = 17610;

        /// <summary>Mainnet JSON. rusty-kaspa default 18110.</summary>
        public const int MainnetJson = 18110;

        /// <summary>Testnet JSON. rusty-kaspa default 18210.</summary>
        public const int TestnetJson = 18210;

        /// <summary>Simnet JSON. rusty-kaspa default 18510.</summary>
        public const int SimnetJson = 18510;

        /// <summary>Devnet JSON. rusty-kaspa default 18610.</summary>
        public const int DevnetJson = 18610;

        /// <summary>Mainnet gRPC. NKaspa does not speak gRPC. rusty-kaspa default 16110.</summary>
        public const int MainnetGrpc = 16110;

        /// <summary>Testnet gRPC. NKaspa does not speak gRPC. rusty-kaspa default 16210.</summary>
        public const int TestnetGrpc = 16210;

        /// <summary>Simnet gRPC. NKaspa does not speak gRPC. rusty-kaspa default 16510.</summary>
        public const int SimnetGrpc = 16510;

        /// <summary>Devnet gRPC. NKaspa does not speak gRPC. rusty-kaspa default 16610.</summary>
        public const int DevnetGrpc = 16610;

        /// <summary>True when <paramref name="port"/> is one of the eight default wRPC ports.</summary>
        public static bool TrySelect(int port, out KaspaRpcEncoding encoding)
        {
            if (port == MainnetBorsh || port == TestnetBorsh || port == SimnetBorsh || port == DevnetBorsh)
            {
                encoding = KaspaRpcEncoding.Borsh;
                return true;
            }

            if (port == MainnetJson || port == TestnetJson || port == SimnetJson || port == DevnetJson)
            {
                encoding = KaspaRpcEncoding.Json;
                return true;
            }

            encoding = KaspaRpcEncoding.Auto;
            return false;
        }

        /// <summary>True when <paramref name="port"/> is a default Kaspa gRPC port. NKaspa does not open those ports.</summary>
        public static bool IsGrpcPort(int port)
        {
            return port == MainnetGrpc || port == TestnetGrpc || port == SimnetGrpc || port == DevnetGrpc;
        }

        /// <summary>
        /// True when <paramref name="absolutePath"/> selects an encoding.
        /// A path ending in <c>/wrpc/borsh</c>, or containing <c>/borsh</c>, selects Borsh.
        /// A path ending in <c>/wrpc/json</c>, or containing <c>/json</c>, selects JSON.
        /// Borsh is tested first, so a path that contains both selects Borsh.
        /// Comparison is ordinal and case-insensitive. Trailing slashes are ignored for the ending check.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="absolutePath"/> is null.</exception>
        public static bool TrySelectPath(string absolutePath, out KaspaRpcEncoding encoding)
        {
            if (absolutePath == null)
            {
                throw new ArgumentNullException(nameof(absolutePath));
            }

            if (PathSelects(absolutePath, "/wrpc/borsh", "/borsh"))
            {
                encoding = KaspaRpcEncoding.Borsh;
                return true;
            }

            if (PathSelects(absolutePath, "/wrpc/json", "/json"))
            {
                encoding = KaspaRpcEncoding.Json;
                return true;
            }

            encoding = KaspaRpcEncoding.Auto;
            return false;
        }

        private static bool PathSelects(string absolutePath, string ending, string fragment)
        {
            var end = absolutePath.TrimEnd('/');
            return end.EndsWith(ending, StringComparison.OrdinalIgnoreCase)
                || absolutePath.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>Opens a Kaspa wRPC connection. <see cref="Create"/> does not connect. Call <see cref="KaspaRpcConnection.ConnectAsync"/>.</summary>
    public static class KaspaRpcClient
    {
        /// <summary>
        /// Builds a client for <paramref name="url"/>.
        /// <see cref="KaspaRpcEncoding.Auto"/> uses the URL path, then <see cref="KaspaWrpcPorts"/>. The path wins over the port.
        /// Pass <see cref="KaspaRpcEncoding.Borsh"/> or <see cref="KaspaRpcEncoding.Json"/> to override both the path and the port.
        /// An explicit encoding does not override a gRPC port. Ports 16110, 16210, 16510, and 16610 always throw.
        /// When the path has no <c>/borsh</c> or <c>/json</c> hint and the port is not a default wRPC port, this method throws.
        /// NKaspa does not default to either encoding in that case. <c>wss</c> on port 443 with no path hint is an example.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="url"/> is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="url"/> is not an absolute WebSocket URL, the port is a gRPC port, or <see cref="KaspaRpcEncoding.Auto"/> cannot tell Borsh from JSON.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A timeout or reconnect option is outside the allowed range.</exception>
        public static KaspaRpcConnection Create(string url, KaspaRpcEncoding encoding = KaspaRpcEncoding.Auto, KaspaWebSocketOptions? options = null)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss"))
            {
                throw new ArgumentException("wRPC URL must be an absolute ws or wss URI.", nameof(url));
            }

            if (KaspaWrpcPorts.IsGrpcPort(uri.Port))
            {
                throw new ArgumentException(
                    "Port " + uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " is a Kaspa gRPC port. NKaspa speaks wRPC, not gRPC. Borsh wRPC ports are 17110 (mainnet), 17210 (testnet), 17510 (simnet), and 17610 (devnet). JSON wRPC ports are 18110, 18210, 18510, and 18610. A path ending in /wrpc/borsh or containing /borsh selects Borsh. A path ending in /wrpc/json or containing /json selects JSON. The path wins over a wRPC port. Passing KaspaRpcEncoding does not turn a gRPC port into wRPC.",
                    nameof(url));
            }

            var selected = encoding;
            if (selected == KaspaRpcEncoding.Auto)
            {
                if (!KaspaWrpcPorts.TrySelectPath(uri.AbsolutePath, out selected)
                    && (!KaspaWrpcPorts.TrySelect(uri.Port, out selected) || selected == KaspaRpcEncoding.Auto))
                {
                    throw new ArgumentException(
                        "wRPC encoding could not be selected from port " + uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " or the URL path. NKaspa does not default to Borsh or JSON when both the port and the path are unknown (for example wss on port 443 with no /borsh or /json hint). A path ending in /wrpc/borsh or containing /borsh selects Borsh. A path ending in /wrpc/json or containing /json selects JSON. The path wins over the port. Borsh ports are 17110 (mainnet), 17210 (testnet), 17510 (simnet), and 17610 (devnet). JSON ports are 18110, 18210, 18510, and 18610. Pass KaspaRpcEncoding.Borsh or KaspaRpcEncoding.Json.",
                        nameof(url));
                }
            }

            if (selected == KaspaRpcEncoding.Borsh)
            {
                var channel = new KaspaBorshWebSocketChannel(uri, options);
                return new KaspaRpcConnection(KaspaRpcEncoding.Borsh, new KaspaWrpcBorshClient(channel), null, channel);
            }

            var json = new KaspaWebSocketChannel(uri, options);
            return new KaspaRpcConnection(KaspaRpcEncoding.Json, new KaspaWrpcJsonClient(json), json, null);
        }
    }

    /// <summary>
    /// One wRPC connection created by <see cref="KaspaRpcClient.Create"/>.
    /// It implements <see cref="IKaspaRpcClient"/> and owns the socket.
    /// A binary frame on a JSON connection, or a text frame on a Borsh connection, fails pending calls with <see cref="KaspaRpcEncodingMismatchException"/>.
    /// </summary>
    public sealed class KaspaRpcConnection : IKaspaRpcClient, IAsyncDisposable
    {
        private readonly IKaspaRpcClient _client;
        private readonly KaspaWebSocketChannel? _json;
        private readonly KaspaBorshWebSocketChannel? _borsh;

        internal KaspaRpcConnection(KaspaRpcEncoding encoding, IKaspaRpcClient client, KaspaWebSocketChannel? json, KaspaBorshWebSocketChannel? borsh)
        {
            Encoding = encoding;
            _client = client;
            _json = json;
            _borsh = borsh;
            if (_json != null)
            {
                _json.StateChanged += state => StateChanged?.Invoke(state);
                _json.Reconnected += () => Reconnected?.Invoke();
            }

            if (_borsh != null)
            {
                _borsh.StateChanged += state => StateChanged?.Invoke(state);
                _borsh.Reconnected += () => Reconnected?.Invoke();
            }
        }

        /// <summary>The encoding selected for this connection.</summary>
        public KaspaRpcEncoding Encoding { get; }

        /// <summary>True while the current socket is open.</summary>
        public bool IsConnected
        {
            get { return _json != null ? _json.IsConnected : _borsh!.IsConnected; }
        }

        /// <summary>The latest connection state.</summary>
        public KaspaWebSocketState State
        {
            get { return _json != null ? _json.State : _borsh!.State; }
        }

        /// <summary>Timeouts and reconnect settings for this socket.</summary>
        public KaspaWebSocketOptions Options
        {
            get { return _json != null ? _json.Options : _borsh!.Options; }
        }

        /// <summary>Raised when <see cref="State"/> changes.</summary>
        public event Action<KaspaWebSocketState>? StateChanged;

        /// <summary>Raised after a dropped socket is open again. The first <see cref="ConnectAsync"/> does not raise it.</summary>
        public event Action? Reconnected;

        /// <summary>Opens the WebSocket. Requests made before this call fail.</summary>
        /// <exception cref="System.InvalidOperationException">WebSocket is already connected.</exception>
        /// <exception cref="NKaspa.RPC.KaspaNodeUnreachableException">Kaspa node is unreachable. The inner exception may be a timeout or a WebSocket error.</exception>
        /// <exception cref="System.OperationCanceledException">The caller's token was canceled.</exception>
        /// <exception cref="System.ObjectDisposedException">The connection has been disposed.</exception>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_json != null)
                {
                    await _json.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                await _borsh!.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                var mapped = KaspaWrpcErrors.Transport(exception);
                if (ReferenceEquals(mapped, exception))
                {
                    throw;
                }

                throw mapped;
            }
        }

        /// <inheritdoc />
        public Task EnsureReadyAsync(Network network, bool requireSynced = true, bool requireUtxoIndex = true, CancellationToken cancellationToken = default)
        {
            return _client.EnsureReadyAsync(network, requireSynced, requireUtxoIndex, cancellationToken);
        }

        /// <inheritdoc />
        public Task<RpcServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default)
        {
            return _client.GetServerInfoAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<RpcBlockDagInfo> GetBlockDagInfoAsync(CancellationToken cancellationToken = default)
        {
            return _client.GetBlockDagInfoAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<ulong> GetSinkBlueScoreAsync(CancellationToken cancellationToken = default)
        {
            return _client.GetSinkBlueScoreAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<RpcVirtualChain> GetVirtualChainFromBlockAsync(uint256 startHash, bool includeAcceptedTransactionIds = false, ulong? minConfirmationCount = null, CancellationToken cancellationToken = default)
        {
            return _client.GetVirtualChainFromBlockAsync(startHash, includeAcceptedTransactionIds, minConfirmationCount, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<RpcUtxo>> GetUtxosByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
        {
            return _client.GetUtxosByAddressesAsync(addresses, cancellationToken);
        }

        /// <inheritdoc />
        public Task<Money> GetBalanceByAddressAsync(string address, CancellationToken cancellationToken = default)
        {
            return _client.GetBalanceByAddressAsync(address, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<RpcAddressBalance>> GetBalancesByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
        {
            return _client.GetBalancesByAddressesAsync(addresses, cancellationToken);
        }

        /// <inheritdoc />
        public Task<uint256> SubmitTransactionAsync(Transaction transaction, bool allowOrphan = false, CancellationToken cancellationToken = default)
        {
            return _client.SubmitTransactionAsync(transaction, allowOrphan, cancellationToken);
        }

        /// <inheritdoc />
        public Task<RpcFeeEstimate> GetFeeEstimateAsync(CancellationToken cancellationToken = default)
        {
            return _client.GetFeeEstimateAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<RpcChainBlock?> GetBlockAsync(uint256 hash, bool includeTransactions = false, CancellationToken cancellationToken = default)
        {
            return _client.GetBlockAsync(hash, includeTransactions, cancellationToken);
        }

        /// <inheritdoc />
        public Task<KaspaSubscription> NotifyUtxosChangedAsync(IReadOnlyList<string> addresses, Action<RpcUtxosChanged> onChanged, CancellationToken cancellationToken = default)
        {
            return _client.NotifyUtxosChangedAsync(addresses, onChanged, cancellationToken);
        }

        /// <inheritdoc />
        public Task<KaspaSubscription> NotifyVirtualDaaScoreChangedAsync(Action<RpcVirtualDaaScoreChanged> onChanged, CancellationToken cancellationToken = default)
        {
            return _client.NotifyVirtualDaaScoreChangedAsync(onChanged, cancellationToken);
        }

        /// <inheritdoc />
        public Task<KaspaSubscription> NotifyBlockAddedAsync(Action<RpcBlockAdded> onAdded, CancellationToken cancellationToken = default)
        {
            return _client.NotifyBlockAddedAsync(onAdded, cancellationToken);
        }

        /// <summary>Closes the socket. Pending requests fail.</summary>
        public async ValueTask DisposeAsync()
        {
            if (_json != null)
            {
                await _json.DisposeAsync().ConfigureAwait(false);
            }

            if (_borsh != null)
            {
                await _borsh.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
