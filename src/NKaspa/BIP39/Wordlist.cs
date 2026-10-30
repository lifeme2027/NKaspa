using System.Reflection;

namespace NKaspa
{
    /// <summary>
    /// BIP39 wordlist. Kaspa counterpart of NBitcoin.Wordlist.
    /// The English list is the NBitcoin copy of the BIP39 English wordlist.
    /// See <c>BIP39/WORDLIST-LICENSE.txt</c>.
    /// </summary>
    public sealed class Wordlist
    {
        private const string EnglishResource = "NKaspa.BIP39.english.txt";

        private readonly string[] _words;
        private readonly Dictionary<string, int> _indexes;

        private Wordlist(string[] words)
        {
            _words = words;
            _indexes = new Dictionary<string, int>(words.Length, StringComparer.Ordinal);
            for (var i = 0; i < words.Length; i++)
            {
                _indexes.Add(words[i], i);
            }
        }

        /// <summary>English BIP39 wordlist (2048 words).</summary>
        public static Wordlist English { get; } = LoadEnglish();

        /// <summary>Number of words. The English list has 2048.</summary>
        public int Count
        {
            get { return _words.Length; }
        }

        /// <summary>Word at <paramref name="index"/>. Indexes run from 0 through 2047.</summary>
        public string this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_words.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _words[index];
            }
        }

        /// <summary>The list for <paramref name="language"/>.</summary>
        public static Wordlist Load(Language language)
        {
            if (language == Language.English)
            {
                return English;
            }

            throw new ArgumentOutOfRangeException(nameof(language));
        }

        /// <summary>True when every word belongs to this list.</summary>
        public bool TryGetIndex(string word, out int index)
        {
            if (word == null)
            {
                index = 0;
                return false;
            }

            return _indexes.TryGetValue(word, out index);
        }

        /// <summary>Index of <paramref name="word"/>. An unknown word throws <see cref="FormatException"/>.</summary>
        public int GetIndex(string word)
        {
            if (!TryGetIndex(word, out var index))
            {
                throw new FormatException("Unknown BIP39 word.");
            }

            return index;
        }

        private static Wordlist LoadEnglish()
        {
            var stream = typeof(Wordlist).GetTypeInfo().Assembly.GetManifestResourceStream(EnglishResource);
            if (stream == null)
            {
                throw new InvalidOperationException("The English BIP39 wordlist resource is missing.");
            }

            using (stream)
            using (var reader = new StreamReader(stream))
            {
                var text = reader.ReadToEnd();
                var words = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length != 2048)
                {
                    throw new InvalidOperationException("The English BIP39 wordlist must contain 2048 words.");
                }

                return new Wordlist(words);
            }
        }
    }
}
