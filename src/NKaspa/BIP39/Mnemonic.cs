using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using NKaspa.Crypto;
using NKaspa.JsonConverters;

namespace NKaspa
{
    /// <summary>
    /// BIP39 mnemonic. Kaspa counterpart of NBitcoin.Mnemonic.
    /// Parsing strips zero-width characters, splits on Unicode whitespace (including U+3000 and NBSP),
    /// NFKD-normalizes each word, and lower-cases it before the wordlist lookup.
    /// A variant is accepted only when that lookup hits the wordlist. The stored sentence is the canonical
    /// wordlist entry, so the variant derives the same seed as the canonical phrase. It does not open a second wallet.
    /// Unknown words throw <see cref="FormatException"/>. Zero-width characters are not separators.
    /// The seed is PBKDF2-HMAC-SHA512 of the NFKD canonical sentence, 2048 iterations, 64 bytes.
    /// The salt is the NFKD string <c>mnemonic</c> plus the optional passphrase.
    /// The passphrase is not lower-cased, and zero-width characters in it are kept.
    /// </summary>
    /// <remarks>
    /// <see cref="Words"/> is a read-only view of wordlist strings. Those strings are shared and cannot be wiped:
    /// <see cref="string"/> has no erase API. The word order in <c>_indexes</c> is the secret.
    /// NBitcoin stores the sentence in a <see cref="string"/> as well.
    /// <see cref="ToString()"/> returns <c>NKaspa.Mnemonic</c> and does not contain the words.
    /// Use <see cref="ToPhrase()"/> or <see cref="GetWords()"/> for the secret text.
    /// <see cref="ParseValidated"/> and <see cref="Parse"/> throw when the checksum is wrong.
    /// The string constructor still returns an object with <see cref="IsValidChecksum"/> false, matching NBitcoin,
    /// unless <c>requireValidChecksum</c> is true.
    /// <see cref="DeriveSeed"/> and <see cref="DeriveExtKey"/> throw on a bad checksum unless
    /// <c>allowInvalidChecksum</c> is true.
    /// System.Text.Json refuses this type unless the caller opts in through <c>NKaspaJsonSerializer</c>
    /// with <c>allowPrivateKeys: true</c>.
    /// <see cref="Reveal"/> copies the sentence into a <see cref="KeystoreSecret"/> whose buffer can be zeroed.
    /// The wordlist strings cannot.
    /// </remarks>
    [JsonConverter(typeof(MnemonicJsonConverter))]
    public sealed class Mnemonic
    {
        private static readonly int[] WordCounts = { 12, 15, 18, 21, 24 };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly int[] _indexes;
        private readonly string[] _words;

        /// <summary>
        /// Parses a mnemonic. The default wordlist is English.
        /// A bad checksum is kept when <paramref name="requireValidChecksum"/> is false.
        /// <see cref="IsValidChecksum"/> reports it. <see cref="DeriveSeed"/> still refuses it.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="mnemonic"/> is null.</exception>
        /// <exception cref="System.FormatException">
        /// BIP39 checksum does not match. Word count should be 12, 15, 18, 21, or 24. Unknown BIP39 word.
        /// </exception>
        public Mnemonic(string mnemonic, Wordlist? wordlist = null, bool requireValidChecksum = false)
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
            _words = parsed.Words;
            Words = Array.AsReadOnly(_words);
            IsValidChecksum = ChecksumMatches(_indexes);
            if (requireValidChecksum && !IsValidChecksum)
            {
                throw new FormatException("BIP39 checksum does not match.");
            }
        }

        /// <summary>
        /// Parses a mnemonic and throws when the BIP39 checksum does not match.
        /// Same as <see cref="ParseValidated"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="mnemonic"/> is null.</exception>
        /// <exception cref="System.FormatException">
        /// BIP39 checksum does not match. Word count should be 12, 15, 18, 21, or 24. Unknown BIP39 word.
        /// </exception>
        public static Mnemonic Parse(string mnemonic, Wordlist? wordlist = null)
        {
            return ParseValidated(mnemonic, wordlist);
        }

        /// <summary>
        /// Parses a mnemonic and throws <see cref="FormatException"/> with the message
        /// <c>BIP39 checksum does not match.</c> when <see cref="IsValidChecksum"/> is false.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="mnemonic"/> is null.</exception>
        /// <exception cref="System.FormatException">
        /// BIP39 checksum does not match. Word count should be 12, 15, 18, 21, or 24. Unknown BIP39 word.
        /// </exception>
        public static Mnemonic ParseValidated(string mnemonic, Wordlist? wordlist = null)
        {
            return new Mnemonic(mnemonic, wordlist, requireValidChecksum: true);
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
            _words = WordsOf(_indexes, wordlist);
            Words = Array.AsReadOnly(_words);
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

        /// <summary>
        /// Words in sentence order. The list is read-only. Replacing an element throws <see cref="NotSupportedException"/>.
        /// The strings themselves are the shared wordlist entries.
        /// </summary>
        public IReadOnlyList<string> Words { get; }

        /// <summary>True when the embedded SHA-256 checksum matches the entropy.</summary>
        public bool IsValidChecksum { get; }

        /// <summary>
        /// 64-byte BIP39 seed.
        /// An empty passphrase is used when <paramref name="passphrase"/> is null.
        /// The password is the NFKD UTF-8 of the canonical sentence. The salt is the NFKD UTF-8 of <c>mnemonic</c> plus the passphrase.
        /// The passphrase keeps its case and any zero-width characters.
        /// A mnemonic with <see cref="IsValidChecksum"/> false throws unless <paramref name="allowInvalidChecksum"/> is true.
        /// That opt-in derives from the parsed words anyway. Do not use it for a wallet the user expects to restore elsewhere.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">BIP39 checksum does not match. Pass allowInvalidChecksum: true to derive a seed anyway.</exception>
        public byte[] DeriveSeed(string? passphrase = null, bool allowInvalidChecksum = false)
        {
            if (!IsValidChecksum && !allowInvalidChecksum)
            {
                throw new InvalidOperationException("BIP39 checksum does not match. Pass allowInvalidChecksum: true to derive a seed anyway.");
            }

            var password = Normalize(ToPhrase());
            var salt = Normalize("mnemonic" + (passphrase ?? string.Empty));
            using var derive = new Rfc2898DeriveBytes(password, salt, 2048, HashAlgorithmName.SHA512);
            return derive.GetBytes(64);
        }

        /// <summary>
        /// Master extended key from <see cref="DeriveSeed"/>.
        /// A bad checksum throws unless <paramref name="allowInvalidChecksum"/> is true.
        /// </summary>
        /// <exception cref="System.ArgumentException">BIP32 seed must be between 16 and 64 bytes. Private key is zero or not in the secp256k1 range. Private key must be 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">
        /// BIP39 checksum does not match. Pass allowInvalidChecksum: true to derive a seed anyway. BIP32 master
        /// key is invalid.
        /// </exception>
        public ExtKey DeriveExtKey(string? passphrase = null, bool allowInvalidChecksum = false)
        {
            var seed = DeriveSeed(passphrase, allowInvalidChecksum);
            try
            {
                return ExtKey.CreateFromSeed(seed);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(seed);
            }
        }

        /// <summary>Space-separated canonical sentence. This is the secret. <see cref="ToString()"/> does not return it.</summary>
        public string ToPhrase()
        {
            return string.Join(" ", _words);
        }

        /// <summary>
        /// A <see cref="KeystoreSecret"/> holding the UTF-8 canonical sentence.
        /// Dispose it to zero that buffer. <see cref="Words"/> still returns the shared wordlist strings. Those strings cannot be wiped.
        /// </summary>
        public KeystoreSecret Reveal()
        {
            return KeystoreSecret.FromPhrase(ToPhrase());
        }

        /// <summary>A copy of the canonical words. Changing the returned array does not change this mnemonic.</summary>
        public string[] GetWords()
        {
            var copy = new string[_words.Length];
            Array.Copy(_words, copy, _words.Length);
            return copy;
        }

        /// <summary>
        /// Type name only (<c>NKaspa.Mnemonic</c>). Safe to log.
        /// Use <see cref="ToPhrase()"/> or <see cref="GetWords()"/> for the secret sentence.
        /// </summary>
        public override string ToString()
        {
            return "NKaspa.Mnemonic";
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
