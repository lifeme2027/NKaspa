using NKaspa.DataEncoders;

namespace NKaspa.Crypto
{
    /// <summary>
    /// 64-byte compact ECDSA signature, R and S as 32-byte big-endian scalars.
    /// Kaspa counterpart of NBitcoin.Crypto.ECDSASignature for the compact form Kaspa verifies.
    /// </summary>
    /// <remarks>
    /// Kaspa script verification parses this compact form
    /// (<c>secp256k1_ecdsa_signature_parse_compact</c>), not Bitcoin DER.
    /// The sighash byte is stored beside the signature in a sigScript.
    /// Signatures produced by <see cref="Key"/> use the low-S form.
    /// </remarks>
    public sealed class ECDSASignature : IEquatable<ECDSASignature>
    {
        /// <summary>Length of the compact signature without the sighash byte.</summary>
        public const int Length = 64;

        private readonly byte[] _bytes;

        /// <summary>Copies a 64-byte compact signature.</summary>
        public ECDSASignature(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != Length)
            {
                throw new ArgumentException("ECDSA signature must be 64 bytes.", nameof(bytes));
            }

            _bytes = bytes.ToArray();
        }

        /// <summary>Parses a 128-character hex signature.</summary>
        public static ECDSASignature Parse(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            return new ECDSASignature(Encoders.Hex.DecodeData(hex));
        }

        /// <summary>A copy of the 64 signature bytes.</summary>
        public byte[] ToBytes()
        {
            return (byte[])_bytes.Clone();
        }

        /// <summary>Writes the 64 signature bytes into <paramref name="destination"/>.</summary>
        public void WriteTo(Span<byte> destination)
        {
            if (destination.Length != Length)
            {
                throw new ArgumentException("Destination must be 64 bytes.", nameof(destination));
            }

            _bytes.CopyTo(destination);
        }

        /// <summary>Lowercase hex of the 64 signature bytes.</summary>
        public override string ToString()
        {
            return Encoders.Hex.EncodeData(_bytes);
        }

        /// <inheritdoc />
        public bool Equals(ECDSASignature? other)
        {
            return other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as ECDSASignature);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = 17;
            for (var i = 0; i < _bytes.Length; i++)
            {
                hash = (hash * 31) + _bytes[i];
            }

            return hash;
        }
    }
}
