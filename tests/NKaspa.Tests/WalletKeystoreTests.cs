using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// PBKDF2-HMAC-SHA256 (RFC 7914), AES-256-GCM (Go crypto/cipher gcm_test.go), and the keystore JSON format.
    /// Secrets here are synthetic. They are not wallet mnemonics.
    /// </summary>
    public class WalletKeystoreTests
    {
        [Fact]
        public void Pbkdf2_sha256_matches_rfc_7914_section_11()
        {
            var one = WalletKeystore.Pbkdf2Sha256(Encoding.ASCII.GetBytes("passwd"), Encoding.ASCII.GetBytes("salt"), 1, 64);
            Assert.Equal(Hex("55ac046e56e3089fec1691c22544b605f94185216dde0465e68b9d57c20dacbc49ca9cccf179b645991664b39d77ef317c71b845b1e30bd509112041d3a19783"), one);

            var many = WalletKeystore.Pbkdf2Sha256(Encoding.ASCII.GetBytes("Password"), Encoding.ASCII.GetBytes("NaCl"), 80000, 64);
            Assert.Equal(Hex("4ddcd8f60b98be21830cee5ef22701f9641a4418d04c0414aeff08876b34ab56a1d425a1225833549adb841b51c9b3176a272bdebba1d078478f62b397f33c8d"), many);
        }

        [Fact]
        public void Aes_256_gcm_matches_the_go_standard_library_vector()
        {
            // go1.23.4 src/crypto/cipher/gcm_test.go, comment "key=32, plaintext=16". Empty additional data.
            var key = Hex("feffe9928665731c6d6a8f9467308308feffe9928665731c6d6a8f9467308308");
            var nonce = Hex("54cc7dc2c37ec006bcc6d1da");
            var plaintext = Hex("007c5e5b3e59df24a7c355584fc1518d");
            var expected = Hex("d50b9e252b70945d4240d351677eb10f937cdaef6f2822b6a3191654ba41b197");
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using (var gcm = new AesGcm(key, 16))
            {
                gcm.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            var actual = new byte[expected.Length];
            Buffer.BlockCopy(ciphertext, 0, actual, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, actual, ciphertext.Length, tag.Length);
            Assert.Equal(expected, actual);

            var opened = new byte[plaintext.Length];
            using (var gcm = new AesGcm(key, 16))
            {
                gcm.Decrypt(nonce, ciphertext, tag, opened);
            }

            Assert.Equal(plaintext, opened);
        }

        [Fact]
        public void Fixed_salt_and_nonce_round_trip_matches_pbkdf2_then_aes_gcm()
        {
            var secret = Encoding.UTF8.GetBytes("test-keystore-secret");
            var password = "password";
            var salt = Sequence(1, WalletKeystore.SaltLength);
            var nonce = Sequence(0x11, WalletKeystore.NonceLength);
            var parameters = new WalletKeystoreParameters(1, salt, nonce);
            var json = WalletKeystore.Encrypt(secret, password, parameters);

            var key = WalletKeystore.Pbkdf2Sha256(Encoding.UTF8.GetBytes(password), salt, 1, WalletKeystore.KeyLength);
            var expectedCiphertext = new byte[secret.Length];
            var expectedTag = new byte[WalletKeystore.TagLength];
            var aad = Encoding.UTF8.GetBytes("nkaspa-keystore-v1|pbkdf2-sha256|1|sha256|32|aes-256-gcm");
            using (var gcm = new AesGcm(key, 16))
            {
                gcm.Encrypt(nonce, secret, expectedCiphertext, expectedTag, aad);
            }

            var expected = "{\"version\":1,\"kdf\":\"pbkdf2-sha256\",\"kdfParams\":{\"iterations\":1,\"hash\":\"sha256\",\"keyLength\":32},\"cipher\":\"aes-256-gcm\",\"salt\":\""
                + Convert.ToBase64String(salt) + "\",\"nonce\":\"" + Convert.ToBase64String(nonce) + "\",\"tag\":\""
                + Convert.ToBase64String(expectedTag) + "\",\"ciphertext\":\"" + Convert.ToBase64String(expectedCiphertext) + "\"}";
            Assert.Equal(expected, json);
            Assert.Equal("test-keystore-secret", WalletKeystore.DecryptToString(json, password));
            Assert.Equal(secret, WalletKeystore.Decrypt(json, password));
            Assert.Equal(600_000, WalletKeystore.DefaultIterations);
            Assert.DoesNotContain("test-keystore-secret", parameters.ToString(), StringComparison.Ordinal);
            Assert.Equal("NKaspa.WalletKeystoreParameters", parameters.ToString());
        }

        [Fact]
        public void Wrong_password_and_tampering_fail_without_printing_the_secret()
        {
            var phrase = "test-keystore-secret";
            using var secret = KeystoreSecret.FromPhrase(phrase);
            var json = WalletKeystore.Encrypt(secret, "correct password", new WalletKeystoreParameters(1, Sequence(2, 16), Sequence(3, 12)));
            Assert.Equal("NKaspa.KeystoreSecret", secret.ToString());
            Assert.DoesNotContain(phrase, secret.ToString(), StringComparison.Ordinal);
            Assert.Equal(phrase, secret.GetPhrase());

            var wrong = Assert.Throws<WrongKeystorePasswordException>(() => WalletKeystore.DecryptToString(json, "wrong password"));
            Assert.Equal("Wrong keystore password.", wrong.Message);
            Assert.DoesNotContain(phrase, wrong.ToString(), StringComparison.Ordinal);

            Assert.Throws<WrongKeystorePasswordException>(() => WalletKeystore.Decrypt(Flip(json, "ciphertext"), "correct password"));
            Assert.Throws<WrongKeystorePasswordException>(() => WalletKeystore.Decrypt(Flip(json, "tag"), "correct password"));
            Assert.Throws<WrongKeystorePasswordException>(() => WalletKeystore.Decrypt(Flip(json, "nonce"), "correct password"));
            Assert.Throws<WrongKeystorePasswordException>(() => WalletKeystore.Decrypt(SetIterations(json, 2), "correct password"));

            Assert.Throws<KeystoreException>(() => WalletKeystore.Decrypt(json.Replace("\"version\":1", "\"version\":9", StringComparison.Ordinal), "correct password"));
            var kdf = Assert.Throws<KeystoreException>(() => WalletKeystore.Decrypt(json.Replace("pbkdf2-sha256", "argon2id", StringComparison.Ordinal), "correct password"));
            Assert.Contains("KDF", kdf.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(phrase, kdf.Message, StringComparison.Ordinal);
            Assert.Throws<KeystoreException>(() => WalletKeystore.Decrypt(json.Replace("aes-256-gcm", "aes-256-cbc", StringComparison.Ordinal), "correct password"));
            Assert.Throws<KeystoreException>(() => WalletKeystore.Decrypt("{}", "correct password"));
            Assert.Throws<KeystoreException>(() => WalletKeystore.Decrypt("{", "correct password"));
        }

        [Fact]
        public void Random_salt_round_trips_a_seed_and_a_disposed_secret_stays_redacted()
        {
            var seed = Sequence(9, 32);
            using var secret = KeystoreSecret.FromSeed(seed);
            var json = WalletKeystore.Encrypt(secret, "another password", new WalletKeystoreParameters(2));
            Assert.Equal(seed, WalletKeystore.Decrypt(json, "another password"));
            using var opened = WalletKeystore.DecryptSecret(json, "another password");
            Assert.Equal("NKaspa.KeystoreSecret", opened.ToString());
            Assert.False(opened.IsPhrase);
            Assert.Throws<InvalidOperationException>(() => opened.GetPhrase());
            secret.Dispose();
            Assert.Equal("NKaspa.KeystoreSecret", secret.ToString());
            Assert.Throws<ObjectDisposedException>(() => secret.CopyBytes());
        }

        private static string Flip(string json, string field)
        {
            var node = JsonNode.Parse(json)!.AsObject();
            var bytes = Convert.FromBase64String(node[field]!.GetValue<string>());
            bytes[0] ^= 0x01;
            node[field] = Convert.ToBase64String(bytes);
            return node.ToJsonString();
        }

        private static string SetIterations(string json, int iterations)
        {
            var node = JsonNode.Parse(json)!.AsObject();
            node["kdfParams"]!["iterations"] = iterations;
            return node.ToJsonString();
        }

        private static byte[] Sequence(byte start, int length)
        {
            var bytes = new byte[length];
            for (var i = 0; i < length; i++)
            {
                bytes[i] = (byte)(start + i);
            }

            return bytes;
        }

        private static byte[] Hex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
