using System.Text.Json;
using NKaspa;
using NKaspa.DataEncoders;
using NKaspa.JsonConverters;
using KaspaPskt = NKaspa.PSKT.PSKT;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Round trips for the public JSON converters, rejected input, and the three mainnet
    /// transactions accepted on 2026-10-30. The fixtures are wRPC objects. The fields were
    /// read from the public explorer API. Nothing here is broadcast.
    /// </summary>
    public class JsonConverterTests
    {
        private const string Secret = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string Abandon = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string Payee = "kaspa:qzgvjx98kppf3vwurqs4p6c3569myqdejr45zksyj2998dxeds4rq7xdwnq74";
        private const string Change = "kaspa:qp8j3wvjlfm47zllp8e8jj0e8ah5nlpqmk92xnv2dajg0hj4vlxqzsm25e2cx";
        private const string PayeeScript = "2090c918a7b04298b1dc182150eb11a68bb201b990eb415a04928a53b4d96c2a30ac";
        private const string ChangeScript = "204f28b992fa775f0bff09f27949f93f6f49fc20dd8aa34d8a6f6487de5567cc01ac";

        [Fact]
        public void Each_public_type_round_trips_and_transaction_json_matches_the_rpc_writer()
        {
            using var key = Key.Parse(Secret);
            var pub = key.PubKey;
            var ecdsa = key.PubKeyECDSA;
            var address = pub.GetAddress(Network.Mainnet);
            var script = pub.GetScriptPubKey();
            var hash = new uint256(1);
            var money = new Money(9007199254740993UL);
            var outpoint = new OutPoint(hash, 2);
            var utxo = new UtxoEntry(money, script, 541054755UL, true, new uint256(9));
            var coin = new Coin(outpoint, utxo);
            var signature = Script.Parse("41" + new string('a', 130));
            var input = new TxIn(outpoint, signature, 4, 1);
            var output = new TxOut(money, script);
            var version0 = new Transaction(0, new[] { input }, new[] { output }, 8, SubnetworkId.Native, 0, new byte[] { 1, 2, 3 });
            version0.StorageMass = 2017;

            var budget = TxIn.WithComputeBudget(outpoint, signature, 4, 10);
            var version1Output = new TxOut(money, new ScriptPubKey(0xc0de, script.Script));
            version1Output.Covenant = new CovenantBinding(0, new uint256(7));
            var version1 = new Transaction(1, new[] { budget }, new[] { version1Output }, 0, SubnetworkId.Native, 0, Array.Empty<byte>());
            version1.StorageMass = 152080;

            Assert.Equal(money, RoundTrip(money));
            Assert.Equal("9007199254740993", NKaspaJsonSerializer.Serialize(money, Network.Mainnet));
            Assert.Equal(money, NKaspaJsonSerializer.Deserialize<Money>("\"9007199254740993\"", Network.Mainnet));
            Assert.Equal(hash, RoundTrip(hash));
            Assert.Equal(address.ToString(), RoundTrip<KaspaAddress>(address).ToString());
            Assert.Equal(Network.Testnet, NKaspaJsonSerializer.Deserialize<Network>("{\"networkType\":\"testnet\",\"suffix\":10}", Network.Mainnet));
            Assert.Equal("\"testnet\"", NKaspaJsonSerializer.Serialize(Network.Testnet, Network.Mainnet));
            Assert.Equal(signature.ToHex(), RoundTrip(signature).ToHex());
            Assert.Equal(script, RoundTrip(script));
            var prefixed = "\"c0de" + Encoders.Hex.EncodeData(script.Script) + "\"";
            var fromPrefixed = NKaspaJsonSerializer.Deserialize<ScriptPubKey>(prefixed, Network.Mainnet);
            Assert.Equal((ushort)0xc0de, fromPrefixed.Version);
            Assert.Equal(script.Script, fromPrefixed.Script);
            var alias = "{\"version\":0,\"scriptPublicKey\":\"" + script.ToHex() + "\"}";
            Assert.Equal(script, NKaspaJsonSerializer.Deserialize<ScriptPubKey>(alias, Network.Mainnet));
            Assert.Equal(outpoint.ToString(), RoundTrip(outpoint).ToString());
            Assert.Equal(outpoint.Index, NKaspaJsonSerializer.Deserialize<OutPoint>("\"" + hash + ":2\"", Network.Mainnet).Index);
            AssertSameInput(input, RoundTrip(input));
            Assert.Null(RoundTrip(input).ComputeBudget);
            AssertSameInput(budget, RoundTrip(budget));
            Assert.Equal((ushort)10, RoundTrip(budget).ComputeBudget);
            AssertSameOutput(output, RoundTrip(output));
            AssertSameOutput(version1Output, RoundTrip(version1Output));
            Assert.Equal(version1Output.Covenant!.CovenantId, RoundTrip(version1Output.Covenant).CovenantId);
            AssertSameTransaction(version0, RoundTrip(version0));
            AssertSameTransaction(version1, RoundTrip(version1));
            Assert.Equal(KaspaRpcJson.WriteTransaction(version0), NKaspaJsonSerializer.Serialize(version0, Network.Mainnet));
            Assert.Equal(KaspaRpcJson.WriteTransaction(version1), NKaspaJsonSerializer.Serialize(version1, Network.Mainnet));
            Assert.Contains("\"computeBudget\":10", NKaspaJsonSerializer.Serialize(version1, Network.Mainnet), StringComparison.Ordinal);
            Assert.DoesNotContain("computeBudget", NKaspaJsonSerializer.Serialize(version0, Network.Mainnet), StringComparison.Ordinal);
            AssertSameUtxo(utxo, RoundTrip(utxo));
            Assert.Equal(coin.Outpoint.ToString(), RoundTrip(coin).Outpoint.ToString());
            AssertSameUtxo(coin.Utxo, RoundTrip(coin).Utxo);
            Assert.Equal(pub.ToString(), RoundTrip(pub).ToString());
            Assert.Equal(ecdsa.ToBytes(), RoundTrip(ecdsa).ToBytes());
            Assert.Equal(FeeRate.MinimumRelay.SompiPerGram, RoundTrip(FeeRate.MinimumRelay).SompiPerGram);
            Assert.Equal(100UL, NKaspaJsonSerializer.Deserialize<FeeRate>("\"100\"", Network.Mainnet).SompiPerGram);

            var mnemonic = new Mnemonic(Abandon, Wordlist.English);
            using var master = mnemonic.DeriveExtKey();
            var extended = master.Neuter();
            var extendedJson = NKaspaJsonSerializer.Serialize(extended, Network.Mainnet);
            Assert.StartsWith("\"kpub", extendedJson, StringComparison.Ordinal);
            Assert.Equal(extended, NKaspaJsonSerializer.Deserialize<ExtPubKey>(extendedJson, Network.Mainnet));
            Assert.StartsWith("\"ktub", NKaspaJsonSerializer.Serialize(extended, Network.Testnet), StringComparison.Ordinal);

            var psktCoin = new Coin(new OutPoint(hash, 3), new UtxoEntry(Money.Coins(0.2m), script, 1, false));
            var pskt = new TransactionBuilder()
                .AddCoins(psktCoin)
                .Send(address, Money.Coins(0.1m))
                .SetChange(address)
                .SendFees(new Money(203600))
                .BuildPSKT();
            Assert.Equal(pskt.ToJson(), NKaspaJsonSerializer.Serialize(pskt, Network.Mainnet));
            Assert.Equal(pskt.ToJson(), RoundTrip(pskt).ToJson());
            Assert.Equal(KaspaPskt.Create().ToJson(), RoundTrip(KaspaPskt.Create()).ToJson());

            var options = new JsonSerializerOptions();
            options.AddNKaspaConverters(Network.Mainnet);
            var quote = new PaymentQuote { Amount = Money.Coins(1), Payee = address };
            var quoteJson = JsonSerializer.Serialize(quote, options);
            Assert.Contains("\"amount\":100000000", quoteJson, StringComparison.Ordinal);
            Assert.Contains("\"payee\":\"" + address + "\"", quoteJson, StringComparison.Ordinal);
            var quoteBack = JsonSerializer.Deserialize<PaymentQuote>(quoteJson, options);
            Assert.Equal(quote.Amount, quoteBack!.Amount);
            Assert.Equal(address.ToString(), quoteBack.Payee!.ToString());

            var indented = NKaspaJsonSerializer.Serialize(version0, Network.Mainnet, writeIndented: true);
            Assert.Contains("\n", indented, StringComparison.Ordinal);
            AssertSameTransaction(version0, NKaspaJsonSerializer.Deserialize<Transaction>(indented, Network.Mainnet));
        }

        [Fact]
        public void Accepted_mainnet_transactions_round_trip_through_wrpc_json()
        {
            // Public explorer fields from 2026-10-30, rewritten as wRPC objects.
            // lockTime, sequence, and gas were 0. Explorer "mass" is relay mass, not storageMass, so it is omitted.
            // 2026-10-30 公共浏览器字段，改写成 wRPC。lockTime、sequence、gas 为 0。浏览器的 mass 是中继 mass，不是 storageMass。
            AssertMainnet(Version0(SingleInput(), 366_623_000), "5fd8f8600950c36b1d9195d9afab0bbf5bde94594fd8246d2c2419c64f703868", 0, null, 20_000_000, 366_623_000);
            AssertMainnet(Version0(TwoInputs(), 49_684_600), "2febe6cafd5f78f8ce21636d48f060560b39a7529b00646a00cf5d62098aa3d4", 0, null, 20_000_000, 49_684_600);
            AssertMainnet(Version1(), "4d1c6a5e1b127278945673b3ed116786110b2b1ca44090c9f810a50e12350714", 1, 10, 10_000_000, 9_796_200);

            var prefixed = Version0(SingleInput(), 366_623_000).Replace("{\"version\":0,\"script\":\"" + PayeeScript + "\"}", "\"0000" + PayeeScript + "\"", StringComparison.Ordinal);
            prefixed = prefixed.Replace("\"index\":2", "\"index\":\"2\"", StringComparison.Ordinal);
            prefixed = prefixed.Replace("\"value\":20000000", "\"amount\":\"20000000\"", StringComparison.Ordinal);
            var fromPrefixed = NKaspaJsonSerializer.Deserialize<Transaction>(prefixed, Network.Mainnet);
            Assert.Equal("5fd8f8600950c36b1d9195d9afab0bbf5bde94594fd8246d2c2419c64f703868", fromPrefixed.GetId().ToString());
            Assert.Equal(KaspaAddress.Parse(Payee).ScriptPubKey, fromPrefixed.Outputs[0].ScriptPublicKey);
        }

        [Fact]
        public void Malformed_values_are_rejected_and_private_keys_stay_out_of_json()
        {
            Reject<Money>("-1");
            Reject<Money>("1.5");
            Reject<Money>("\"1 KAS\"");
            Reject<Money>("\"-1\"");
            Reject<uint256>("\"abcd\"");
            Reject<uint256>("1");
            Reject<KaspaAddress>("\"kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4l\"");
            Reject<KaspaAddress>("\"kaspatest:qypt5q0uta8fmxretxwxng76lkp45uj7tuhfxn5nytkd8tcep2c0vrsxarh9t02\"");
            Reject<Network>("\"regtest\"");
            Reject<Script>("\"zz\"");
            Reject<Script>("\"0\"");
            Reject<ScriptPubKey>("\"00\"");
            Reject<ScriptPubKey>("{}");
            Reject<OutPoint>("{\"transactionId\":\"" + uint256.Zero + "\"}");
            Reject<Transaction>("{\"inputs\":[],\"outputs\":[]}");
            Reject<FeeRate>("1.5");
            Reject<PubKey>("\"00\"");
            Reject<KaspaPskt>("[]");
            Reject<UtxoEntry>("{\"amount\":1,\"scriptPublicKey\":\"00\",\"blockDaaScore\":0,\"isCoinbase\":false}");

            using var key = Key.Parse(Secret);
            var refused = Assert.Throws<InvalidOperationException>(() => NKaspaJsonSerializer.Serialize(key, Network.Mainnet));
            Assert.Contains("WARNING", refused.Message, StringComparison.Ordinal);
            Assert.Contains("allowPrivateKeys", refused.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, refused.Message, StringComparison.Ordinal);

            var mnemonic = new Mnemonic(Abandon, Wordlist.English);
            using var master = mnemonic.DeriveExtKey();
            var refusedExtended = Assert.Throws<InvalidOperationException>(() => NKaspaJsonSerializer.Serialize(master, Network.Mainnet));
            Assert.Contains("WARNING", refusedExtended.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("kprv", refusedExtended.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(master.ToString(Network.Mainnet), refusedExtended.Message, StringComparison.Ordinal);

            var secretJson = NKaspaJsonSerializer.Serialize(key, Network.Mainnet, writeIndented: false, allowPrivateKeys: true);
            Assert.Equal(Secret, NKaspaJsonSerializer.Deserialize<Key>(secretJson, Network.Mainnet, allowPrivateKeys: true).ToHex());
            Assert.Throws<InvalidOperationException>(() => NKaspaJsonSerializer.Deserialize<Key>(secretJson, Network.Mainnet));

            var extendedJson = NKaspaJsonSerializer.Serialize(master, Network.Mainnet, writeIndented: false, allowPrivateKeys: true);
            Assert.StartsWith("\"kprv", extendedJson, StringComparison.Ordinal);
            using var parsed = NKaspaJsonSerializer.Deserialize<ExtKey>(extendedJson, Network.Mainnet, allowPrivateKeys: true);
            Assert.Equal(master, parsed);
            Assert.Throws<InvalidOperationException>(() => NKaspaJsonSerializer.Deserialize<ExtKey>(extendedJson, Network.Mainnet));
            Assert.Throws<FormatException>(() => NKaspaJsonSerializer.Deserialize<ExtKey>(extendedJson, Network.Testnet, allowPrivateKeys: true));

            var mismatched = KaspaRpcJson.WriteTransaction(new Transaction()).Replace("\"storageMass\":0", "\"storageMass\":1", StringComparison.Ordinal);
            Assert.Throws<FormatException>(() => NKaspaJsonSerializer.Deserialize<Transaction>(mismatched, Network.Mainnet));
            Assert.Throws<ArgumentNullException>(() => NKaspaJsonSerializer.Deserialize<Money>(null!, Network.Mainnet));
            Assert.Throws<ArgumentNullException>(() => new JsonSerializerOptions().AddNKaspaConverters(null!));
            Assert.Throws<FormatException>(() => NKaspaJsonSerializer.Deserialize<Money>("null", Network.Mainnet));
            Assert.Throws<JsonException>(() => NKaspaJsonSerializer.Deserialize<Transaction>("{", Network.Mainnet));
        }

        private static void AssertMainnet(string json, string id, ushort version, int? budget, ulong payee, ulong change)
        {
            var transaction = NKaspaJsonSerializer.Deserialize<Transaction>(json, Network.Mainnet);
            Assert.Equal(id, transaction.GetId().ToString());
            Assert.Equal(version, transaction.Version);
            Assert.Equal(payee, transaction.Outputs[0].Value.Sompi);
            Assert.Equal(change, transaction.Outputs[1].Value.Sompi);
            Assert.Equal(KaspaAddress.Parse(Payee).ScriptPubKey, transaction.Outputs[0].ScriptPublicKey);
            Assert.Equal(KaspaAddress.Parse(Change).ScriptPubKey, transaction.Outputs[1].ScriptPublicKey);
            Assert.Equal(66, transaction.Inputs[0].SignatureScript.Length);
            int? actualBudget = transaction.Inputs[0].ComputeBudget is ushort budgetValue ? budgetValue : null;
            Assert.Equal(budget, actualBudget);
            Assert.Equal(SubnetworkId.Native, transaction.SubnetworkId);
            Assert.Equal(0UL, transaction.LockTime);
            Assert.Empty(transaction.Payload);
            var written = NKaspaJsonSerializer.Serialize(transaction, Network.Mainnet);
            Assert.Equal(KaspaRpcJson.WriteTransaction(transaction), written);
            AssertSameTransaction(transaction, NKaspaJsonSerializer.Deserialize<Transaction>(written, Network.Mainnet));
            if (version >= 1)
            {
                Assert.Contains("\"computeBudget\":10", written, StringComparison.Ordinal);
                Assert.Contains("\"sigOpCount\":0", written, StringComparison.Ordinal);
            }
            else
            {
                Assert.DoesNotContain("computeBudget", written, StringComparison.Ordinal);
                Assert.Equal(1, transaction.Inputs[0].SigOpCount);
            }
        }

        private static string Version0(string inputs, ulong change)
        {
            return "{\"version\":0,\"inputs\":[" + inputs + "],\"outputs\":[" + Output(20_000_000, PayeeScript) + "," + Output(change, ChangeScript) + "],\"lockTime\":0,\"subnetworkId\":\"0000000000000000000000000000000000000000\",\"gas\":0,\"payload\":\"\"}";
        }

        private static string Version1()
        {
            var input = Input("b301df292d1490eb6b7a1d06e0a9b198fc3a13068f7a7000b57a95c37c320d29", 0, "4197920225ff11fd3a62908a07b74dfad8458526c71436c2d9f1cbe7b9db4c13d62d60e0481a03e7dcf52f5e31078924afb7862fa13ee4810f897a6fbeebee2dc901", 0, true);
            return "{\"version\":1,\"inputs\":[" + input + "],\"outputs\":[" + Output(10_000_000, PayeeScript) + "," + Output(9_796_200, ChangeScript) + "],\"lockTime\":0,\"subnetworkId\":\"0000000000000000000000000000000000000000\",\"gas\":0,\"payload\":\"\"}";
        }

        private static string SingleInput()
        {
            return Input("ce6f1729664e4bdbd13e6a997492ab7fb2453f7eb60c15776ea353ce332b6a42", 2, "41ed3d9c392db85ea72d5ed0835f1022113c1f2a9b0980a5f64438562c1030a84c9b5f88f37438c5829800c014552fe927894db3376530e8d1025d6f5e2467308b01", 1, false);
        }

        private static string TwoInputs()
        {
            return Input("133378c14ef50b9320dfefecad2a09e027667083db945d2d80672450e89edc05", 0, "41c98116b502f57fa44240c8a9aa4c6e27e36c377e97d1eddd675fb061db30a3a1bf53cdc7c050adf20dc85da8bfcb21b9d16a8440015ba51b02d12eeff95a54f401", 1, false)
                + ","
                + Input("b505f64a0f36f86c203768e22c0bf252e94045fb292ae40aae7ecc631f6b4342", 0, "41bfd9b52c07c65abdd79bc5c33bdc57ad7482773112c746dabf4e1fb747b4f5eaa572f001b74b7f24650b3c6f711d73620b6119d9429e5e6783bd973a732ba72301", 1, false);
        }

        private static string Input(string id, uint index, string signature, int sigOp, bool budget)
        {
            var text = "{\"previousOutpoint\":{\"transactionId\":\"" + id + "\",\"index\":" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "},\"signatureScript\":\"" + signature + "\",\"sequence\":0,\"sigOpCount\":" + sigOp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (budget)
            {
                text += ",\"computeBudget\":10";
            }

            return text + "}";
        }

        private static string Output(ulong amount, string script)
        {
            return "{\"value\":" + amount.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"scriptPublicKey\":{\"version\":0,\"script\":\"" + script + "\"}}";
        }

        private static T RoundTrip<T>(T value)
        {
            var json = NKaspaJsonSerializer.Serialize(value, Network.Mainnet);
            return NKaspaJsonSerializer.Deserialize<T>(json, Network.Mainnet);
        }

        private static void Reject<T>(string json)
        {
            Assert.Throws<FormatException>(() => NKaspaJsonSerializer.Deserialize<T>(json, Network.Mainnet));
        }

        private static void AssertSameTransaction(Transaction expected, Transaction actual)
        {
            Assert.Equal(expected.GetId(), actual.GetId());
            Assert.Equal(expected.GetHash(), actual.GetHash());
            Assert.Equal(expected.Version, actual.Version);
            Assert.Equal(expected.LockTime, actual.LockTime);
            Assert.Equal(expected.Gas, actual.Gas);
            Assert.Equal(expected.StorageMass, actual.StorageMass);
            Assert.Equal(expected.Payload, actual.Payload);
            Assert.Equal(expected.SubnetworkId, actual.SubnetworkId);
            Assert.Equal(expected.Inputs.Count, actual.Inputs.Count);
            Assert.Equal(expected.Outputs.Count, actual.Outputs.Count);
            for (var i = 0; i < expected.Inputs.Count; i++)
            {
                AssertSameInput(expected.Inputs[i], actual.Inputs[i]);
            }

            for (var i = 0; i < expected.Outputs.Count; i++)
            {
                AssertSameOutput(expected.Outputs[i], actual.Outputs[i]);
            }
        }

        private static void AssertSameInput(TxIn expected, TxIn actual)
        {
            Assert.Equal(expected.PreviousOutpoint.ToString(), actual.PreviousOutpoint.ToString());
            Assert.Equal(expected.SignatureScript.ToHex(), actual.SignatureScript.ToHex());
            Assert.Equal(expected.Sequence, actual.Sequence);
            Assert.Equal(expected.SigOpCount, actual.SigOpCount);
            Assert.Equal(expected.ComputeBudget, actual.ComputeBudget);
        }

        private static void AssertSameOutput(TxOut expected, TxOut actual)
        {
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.ScriptPublicKey, actual.ScriptPublicKey);
            if (expected.Covenant == null)
            {
                Assert.Null(actual.Covenant);
                return;
            }

            Assert.Equal(expected.Covenant.AuthorizingInput, actual.Covenant!.AuthorizingInput);
            Assert.Equal(expected.Covenant.CovenantId, actual.Covenant.CovenantId);
        }

        private static void AssertSameUtxo(UtxoEntry expected, UtxoEntry actual)
        {
            Assert.Equal(expected.Amount, actual.Amount);
            Assert.Equal(expected.ScriptPublicKey, actual.ScriptPublicKey);
            Assert.Equal(expected.BlockDaaScore, actual.BlockDaaScore);
            Assert.Equal(expected.IsCoinbase, actual.IsCoinbase);
            Assert.Equal(expected.CovenantId, actual.CovenantId);
        }

        private sealed class PaymentQuote
        {
            public Money Amount { get; set; }

            public KaspaAddress? Payee { get; set; }
        }
    }
}
