using System.Globalization;

namespace NKaspa
{
    /// <summary>
    /// BIP32 derivation path. Kaspa counterpart of NBitcoin.KeyPath.
    /// A hardened index is written with a trailing apostrophe.
    /// The Kaspa default account is <c>m/44'/111111'/account'</c>.
    /// Receive addresses use change index 0. Change addresses use change index 1.
    /// </summary>
    public sealed class KeyPath
    {
        /// <summary>BIP44 purpose.</summary>
        public const uint KaspaPurpose = 44;

        /// <summary>Kaspa coin type registered for BIP44.</summary>
        public const uint KaspaCoinType = 111111;

        private readonly uint[] _indexes;

        /// <summary>Parses <c>m/44'/0'/0'/0/1</c>. The leading <c>m</c> is optional.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Hardened index must be below 2^31.</exception>
        /// <exception cref="System.FormatException">Key path is empty. Key path index is not a number. Key path contains an empty index.</exception>
        public KeyPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var text = path.Trim();
            if (text.Length == 0)
            {
                throw new FormatException("Key path is empty.");
            }

            var parts = text.Split('/');
            var start = 0;
            if (parts[0] == "m" || parts[0] == "M")
            {
                start = 1;
            }

            var indexes = new List<uint>(parts.Length - start);
            for (var i = start; i < parts.Length; i++)
            {
                indexes.Add(ParseIndex(parts[i]));
            }

            _indexes = indexes.ToArray();
        }

        /// <summary>Copies child indexes. Each value may already include the hardened bit.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="indexes"/> is null.</exception>
        public KeyPath(IEnumerable<uint> indexes)
        {
            if (indexes == null)
            {
                throw new ArgumentNullException(nameof(indexes));
            }

            _indexes = indexes.ToArray();
        }

        /// <summary>Child indexes, hardened bit included.</summary>
        public IReadOnlyList<uint> Indexes
        {
            get { return _indexes; }
        }

        /// <summary>Account node <c>m/44'/111111'/account'</c>.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Hardened index must be below 2^31.</exception>
        public static KeyPath KaspaAccount(uint account = 0)
        {
            return new KeyPath(new[]
            {
                Harden(KaspaPurpose),
                Harden(KaspaCoinType),
                Harden(account),
            });
        }

        /// <summary>Receive path <c>m/44'/111111'/account'/0/index</c>.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Hardened index must be below 2^31.</exception>
        public static KeyPath KaspaReceive(uint index, uint account = 0)
        {
            return KaspaAccount(account).Derive(0).Derive(index);
        }

        /// <summary>Change path <c>m/44'/111111'/account'/1/index</c>.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Hardened index must be below 2^31.</exception>
        public static KeyPath KaspaChange(uint index, uint account = 0)
        {
            return KaspaAccount(account).Derive(1).Derive(index);
        }

        /// <summary>Sets the BIP32 hardened bit. <paramref name="index"/> must be below 2^31.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Hardened index must be below 2^31.</exception>
        public static uint Harden(uint index)
        {
            if (index >= 0x80000000u)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Hardened index must be below 2^31.");
            }

            return index | 0x80000000u;
        }

        /// <summary>Appends one child index.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        public KeyPath Derive(uint index)
        {
            var next = new uint[_indexes.Length + 1];
            Array.Copy(_indexes, next, _indexes.Length);
            next[_indexes.Length] = index;
            return new KeyPath(next);
        }

        /// <summary>Appends <paramref name="index"/>. <paramref name="hardened"/> sets the high bit.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Hardened index must be below 2^31.</exception>
        public KeyPath Derive(uint index, bool hardened)
        {
            return Derive(hardened ? Harden(index) : index);
        }

        /// <summary><c>m/44'/0'/0'/0/1</c>.</summary>
        public override string ToString()
        {
            var parts = new string[_indexes.Length + 1];
            parts[0] = "m";
            for (var i = 0; i < _indexes.Length; i++)
            {
                var index = _indexes[i];
                if ((index & 0x80000000u) != 0)
                {
                    parts[i + 1] = (index & 0x7fffffffu).ToString(CultureInfo.InvariantCulture) + "'";
                }
                else
                {
                    parts[i + 1] = index.ToString(CultureInfo.InvariantCulture);
                }
            }

            return string.Join("/", parts);
        }

        private static uint ParseIndex(string text)
        {
            if (text.Length == 0)
            {
                throw new FormatException("Key path contains an empty index.");
            }

            var hardened = false;
            var number = text;
            var last = text[text.Length - 1];
            if (last == '\'' || last == 'h' || last == 'H')
            {
                hardened = true;
                number = text.Substring(0, text.Length - 1);
            }

            if (!uint.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                throw new FormatException("Key path index is not a number.");
            }

            if (!hardened)
            {
                return index;
            }

            return Harden(index);
        }
    }
}
