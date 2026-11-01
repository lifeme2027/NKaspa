using NKaspa;
using AddressSample = NKaspa.Samples.Address.Program;
using JsonSample = NKaspa.Samples.Json.Program;
using MnemonicSample = NKaspa.Samples.Mnemonic.Program;
using PsktSample = NKaspa.Samples.PsktMultisig.Program;
using SignSample = NKaspa.Samples.SignMessage.Program;
using SpendSample = NKaspa.Samples.Spend.Program;
using AddressTutorial = NKaspa.Samples.Tutorials.AddressTutorial;
using SendTutorial = NKaspa.Samples.Tutorials.SendTutorial;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Runs the offline samples in-process. NodeQuery is not included: it opens a public socket.
    /// Spend is included only on the path that does not broadcast.
    /// 进程内跑离线示例。NodeQuery 要连公共节点，不在这里跑。Spend 只跑不广播的路径。
    /// </summary>
    public class SampleSmokeTests
    {
        private const string Abandon = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Fact]
        public void Mnemonic_sample_derives_mainnet_and_testnet_from_a_known_sentence()
        {
            var writer = new StringWriter();
            var code = MnemonicSample.Run(Abandon.Split(' '), writer);
            var text = writer.ToString();
            Assert.Equal(0, code);
            Assert.Contains("path[0]: m/44'/111111'/0'/0/0", text, StringComparison.Ordinal);
            Assert.Contains("path[2]: m/44'/111111'/0'/0/2", text, StringComparison.Ordinal);
            Assert.Contains("kpub: kpub", text, StringComparison.Ordinal);
            Assert.Contains("ktub: ktub", text, StringComparison.Ordinal);
            Assert.Contains("mnemonic: " + Abandon, text, StringComparison.Ordinal);
            Assert.DoesNotContain("mnemonic: NKaspa.Mnemonic", text, StringComparison.Ordinal);
            Assert.Contains("\nok", text, StringComparison.Ordinal);

            var mnemonic = new Mnemonic(Abandon, Wordlist.English);
            using var master = mnemonic.DeriveExtKey();
            using var child = master.Derive(KeyPath.KaspaReceive(0));
            using var key = child.PrivateKey;
            Assert.Contains("mainnet[0]: " + key.GetAddress(Network.Mainnet), text, StringComparison.Ordinal);
            Assert.Contains("testnet[0]: " + key.GetAddress(Network.Testnet), text, StringComparison.Ordinal);
            Assert.DoesNotContain("kprv", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Mnemonic_sample_generates_a_sentence_when_none_is_passed()
        {
            var writer = new StringWriter();
            var code = MnemonicSample.Run(Array.Empty<string>(), writer);
            var text = writer.ToString();
            Assert.Equal(0, code);
            Assert.Contains("kaspa:", text, StringComparison.Ordinal);
            Assert.Contains("kaspatest:", text, StringComparison.Ordinal);
            Assert.Contains("m/44'/111111'/0'/0/0", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Tutorial_samples_match_the_public_vectors_and_do_not_broadcast()
        {
            var writer = new StringWriter();
            var code = AddressTutorial.Run(writer);
            Assert.Equal(0, code);
            code = SendTutorial.RunOffline(writer);
            var text = writer.ToString();
            Assert.Equal(0, code);
            Assert.Contains("mainnet: kaspa:qqd6e65yefepe9wk0m9vuxdufxd80sphy67gwwd0vdaumzdt4tc9s3qt0lqeh", text, StringComparison.Ordinal);
            Assert.Contains("schnorr: kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva", text, StringComparison.Ordinal);
            Assert.Contains("script-hex: 20dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac", text, StringComparison.Ordinal);
            Assert.Contains("json-key: refused", text, StringComparison.Ordinal);
            Assert.Contains("fee-sompi: 203600", text, StringComparison.Ordinal);
            Assert.Contains("relay-mass: 2036", text, StringComparison.Ordinal);
            Assert.Contains("broadcast: no", text, StringComparison.Ordinal);
            Assert.DoesNotContain("submitted:", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Address_sample_accepts_known_vectors_and_rejects_a_bad_checksum()
        {
            var writer = new StringWriter();
            var code = AddressSample.Run(Array.Empty<string>(), writer);
            var text = writer.ToString();
            Assert.Equal(0, code);
            Assert.Contains("script-hex: 20dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac", text, StringComparison.Ordinal);
            Assert.Contains("script-hex: 2102ba01fc5f4e9d9879599c69a3dafd835a725e5f2e934e9322ecd3af190ab0f60eab", text, StringComparison.Ordinal);
            Assert.Contains("type: KaspaScriptHashAddress", text, StringComparison.Ordinal);
            Assert.Contains("valid: false", text, StringComparison.Ordinal);
            Assert.Contains("network-mismatch:", text, StringComparison.Ordinal);
            Assert.Contains("\nok", text, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Spend_sample_prints_version_0_must_spend_and_version_1_without_broadcasting()
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var code = await SpendSample.Run(Array.Empty<string>(), stdout, stderr);
            var text = stdout.ToString();
            Assert.Equal(0, code);
            Assert.Contains("broadcast: no", text, StringComparison.Ordinal);
            Assert.Contains("sample: version-0", text, StringComparison.Ordinal);
            Assert.Contains("sample: must-spend", text, StringComparison.Ordinal);
            Assert.Contains("sample: version-1", text, StringComparison.Ordinal);
            Assert.Contains("fee-sompi: 203600", text, StringComparison.Ordinal);
            Assert.Contains("fee-sompi: 315400", text, StringComparison.Ordinal);
            Assert.Contains("fee-sompi: 203800", text, StringComparison.Ordinal);
            Assert.Contains("relay-mass: 2036", text, StringComparison.Ordinal);
            Assert.Contains("relay-mass: 3154", text, StringComparison.Ordinal);
            Assert.Contains("relay-mass: 2038", text, StringComparison.Ordinal);
            Assert.Contains("inputs: 2", text, StringComparison.Ordinal);
            Assert.Contains("must-spend: true", text, StringComparison.Ordinal);
            Assert.Contains("compute-budget: 10", text, StringComparison.Ordinal);
            Assert.Contains("\"sigOpCount\":1", text, StringComparison.Ordinal);
            Assert.Contains("\"computeBudget\":10", text, StringComparison.Ordinal);
            Assert.DoesNotContain("submitted:", text, StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }

        [Fact]
        public async Task Spend_sample_refuses_broadcast_without_a_mnemonic()
        {
            var previous = Environment.GetEnvironmentVariable("NKASPA_MNEMONIC");
            try
            {
                Environment.SetEnvironmentVariable("NKASPA_MNEMONIC", null);
                var stdout = new StringWriter();
                var stderr = new StringWriter();
                var code = await SpendSample.Run(new[] { "--broadcast", "--to", "kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva", "--amount", "0.1" }, stdout, stderr);
                Assert.Equal(2, code);
                Assert.Contains("NKASPA_MNEMONIC", stderr.ToString(), StringComparison.Ordinal);
                Assert.Contains("WARNING", stderr.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("submitted:", stdout.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NKASPA_MNEMONIC", previous);
            }
        }

        [Fact]
        public async Task Spend_sample_refuses_broadcast_without_a_destination()
        {
            var previous = Environment.GetEnvironmentVariable("NKASPA_MNEMONIC");
            try
            {
                Environment.SetEnvironmentVariable("NKASPA_MNEMONIC", Abandon);
                var stdout = new StringWriter();
                var stderr = new StringWriter();
                var code = await SpendSample.Run(new[] { "--broadcast" }, stdout, stderr);
                Assert.Equal(2, code);
                Assert.Contains("--to", stderr.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("submitted:", stdout.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NKASPA_MNEMONIC", previous);
            }
        }

        [Fact]
        public void Pskt_sample_runs_create_sign_combine_finalize_extract()
        {
            var writer = new StringWriter();
            var code = PsktSample.Run(Array.Empty<string>(), writer);
            var text = writer.ToString();
            Assert.Equal(0, code);
            Assert.Contains("step: create", text, StringComparison.Ordinal);
            Assert.Contains("step: sign", text, StringComparison.Ordinal);
            Assert.Contains("step: combine", text, StringComparison.Ordinal);
            Assert.Contains("step: finalize", text, StringComparison.Ordinal);
            Assert.Contains("step: extract", text, StringComparison.Ordinal);
            Assert.Contains("party-a-sigs: 1", text, StringComparison.Ordinal);
            Assert.Contains("party-b-sigs: 1", text, StringComparison.Ordinal);
            Assert.Contains("combined-sigs: 2", text, StringComparison.Ordinal);
            Assert.Contains("verified: true", text, StringComparison.Ordinal);
            Assert.Contains("broadcast: no", text, StringComparison.Ordinal);
        }

        [Fact]
        public void SignMessage_sample_verifies_the_keyed_personal_message_digest()
        {
            var writer = new StringWriter();
            var code = SignSample.Run(new[] { "NKaspa" }, writer);
            var text = writer.ToString();
            Assert.Equal(0, code);
            Assert.Contains("message: NKaspa", text, StringComparison.Ordinal);
            Assert.Contains("digest: " + NKaspa.DataEncoders.Encoders.Hex.EncodeData(PersonalMessage.Hash("NKaspa")), text, StringComparison.Ordinal);
            Assert.Contains("schnorr-ok: true", text, StringComparison.Ordinal);
            Assert.Contains("address-ok: true", text, StringComparison.Ordinal);
            Assert.Contains("tampered-ok: false", text, StringComparison.Ordinal);
            Assert.Contains("\nok", text, StringComparison.Ordinal);
            Assert.DoesNotContain("0000000000000000000000000000000000000000000000000000000000000003", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Json_sample_round_trips_public_types_and_refuses_a_private_key()
        {
            var writer = new StringWriter();
            var code = JsonSample.Run(Array.Empty<string>(), writer);
            var text = writer.ToString();
            Assert.Equal(0, code);
            Assert.Contains("money: 20000000", text, StringComparison.Ordinal);
            Assert.Contains("address-network: mainnet", text, StringComparison.Ordinal);
            Assert.Contains("script-prefixed: true", text, StringComparison.Ordinal);
            Assert.Contains("\"computeBudget\":10", text, StringComparison.Ordinal);
            Assert.Contains("\"covenant\"", text, StringComparison.Ordinal);
            Assert.Contains("compute-budget: 10", text, StringComparison.Ordinal);
            Assert.Contains("covenant: true", text, StringComparison.Ordinal);
            Assert.Contains("feerate: 100", text, StringComparison.Ordinal);
            Assert.Contains("pskt: true", text, StringComparison.Ordinal);
            Assert.Contains("kpub: \"kpub", text, StringComparison.Ordinal);
            Assert.Contains("private-key: refused", text, StringComparison.Ordinal);
            Assert.Contains("extkey-json: refused", text, StringComparison.Ordinal);
            Assert.Contains("mnemonic-json: refused", text, StringComparison.Ordinal);
            Assert.Contains("warning: true", text, StringComparison.Ordinal);
            Assert.Contains("\nok", text, StringComparison.Ordinal);
            Assert.DoesNotContain("abandon abandon", text, StringComparison.Ordinal);
            Assert.DoesNotContain("kprv", text, StringComparison.Ordinal);
            Assert.DoesNotContain("0000000000000000000000000000000000000000000000000000000000000003", text, StringComparison.Ordinal);
        }
    }
}
