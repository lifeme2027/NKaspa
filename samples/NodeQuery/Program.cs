using System.Globalization;
using System.Net.WebSockets;
using NKaspa;
using NKaspa.RPC;

namespace NKaspa.Samples.NodeQuery
{
    /// <summary>
    /// Read-only wRPC JSON queries. Kaspa counterpart of a small slice of NBitcoin <c>RPCClient</c>.
    /// 只读查询。不调用 submitTransaction，不签名，不广播。
    /// </summary>
    public static class Program
    {
        /// <summary>Public mainnet wRPC JSON endpoint. Override with <c>--url</c>.</summary>
        public const string DefaultUrl = "wss://eva.kaspa.green/kaspa/mainnet/wrpc/json";

        // BIP-340 public key 1, the address vector in KaspaAddressTests. Balance may be zero.
        // BIP-340 第 1 条公钥的地址。余额可以是 0，查询本身仍然成立。
        public const string DefaultAddress = "kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva";

        public static Task<int> Main(string[] args)
        {
            return Run(args, Console.Out);
        }

        /// <summary>
        /// <c>--url</c> replaces the socket. <c>--address</c> replaces the balance and UTXO query.
        /// <c>--url</c> 改节点，<c>--address</c> 改查询的地址。
        /// </summary>
        public static async Task<int> Run(string[] args, TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            args ??= Array.Empty<string>();
            if (Has(args, "--help"))
            {
                writer.WriteLine("dotnet run --project samples/NodeQuery -- --url <wss://...> --address <kaspa:...>");
                return 0;
            }

            var url = Option(args, "--url") ?? DefaultUrl;
            var address = Option(args, "--address") ?? DefaultAddress;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "wss" && uri.Scheme != "ws"))
            {
                writer.WriteLine("error: url must be an absolute ws or wss URI");
                return 1;
            }

            if (!KaspaAddress.IsValid(address))
            {
                writer.WriteLine("error: address is not a Kaspa address");
                return 1;
            }

            writer.WriteLine("readonly: true");
            writer.WriteLine("url: " + url);
            writer.WriteLine("address: " + address);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try
            {
                // The channel matches responses by id. Connect before the first exchange.
                // 通道按请求 id 匹配响应。先 Connect，再发 getBlockDagInfo 这一类方法。
                await using var channel = new KaspaWebSocketChannel(uri);
                await channel.ConnectAsync(timeout.Token).ConfigureAwait(false);
                IKaspaRpcClient rpc = new KaspaWrpcJsonClient(channel);

                var server = await rpc.GetServerInfoAsync(timeout.Token).ConfigureAwait(false);
                writer.WriteLine("server-version: " + server.ServerVersion);
                writer.WriteLine("rpc-api: " + server.RpcApiVersion.ToString(CultureInfo.InvariantCulture) + "." + server.RpcApiRevision.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("network-id: " + server.NetworkId);
                writer.WriteLine("synced: " + (server.IsSynced ? "true" : "false"));
                writer.WriteLine("utxo-index: " + (server.HasUtxoIndex ? "true" : "false"));

                var dag = await rpc.GetBlockDagInfoAsync(timeout.Token).ConfigureAwait(false);
                writer.WriteLine("network: " + dag.Network);
                writer.WriteLine("block-count: " + dag.BlockCount.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("header-count: " + dag.HeaderCount.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("virtual-daa-score: " + dag.VirtualDaaScore.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("difficulty: " + dag.Difficulty.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("past-median-time: " + dag.PastMedianTime.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("pruning-point: " + dag.PruningPointHash);
                writer.WriteLine("sink: " + dag.Sink);
                writer.WriteLine("tip-count: " + dag.TipHashes.Count.ToString(CultureInfo.InvariantCulture));
                if (dag.TipHashes.Count > 0)
                {
                    writer.WriteLine("tip: " + dag.TipHashes[0]);
                }

                var balance = await rpc.GetBalanceByAddressAsync(address, timeout.Token).ConfigureAwait(false);
                writer.WriteLine("balance-sompi: " + balance.Sompi.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("balance: " + balance);

                var utxos = await rpc.GetUtxosByAddressesAsync(new[] { address }, timeout.Token).ConfigureAwait(false);
                writer.WriteLine("utxo-count: " + utxos.Count.ToString(CultureInfo.InvariantCulture));
                var shown = Math.Min(utxos.Count, 5);
                for (var i = 0; i < shown; i++)
                {
                    var utxo = utxos[i];
                    writer.WriteLine(
                        "utxo[" + i.ToString(CultureInfo.InvariantCulture) + "]: "
                        + utxo.Outpoint.TransactionId + ":" + utxo.Outpoint.Index.ToString(CultureInfo.InvariantCulture)
                        + " " + utxo.Entry.Amount);
                }

                if (dag.BlockCount == 0 || dag.HeaderCount == 0)
                {
                    writer.WriteLine("error: DAG counts were zero");
                    return 1;
                }
            }
            catch (Exception ex) when (ex is KaspaRpcException or WebSocketException or InvalidOperationException or FormatException or OperationCanceledException or TimeoutException)
            {
                writer.WriteLine("error: " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }

            writer.WriteLine("ok");
            return 0;
        }

        private static bool Has(string[] args, string name)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? Option(string[] args, string name)
        {
            for (var i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
