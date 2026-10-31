using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using NKaspa;
using NKaspa.Policy;
using NKaspa.RPC;

namespace NKaspa.Samples.Spend
{
    /// <summary>
    /// Build and sign version 0, a two-input MustSpend payment, and version 1.
    /// Fees are the minimum relay fee. The default path only prints the transaction and its wRPC JSON.
    /// 构建并签名版本 0、MustSpend 双输入、版本 1。费用是最低中继费。默认只打印交易和 JSON。
    /// </summary>
    /// <remarks>
    /// WARNING / 警告:
    /// <c>--broadcast</c> submits a signed transaction to a node.
    /// It runs only when <c>--broadcast</c> is present AND the environment variable
    /// <c>NKASPA_MNEMONIC</c> holds the mnemonic. You must also pass <c>--to</c> and <c>--amount</c>.
    /// The sample refuses to submit unless the payment output, the minimum relay fee, and <c>Transaction.Verify</c> all succeed.
    /// Optional <c>--from</c> must match the derived address. Optional <c>--max-fee-sompi</c> caps the fee.
    /// This sample does not read a secrets directory. Do not set the variable unless you intend to spend.
    /// <c>--broadcast</c> 会把签好的交易提交到节点。
    /// 只有同时传入 <c>--broadcast</c>，并且环境变量 <c>NKASPA_MNEMONIC</c> 里有助记词，才会进入广播。
    /// 还要有 <c>--to</c> 和 <c>--amount</c>。本示例不读取密钥目录。不打算花钱就不要设置那个变量。
    /// </remarks>
    public static class Program
    {
        /// <summary>Same public mainnet socket as the read-only sample. <c>--url</c> overrides it.</summary>
        public const string DefaultUrl = "wss://eva.kaspa.green/kaspa/mainnet/wrpc/json";

        // Public test scalars from the library tests. The offline coins are synthetic.
        // 库测试里的公开标量。离线输入是造出来的币，链上不存在。
        private const string PayerSecret = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string PayeeSecret = "b7e151628aed2a6abf7158809cf4f3c762e7160f38b4da56a784d9045190cfef";

        public static Task<int> Main(string[] args)
        {
            return Run(args ?? Array.Empty<string>(), Console.Out, Console.Error);
        }

        /// <summary>
        /// No arguments: print three offline transactions and do not open a socket.
        /// <c>--broadcast</c> is refused unless <c>NKASPA_MNEMONIC</c>, <c>--to</c>, and <c>--amount</c> are all present.
        /// 不带参数时只打印三笔离线交易，不开套接字。
        /// </summary>
        public static Task<int> Run(string[] args, TextWriter writer, TextWriter error)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            args ??= Array.Empty<string>();
            if (!Has(args, "--broadcast"))
            {
                return Task.FromResult(Offline(writer));
            }

            // The refusal path returns before any socket is opened.
            // 拒绝广播时直接返回，不会去连节点。
            var refused = RefuseBroadcast(args, error);
            if (refused != 0)
            {
                return Task.FromResult(refused);
            }

            return BroadcastAsync(args, writer, error);
        }

        private static int Offline(TextWriter writer)
        {
            writer.WriteLine("broadcast: no");
            using var payer = Key.Parse(PayerSecret);
            using var payee = Key.Parse(PayeeSecret);
            var payeeAddress = payee.GetAddress(Network.Mainnet);
            var change = payer.GetAddress(Network.Mainnet);

            // Documented mainnet shapes: 2036 grams / 203600 sompi, 3154 / 315400, version 1 is 2038 / 203800.
            // 文档里的主网形状。版本 0 单输入 203600 sompi，双输入 315400，版本 1 单输入 203800。
            var singleCoin = CoinOf(payer, 100_000_000UL, 70);
            var single = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(singleCoin)
                .Send(payeeAddress, Money.Coins(0.2m))
                .SetChange(change)
                .SendMinimumRelayFee()
                .BuildTransaction(true);
            if (!WriteSample(writer, "version-0", single, new[] { singleCoin }, 0, 203_600UL, MassCalculator.StandardSchnorrPaymentMass, false))
            {
                return 1;
            }

            var left = CoinOf(payer, 50_000_000UL, 71);
            var right = CoinOf(payer, 50_000_000UL, 72);
            var forced = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(left, right)
                .MustSpend(left, right)
                .Send(payeeAddress, Money.Coins(0.2m))
                .SetChange(change)
                .SendMinimumRelayFee()
                .BuildTransaction(true);
            if (!WriteSample(writer, "must-spend", forced, new[] { left, right }, 0, 315_400UL, MassCalculator.StandardTwoInputSchnorrPaymentMass, true))
            {
                return 1;
            }

            var version1Coin = CoinOf(payer, 100_000_000UL, 73);
            var version1 = new TransactionBuilder()
                .SetVersion(1)
                .AddKeys(payer)
                .AddCoins(version1Coin)
                .Send(payeeAddress, Money.Coins(0.1m))
                .SetChange(change)
                .SendMinimumRelayFee()
                .BuildTransaction(true);
            if (!WriteSample(writer, "version-1", version1, new[] { version1Coin }, 1, 203_800UL, MassCalculator.StandardSchnorrPaymentMassVersion1, false))
            {
                return 1;
            }

            writer.WriteLine("ok");
            return 0;
        }

        private static bool WriteSample(TextWriter writer, string name, Transaction tx, Coin[] coins, int version, ulong expectedFee, ulong expectedRelay, bool mustSpend)
        {
            var entries = new UtxoEntry[tx.Inputs.Count];
            ulong inputSum = 0;
            for (var i = 0; i < coins.Length; i++)
            {
                inputSum += coins[i].Amount.Sompi;
            }

            ulong outputSum = 0;
            for (var i = 0; i < tx.Outputs.Count; i++)
            {
                outputSum += tx.Outputs[i].Value.Sompi;
            }

            for (var i = 0; i < tx.Inputs.Count; i++)
            {
                UtxoEntry? found = null;
                for (var c = 0; c < coins.Length; c++)
                {
                    if (coins[c].Outpoint.Equals(tx.Inputs[i].PreviousOutpoint))
                    {
                        found = coins[c].Utxo;
                        break;
                    }
                }

                if (found == null || !tx.VerifyInput(i, found))
                {
                    writer.WriteLine("error: input " + i.ToString(CultureInfo.InvariantCulture) + " did not verify");
                    return false;
                }

                entries[i] = found;
            }

            var mass = MassCalculator.Calculate(tx, entries);
            var relay = MassCalculator.RelayMass(mass);
            var fee = inputSum - outputSum;
            var json = KaspaRpcJson.WriteTransaction(tx);
            var roundTrip = Transaction.FromHex(tx.ToHex());
            writer.WriteLine("sample: " + name);
            writer.WriteLine("version: " + tx.Version.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("inputs: " + tx.Inputs.Count.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("must-spend: " + (mustSpend ? "true" : "false"));
            writer.WriteLine("fee-sompi: " + fee.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("fee: " + new Money(fee));
            writer.WriteLine("relay-mass: " + relay.ToString(CultureInfo.InvariantCulture));
            var budget = tx.Inputs[0].ComputeBudget;
            writer.WriteLine("compute-budget: " + (budget is ushort budgetValue ? budgetValue.ToString(CultureInfo.InvariantCulture) : "none"));
            writer.WriteLine("id: " + tx.GetId());
            writer.WriteLine("hex: " + tx.ToHex());
            writer.WriteLine("json: " + json);

            if (tx.Version != version
                || fee != expectedFee
                || relay != expectedRelay
                || fee != MassCalculator.MinimumRelayFee(mass)
                || !roundTrip.GetId().Equals(tx.GetId())
                || !JsonMatches(json, version, tx.Inputs.Count))
            {
                writer.WriteLine("error: " + name + " did not match the minimum-relay shape");
                return false;
            }

            writer.WriteLine("verified: true");
            return true;
        }

        private static bool JsonMatches(string json, int version, int inputs)
        {
            using var document = JsonDocument.Parse(json);
            var array = document.RootElement.GetProperty("inputs");
            if (array.GetArrayLength() != inputs)
            {
                return false;
            }

            for (var i = 0; i < inputs; i++)
            {
                var input = array[i];
                if (version == 0)
                {
                    if (input.TryGetProperty("computeBudget", out _) || input.GetProperty("sigOpCount").GetInt32() != 1)
                    {
                        return false;
                    }
                }
                else if (input.GetProperty("sigOpCount").GetInt32() != 0 || input.GetProperty("computeBudget").GetInt32() != TransactionBuilder.DefaultComputeBudget)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// WARNING / 警告: returning 0 from this method is the only way <see cref="BroadcastAsync"/> runs.
        /// 这个方法返回 0，后面才会真正提交。缺助记词、缺地址或缺金额时返回 2，并且不开套接字。
        /// </summary>
        private static int RefuseBroadcast(string[] args, TextWriter error)
        {
            error.WriteLine("WARNING: --broadcast submits a signed transaction to a Kaspa node.");
            error.WriteLine("警告：--broadcast 会把已签名交易提交到 Kaspa 节点。");
            var words = Environment.GetEnvironmentVariable("NKASPA_MNEMONIC");
            if (string.IsNullOrWhiteSpace(words))
            {
                error.WriteLine("Refusing to broadcast: NKASPA_MNEMONIC is not set.");
                error.WriteLine("拒绝广播：没有设置环境变量 NKASPA_MNEMONIC。");
                return 2;
            }

            if (Option(args, "--to") == null || Option(args, "--amount") == null)
            {
                error.WriteLine("Refusing to broadcast: pass --to <address> and --amount <KAS>.");
                error.WriteLine("拒绝广播：还需要 --to 地址和 --amount KAS 数量。");
                return 2;
            }

            var version = Option(args, "--version") ?? "0";
            if (version != "0" && version != "1")
            {
                error.WriteLine("Refusing to broadcast: --version must be 0 or 1.");
                error.WriteLine("拒绝广播：--version 只能是 0 或 1。");
                return 2;
            }

            var networkName = Option(args, "--network") ?? "mainnet";
            if (!Network.TryGetNetwork(networkName, out _))
            {
                error.WriteLine("Refusing to broadcast: unknown --network.");
                error.WriteLine("拒绝广播：不认识这个 --network。");
                return 2;
            }

            return 0;
        }

        /// <summary>
        /// WARNING / 警告: this method calls <c>submitTransaction</c>.
        /// <see cref="RefuseBroadcast"/> must have returned 0. Do not call it from a test.
        /// 警告：这个方法会调用 submitTransaction。只有 RefuseBroadcast 返回 0 才进得来。测试不要调用它。
        /// </summary>
        private static async Task<int> BroadcastAsync(string[] args, TextWriter writer, TextWriter error)
        {
            var words = Environment.GetEnvironmentVariable("NKASPA_MNEMONIC");
            if (string.IsNullOrWhiteSpace(words))
            {
                error.WriteLine("Refusing to broadcast: NKASPA_MNEMONIC is not set.");
                return 2;
            }

            Mnemonic mnemonic;
            try
            {
                mnemonic = new Mnemonic(words, Wordlist.English);
            }
            catch (FormatException ex)
            {
                error.WriteLine("Refusing to broadcast: " + ex.Message);
                return 2;
            }

            if (!mnemonic.IsValidChecksum)
            {
                error.WriteLine("Refusing to broadcast: mnemonic checksum is invalid.");
                error.WriteLine("拒绝广播：助记词校验和不对。");
                return 2;
            }

            var networkName = Option(args, "--network") ?? "mainnet";
            if (!Network.TryGetNetwork(networkName, out var network) || network == null)
            {
                error.WriteLine("Refusing to broadcast: unknown --network.");
                return 2;
            }

            var versionText = Option(args, "--version") ?? "0";
            var version = versionText == "1" ? (ushort)1 : (ushort)0;
            KaspaAddress payee;
            try
            {
                payee = KaspaAddress.Parse(Option(args, "--to") ?? string.Empty, network);
            }
            catch (FormatException ex)
            {
                error.WriteLine("Refusing to broadcast: " + ex.Message);
                return 2;
            }

            if (!decimal.TryParse(Option(args, "--amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var kas))
            {
                error.WriteLine("Refusing to broadcast: --amount is not a KAS quantity.");
                error.WriteLine("拒绝广播：--amount 不是 KAS 数量。");
                return 2;
            }

            Money amount;
            try
            {
                amount = Money.Coins(kas);
            }
            catch (ArgumentException ex)
            {
                error.WriteLine("Refusing to broadcast: " + ex.Message);
                return 2;
            }

            var url = Option(args, "--url") ?? DefaultUrl;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "wss" && uri.Scheme != "ws"))
            {
                error.WriteLine("Refusing to broadcast: --url must be ws or wss.");
                return 2;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var master = mnemonic.DeriveExtKey();
            using var receive = master.Derive(KeyPath.KaspaReceive(0));
            using var change = master.Derive(KeyPath.KaspaChange(0));
            using var spendKey = receive.PrivateKey;
            using var changeKey = change.PrivateKey;
            var own = spendKey.GetAddress(network);
            var changeAddress = changeKey.GetAddress(network);
            var expectFrom = Option(args, "--from");
            if (expectFrom != null && !string.Equals(own.ToString(), expectFrom, StringComparison.Ordinal))
            {
                error.WriteLine("Refusing to broadcast: derived address does not match --from.");
                error.WriteLine("拒绝广播：派生地址和 --from 不一致。");
                error.WriteLine("derived: " + own);
                return 2;
            }

            try
            {
                await using var channel = new KaspaWebSocketChannel(uri);
                await channel.ConnectAsync(timeout.Token).ConfigureAwait(false);
                IKaspaRpcClient rpc = new KaspaWrpcJsonClient(channel);
                var listed = await rpc.GetUtxosByAddressesAsync(new[] { own.ToString() }, timeout.Token).ConfigureAwait(false);
                if (listed.Count == 0)
                {
                    error.WriteLine("No UTXO on " + own + ". Nothing was submitted.");
                    error.WriteLine("这个地址没有 UTXO，没有提交。");
                    return 1;
                }

                var coins = new Coin[listed.Count];
                for (var i = 0; i < listed.Count; i++)
                {
                    coins[i] = new Coin(listed[i].Outpoint, listed[i].Entry);
                }

                var builder = new TransactionBuilder(network).SetVersion(version).AddKeys(spendKey);
                if (Has(args, "--must-spend"))
                {
                    builder.MustSpend(coins);
                }
                else
                {
                    builder.AddCoins(coins);
                }

                var tx = builder.Send(payee, amount).SetChange(changeAddress).SendMinimumRelayFee().BuildTransaction(true);
                var entries = new UtxoEntry[tx.Inputs.Count];
                ulong inputSum = 0;
                for (var i = 0; i < tx.Inputs.Count; i++)
                {
                    Coin? spent = null;
                    for (var c = 0; c < coins.Length; c++)
                    {
                        if (coins[c].Outpoint.Equals(tx.Inputs[i].PreviousOutpoint))
                        {
                            spent = coins[c];
                            break;
                        }
                    }

                    if (spent == null)
                    {
                        error.WriteLine("Refusing to broadcast: an input is not a listed UTXO.");
                        error.WriteLine("拒绝广播：有一个输入不在查到的 UTXO 里。");
                        return 2;
                    }

                    entries[i] = spent.Utxo;
                    inputSum += spent.Amount.Sompi;
                }

                ulong outputSum = 0;
                var paid = false;
                for (var i = 0; i < tx.Outputs.Count; i++)
                {
                    var output = tx.Outputs[i];
                    outputSum += output.Value.Sompi;
                    if (output.Value.Sompi == amount.Sompi && output.ScriptPublicKey.Equals(payee.ScriptPubKey))
                    {
                        paid = true;
                    }
                }

                if (!paid || outputSum > inputSum)
                {
                    error.WriteLine("Refusing to broadcast: the payment output is missing or the amounts do not balance.");
                    error.WriteLine("拒绝广播：没有付给收款地址的金额，或者输入输出对不上。");
                    return 2;
                }

                var fee = inputSum - outputSum;
                var mass = MassCalculator.Calculate(tx, entries);
                var minimum = MassCalculator.MinimumRelayFee(mass);
                if (fee != minimum)
                {
                    error.WriteLine("Refusing to broadcast: fee is not the minimum relay fee.");
                    error.WriteLine("拒绝广播：手续费不是最低中继费。");
                    error.WriteLine("fee-sompi: " + fee.ToString(CultureInfo.InvariantCulture));
                    error.WriteLine("minimum-sompi: " + minimum.ToString(CultureInfo.InvariantCulture));
                    return 2;
                }

                var maxFeeText = Option(args, "--max-fee-sompi");
                if (maxFeeText != null)
                {
                    if (!ulong.TryParse(maxFeeText, NumberStyles.None, CultureInfo.InvariantCulture, out var maxFee))
                    {
                        error.WriteLine("Refusing to broadcast: --max-fee-sompi is not an integer.");
                        error.WriteLine("拒绝广播：--max-fee-sompi 不是整数。");
                        return 2;
                    }

                    if (fee > maxFee)
                    {
                        error.WriteLine("Refusing to broadcast: fee is above --max-fee-sompi.");
                        error.WriteLine("拒绝广播：手续费超过 --max-fee-sompi。");
                        error.WriteLine("fee-sompi: " + fee.ToString(CultureInfo.InvariantCulture));
                        return 2;
                    }
                }

                if (!tx.Verify(entries))
                {
                    error.WriteLine("Refusing to broadcast: Transaction.Verify failed.");
                    error.WriteLine("拒绝广播：Transaction.Verify 没有通过。");
                    return 2;
                }

                var json = KaspaRpcJson.WriteTransaction(tx);
                writer.WriteLine("from: " + own);
                writer.WriteLine("to: " + payee);
                writer.WriteLine("amount-sompi: " + amount.Sompi.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("amount: " + amount);
                writer.WriteLine("fee-sompi: " + fee.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("fee: " + new Money(fee));
                writer.WriteLine("verified: true");
                writer.WriteLine("id: " + tx.GetId());
                writer.WriteLine("hex: " + tx.ToHex());
                writer.WriteLine("json: " + json);
                error.WriteLine("WARNING: submitting now.");
                error.WriteLine("警告：现在提交交易。");
                var id = await rpc.SubmitTransactionAsync(tx, false, timeout.Token).ConfigureAwait(false);
                writer.WriteLine("submitted: " + id);
                return 0;
            }
            catch (Exception ex) when (ex is KaspaRpcException or WebSocketException or InvalidOperationException or FormatException or OperationCanceledException or TimeoutException)
            {
                error.WriteLine("error: " + ex.GetType().Name + ": " + ex.Message);
                error.WriteLine("Nothing further was submitted after this error.");
                return 1;
            }
        }

        private static Coin CoinOf(Key owner, ulong amount, ulong txId)
        {
            return new Coin(
                new OutPoint(new uint256(txId), 0),
                new UtxoEntry(amount, owner.PubKey.GetScriptPubKey(), 1, false));
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
