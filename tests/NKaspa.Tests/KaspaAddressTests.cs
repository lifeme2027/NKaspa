using NKaspa.DataEncoders;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Real Kaspa address strings shared by kaswallet and kaspadotnet.
    /// Source: D:\Project2026\kaspadotnet\Kaspadotnet.Clients.Tests\SharedAddressVectors.cs
    /// and D:\Project2026\kaswallet\dotnet\tests\KasWallet.Core.Tests\KaspaBech32Tests.cs.
    /// Payloads that are not all zeros are the x-only keys and the compressed ECDSA key
    /// named in those tests.
    /// </summary>
    public class KaspaAddressTests
    {
        public static readonly string[] Valid =
        {
            "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e",
            "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya",
            "kaspatest:qyqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhe837j2d",
            "kaspa:qp0l70zd5x85ttwd6jv7g3s3a8llzj96d8dncn4zmhv4tlzx5k2jyqh70xmfj",
            "kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva",
            "kaspatest:qypt5q0uta8fmxretxwxng76lkp45uj7tuhfxn5nytkd8tcep2c0vrsxarh9t02",
            "kaspa:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq2xdnz6yk",
            "kaspadev:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqxcg4s836",
            "kaspasim:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq9ksj7y69",
            "kaspa:pqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqlmtfk4dg",
        };

        public static readonly string[] Invalid =
        {
            "kaspa:qz",
            "not-an-address",
            "kaspa:",
            "btc:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq",
            "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4l",
            "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e",
            "kaspatest:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq2xdnz6yk",
            "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqcq84r7qz",
            "KASPA:QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQKX9AWP4E",
        };

        public static IEnumerable<object[]> ValidData()
        {
            foreach (var address in Valid)
            {
                yield return new object[] { address };
            }
        }

        public static IEnumerable<object[]> InvalidData()
        {
            foreach (var address in Invalid)
            {
                yield return new object[] { address };
            }
        }

        [Theory]
        [MemberData(nameof(ValidData))]
        public void Known_addresses_roundtrip(string address)
        {
            Assert.True(KaspaAddress.IsValid(address));
            Assert.True(KaspaAddress.TryParse(address, out var parsed));
            Assert.NotNull(parsed);
            Assert.Equal(address, parsed!.ToString());
            Assert.Equal(address, Rebuild(parsed).ToString());
            Assert.Equal(0, parsed.ScriptPubKey.Version);
        }

        [Theory]
        [MemberData(nameof(InvalidData))]
        public void Known_invalid_addresses_are_rejected(string address)
        {
            Assert.False(KaspaAddress.IsValid(address));
            Assert.False(KaspaAddress.TryParse(address, out _));
            Assert.Throws<FormatException>(() => KaspaAddress.Parse(address));
        }

        [Fact]
        public void Schnorr_xonly_addresses_rebuild_script_public_keys()
        {
            var zeros = new KaspaPubKeyAddress(new byte[32], Network.Mainnet);
            Assert.Equal("kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e", zeros.ToString());
            Assert.Equal(KaspaAddressVersion.PubKey, zeros.Version);
            Assert.Equal("20" + new string('0', 64) + "ac", zeros.ScriptPubKey.ToHex());
            Assert.IsType<KaspaPubKeyAddress>(KaspaAddress.Parse(zeros.ToString()));

            var xOnly = Decode("5fff3c4da18f45adcdd499e44611e9fff148ba69db3c4ea2ddd955fc46a59522");
            var fromKey = new KaspaPubKeyAddress(xOnly, Network.Mainnet);
            Assert.Equal("kaspa:qp0l70zd5x85ttwd6jv7g3s3a8llzj96d8dncn4zmhv4tlzx5k2jyqh70xmfj", fromKey.ToString());
            Assert.Equal("205fff3c4da18f45adcdd499e44611e9fff148ba69db3c4ea2ddd955fc46a59522ac", fromKey.ScriptPubKey.ToHex());

            var bip340 = Decode("dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659");
            Assert.Equal(
                "kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva",
                new KaspaPubKeyAddress(bip340, Network.Mainnet).ToString());

            var withPrefix = new byte[32];
            withPrefix[0] = 0x11;
            Assert.Equal(
                "kaspa:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq2xdnz6yk",
                new KaspaPubKeyAddress(withPrefix, Network.Mainnet).ToString());
            Assert.Equal(
                "kaspadev:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqxcg4s836",
                new KaspaPubKeyAddress(withPrefix, Network.Devnet).ToString());
            Assert.Equal(
                "kaspasim:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq9ksj7y69",
                new KaspaPubKeyAddress(withPrefix, Network.Simnet).ToString());
        }

        [Fact]
        public void Ecdsa_and_script_hash_addresses_use_their_versions_and_scripts()
        {
            var ecdsaZeros = new KaspaPubKeyEcdsaAddress(new byte[33], Network.Testnet);
            Assert.Equal(
                "kaspatest:qyqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhe837j2d",
                ecdsaZeros.ToString());
            Assert.Equal("21" + new string('0', 66) + "ab", ecdsaZeros.ScriptPubKey.ToHex());
            Assert.IsType<KaspaPubKeyEcdsaAddress>(KaspaAddress.Parse(ecdsaZeros.ToString(), Network.Testnet));

            var ecdsa = Decode("02ba01fc5f4e9d9879599c69a3dafd835a725e5f2e934e9322ecd3af190ab0f60e");
            var ecdsaAddress = new KaspaPubKeyEcdsaAddress(ecdsa, Network.Testnet);
            Assert.Equal(
                "kaspatest:qypt5q0uta8fmxretxwxng76lkp45uj7tuhfxn5nytkd8tcep2c0vrsxarh9t02",
                ecdsaAddress.ToString());
            Assert.Equal(
                "2102ba01fc5f4e9d9879599c69a3dafd835a725e5f2e934e9322ecd3af190ab0f60eab",
                ecdsaAddress.ScriptPubKey.ToHex());

            var scriptHash = new KaspaScriptHashAddress(uint256.Zero, Network.Mainnet);
            Assert.Equal(
                "kaspa:pqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqlmtfk4dg",
                scriptHash.ToString());
            Assert.Equal(KaspaAddressVersion.ScriptHash, scriptHash.Version);
            Assert.Equal(uint256.Zero, scriptHash.Hash);
            Assert.Equal("aa20" + new string('0', 64) + "87", scriptHash.ScriptPubKey.ToHex());
            Assert.IsType<KaspaScriptHashAddress>(KaspaAddress.Parse(scriptHash.ToString()));
        }

        [Fact]
        public void Expected_network_rejects_a_different_prefix()
        {
            var mainnet = Valid[0];
            Assert.False(KaspaAddress.TryParse(mainnet, Network.Testnet, out _));
            Assert.Throws<FormatException>(() => KaspaAddress.Parse(mainnet, Network.Testnet));
            Assert.Equal(Network.Mainnet, KaspaAddress.Parse(mainnet).Network);
        }

        [Fact]
        public void Nonstandard_payload_length_is_rejected_by_the_address_parser()
        {
            var encoded = new KaspaBech32Encoder("kaspa").Encode(0, new byte[31]);
            Assert.False(KaspaAddress.IsValid(encoded));
            Assert.True(KaspaBech32Encoder.TryDecode(encoded, out _, out var version, out var payload, out _));
            Assert.Equal(0, version);
            Assert.Equal(31, payload.Length);
        }

        private static KaspaAddress Rebuild(KaspaAddress address)
        {
            return address switch
            {
                KaspaPubKeyAddress pubKey => new KaspaPubKeyAddress(pubKey.PubKey, pubKey.Network),
                KaspaPubKeyEcdsaAddress ecdsa => new KaspaPubKeyEcdsaAddress(ecdsa.PubKey, ecdsa.Network),
                KaspaScriptHashAddress script => new KaspaScriptHashAddress(script.Hash, script.Network),
                _ => throw new InvalidOperationException("Unexpected address type."),
            };
        }

        private static byte[] Decode(string hex)
        {
            return ((HexEncoder)Encoders.Hex).DecodeData(hex);
        }
    }
}
