using System.Security.Cryptography;
using System.Text;
using NKaspa.Crypto;

namespace NKaspa
{
    /// <summary>
    /// BIP39 mnemonic. Kaspa counterpart of NBitcoin.Mnemonic.
    /// Parsing strips zero-width characters, splits on Unicode whitespace (including U+3000),
    /// NFKD-normalizes each word, and lower-cases it before the wordlist lookup.
    /// The seed is PBKDF2-HMAC-SHA512 of the NFKD canonical sentence, 2048 iterations, 64 bytes.
    /// The salt is the NFKD string <c>mnemonic</c> plus the optional passphrase.
    /// The passphrase is not lower-cased, and zero-width characters in it are kept.
    /// </summary>
    /// <remarks>
    /// <see cref="Words"/> holds wordlist strings. Those objects are shared and cannot be wiped:
    /// <see cref="string"/> has no erase API. The word order in <c>_indexes</c> is the secret.
    /// NBitcoin stores the sentence in a <see cref="string"/> as well.
    /// </remarks>
    public sealed class Mnemonic
    {
        private static readonly int[] WordCounts = { 12, 15, 18, 21, 24 };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly int[] _indexes;

        /// <summary>Parses a mnemonic. The default wordlist is English.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="mnemonic"/> is null.</exception>
        /// <exception cref="System.FormatException">Word count should be 12, 15, 18, 21, or 24. Unknown BIP39 word.</exception>
        public Mnemonic(string mnemonic, Wordlist? wordlist = null)
        {
            if (mnemonic == null)
            {
                throw new ArgumentNullException(nameof(mnemonic));
            }

            WordList = wordlist ?? Wordlist.English;
            var parsed = SplitCanonical(mnemonic, WordList);
            if (!IsLegalCount(parsed.Indexes.Length))
            {
                throw new FormatException("Word count should be 12, 15, 18, 21, or 24.");
            }

            _indexes = parsed.Indexes;
            Words = parsed.Words;
            IsValidChecksum = ChecksumMatches(_indexes);
        }

        /// <summary>Builds a mnemonic from entropy. The length must be 16, 20, 24, 28, or 32 bytes.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="entropy"/> is null. <paramref name="wordlist"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">Entropy must be 128, 160, 192, 224, or 256 bits.</exception>
        public Mnemonic(Wordlist wordlist, byte[] entropy)
        {
            if (wordlist == null)
            {
                throw new ArgumentNullException(nameof(wordlist));
            }

            if (entropy == null)
            {
                throw new ArgumentNullException(nameof(entropy));
            }

            var checksumBits = ChecksumBits(entropy.Length);
            WordList = wordlist;
            _indexes = IndexesFromEntropy(entropy, checksumBits);
            Words = WordsOf(_indexes, wordlist);
            IsValidChecksum = true;
        }

        /// <summary>Draws entropy for <paramref name="wordCount"/> and builds a mnemonic.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="wordlist"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">Entropy must be 128, 160, 192, 224, or 256 bits. Word count should be 12, 15, 18, 21, or 24.</exception>
        public Mnemonic(Wordlist wordlist, WordCount wordCount)
            : this(wordlist, DrawEntropy(wordCount))
        {
        }

        /// <summary>Wordlist used to parse or build this mnemonic.</summary>
        public Wordlist WordList { get; }

        /// <summary>Words in sentence order.</summary>
        public string[] Words { get; }

        /// <summary>True when the embedded SHA-256 checksum matches the entropy.</summary>
        public bool IsValidChecksum { get; }

        /// <summary>
        /// 64-byte BIP39 seed.
        /// An empty passphrase is used when <paramref name="passphrase"/> is null.
        /// The password is the NFKD UTF-8 of the canonical sentence. The salt is the NFKD UTF-8 of <c>mnemonic</c> plus the passphrase.
        /// The passphrase keeps its case and any zero-width characters. The checksum is not consulted.
        /// </summary>
        public byte[] DeriveSeed(string? passphrase = null)
        {
            var password = Normalize(ToString());
            var salt = Normalize("mnemonic" + (passphrase ?? string.Empty));
            using var derive = new Rfc2898DeriveBytes(password, salt, 2048, HashAlgorithmName.SHA512);
            return derive.GetBytes(64);
        }

        /// <summary>Master extended key from <see cref="DeriveSeed"/>.</summary>
        /// <exception cref="System.ArgumentException">BIP32 seed must be between 16 and 64 bytes. Private key is zero or not in the secp256k1 range. Private key must be 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">BIP32 master key is invalid.</exception>
        public ExtKey DeriveExtKey(string? passphrase = null)
        {
            var seed = DeriveSeed(passphrase);
            try
            {
                return ExtKey.CreateFromSeed(seed);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(seed);
            }
        }

        /// <summary>Space-separated mnemonic sentence.</summary>
        public override string ToString()
        {
            return string.Join(" ", Words);
        }

        private static byte[] DrawEntropy(WordCount wordCount)
        {
            var words = (int)wordCount;
            if (!IsLegalCount(words))
            {
                throw new ArgumentException("Word count should be 12, 15, 18, 21, or 24.", nameof(wordCount));
            }

            var entropy = new byte[(words * 11 - (words / 3)) / 8];
            RandomNumberGenerator.Fill(entropy);
            return entropy;
        }

        private static bool IsLegalCount(int words)
        {
            for (var i = 0; i < WordCounts.Length; i++)
            {
                if (WordCounts[i] == words)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ChecksumBits(int entropyLength)
        {
            var bits = entropyLength * 8;
            if (bits != 128 && bits != 160 && bits != 192 && bits != 224 && bits != 256)
            {
                throw new ArgumentException("Entropy must be 128, 160, 192, 224, or 256 bits.", nameof(entropyLength));
            }

            return bits / 32;
        }

        private static int[] IndexesFromEntropy(byte[] entropy, int checksumBits)
        {
            var hash = Hashes.SHA256(entropy);
            var wordCount = (entropy.Length * 8 + checksumBits) / 11;
            var indexes = new int[wordCount];
            var entropyBits = entropy.Length * 8;
            var bitPosition = 0;
            for (var word = 0; word < wordCount; word++)
            {
                var value = 0;
                for (var bit = 0; bit < 11; bit++)
                {
                    int one;
                    if (bitPosition < entropyBits)
                    {
                        var offset = 7 - (bitPosition % 8);
                        one = (entropy[bitPosition / 8] >> offset) & 1;
                    }
                    else
                    {
                        var checksumBit = bitPosition - entropyBits;
                        var offset = 7 - (checksumBit % 8);
                        one = (hash[checksumBit / 8] >> offset) & 1;
                    }

                    value = (value << 1) | one;
                    bitPosition++;
                }

                indexes[word] = value;
            }

            return indexes;
        }

        private bool ChecksumMatches(int[] indexes)
        {
            var wordBits = indexes.Length * 11;
            var checksumBits = indexes.Length / 3;
            var entropyBits = wordBits - checksumBits;
            var entropy = new byte[entropyBits / 8];
            var bitPosition = 0;
            for (var word = 0; word < indexes.Length; word++)
            {
                for (var bit = 10; bit >= 0; bit--)
                {
                    var one = (indexes[word] >> bit) & 1;
                    if (bitPosition < entropyBits)
                    {
                        var offset = 7 - (bitPosition % 8);
                        entropy[bitPosition / 8] |= (byte)(one << offset);
                    }
                    else
                    {
                        var checksumBit = bitPosition - entropyBits;
                        var offset = 7 - (checksumBit % 8);
                        var expected = (Hashes.SHA256(entropy)[checksumBit / 8] >> offset) & 1;
                        if (one != expected)
                        {
                            return false;
                        }
                    }

                    bitPosition++;
                }
            }

            return true;
        }

        private static string[] WordsOf(int[] indexes, Wordlist wordlist)
        {
            var words = new string[indexes.Length];
            for (var i = 0; i < indexes.Length; i++)
            {
                words[i] = wordlist[indexes[i]];
            }

            return words;
        }

        private static byte[] Normalize(string text)
        {
            return Utf8.GetBytes(text.Normalize(NormalizationForm.FormKD));
        }

        private readonly struct ParsedSentence
        {
            public ParsedSentence(int[] indexes, string[] words)
            {
                Indexes = indexes;
                Words = words;
            }

            public int[] Indexes { get; }

            public string[] Words { get; }
        }

        /// <summary>
        /// BIP39 sentence split. Zero-width and bidi marks are removed, not used as separators.
        /// Separators are Unicode whitespace, including the ideographic space U+3000.
        /// Each word is NFKD-normalized and lower-cased, then replaced by the wordlist entry.
        /// </summary>
        private static ParsedSentence SplitCanonical(string mnemonic, Wordlist wordlist)
        {
            var cleaned = StripFormatCharacters(mnemonic).Normalize(NormalizationForm.FormKD);
            var indexes = new List<int>();
            var words = new List<string>();
            var start = -1;
            for (var i = 0; i <= cleaned.Length; i++)
            {
                var boundary = i == cleaned.Length || char.IsWhiteSpace(cleaned[i]);
                if (!boundary)
                {
                    if (start < 0)
                    {
                        start = i;
                    }

                    continue;
                }

                if (start < 0)
                {
                    continue;
                }

                var token = StripFormatCharacters(cleaned.Substring(start, i - start));
                start = -1;
                if (token.Length == 0)
                {
                    continue;
                }

                token = token.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
                var index = wordlist.GetIndex(token);
                indexes.Add(index);
                words.Add(wordlist[index]);
            }

            return new ParsedSentence(indexes.ToArray(), words.ToArray());
        }

        private static string StripFormatCharacters(string text)
        {
            StringBuilder? builder = null;
            for (var i = 0; i < text.Length; i++)
            {
                if (!IsStrippedFormat(text[i]))
                {
                    if (builder != null)
                    {
                        builder.Append(text[i]);
                    }

                    continue;
                }

                if (builder == null)
                {
                    builder = new StringBuilder(text.Length);
                    builder.Append(text, 0, i);
                }
            }

            return builder == null ? text : builder.ToString();
        }

        /// <summary>
        /// Characters removed from a mnemonic before the word split.
        /// U+200B ZERO WIDTH SPACE, U+200C ZWNJ, U+200D ZWJ, U+FEFF BOM / ZWNBSP,
        /// U+2060 WORD JOINER, U+180E MONGOLIAN VOWEL SEPARATOR, U+200E LTR, U+200F RTL.
        /// </summary>
        private static bool IsStrippedFormat(char value)
        {
            return value == '\u200B'
                || value == '\u200C'
                || value == '\u200D'
                || value == '\uFEFF'
                || value == '\u2060'
                || value == '\u180E'
                || value == '\u200E'
                || value == '\u200F';
        }
    }
}
