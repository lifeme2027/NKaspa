using System.Globalization;
using NKaspa;
using NKaspa.Policy;
using NKaspa.RPC;

namespace NKaspa.Samples.Tutorials
{
    /// <summary>
    /// Code from docs/tutorials/send-kaspa-transaction-dotnet.md.
    /// <see cref="RunOffline"/> does not open a socket.
    /// <see cref="SendOnTestnetAsync"/> is the broadcast path. Nothing in this sample calls it.
    /// </summary>
    public static class SendTutorial
    {
        private const string PayerSecret = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string PayeeSecret = "b7e151628aed2a6abf7158809cf4f3c762e7160f38b4da56a784d9045190cfef";

        public static int RunOffline(TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteLine("broadcast: no");
            using var payer = Key.Parse(PayerSecret);
            using var payeeKey = Key.Parse(PayeeSecret);
            var payee = payeeKey.GetAddress(Network.Mainnet);
            var change = payer.GetAddress(Network.Mainnet);
            var coin = new Coin(
                new OutPoint(new uint256(70), 0),
                new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));

            // Offline shape only. These mainnet address strings are not broadcast.
            var tx = new TransactionBuilder(Network.Mainnet)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee, Money.Coins(0.2m))
                .SetChange(change)
                .SendMinimumRelayFee()
                .BuildTransaction(true);

            var spent = new[] { coin.Utxo };
            bool inputOk = Script.VerifyScript(tx.Inputs[0].SignatureScript, coin.Utxo.ScriptPublicKey, tx, 0, spent);
            bool allOk = tx.Verify(spent);
            ulong outputSum = 0;
            var paid = false;
            for (var i = 0; i < tx.Outputs.Count; i++)
            {
                outputSum += tx.Outputs[i].Value.Sompi;
                if (tx.Outputs[i].Value.Sompi == Money.Coins(0.2m).Sompi && tx.Outputs[i].ScriptPublicKey.Equals(payee.ScriptPubKey))
                {
                    paid = true;
                }
            }

            var fee = 100_000_000UL - outputSum;
            var mass = MassCalculator.Calculate(tx, spent);
            var relay = MassCalculator.RelayMass(mass);
            writer.WriteLine("paid: " + (paid ? "true" : "false"));
            writer.WriteLine("input-ok: " + (inputOk ? "true" : "false"));
            writer.WriteLine("verified: " + (allOk ? "true" : "false"));
            writer.WriteLine("fee-sompi: " + fee.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("relay-mass: " + relay.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("floor: " + TransactionBuilder.EstimateFee(FeeRate.MinimumRelay, MassCalculator.StandardSchnorrPaymentMass));

            var left = new Coin(new OutPoint(new uint256(71), 0), new UtxoEntry(50_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var right = new Coin(new OutPoint(new uint256(72), 0), new UtxoEntry(50_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var forced = new TransactionBuilder(Network.Mainnet)
                .AddKeys(payer)
                .AddCoins(left, right)
                .MustSpend(left, right)
                .Send(payee, Money.Coins(0.2m))
                .SetChange(change)
                .SendMinimumRelayFee()
                .BuildTransaction(true);
            writer.WriteLine("must-spend-inputs: " + forced.Inputs.Count.ToString(CultureInfo.InvariantCulture));

            var version1 = new TransactionBuilder(Network.Mainnet)
                .SetVersion(1)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee, Money.Coins(0.1m))
                .SetChange(change)
                .SendMinimumRelayFee()
                .BuildTransaction(true);
            writer.WriteLine("version: " + version1.Version.ToString(CultureInfo.InvariantCulture));

            if (!paid || !inputOk || !allOk || fee != 203600UL || relay != 2036UL || forced.Inputs.Count != 2 || version1.Version != 1)
            {
                writer.WriteLine("error: offline payment did not match the minimum-relay shape");
                return 1;
            }

            writer.WriteLine("offline-ok");
            return 0;
        }

        /// <summary>
        /// WARNING: this method calls <c>SubmitTransactionAsync</c> when the environment is set.
        /// The network defaults to testnet. A mainnet override prints a warning and still requires
        /// <c>NKASPA_ALLOW_MAINNET=yes</c>. The mnemonic is read from <c>NKASPA_MNEMONIC</c>.
        /// Requires 0.1.11-alpha for <c>KaspaRpcClient</c>. The builder calls require 0.1.10-alpha
        /// where noted in the tutorial. This sample's <c>Main</c> does not call this method.
        /// </summary>
        public static async Task<int> SendOnTestnetAsync(TextWriter writer, TextWriter error, CancellationToken cancellationToken = default)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            error.WriteLine("WARNING: SubmitTransactionAsync broadcasts a signed transaction.");
            var words = Environment.GetEnvironmentVariable("NKASPA_MNEMONIC");
            if (string.IsNullOrWhiteSpace(words))
            {
                error.WriteLine("Refusing to broadcast: NKASPA_MNEMONIC is not set.");
                return 2;
            }

            var url = Environment.GetEnvironmentVariable("NKASPA_WRPC_URL");
            if (string.IsNullOrWhiteSpace(url))
            {
                error.WriteLine("Refusing to broadcast: NKASPA_WRPC_URL is not set. Use a testnet wRPC URL.");
                return 2;
            }

            var network = Network.Testnet;
            var networkName = Environment.GetEnvironmentVariable("NKASPA_NETWORK");
            if (!string.IsNullOrWhiteSpace(networkName))
            {
                if (!Network.TryGetNetwork(networkName, out var chosen) || chosen == null)
                {
                    error.WriteLine("Refusing to broadcast: unknown NKASPA_NETWORK.");
                    return 2;
                }

                network = chosen;
            }

            if (network == Network.Mainnet)
            {
                error.WriteLine("WARNING: mainnet spends real KAS. The tutorial default is testnet.");
                if (!string.Equals(Environment.GetEnvironmentVariable("NKASPA_ALLOW_MAINNET"), "yes", StringComparison.Ordinal))
                {
                    error.WriteLine("Refusing to broadcast: set NKASPA_ALLOW_MAINNET=yes to spend on mainnet.");
                    return 2;
                }
            }

            var mnemonic = new Mnemonic(words, Wordlist.English);
            if (!mnemonic.IsValidChecksum)
            {
                error.WriteLine("Refusing to broadcast: mnemonic checksum is invalid.");
                return 2;
            }

            var to = Environment.GetEnvironmentVariable("NKASPA_TO");
            var amountText = Environment.GetEnvironmentVariable("NKASPA_AMOUNT");
            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(amountText))
            {
                error.WriteLine("Refusing to broadcast: set NKASPA_TO and NKASPA_AMOUNT.");
                return 2;
            }

            var payee = KaspaAddress.Parse(to, network);
            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var kas))
            {
                error.WriteLine("Refusing to broadcast: NKASPA_AMOUNT is not a KAS quantity.");
                return 2;
            }

            var amount = Money.Coins(kas);
            using var master = mnemonic.DeriveExtKey();
            using var receive = master.Derive(KeyPath.KaspaReceive(0));
            using var changeKey = master.Derive(KeyPath.KaspaChange(0));
            using var spendKey = receive.PrivateKey;
            var own = spendKey.GetAddress(network);
            var changeAddress = changeKey.PrivateKey.GetAddress(network);

            // Requires 0.1.11-alpha. The path selects JSON or Borsh. Create does not connect.
            await using var rpc = KaspaRpcClient.Create(url);
            await rpc.ConnectAsync(cancellationToken).ConfigureAwait(false);
            var listed = await rpc.GetUtxosByAddressesAsync(new[] { own.ToString() }, cancellationToken).ConfigureAwait(false);
            if (listed.Count == 0)
            {
                error.WriteLine("No UTXO on " + own + ". Nothing was submitted.");
                return 1;
            }

            var coins = new Coin[listed.Count];
            for (var i = 0; i < listed.Count; i++)
            {
                coins[i] = new Coin(listed[i].Outpoint, listed[i].Entry);
            }

            var builder = new TransactionBuilder(network).AddKeys(spendKey).AddCoins(coins);
            var tx = builder.Send(payee, amount).SetChange(changeAddress).SendMinimumRelayFee().BuildTransaction(true);
            var entries = new UtxoEntry[tx.Inputs.Count];
            ulong inputSum = 0;
            for (var i = 0; i < tx.Inputs.Count; i++)
            {
                Coin? spentCoin = null;
                for (var c = 0; c < coins.Length; c++)
                {
                    if (coins[c].Outpoint.Equals(tx.Inputs[i].PreviousOutpoint))
                    {
                        spentCoin = coins[c];
                        break;
                    }
                }

                if (spentCoin == null)
                {
                    error.WriteLine("Refusing to broadcast: an input is not a listed UTXO.");
                    return 2;
                }

                entries[i] = spentCoin.Utxo;
                inputSum += spentCoin.Amount.Sompi;
            }

            ulong outputSum = 0;
            var paid = false;
            for (var i = 0; i < tx.Outputs.Count; i++)
            {
                outputSum += tx.Outputs[i].Value.Sompi;
                if (tx.Outputs[i].Value.Sompi == amount.Sompi && tx.Outputs[i].ScriptPublicKey.Equals(payee.ScriptPubKey))
                {
                    paid = true;
                }
            }

            if (!paid || outputSum > inputSum || !tx.Verify(entries))
            {
                error.WriteLine("Refusing to broadcast: verification failed. Nothing was submitted.");
                return 2;
            }

            var fee = inputSum - outputSum;
            var minimum = MassCalculator.MinimumRelayFee(MassCalculator.Calculate(tx, entries));
            if (fee != minimum)
            {
                error.WriteLine("Refusing to broadcast: fee is not the minimum relay fee.");
                return 2;
            }

            writer.WriteLine("folded-sompi: " + builder.ChangeFoldedIntoFee.Sompi.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("network: " + network.Name);
            writer.WriteLine("encoding: " + rpc.Encoding);
            writer.WriteLine("fee-sompi: " + fee.ToString(CultureInfo.InvariantCulture));
            var id = await rpc.SubmitTransactionAsync(tx, false, cancellationToken).ConfigureAwait(false);
            writer.WriteLine("submitted: " + id);
            return 0;
        }
    }
}
