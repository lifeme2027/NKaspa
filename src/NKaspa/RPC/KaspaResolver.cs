using System.Net.Http;
using System.Text.Json;

namespace NKaspa.RPC
{
    /// <summary>
    /// Whether the resolver should ask for a TLS node.
    /// rusty-kaspa <c>rpc/wrpc/client/src/resolver.rs</c> writes <c>tls</c> or <c>any</c> into the path.
    /// </summary>
    public enum KaspaResolverTransport
    {
        /// <summary>Path segment <c>any</c>. The resolver may return a TLS or a clear-text node.</summary>
        Any = 0,

        /// <summary>Path segment <c>tls</c>. Ask the resolver for a TLS node.</summary>
        Tls = 1,
    }

    /// <summary>
    /// One wRPC node returned by a resolver. <see cref="Url"/> is the WebSocket address. <see cref="Uid"/> is the resolver's node id when the JSON includes it.
    /// </summary>
    public sealed class KaspaNodeEndpoint
    {
        /// <summary>Creates an endpoint.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="url"/> is null.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="url"/> is not an absolute ws or wss URI.</exception>
        public KaspaNodeEndpoint(string url, string? uid)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss"))
            {
                throw new ArgumentException("Node URL must be an absolute ws or wss URI.", nameof(url));
            }

            Url = url;
            Uid = uid;
        }

        /// <summary>wRPC WebSocket URL.</summary>
        public string Url { get; }

        /// <summary>Resolver node id. Null when the JSON has no <c>uid</c>.</summary>
        public string? Uid { get; }
    }

    /// <summary>
    /// Client for the public Kaspa wRPC resolver.
    /// rusty-kaspa <c>rpc/wrpc/client/src/resolver.rs</c> and <c>Resolvers.toml</c>
    /// (commit <c>1c1a6927d20042953f0389b1089720e515fe74e5</c>, the last commit that changed that file).
    /// A query is <c>GET {resolver}/v2/kaspa/{network}/{tls|any}/wrpc/{json|borsh}</c>.
    /// The JSON field used is <c>url</c>. <c>uid</c> is optional.
    /// Resolvers are tried in shuffled order until one returns a WebSocket URL.
    /// The disabled localhost row in <c>Resolvers.toml</c> is not in <see cref="PublicResolvers"/>.
    /// </summary>
    public sealed class KaspaResolver
    {
        /// <summary>Path version. rusty-kaspa <c>CURRENT_VERSION</c> is 2.</summary>
        public const int CurrentVersion = 2;

        /// <summary>Per-resolver HTTP timeout when the caller does not pass one. Five seconds.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        private const int MaximumBodyBytes = 65536;

        private static readonly string[] PublicResolverUrls =
        {
            "https://eric.kaspa.stream",
            "https://maxim.kaspa.stream",
            "https://sean.kaspa.stream",
            "https://troy.kaspa.stream",
            "https://john.kaspa.red",
            "https://mike.kaspa.red",
            "https://paul.kaspa.red",
            "https://alex.kaspa.red",
            "https://jake.kaspa.green",
            "https://mark.kaspa.green",
            "https://adam.kaspa.green",
            "https://liam.kaspa.green",
            "https://noah.kaspa.blue",
            "https://ryan.kaspa.blue",
            "https://jack.kaspa.blue",
            "https://luke.kaspa.blue",
        };

        private readonly string[] _resolvers;
        private readonly HttpMessageHandler? _handler;

        /// <summary>
        /// Creates a resolver.
        /// A null <paramref name="resolvers"/> list uses <see cref="PublicResolvers"/>.
        /// An empty list is rejected. rusty-kaspa panics on an empty custom list.
        /// </summary>
        /// <exception cref="System.ArgumentException">
        /// Resolver URL list is empty. Resolver URL is empty. Resolver URL must be an absolute http or https URI.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Resolver timeout must be positive. Resolver transport is not any or tls.
        /// </exception>
        public KaspaResolver(
            IReadOnlyList<string>? resolvers = null,
            KaspaResolverTransport transport = KaspaResolverTransport.Any,
            TimeSpan? timeout = null,
            bool shuffle = true,
            HttpMessageHandler? httpHandler = null)
        {
            var timeoutValue = timeout ?? DefaultTimeout;
            if (timeoutValue <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Resolver timeout must be positive.");
            }

            TransportSegment(transport);
            var source = resolvers ?? PublicResolvers;
            if (source.Count == 0)
            {
                throw new ArgumentException("Resolver URL list is empty.", nameof(resolvers));
            }

            var copy = new string[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = RequireResolver(source[i]);
            }

            _resolvers = copy;
            _handler = httpHandler;
            Transport = transport;
            Timeout = timeoutValue;
            Shuffle = shuffle;
        }

        /// <summary>
        /// Enabled public resolvers from rusty-kaspa <c>rpc/wrpc/client/Resolvers.toml</c>.
        /// The <c>http://127.0.0.1:8888</c> row has <c>enable = false</c> and is omitted.
        /// Commented <c>kaspa-ng.org</c> groups are omitted.
        /// Order is the file order: kaspa.stream, kaspa.red, kaspa.green, kaspa.blue.
        /// </summary>
        public static IReadOnlyList<string> PublicResolvers { get; } = Array.AsReadOnly(PublicResolverUrls);

        /// <summary>Resolver bases this instance queries, in the order they were supplied.</summary>
        public IReadOnlyList<string> Resolvers
        {
            get { return Array.AsReadOnly(_resolvers); }
        }

        /// <summary><c>tls</c> or <c>any</c>.</summary>
        public KaspaResolverTransport Transport { get; }

        /// <summary>Time allowed for one resolver HTTP call.</summary>
        public TimeSpan Timeout { get; }

        /// <summary>True when each <see cref="ResolveAsync"/> shuffles the resolver list. rusty-kaspa shuffles.</summary>
        public bool Shuffle { get; }

        /// <summary>
        /// rusty-kaspa <c>NetworkId</c> display string for <paramref name="network"/>.
        /// <see cref="Network.Testnet"/> is <c>testnet-10</c>. Mainnet, simnet, and devnet use their short names.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Unknown Kaspa network.</exception>
        public static string NetworkId(Network network)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            switch (network.Name)
            {
                case "mainnet":
                    return "mainnet";
                case "testnet":
                    return "testnet-10";
                case "simnet":
                    return "simnet";
                case "devnet":
                    return "devnet";
                default:
                    throw new ArgumentException("Unknown Kaspa network '" + network.Name + "'.", nameof(network));
            }
        }

        /// <summary>
        /// Absolute resolver query.
        /// <c>{resolver}/v2/kaspa/{network}/{tls|any}/wrpc/{json|borsh}</c>.
        /// A trailing slash on <paramref name="resolver"/> is removed.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="resolver"/> is null. <paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// Resolver URL is empty. Resolver URL must be an absolute http or https URI. Unknown Kaspa network.
        /// Resolver encoding must be JSON or Borsh.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Resolver transport is not any or tls.</exception>
        public static string BuildUrl(string resolver, Network network, KaspaResolverTransport transport, KaspaRpcEncoding encoding)
        {
            return RequireResolver(resolver).TrimEnd('/')
                + "/v" + CurrentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "/kaspa/" + NetworkId(network)
                + "/" + TransportSegment(transport)
                + "/wrpc/" + EncodingSegment(encoding);
        }

        /// <summary>
        /// Asks each resolver, in shuffled order when <see cref="Shuffle"/> is true, for one node.
        /// The first JSON body with a non-empty <c>url</c> of scheme <c>ws</c> or <c>wss</c> wins.
        /// A timeout, an HTTP error, or a body without that URL is skipped.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Unknown Kaspa network. Resolver encoding must be JSON or Borsh.</exception>
        /// <exception cref="NKaspa.RPC.KaspaResolverException">Kaspa resolver returned no endpoint.</exception>
        /// <exception cref="System.OperationCanceledException">The caller's token was canceled.</exception>
        public async Task<KaspaNodeEndpoint> ResolveAsync(Network network, KaspaRpcEncoding encoding, CancellationToken cancellationToken = default)
        {
            var order = new List<string>(_resolvers);
            if (Shuffle && order.Count > 1)
            {
                ShuffleList(order);
            }

            Exception? last = null;
            var attempted = new List<string>(order.Count);
            using (var client = CreateClient())
            {
                for (var i = 0; i < order.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var url = BuildUrl(order[i], network, Transport, encoding);
                    attempted.Add(url);
                    try
                    {
                        var endpoint = await FetchAsync(client, url, cancellationToken).ConfigureAwait(false);
                        if (endpoint != null)
                        {
                            return endpoint;
                        }

                        last = new FormatException("Resolver response has no WebSocket url.");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (IsResolverFailure(exception))
                    {
                        last = exception;
                    }
                }
            }

            throw new KaspaResolverException(KaspaResolverException.DefaultMessage, attempted.ToArray(), last);
        }

        /// <summary>
        /// Resolves one node and opens its wRPC socket.
        /// <see cref="KaspaRpcEncoding.Json"/> is the default. <see cref="KaspaRpcEncoding.Auto"/> is not a resolver path segment.
        /// The returned connection is connected. Dispose it.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// Unknown Kaspa network. Resolver encoding must be JSON or Borsh. wRPC URL must be an absolute ws or wss URI.
        /// The port is a gRPC port, or the URL path does not select an encoding.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A timeout or reconnect option is outside the allowed range.</exception>
        /// <exception cref="NKaspa.RPC.KaspaResolverException">Kaspa resolver returned no endpoint.</exception>
        /// <exception cref="NKaspa.RPC.KaspaNodeUnreachableException">Kaspa node is unreachable. The inner exception may be a timeout or a WebSocket error.</exception>
        /// <exception cref="System.OperationCanceledException">The caller's token was canceled.</exception>
        /// <exception cref="System.ObjectDisposedException">The connection has been disposed.</exception>
        public async Task<KaspaRpcConnection> ConnectAsync(
            Network network,
            KaspaRpcEncoding encoding = KaspaRpcEncoding.Json,
            KaspaWebSocketOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var endpoint = await ResolveAsync(network, encoding, cancellationToken).ConfigureAwait(false);
            var connection = KaspaRpcClient.Create(endpoint.Url, encoding, options);
            try
            {
                await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private HttpClient CreateClient()
        {
            var client = _handler == null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            return client;
        }

        private async Task<KaspaNodeEndpoint?> FetchAsync(HttpClient client, string url, CancellationToken cancellationToken)
        {
            using (var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                budget.CancelAfter(Timeout);
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException(
                            "Resolver status " + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
                    }

                    var body = await ReadLimitedAsync(response, budget.Token).ConfigureAwait(false);
                    return ParseEndpoint(body);
                }
            }
        }

        private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using (stream)
            {
                var buffer = new byte[MaximumBodyBytes + 1];
                var filled = 0;
                while (filled < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer, filled, buffer.Length - filled, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    filled += read;
                }

                if (filled > MaximumBodyBytes)
                {
                    throw new FormatException("Resolver response is larger than 65536 bytes.");
                }

                return System.Text.Encoding.UTF8.GetString(buffer, 0, filled);
            }
        }

        private static KaspaNodeEndpoint? ParseEndpoint(string body)
        {
            using (var document = JsonDocument.Parse(body))
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                var url = urlElement.GetString();
                if (string.IsNullOrWhiteSpace(url))
                {
                    return null;
                }

                string? uid = null;
                if (root.TryGetProperty("uid", out var uidElement) && uidElement.ValueKind == JsonValueKind.String)
                {
                    uid = uidElement.GetString();
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss"))
                {
                    return null;
                }

                return new KaspaNodeEndpoint(url, uid);
            }
        }

        private static bool IsResolverFailure(Exception exception)
        {
            return exception is HttpRequestException
                || exception is OperationCanceledException
                || exception is InvalidOperationException
                || exception is IOException
                || exception is FormatException
                || exception is JsonException;
        }

        private static void ShuffleList(List<string> urls)
        {
            var random = new Random();
            for (var i = urls.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                var swap = urls[i];
                urls[i] = urls[j];
                urls[j] = swap;
            }
        }

        private static string RequireResolver(string? resolver)
        {
            if (resolver == null)
            {
                throw new ArgumentException("Resolver URL is empty.", nameof(resolver));
            }

            if (string.IsNullOrWhiteSpace(resolver))
            {
                throw new ArgumentException("Resolver URL is empty.", nameof(resolver));
            }

            if (!Uri.TryCreate(resolver, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new ArgumentException("Resolver URL must be an absolute http or https URI.", nameof(resolver));
            }

            return resolver;
        }

        private static string TransportSegment(KaspaResolverTransport transport)
        {
            if (transport == KaspaResolverTransport.Any)
            {
                return "any";
            }

            if (transport == KaspaResolverTransport.Tls)
            {
                return "tls";
            }

            throw new ArgumentOutOfRangeException(nameof(transport), "Resolver transport is not any or tls.");
        }

        private static string EncodingSegment(KaspaRpcEncoding encoding)
        {
            if (encoding == KaspaRpcEncoding.Json)
            {
                return "json";
            }

            if (encoding == KaspaRpcEncoding.Borsh)
            {
                return "borsh";
            }

            throw new ArgumentException("Resolver encoding must be JSON or Borsh.", nameof(encoding));
        }
    }

    /// <summary>
    /// Every configured resolver failed or returned a body without a WebSocket <c>url</c>.
    /// The message is <see cref="DefaultMessage"/>. <see cref="AttemptedUrls"/> lists the GET URLs in the order they were tried.
    /// </summary>
    public sealed class KaspaResolverException : KaspaWrpcException
    {
        /// <summary>Failure message when no resolver returns a node.</summary>
        public const string DefaultMessage = "Kaspa resolver returned no endpoint.";

        /// <summary>Creates an exception.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null. <paramref name="attemptedUrls"/> is null.</exception>
        public KaspaResolverException(string message, IReadOnlyList<string> attemptedUrls, Exception? innerException = null)
            : base(message, innerException)
        {
            if (attemptedUrls == null)
            {
                throw new ArgumentNullException(nameof(attemptedUrls));
            }

            AttemptedUrls = attemptedUrls;
        }

        /// <summary>GET URLs that were tried, in order.</summary>
        public IReadOnlyList<string> AttemptedUrls { get; }
    }
}
