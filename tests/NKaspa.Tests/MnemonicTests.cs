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
            Assert.Equal(sentence, mnemonic.ToPhrase());
            Assert.Equal("NKaspa.Mnemonic", mnemonic.ToString());
            Assert.DoesNotContain(sentence, mnemonic.ToString(), StringComparison.Ordinal);
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
            Assert.Equal(sentence, mnemonic.ToPhrase());
            Assert.Equal("NKaspa.Mnemonic", mnemonic.ToString());
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
            Assert.Equal("NKaspa.Mnemonic", mnemonic.ToString());
            Assert.DoesNotContain("abandon", mnemonic.ToString(), StringComparison.Ordinal);
            Assert.Equal(sentence, mnemonic.ToPhrase());
            var derive = Assert.Throws<InvalidOperationException>(() => mnemonic.DeriveSeed());
            Assert.Contains("allowInvalidChecksum", derive.Message, StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => mnemonic.DeriveExtKey());
            Assert.Equal(64, mnemonic.DeriveSeed(allowInvalidChecksum: true).Length);
            using var derived = mnemonic.DeriveExtKey(allowInvalidChecksum: true);
            Assert.Equal("NKaspa.ExtKey", derived.ToString());

            var parse = Assert.Throws<FormatException>(() => Mnemonic.ParseValidated(sentence));
            Assert.Equal("BIP39 checksum does not match.", parse.Message);
            Assert.Equal("BIP39 checksum does not match.", Assert.Throws<FormatException>(() => Mnemonic.Parse(sentence)).Message);
            Assert.Equal("BIP39 checksum does not match.", Assert.Throws<FormatException>(() => new Mnemonic(sentence, Wordlist.English, requireValidChecksum: true)).Message);
            var valid = Mnemonic.ParseValidated("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
            Assert.True(valid.IsValidChecksum);
            Assert.Equal("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", valid.ToPhrase());

            Assert.Throws<FormatException>(() => new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon notaword"));
            Assert.Throws<FormatException>(() => new Mnemonic("abandon abandon"));
        }

        [Fact]
        public void Words_are_a_read_only_view_and_get_words_returns_a_copy()
        {
            const string Canonical = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            var mnemonic = new Mnemonic(Canonical);
            Assert.IsAssignableFrom<IReadOnlyList<string>>(mnemonic.Words);
            Assert.False(mnemonic.Words is string[]);
            Assert.Equal(12, mnemonic.Words.Count);
            Assert.Equal("abandon", mnemonic.Words[0]);
            Assert.Equal("about", mnemonic.Words[11]);
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)mnemonic.Words)[0] = "zoo");

            var copy = mnemonic.GetWords();
            copy[0] = "zoo";
            Assert.Equal("abandon", mnemonic.Words[0]);
            Assert.Equal(Canonical, mnemonic.ToPhrase());
            Assert.NotEqual(Canonical, string.Join(" ", copy));
        }

        /// <summary>
        /// Case folding, NFKD, and stripping zero-width marks are NKaspa extensions to BIP39.
        /// That includes a leading BOM and marks inside a word. ToPhrase stores the canonical
        /// lowercase wordlist words, so the seed matches other wallets given that phrase.
        /// A zero-width character used as the only separator is still rejected.
        /// </summary>
        [Fact]
        public void Case_fold_nfkd_and_zero_width_are_accepted_as_the_canonical_phrase()
        {
            const string Canonical = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            var canonical = new Mnemonic(Canonical);
            var canonicalSeed = Encoders.Hex.EncodeData(canonical.DeriveSeed());
            var canonicalPassphraseSeed = Encoders.Hex.EncodeData(canonical.DeriveSeed("TREZOR"));
            Assert.True(canonical.IsValidChecksum);

            AssertExtension(Canonical.ToUpperInvariant(), "uppercase");
            AssertExtension(string.Join("\u3000\u3000", Canonical.Split(' ')), "ideographic space");
            AssertExtension("\uFEFF" + Canonical, "leading BOM");
            AssertExtension(Canonical.Replace("abandon", "aban\u200bdon", StringComparison.Ordinal), "zero-width inside a word");
            AssertExtension("\uFF21bandon " + Canonical.Substring("abandon ".Length), "fullwidth");
            AssertExtension(" \t" + Canonical.Replace(" ", "  \t ") + "\u3000", "extra whitespace");
            AssertExtension(Canonical.Replace(" ", "\u00A0", StringComparison.Ordinal), "nbsp");
            var noisy = "\uFEFF" + string.Join("\u3000\u3000", Canonical.Split(' ')).Replace("abandon", "aban\u200bdon", StringComparison.Ordinal);
            AssertExtension(noisy, "bom, ideographic space, and inner zero-width");

            Assert.Throws<FormatException>(() => new Mnemonic(Canonical.Replace(" ", "\u200b", StringComparison.Ordinal)));

            void AssertExtension(string input, string name)
            {
                var parsed = new Mnemonic(input);
                Assert.True(parsed.IsValidChecksum, name);
                Assert.Equal(Canonical, parsed.ToPhrase());
                Assert.Equal("NKaspa.Mnemonic", parsed.ToString());
                Assert.Equal(canonicalSeed, Encoders.Hex.EncodeData(parsed.DeriveSeed()));
                Assert.Equal(canonicalPassphraseSeed, Encoders.Hex.EncodeData(parsed.DeriveSeed("TREZOR")));
            }
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
            // The passphrase is NFKD only. Zero-width marks in the passphrase are not stripped.
            Assert.NotEqual(
                Encoders.Hex.EncodeData(mnemonic.DeriveSeed(composed)),
                Encoders.Hex.EncodeData(mnemonic.DeriveSeed(composed + "\u200b")));
        }
    }
}
