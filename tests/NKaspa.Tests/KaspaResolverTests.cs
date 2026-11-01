using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Resolver path and fallback. rusty-kaspa rpc/wrpc/client/src/resolver.rs and Resolvers.toml
    /// at commit 1c1a6927d20042953f0389b1089720e515fe74e5.
    /// https://github.com/kaspanet/rusty-kaspa/blob/1c1a6927d20042953f0389b1089720e515fe74e5/rpc/wrpc/client/Resolvers.toml
    /// GET {resolver}/v2/kaspa/{network}/{tls|any}/wrpc/{json|borsh}. The node field is url.
    /// </summary>
    public class KaspaResolverTests
    {
        [Fact]
        public void Public_list_follows_resolvers_toml_and_skips_the_disabled_localhost_row()
        {
            Assert.Equal(16, KaspaResolver.PublicResolvers.Count);
            Assert.Equal("https://eric.kaspa.stream", KaspaResolver.PublicResolvers[0]);
            Assert.Equal("https://troy.kaspa.stream", KaspaResolver.PublicResolvers[3]);
            Assert.Equal("https://john.kaspa.red", KaspaResolver.PublicResolvers[4]);
            Assert.Equal("https://jake.kaspa.green", KaspaResolver.PublicResolvers[8]);
            Assert.Equal("https://noah.kaspa.blue", KaspaResolver.PublicResolvers[12]);
            Assert.Equal("https://luke.kaspa.blue", KaspaResolver.PublicResolvers[15]);
            Assert.DoesNotContain(KaspaResolver.PublicResolvers, url => url.Contains("127.0.0.1"));
            Assert.DoesNotContain(KaspaResolver.PublicResolvers, url => url.Contains("kaspa-ng"));
            Assert.Equal(2, KaspaResolver.CurrentVersion);
        }

        [Fact]
        public void Build_url_uses_testnet_10_and_the_tls_or_any_segment()
        {
            Assert.Equal("testnet-10", KaspaResolver.NetworkId(Network.Testnet));
            Assert.Equal("mainnet", KaspaResolver.NetworkId(Network.Mainnet));
            Assert.Equal(
                "https://eric.kaspa.stream/v2/kaspa/mainnet/any/wrpc/json",
                KaspaResolver.BuildUrl("https://eric.kaspa.stream/", Network.Mainnet, KaspaResolverTransport.Any, KaspaRpcEncoding.Json));
            Assert.Equal(
                "https://noah.kaspa.blue/v2/kaspa/testnet-10/tls/wrpc/borsh",
                KaspaResolver.BuildUrl("https://noah.kaspa.blue", Network.Testnet, KaspaResolverTransport.Tls, KaspaRpcEncoding.Borsh));
            Assert.Equal("testnet-10", KaspaResolver.NetworkId(Network.Testnet.WithToccataActivationDaaScore(1)));
            Assert.Throws<ArgumentNullException>(() => KaspaResolver.NetworkId(null!));
            Assert.Throws<ArgumentException>(() => KaspaResolver.BuildUrl("https://eric.kaspa.stream", Network.Mainnet, KaspaResolverTransport.Any, KaspaRpcEncoding.Auto));
            Assert.Throws<ArgumentException>(() => new KaspaResolver(Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new KaspaResolver(new[] { "ws://eric.kaspa.stream" }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KaspaResolver(new[] { "https://eric.kaspa.stream" }, timeout: TimeSpan.Zero));
        }

        [Fact]
        public async Task Fallback_skips_http_errors_and_bodies_without_a_websocket_url()
        {
            var script = new ScriptedHandler();
            script.Add(Status(HttpStatusCode.NotFound));
            script.Add(Json("{\"uid\":\"no-url\"}"));
            script.Add(Json("not-json"));
            script.Add(Json("{\"url\":\"https://not-a-socket.example\"}"));
            script.Add(Json("{\"uid\":\"node-a\",\"url\":\"wss://vector-10.kaspa.green/kaspa/testnet-10/wrpc/json\"}"));
            var resolver = new KaspaResolver(
                new[] { "https://first.example", "https://second.example", "https://third.example", "https://fourth.example", "https://fifth.example" },
                KaspaResolverTransport.Any,
                TimeSpan.FromSeconds(2),
                shuffle: false,
                script);
            var node = await resolver.ResolveAsync(Network.Testnet, KaspaRpcEncoding.Json);
            Assert.Equal("wss://vector-10.kaspa.green/kaspa/testnet-10/wrpc/json", node.Url);
            Assert.Equal("node-a", node.Uid);
            Assert.Equal(5, script.Urls.Count);
            Assert.Equal("https://first.example/v2/kaspa/testnet-10/any/wrpc/json", script.Urls[0]);
            Assert.Equal("https://fifth.example/v2/kaspa/testnet-10/any/wrpc/json", script.Urls[4]);
        }

        [Fact]
        public async Task Every_resolver_failing_throws_and_records_the_attempted_urls()
        {
            var script = new ScriptedHandler();
            script.Add(Status(HttpStatusCode.InternalServerError));
            script.Add(Status(HttpStatusCode.NotFound));
            var resolver = new KaspaResolver(
                new[] { "https://a.example", "https://b.example" },
                KaspaResolverTransport.Tls,
                TimeSpan.FromSeconds(2),
                shuffle: false,
                script);
            var error = await Assert.ThrowsAsync<KaspaResolverException>(() => resolver.ResolveAsync(Network.Mainnet, KaspaRpcEncoding.Borsh));
            Assert.Equal(KaspaResolverException.DefaultMessage, error.Message);
            Assert.Equal(2, error.AttemptedUrls.Count);
            Assert.Equal("https://a.example/v2/kaspa/mainnet/tls/wrpc/borsh", error.AttemptedUrls[0]);
            Assert.Equal("https://b.example/v2/kaspa/mainnet/tls/wrpc/borsh", error.AttemptedUrls[1]);
            Assert.IsType<HttpRequestException>(error.InnerException);
        }

        [Fact]
        public async Task A_timed_out_resolver_is_skipped()
        {
            var script = new ScriptedHandler();
            script.AddDelay(TimeSpan.FromSeconds(5));
            script.Add(Json("{\"url\":\"wss://eva.kaspa.green/kaspa/mainnet/wrpc/json\"}"));
            var resolver = new KaspaResolver(
                new[] { "https://slow.example", "https://fast.example" },
                timeout: TimeSpan.FromMilliseconds(150),
                shuffle: false,
                httpHandler: script);
            var node = await resolver.ResolveAsync(Network.Mainnet, KaspaRpcEncoding.Json);
            Assert.Equal("wss://eva.kaspa.green/kaspa/mainnet/wrpc/json", node.Url);
            Assert.Null(node.Uid);
            Assert.Equal(2, script.Urls.Count);
        }

        [Fact]
        public async Task Caller_cancellation_is_not_a_resolver_failure()
        {
            var script = new ScriptedHandler();
            script.AddDelay(TimeSpan.FromSeconds(5));
            var resolver = new KaspaResolver(new[] { "https://slow.example" }, timeout: TimeSpan.FromSeconds(5), shuffle: false, httpHandler: script);
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.ResolveAsync(Network.Mainnet, KaspaRpcEncoding.Json, cancel.Token));
        }

        [Fact]
        public async Task Connect_uses_the_resolved_socket_and_maps_a_handshake_timeout()
        {
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            var accept = tcp.AcceptTcpClientAsync();
            var url = "ws://127.0.0.1:" + port.ToString() + "/kaspa/mainnet/wrpc/json";
            var script = new ScriptedHandler();
            script.Add(Json("{\"url\":\"" + url + "\"}"));
            var resolver = new KaspaResolver(new[] { "https://only.example" }, timeout: TimeSpan.FromSeconds(2), shuffle: false, httpHandler: script);
            var options = new KaspaWebSocketOptions
            {
                ConnectTimeout = TimeSpan.FromMilliseconds(400),
                AutoReconnect = false,
            };
            var error = await Assert.ThrowsAsync<KaspaNodeUnreachableException>(() => resolver.ConnectAsync(Network.Mainnet, KaspaRpcEncoding.Json, options));
            Assert.Equal(KaspaNodeUnreachableException.DefaultMessage, error.Message);
            Assert.IsType<TimeoutException>(error.InnerException);
            tcp.Stop();
            try
            {
                using var client = await accept;
                client.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static HttpResponseMessage Status(HttpStatusCode code)
        {
            return new HttpResponseMessage(code);
        }

        private static HttpResponseMessage Json(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }

        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _steps = new Queue<Func<CancellationToken, Task<HttpResponseMessage>>>();

            public List<string> Urls { get; } = new List<string>();

            public void Add(HttpResponseMessage response)
            {
                _steps.Enqueue(_ => Task.FromResult(response));
            }

            public void AddDelay(TimeSpan delay)
            {
                _steps.Enqueue(async token =>
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                    return Json("{\"url\":\"wss://late.example/wrpc/json\"}");
                });
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.RequestUri!.AbsoluteUri);
                cancellationToken.ThrowIfCancellationRequested();
                var step = _steps.Dequeue();
                return step(cancellationToken);
            }
        }
    }
}
