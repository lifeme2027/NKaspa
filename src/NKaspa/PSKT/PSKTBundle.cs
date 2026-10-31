namespace NKaspa.PSKT
{
    /// <summary>
    /// A sequence of PSKTs transported as one payload.
    /// rusty-kaspa serializes <c>Bundle(Vec&lt;Inner&gt;)</c> as a JSON array, then <c>PSKB</c> plus hex.
    /// </summary>
    public sealed class PSKTBundle
    {
        private readonly List<PSKT> _items = new List<PSKT>();

        /// <summary>Creates an empty bundle.</summary>
        public PSKTBundle()
        {
        }

        /// <summary>Creates a bundle that holds <paramref name="pskt"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="pskt"/> is null.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public PSKTBundle(PSKT pskt)
        {
            Add(pskt);
        }

        /// <summary>PSKT values, in order. Each one is a Combiner after <see cref="FromHex"/>.</summary>
        public IReadOnlyList<PSKT> Items
        {
            get { return _items; }
        }

        /// <summary>Appends a copy so later role changes do not alter <paramref name="pskt"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="pskt"/> is null.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public void Add(PSKT pskt)
        {
            if (pskt == null)
            {
                throw new ArgumentNullException(nameof(pskt));
            }

            _items.Add(pskt.Clone());
        }

        /// <summary>Appends every item from <paramref name="other"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public void Merge(PSKTBundle other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            for (var i = 0; i < other._items.Count; i++)
            {
                _items.Add(other._items[i].Clone());
            }
        }

        /// <summary>JSON array of inner objects.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">ECDSA signature must be 64 bytes.</exception>
        public string ToJson()
        {
            return PSKTJson.WriteBundle(_items);
        }

        /// <summary><c>PSKB</c> plus lowercase hex of <see cref="ToJson"/>.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">ECDSA signature must be 64 bytes.</exception>
        public string ToHex()
        {
            return PSKTJson.ToTransport(PSKTJson.PskbPrefix, ToJson());
        }

        /// <summary>Parses <c>PSKB</c> plus hex. Each item is a Combiner.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Hardened index must be below 2^31.</exception>
        /// <exception cref="System.ArgumentException">
        /// An HD fingerprint is 4 bytes. ECDSA public key is not on the curve. ECDSA public key must be
        /// compressed (0x02 or 0x03). ECDSA public key must be 33 bytes. ECDSA signature must be 64 bytes.
        /// Schnorr signature must be 64 bytes.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// PSKT JSON must be an object. Missing JSON property {name}. uint256 hex must be 32 bytes. uint256
        /// requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even. Expected
        /// an unsigned integer. Key path is empty. Key path index is not a number. Key path contains an empty
        /// index. Script push is too long. Script push is truncated. Script public key hex must start with a
        /// 2-byte version. Sighash type is not one of the six Kaspa values. Expected a byte. Partial signature
        /// is empty. Partial signature must be schnorr or eCDSA. ECDSA signature is not strict DER. Further
        /// failures of this type use the same exception.
        /// </exception>
        public static PSKTBundle FromHex(string text)
        {
            return FromJson(PSKTJson.FromTransport(text, PSKTJson.PskbPrefix, "PSKB serialization requires 'PSKB' prefix."));
        }

        /// <summary>Parses a JSON array of PSKT objects.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="json"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Hardened index must be below 2^31.</exception>
        /// <exception cref="System.ArgumentException">
        /// An HD fingerprint is 4 bytes. ECDSA public key is not on the curve. ECDSA public key must be
        /// compressed (0x02 or 0x03). ECDSA public key must be 33 bytes. ECDSA signature must be 64 bytes.
        /// Schnorr signature must be 64 bytes.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// PSKT JSON must be an object. Missing JSON property {name}. uint256 hex must be 32 bytes. uint256
        /// requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even. Expected
        /// an unsigned integer. Key path is empty. Key path index is not a number. Key path contains an empty
        /// index. Script push is too long. Script push is truncated. Script public key hex must start with a
        /// 2-byte version. Sighash type is not one of the six Kaspa values. Expected a byte. Partial signature
        /// is empty. Partial signature must be schnorr or eCDSA. ECDSA signature is not strict DER. Further
        /// failures of this type use the same exception.
        /// </exception>
        public static PSKTBundle FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var bundle = new PSKTBundle();
            var items = PSKTJson.ReadBundleArray(json);
            for (var i = 0; i < items.Count; i++)
            {
                bundle._items.Add(PSKT.FromJson(items[i]));
            }

            return bundle;
        }
    }
}
