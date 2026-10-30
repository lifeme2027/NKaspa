using NKaspa.Crypto;

namespace NKaspa
{
    /// <summary>
    /// First four bytes of HASH160(compressed public key).
    /// Kaspa counterpart of NBitcoin.HDFingerprint.
    /// The bytes are stored in hash order. They are not reversed.
    /// </summary>
    public readonly struct HDFingerprint : IEquatable<HDFingerprint>
    {
        private readonly byte _b0;
        private readonly byte _b1;
        private readonly byte _b2;
        private readonly byte _b3;

        /// <summary>Copies four fingerprint bytes.</summary>
        public HDFingerprint(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != 4)
            {
                throw new ArgumentException("An HD fingerprint is 4 bytes.", nameof(bytes));
            }

            _b0 = bytes[0];
            _b1 = bytes[1];
            _b2 = bytes[2];
            _b3 = bytes[3];
        }

        /// <summary>HASH160 of a 33-byte compressed public key, truncated to 4 bytes.</summary>
        public static HDFingerprint FromCompressedPublicKey(ReadOnlySpan<byte> compressedPublicKey)
        {
            if (compressedPublicKey.Length != 33)
            {
                throw new ArgumentException("Compressed public key must be 33 bytes.", nameof(compressedPublicKey));
            }

            var hash = Hashes.Hash160(compressedPublicKey);
            return new HDFingerprint(hash.AsSpan(0, 4));
        }

        /// <summary>True when all four bytes are zero. A master key uses that fingerprint.</summary>
        public bool IsZero
        {
            get { return _b0 == 0 && _b1 == 0 && _b2 == 0 && _b3 == 0; }
        }

        /// <summary>The four fingerprint bytes, in hash order.</summary>
        public byte[] ToBytes()
        {
            return new[] { _b0, _b1, _b2, _b3 };
        }

        /// <inheritdoc />
        public bool Equals(HDFingerprint other)
        {
            return _b0 == other._b0 && _b1 == other._b1 && _b2 == other._b2 && _b3 == other._b3;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is HDFingerprint other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (_b0 << 24) | (_b1 << 16) | (_b2 << 8) | _b3;
        }

        /// <summary>Lowercase hex of the four bytes.</summary>
        public override string ToString()
        {
            return DataEncoders.Encoders.Hex.EncodeData(ToBytes());
        }
    }
}
