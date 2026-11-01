using System.Reflection;
using System.Text.Json;
using NKaspa.JsonConverters;
using Xunit;

namespace NKaspa.Tests
{
    public class SecretRedactionTests
    {
        private const string Phrase = "test-keystore-secret";
        private const string Secret = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string Abandon = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Fact]
        public void Phrase_reveal_is_explicit_and_json_refuses_it()
        {
            var secret = KeystoreSecret.FromPhrase(Phrase);
            Assert.Equal("NKaspa.KeystoreSecret", secret.ToString());
            Assert.DoesNotContain(Phrase, secret.ToString(), StringComparison.Ordinal);
            Assert.True(secret.IsPhrase);
            Assert.Equal(Phrase, secret.Reveal());
            Assert.Equal(Phrase, secret.GetPhrase());

            var buffer = BufferOf(secret);
            Assert.Contains(buffer, b => b != 0);
            secret.Clear();
            Assert.All(buffer, b => Assert.Equal(0, b));
            Assert.Equal("NKaspa.KeystoreSecret", secret.ToString());
            Assert.Throws<ObjectDisposedException>(() => secret.Reveal());
            secret.Clear();

            var refused = Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(KeystoreSecret.FromPhrase(Phrase)));
            Assert.Equal(KeystoreSecretJsonConverter.Refusal, refused.Message);
            Assert.DoesNotContain(Phrase, refused.ToString(), StringComparison.Ordinal);
            var nested = Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(new Holder { Secret = KeystoreSecret.FromPhrase(Phrase) }));
            Assert.DoesNotContain(Phrase, nested.ToString(), StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<KeystoreSecret>("\"x\""));
            using var opted = KeystoreSecret.FromPhrase(Phrase);
            var optedRefusal = Assert.Throws<InvalidOperationException>(() => NKaspaJsonSerializer.Serialize(opted, Network.Mainnet, writeIndented: false, allowPrivateKeys: true));
            Assert.Equal(KeystoreSecretJsonConverter.Refusal, optedRefusal.Message);
            Assert.DoesNotContain(Phrase, optedRefusal.ToString(), StringComparison.Ordinal);
            var newtonsoft = Assert.Throws<InvalidOperationException>(() => opted.ShouldSerializeIsPhrase());
            Assert.Equal(KeystoreSecretJsonConverter.Refusal, newtonsoft.Message);
        }

        [Fact]
        public void Seed_reveal_is_hex_and_clear_zeroes_the_buffer()
        {
            var seed = new byte[] { 1, 2, 3, 4 };
            using var secret = KeystoreSecret.FromSeed(seed);
            Assert.False(secret.IsPhrase);
            Assert.Equal("01020304", secret.Reveal());
            Assert.DoesNotContain("01020304", secret.ToString(), StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => secret.GetPhrase());
            var buffer = BufferOf(secret);
            secret.Dispose();
            Assert.All(buffer, b => Assert.Equal(0, b));
            Assert.Throws<ObjectDisposedException>(() => secret.CopyBytes());
        }

        [Fact]
        public void Key_and_mnemonic_and_extkey_reveal_into_keystore_secret()
        {
            using var key = Key.Parse(Secret);
            Assert.Equal("NKaspa.Key", key.ToString());
            Assert.DoesNotContain(Secret, key.ToString(), StringComparison.OrdinalIgnoreCase);
            using (var revealed = key.Reveal())
            {
                Assert.False(revealed.IsPhrase);
                Assert.Equal(key.ToHex(), revealed.Reveal());
                Assert.DoesNotContain(Secret, revealed.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            var keyBuffer = (byte[])typeof(Key).GetField("_secret", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(key)!;
            key.Clear();
            Assert.All(keyBuffer, b => Assert.Equal(0, b));
            Assert.Equal("NKaspa.Key", key.ToString());
            Assert.Throws<ObjectDisposedException>(() => key.ToBytes());

            var mnemonic = new Mnemonic(Abandon);
            Assert.Equal("NKaspa.Mnemonic", mnemonic.ToString());
            Assert.DoesNotContain("abandon", mnemonic.ToString(), StringComparison.Ordinal);
            using (var revealed = mnemonic.Reveal())
            {
                Assert.True(revealed.IsPhrase);
                Assert.Equal(mnemonic.ToPhrase(), revealed.Reveal());
                Assert.DoesNotContain("abandon", revealed.ToString(), StringComparison.Ordinal);
                var buffer = BufferOf(revealed);
                revealed.Clear();
                Assert.All(buffer, b => Assert.Equal(0, b));
            }

            Assert.Equal(Abandon, mnemonic.ToPhrase());
            using var master = mnemonic.DeriveExtKey();
            Assert.Equal("NKaspa.ExtKey", master.ToString());
            Assert.DoesNotContain("kprv", master.ToString(), StringComparison.Ordinal);
            using (var revealed = master.Reveal())
            {
                Assert.True(revealed.IsPhrase);
                Assert.Equal(master.ExportXprv(), revealed.Reveal());
                Assert.DoesNotContain("kprv", revealed.ToString(), StringComparison.Ordinal);
            }

            master.Clear();
            Assert.Equal("NKaspa.ExtKey", master.ToString());
            Assert.Throws<ObjectDisposedException>(() => master.ExportXprv());
        }

        private static byte[] BufferOf(KeystoreSecret secret)
        {
            return (byte[])typeof(KeystoreSecret).GetField("_bytes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(secret)!;
        }

        private sealed class Holder
        {
            public KeystoreSecret? Secret { get; set; }
        }
    }
}
