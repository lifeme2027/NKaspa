using System.Text.Json;
using NKaspa.DataEncoders;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// English rows are the Trezor vectors linked from BIP39. Every seed in that file uses the
    /// passphrase "TREZOR" (see the Test vectors section of bip-0039.mediawiki). The copy is
    /// NBitcoin.Tests/data/bip39_vectors.json. Passphrase rows are NBitcoin.Tests/data/bip39_vectors.en.json.
    /// That passphrase is NFKD-sensitive: "nullius" plus an ideographic space and "à".
    /// </summary>
    public class MnemonicTests
    {
        public static IEnumerable<object[]> OfficialEnglish()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "bip39_vectors.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var row in document.RootElement.GetProperty("english").EnumerateArray())
            {
                yield return new object[]
                {
                    row[0].GetString()!,
                    row[1].GetString()!,
                    row[2].GetString()!,
                };
            }
        }

        public static IEnumerable<object[]> PassphraseEnglish()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "bip39_vectors.en.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var row in document.RootElement.EnumerateArray())
            {
                yield return new object[]
                {
                    row.GetProperty("entropy").GetString()!,
                    row.GetProperty("mnemonic").GetString()!,
                    row.GetProperty("passphrase").GetString()!,
                    row.GetProperty("seed").GetString()!,
                    row.GetProperty("bip32_xprv").GetString()!,
                };
            }
        }

        [Fact]
        public void English_wordlist_has_2048_sorted_words()
        {
            Assert.Equal(2048, Wordlist.English.Count);
            Assert.Equal("abandon", Wordlist.English[0]);
            Assert.Equal("about", Wordlist.English[3]);
            Assert.Equal("zoo", Wordlist.English[2047]);
            Assert.Equal(0, Wordlist.English.GetIndex("abandon"));
            Assert.Equal(2047, Wordlist.English.GetIndex("zoo"));
        }

        [Theory]
        [MemberData(nameof(OfficialEnglish))]
        public void Official_trezor_vector_roundtrips_entropy_and_seed(string entropyHex, string sentence, string seedHex)
        {
            var entropy = Encoders.Hex.DecodeData(entropyHex);
            var mnemonic = new Mnemonic(Wordlist.English, entropy);
            Assert.Equal(sentence, mnemonic.ToString());
            Assert.True(mnemonic.IsValidChecksum);

            var parsed = new Mnemonic(sentence);
            Assert.True(parsed.IsValidChecksum);
            Assert.Equal(seedHex, Encoders.Hex.EncodeData(parsed.DeriveSeed("TREZOR")));
            Assert.Equal(seedHex, Encoders.Hex.EncodeData(mnemonic.DeriveSeed("TREZOR")));
        }

        [Theory]
        [MemberData(nameof(PassphraseEnglish))]
        public void NBitcoin_passphrase_vector_matches_seed_and_master_xprv(
            string entropyHex,
            string sentence,
            string passphrase,
            string seedHex,
            string xprv)
        {
            var mnemonic = new Mnemonic(Wordlist.English, Encoders.Hex.DecodeData(entropyHex));
            Assert.Equal(sentence, mnemonic.ToString());
            var seed = mnemonic.DeriveSeed(passphrase);
            Assert.Equal(seedHex, Encoders.Hex.EncodeData(seed));
            using var master = ExtKey.CreateFromSeed(seed);
            Assert.Equal(xprv, master.ToBase58(HdKeyVersions.Xprv));
        }

        [Fact]
        public void Bad_checksum_is_detected_and_unknown_word_is_rejected()
        {
            var sentence = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon";
            var mnemonic = new Mnemonic(sentence);
            Assert.False(mnemonic.IsValidChecksum);
            Assert.Throws<FormatException>(() => new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon notaword"));
            Assert.Throws<FormatException>(() => new Mnemonic("abandon abandon"));
        }

        [Fact]
        public void Upper_case_ideographic_spaces_and_zero_width_match_the_canonical_sentence()
        {
            const string Canonical = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            var canonical = new Mnemonic(Canonical);
            Assert.True(canonical.IsValidChecksum);

            var upper = Canonical.ToUpperInvariant();
            var ideographic = string.Join("\u3000\u3000", Canonical.Split(' '));
            var noisy = "\uFEFF" + ideographic.Replace("abandon", "aban\u200bdon", StringComparison.Ordinal);
            var fullwidth = "\uFF21bandon " + Canonical.Substring("abandon ".Length);
            var spaced = " \t" + Canonical.Replace(" ", "  \t ") + "\u3000";

            foreach (var input in new[] { upper, ideographic, noisy, fullwidth, spaced })
            {
                var parsed = new Mnemonic(input);
                Assert.Equal(Canonical, parsed.ToString());
                Assert.True(parsed.IsValidChecksum);
                Assert.Equal(Encoders.Hex.EncodeData(canonical.DeriveSeed()), Encoders.Hex.EncodeData(parsed.DeriveSeed()));
                Assert.Equal(Encoders.Hex.EncodeData(canonical.DeriveSeed("TREZOR")), Encoders.Hex.EncodeData(parsed.DeriveSeed("TREZOR")));
            }

            Assert.Throws<FormatException>(() => new Mnemonic(Canonical.Replace(" ", "\u200b", StringComparison.Ordinal)));
        }

        [Fact]
        public void Passphrase_nfkd_folds_composed_and_decomposed_grave()
        {
            var mnemonic = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
            var composed = "nullius\u3000\u00e0";
            var decomposed = "nullius\u3000a\u0300";
            Assert.Equal(
                Encoders.Hex.EncodeData(mnemonic.DeriveSeed(composed)),
                Encoders.Hex.EncodeData(mnemonic.DeriveSeed(decomposed)));
            Assert.NotEqual(
                Encoders.Hex.EncodeData(mnemonic.DeriveSeed(composed)),
                Encoders.Hex.EncodeData(mnemonic.DeriveSeed(composed + "\u200b")));
        }
    }
}
