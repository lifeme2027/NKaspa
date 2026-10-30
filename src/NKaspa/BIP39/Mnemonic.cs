using System.Security.Cryptography;
using System.Text;
using NKaspa.Crypto;

namespace NKaspa
{
    /// <summary>
    /// BIP39 mnemonic. Kaspa counterpart of NBitcoin.Mnemonic.
    /// The seed is PBKDF2-HMAC-SHA512 of the NFKD mnemonic, 2048 iterations, 64 bytes.
    /// The salt is the NFKD string <c>mnemonic</c> plus the optional passphrase.
    /// </summary>
    public sealed class Mnemonic
    {
        private static readonly int[] WordCounts = { 12, 15, 18, 21, 24 };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly int[] _indexes;

        /// <summary>Parses a mnemonic. The default wordlist is English.</summary>
        public Mnemonic(string mnemonic, Wordlist? wordlist = null)
        {
            if (mnemonic == null)
            {
                throw new ArgumentNullException(nameof(mnemonic));
            }

            WordList = wordlist ?? Wordlist.English;
            var parts = mnemonic.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (!IsLegalCount(parts.Length))
            {
                throw new FormatException("Word count should be 12, 15, 18, 21, or 24.");
            }

            _indexes = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                _indexes[i] = WordList.GetIndex(parts[i]);
            }

            Words = parts;
            IsValidChecksum = ChecksumMatches(_indexes);
        }

        /// <summary>Builds a mnemonic from entropy. The length must be 16, 20, 24, 28, or 32 bytes.</summary>
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
        /// The checksum is not consulted. PBKDF2 runs on the sentence as given.
        /// </summary>
        public byte[] DeriveSeed(string? passphrase = null)
        {
            var password = Normalize(ToString());
            var salt = Normalize("mnemonic" + (passphrase ?? string.Empty));
            using var derive = new Rfc2898DeriveBytes(password, salt, 2048, HashAlgorithmName.SHA512);
            return derive.GetBytes(64);
        }

        /// <summary>Master extended key from <see cref="DeriveSeed"/>.</summary>
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
    }
}
