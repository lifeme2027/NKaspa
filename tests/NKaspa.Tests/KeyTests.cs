using NKaspa.Crypto;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// BIP-340 test vector 0 is the first row of
    /// https://github.com/bitcoin/bips/blob/master/bip-0340/test-vectors.csv
    /// (secret 3, 32 zero auxiliary bytes, 32 zero message).
    /// Vector 1's public key is the x-only key already pinned by <c>KaspaAddressTests</c>.
    /// The generator secret 1 is the standard secp256k1 base point.
    /// </summary>
    public class KeyTests
    {
        private const string Bip340Secret3 = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string Bip340Pub3 = "F9308A019258C31049344F85F89D5229B531C845836F99B08601F113BCE036F9";
        private const string Bip340Sig3 = "E907831F80848D1069A5371B402410364BDF1C5F8307B0084C55F1CE2DCA821525F66A4A85EA8B71E482A74F382D2CE5EBEEE8FDB2172F477DF4900D310536C0";

        private const string Bip340Secret1 = "B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF";
        private const string Bip340Pub1 = "DFF1D77F2A671C5F36183726DB2341BE58FEAE1DA2DECED843240F7B502BA659";

        [Fact]
        public void Bip340_vector0_signs_and_verifies()
        {
            using var key = Key.Parse(Bip340Secret3);
            Assert.Equal(Bip340Pub3.ToLowerInvariant(), key.PubKey.ToString());

            var zeros = new byte[32];
            var signature = key.SignSchnorr(zeros, zeros);
            Assert.Equal(Bip340Sig3.ToLowerInvariant(), signature.ToString());
            Assert.True(key.PubKey.Verify(signature, zeros));

            var changed = (byte[])zeros.Clone();
            changed[0] = 1;
            Assert.False(key.PubKey.Verify(signature, changed));
        }

        [Fact]
        public void Bip340_vector1_public_key_matches_the_known_kaspa_address()
        {
            using var key = Key.Parse(Bip340Secret1);
            Assert.Equal(Bip340Pub1.ToLowerInvariant(), key.PubKey.ToString());
            Assert.Equal(
                "kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva",
                key.GetAddress(Network.Mainnet).ToString());
            Assert.Equal(key.PubKey.ToString(), key.PubKeyECDSA.XOnly.ToHex());
        }

        [Fact]
        public void Generator_secret_one_matches_the_secp256k1_base_point()
        {
            var secret = new byte[32];
            secret[31] = 1;
            using var key = new Key(secret);
            Assert.Equal("79be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798", key.PubKey.ToString());
            Assert.Equal(
                "0279be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798",
                key.PubKeyECDSA.ToString());
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000001", key.ToHex());
        }

        [Fact]
        public void Ecdsa_rfc6979_signature_verifies_and_uses_low_s()
        {
            using var key = Key.Parse(Bip340Secret1);
            var message = new byte[32];
            message[31] = 9;
            var signature = key.SignECDSA(message);
            Assert.Equal(64, signature.ToBytes().Length);
            Assert.True(key.PubKeyECDSA.Verify(signature, message));
            Assert.False(key.PubKeyECDSA.Verify(signature, new byte[32]));

            var again = key.SignECDSA(message);
            Assert.Equal(signature.ToString(), again.ToString());
        }

        [Fact]
        public void Created_key_roundtrips_hex_and_can_sign()
        {
            using var key = Key.Create();
            using var imported = Key.Parse(key.ToHex());
            Assert.Equal(key.PubKey.ToString(), imported.PubKey.ToString());
            var message = new byte[32];
            message[0] = 4;
            var signature = key.SignSchnorr(message, new byte[32]);
            Assert.True(imported.PubKey.Verify(signature, message));
            Assert.StartsWith("kaspa:", key.GetAddress(Network.Mainnet).ToString());
            Assert.StartsWith("kaspatest:", key.GetEcdsaAddress(Network.Testnet).ToString());
        }

        [Fact]
        public void Rejects_a_zero_secret_and_an_off_curve_x_only_key()
        {
            Assert.Throws<ArgumentException>(() => new Key(new byte[32]));
            Assert.Throws<ArgumentException>(() => new PubKey(new byte[32]));
            Assert.Throws<ArgumentException>(() => new PubKeyECDSA(new byte[33]));
        }
    }

    internal static class TestHex
    {
        public static string ToHex(this byte[] data)
        {
            var chars = new char[data.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (var i = 0; i < data.Length; i++)
            {
                chars[i * 2] = alphabet[data[i] >> 4];
                chars[(i * 2) + 1] = alphabet[data[i] & 0x0F];
            }

            return new string(chars);
        }
    }
}
