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
        public void Add(PSKT pskt)
        {
            if (pskt == null)
            {
                throw new ArgumentNullException(nameof(pskt));
            }

            _items.Add(pskt.Clone());
        }

        /// <summary>Appends every item from <paramref name="other"/>.</summary>
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
        public string ToJson()
        {
            return PSKTJson.WriteBundle(_items);
        }

        /// <summary><c>PSKB</c> plus lowercase hex of <see cref="ToJson"/>.</summary>
        public string ToHex()
        {
            return PSKTJson.ToTransport(PSKTJson.PskbPrefix, ToJson());
        }

        /// <summary>Parses <c>PSKB</c> plus hex. Each item is a Combiner.</summary>
        public static PSKTBundle FromHex(string text)
        {
            return FromJson(PSKTJson.FromTransport(text, PSKTJson.PskbPrefix, "PSKB serialization requires 'PSKB' prefix."));
        }

        /// <summary>Parses a JSON array of PSKT objects.</summary>
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
