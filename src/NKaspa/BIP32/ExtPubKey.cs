using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// BIP32 extended public key. Kaspa counterpart of NBitcoin.ExtPubKey.
    /// Hardened children cannot be derived from a public key.
    /// Non-hardened children add the tweak point to the compressed parent.
    /// The Kaspa address at this node is the Schnorr address of <see cref="PubKey"/>.
    /// </summary>
    public sealed class ExtPubKey : IEquatable<ExtPubKey>
    {
        private const int PayloadLength = 78;

        private readonly PubKeyECDSA _compressed;
        private readonly byte[] _chainCode;
        private readonly byte _depth;
        private readonly HDFingerprint _parentFingerprint;
        private readonly uint _child;
        private readonly uint _version;

        internal ExtPubKey(PubKeyECDSA compressed, byte[] chainCode, byte depth, HDFingerprint parentFingerprint, uint child, uint version)
        {
            _compressed = compressed ?? throw new ArgumentNullException(nameof(compressed));
            _chainCode = (byte[])chainCode.Clone();
            _depth = depth;
            _parentFingerprint = parentFingerprint;
            _child = child;
            _version = version;
        }

        /// <summary>Parses a Base58Check extended public key.</summary>
        public static ExtPubKey Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var payload = Base58CheckEncoder.Decode(text.Trim());
            if (payload.Length != PayloadLength)
            {
                throw new FormatException("An extended key payload is 78 bytes.");
            }

            var version = ExtKey.ReadU32BE(payload.AsSpan(0, 4));
            if (!HdKeyVersions.IsPublic(version))
            {
                throw new FormatException("Extended key version is not a public prefix, or it does not match the key type.");
            }

            var depth = payload[4];
            var fingerprint = new HDFingerprint(payload.AsSpan(5, 4));
            var child = ExtKey.ReadU32BE(payload.AsSpan(9, 4));
            if (depth == 0 && (!fingerprint.IsZero || child != 0))
            {
                throw new FormatException("A master extended key must have a zero parent fingerprint and a zero child index.");
            }

            var chain = payload.AsSpan(13, 32).ToArray();
            var compressedBytes = payload.AsSpan(45, 33).ToArray();
            if (compressedBytes[0] != 0x02 && compressedBytes[0] != 0x03)
            {
                throw new FormatException("Extended public key must start with 0x02 or 0x03.");
            }

            PubKeyECDSA compressed;
            try
            {
                compressed = new PubKeyECDSA(compressedBytes);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Extended public key is not on the curve.", exception);
            }

            return new ExtPubKey(compressed, chain, depth, fingerprint, child, version);
        }

        /// <summary>BIP-340 x-only key. Kaspa addresses use this key.</summary>
        public PubKey PubKey
        {
            get { return new PubKey(_compressed.XOnly); }
        }

        /// <summary>Compressed SEC key stored by BIP32.</summary>
        public PubKeyECDSA PubKeyECDSA
        {
            get { return _compressed; }
        }

        /// <summary>A copy of the 32-byte chain code.</summary>
        public byte[] ChainCode
        {
            get { return (byte[])_chainCode.Clone(); }
        }

        /// <summary>Depth. A master key is 0.</summary>
        public byte Depth
        {
            get { return _depth; }
        }

        /// <summary>Parent fingerprint.</summary>
        public HDFingerprint ParentFingerprint
        {
            get { return _parentFingerprint; }
        }

        /// <summary>Child index, including the hardened bit.</summary>
        public uint Child
        {
            get { return _child; }
        }

        /// <summary>Version prefix stored with this key.</summary>
        public uint Version
        {
            get { return _version; }
        }

        /// <summary>Schnorr address of <see cref="PubKey"/> on <paramref name="network"/>.</summary>
        public KaspaPubKeyAddress GetAddress(Network network)
        {
            return PubKey.GetAddress(network);
        }

        /// <summary>Derives a non-hardened child. A hardened index throws <see cref="InvalidOperationException"/>.</summary>
        public ExtPubKey Derive(uint index)
        {
            if ((index & 0x80000000u) != 0)
            {
                throw new InvalidOperationException("A public key cannot derive a hardened child.");
            }

            if (_depth == byte.MaxValue)
            {
                throw new InvalidOperationException("Cannot derive a child at depth 255.");
            }

            var data = new byte[37];
            _compressed.ToBytes().CopyTo(data, 0);
            ExtKey.WriteU32BE(data.AsSpan(33), index);
            var mac = Bip32Curve.HmacSha512(_chainCode, data);
            byte[] childCompressed;
            try
            {
                childCompressed = Bip32Curve.TweakPublic(_compressed.ToBytes(), mac.AsSpan(0, 32));
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException("BIP32 child public key is invalid.", exception);
            }

            var chain = mac.AsSpan(32, 32).ToArray();
            var fingerprint = HDFingerprint.FromCompressedPublicKey(_compressed.ToBytes());
            return new ExtPubKey(new PubKeyECDSA(childCompressed), chain, (byte)(_depth + 1), fingerprint, index, _version);
        }

        /// <summary>Derives each non-hardened index in <paramref name="path"/>.</summary>
        public ExtPubKey Derive(KeyPath path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var current = this;
            for (var i = 0; i < path.Indexes.Count; i++)
            {
                current = current.Derive(path.Indexes[i]);
            }

            return current;
        }

        /// <summary>Base58Check using the version stored on this key.</summary>
        public string ToBase58()
        {
            return ToBase58(_version);
        }

        /// <summary>Base58Check using <paramref name="version"/> as the 4-byte prefix.</summary>
        public string ToBase58(uint version)
        {
            var payload = new byte[PayloadLength];
            ExtKey.WriteU32BE(payload.AsSpan(0, 4), version);
            payload[4] = _depth;
            _parentFingerprint.ToBytes().CopyTo(payload, 5);
            ExtKey.WriteU32BE(payload.AsSpan(9, 4), _child);
            _chainCode.CopyTo(payload, 13);
            _compressed.ToBytes().CopyTo(payload, 45);
            return Base58CheckEncoder.Encode(payload);
        }

        /// <summary>Base58Check with the Kaspa public prefix of <paramref name="network"/>.</summary>
        public string ToString(Network network)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            return ToBase58(network.ExtPubKeyVersion);
        }

        /// <summary>Base58Check with the stored version.</summary>
        public override string ToString()
        {
            return ToBase58();
        }

        /// <inheritdoc />
        public bool Equals(ExtPubKey? other)
        {
            if (other is null)
            {
                return false;
            }

            return _depth == other._depth
                && _child == other._child
                && _parentFingerprint.Equals(other._parentFingerprint)
                && _compressed.Equals(other._compressed)
                && _chainCode.AsSpan().SequenceEqual(other._chainCode);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as ExtPubKey);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (_depth * 397) ^ (int)_child ^ _compressed.GetHashCode();
        }
    }
}
