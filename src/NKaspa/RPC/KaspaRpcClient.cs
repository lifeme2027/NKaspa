namespace NKaspa.RPC
{
    /// <summary>Which wRPC encoding <see cref="KaspaRpcClient.Create"/> should open.</summary>
    public enum KaspaRpcEncoding
    {
        /// <summary>Pick Borsh or JSON from the URL port. An unknown port throws.</summary>
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
    }

    /// <summary>Opens a Kaspa wRPC connection. <see cref="Create"/> does not connect. Call <see cref="KaspaRpcConnection.ConnectAsync"/>.</summary>
    public static class KaspaRpcClient
    {
        /// <summary>
        /// Builds a client for <paramref name="url"/>.
        /// <see cref="KaspaRpcEncoding.Auto"/> uses <see cref="KaspaWrpcPorts"/>. Pass <see cref="KaspaRpcEncoding.Borsh"/> or <see cref="KaspaRpcEncoding.Json"/> to override the port.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="url"/> is null.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="url"/> is not an absolute WebSocket URL, or <see cref="KaspaRpcEncoding.Auto"/> cannot tell Borsh from JSON for that port.</exception>
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

            var selected = encoding;
            if (selected == KaspaRpcEncoding.Auto)
            {
                if (!KaspaWrpcPorts.TrySelect(uri.Port, out selected) || selected == KaspaRpcEncoding.Auto)
                {
                    throw new ArgumentException(
                        "wRPC encoding could not be selected from port " + uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ". Borsh ports are 17110 (mainnet), 17210 (testnet), 17510 (simnet), and 17610 (devnet). JSON ports are 18110, 18210, 18510, and 18610. Pass KaspaRpcEncoding.Borsh or KaspaRpcEncoding.Json.",
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
        /// <exception cref="System.TimeoutException">WebSocket connect timed out.</exception>
        /// <exception cref="System.OperationCanceledException">The caller's token was canceled.</exception>
        /// <exception cref="System.ObjectDisposedException">The connection has been disposed.</exception>
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_json != null)
            {
                return _json.ConnectAsync(cancellationToken);
            }

            return _borsh!.ConnectAsync(cancellationToken);
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
