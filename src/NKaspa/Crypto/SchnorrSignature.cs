using NKaspa.DataEncoders;

namespace NKaspa.Crypto
{
    /// <summary>
    /// 64-byte BIP-340 Schnorr signature. Kaspa counterpart of NBitcoin.Crypto.SchnorrSignature.
    /// The sighash byte is not part of this value. It is stored beside the signature in a sigScript.
    /// </summary>
    public sealed class SchnorrSignature : IEquatable<SchnorrSignature>
    {
        /// <summary>Length of the signature without the sighash byte.</summary>
        public const int Length = 64;

        private readonly byte[] _bytes;

        /// <summary>Copies a 64-byte signature. The bytes are not checked against a public key here.</summary>
        /// <exception cref="System.ArgumentException">Schnorr signature must be 64 bytes.</exception>
        public SchnorrSignature(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != Length)
            {
                throw new ArgumentException("Schnorr signature must be 64 bytes.", nameof(bytes));
            }

            _bytes = bytes.ToArray();
        }

        /// <summary>Parses a 128-character hex signature.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="hex"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Schnorr signature must be 64 bytes.</exception>
        /// <exception cref="System.FormatException">Hex string contains a non-hex character. Hex string length must be even.</exception>
        public static SchnorrSignature Parse(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            var bytes = Encoders.Hex.DecodeData(hex);
            return new SchnorrSignature(bytes);
        }

        /// <summary>A copy of the 64 signature bytes.</summary>
        public byte[] ToBytes()
        {
            return (byte[])_bytes.Clone();
        }

        /// <summary>Writes the 64 signature bytes into <paramref name="destination"/>.</summary>
        /// <exception cref="System.ArgumentException">Destination must be 64 bytes.</exception>
        public void WriteTo(Span<byte> destination)
        {
            if (destination.Length != Length)
            {
                throw new ArgumentException("Destination must be 64 bytes.", nameof(destination));
            }

            _bytes.CopyTo(destination);
        }

        /// <summary>Lowercase hex of the 64 signature bytes.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public override string ToString()
        {
            return Encoders.Hex.EncodeData(_bytes);
        }

        /// <inheritdoc />
        public bool Equals(SchnorrSignature? other)
        {
            return other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as SchnorrSignature);
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
