using System.Text.Json;
using NKaspa;
using NKaspa.JsonConverters;
using KaspaPskt = NKaspa.PSKT.PSKT;

namespace NKaspa.Samples.Json
{
    /// <summary>
    /// Prints compact wRPC JSON for the public Kaspa types and reads it back.
    /// Private keys are refused. Nothing is broadcast.
    /// 打印公开类型的紧凑 wRPC JSON 并读回。私钥被拒绝。不广播。
    /// </summary>
    public static class Program
    {
        // Public test scalar already used by the unit tests. Not a wallet.
        // 与单元测试相同的公开标量，不是钱包。
        private const string Secret = "0000000000000000000000000000000000000000000000000000000000000003";

        public static int Main(string[] args)
        {
            return Run(args, Console.Out);
        }

        /// <summary>Arguments are ignored. The sample prints one fixed document. 参数被忽略。</summary>
        public static int Run(string[] args, TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            _ = args;
            using var key = Key.Parse(Secret);
            var address = key.PubKey.GetAddress(Network.Mainnet);
            var script = address.ScriptPubKey;
            var hash = new uint256(1);
            var money = Money.Coins(0.2m);
            var outpoint = new OutPoint(hash, 2);
            var utxo = new UtxoEntry(money, script, 474165565UL, false);
            var coin = new Coin(outpoint, utxo);
            var input = new TxIn(outpoint, Script.Empty, 0, 1);
            var output = new TxOut(money, script);
            var version0 = new Transaction(0, new[] { input }, new[] { output }, 0, SubnetworkId.Native, 0, Array.Empty<byte>());
            var version1Input = TxIn.WithComputeBudget(outpoint, Script.Empty, 0, 10);
            var version1Output = new TxOut(Money.Coins(0.1m), script);
            version1Output.Covenant = new CovenantBinding(0, hash);
            var version1 = new Transaction(1, new[] { version1Input }, new[] { version1Output }, 0, SubnetworkId.Native, 0, Array.Empty<byte>());
            var pskt = new TransactionBuilder()
                .AddCoins(coin)
                .Send(address, Money.Coins(0.1m))
                .SetChange(address)
                .SendFees(new Money(203600))
                .BuildPSKT();

            var options = new JsonSerializerOptions();
            options.AddNKaspaConverters(Network.Mainnet);
            writer.WriteLine("money: " + JsonSerializer.Serialize(money, options));
            writer.WriteLine("uint256: " + NKaspaJsonSerializer.Serialize(hash, Network.Mainnet));
            writer.WriteLine("address: " + NKaspaJsonSerializer.Serialize(address, Network.Mainnet));
            writer.WriteLine("address-network: " + NKaspaJsonSerializer.Deserialize<KaspaAddress>(NKaspaJsonSerializer.Serialize(address, Network.Mainnet), Network.Mainnet).Network.Name);
            writer.WriteLine("network: " + NKaspaJsonSerializer.Serialize(Network.Mainnet, Network.Mainnet));
            writer.WriteLine("script: " + NKaspaJsonSerializer.Serialize(script.ToScript(), Network.Mainnet));
            writer.WriteLine("script-pubkey: " + NKaspaJsonSerializer.Serialize(script, Network.Mainnet));
            var prefixed = "\"0000" + script.ToHex() + "\"";
            var readPrefixed = NKaspaJsonSerializer.Deserialize<ScriptPubKey>(prefixed, Network.Mainnet);
            writer.WriteLine("script-prefixed: " + (readPrefixed.Equals(script) ? "true" : "false"));
            writer.WriteLine("outpoint: " + NKaspaJsonSerializer.Serialize(outpoint, Network.Mainnet));
            writer.WriteLine("tx-version-0: " + NKaspaJsonSerializer.Serialize(version0, Network.Mainnet));
            writer.WriteLine("tx-version-1: " + NKaspaJsonSerializer.Serialize(version1, Network.Mainnet));
            var back = NKaspaJsonSerializer.Deserialize<Transaction>(NKaspaJsonSerializer.Serialize(version1, Network.Mainnet), Network.Mainnet);
            writer.WriteLine("compute-budget: " + back.Inputs[0].ComputeBudget);
            writer.WriteLine("covenant: " + (back.Outputs[0].Covenant != null ? "true" : "false"));
            writer.WriteLine("utxo: " + NKaspaJsonSerializer.Serialize(utxo, Network.Mainnet));
            writer.WriteLine("coin: " + NKaspaJsonSerializer.Serialize(coin, Network.Mainnet));
            writer.WriteLine("pubkey: " + NKaspaJsonSerializer.Serialize(key.PubKey, Network.Mainnet));
            writer.WriteLine("ecdsa: " + NKaspaJsonSerializer.Serialize(key.PubKeyECDSA, Network.Mainnet));
            var mnemonic = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", Wordlist.English);
            using (var master = mnemonic.DeriveExtKey())
            {
                writer.WriteLine("kpub: " + NKaspaJsonSerializer.Serialize(master.Neuter(), Network.Mainnet));
                try
                {
                    NKaspaJsonSerializer.Serialize(master, Network.Mainnet);
                    writer.WriteLine("extkey-json: leaked");
                    return 1;
                }
                catch (InvalidOperationException)
                {
                    writer.WriteLine("extkey-json: refused");
                }
            }

            try
            {
                NKaspaJsonSerializer.Serialize(mnemonic, Network.Mainnet);
                writer.WriteLine("mnemonic-json: leaked");
                return 1;
            }
            catch (InvalidOperationException)
            {
                writer.WriteLine("mnemonic-json: refused");
            }

            writer.WriteLine("feerate: " + NKaspaJsonSerializer.Serialize(FeeRate.MinimumRelay, Network.Mainnet));
            writer.WriteLine("pskt: " + (NKaspaJsonSerializer.Deserialize<KaspaPskt>(pskt.ToJson(), Network.Mainnet).ToJson() == pskt.ToJson() ? "true" : "false"));
            try
            {
                NKaspaJsonSerializer.Serialize(key, Network.Mainnet);
                writer.WriteLine("private-key: leaked");
                return 1;
            }
            catch (InvalidOperationException exception)
            {
                writer.WriteLine("private-key: refused");
                writer.WriteLine("warning: " + (exception.Message.Contains("WARNING", StringComparison.Ordinal) ? "true" : "false"));
            }

            writer.WriteLine("ok");
            return 0;
        }
    }
}
