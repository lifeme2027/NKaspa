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

        private static readonly byte[] Secp256k1Order = Encoders.Hex.DecodeData("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141");

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

        /// <summary>
        /// DER encoding used by rusty-kaspa PSKT JSON for an ECDSA signature.
        /// The script still uses <see cref="ToBytes"/>. This follows libsecp256k1
        /// <c>secp256k1_ecdsa_signature_serialize_der</c>: high-bit integers get a leading 0x00.
        /// </summary>
        public byte[] ToDer()
        {
            var r = EncodeDerInteger(_bytes.AsSpan(0, 32));
            var s = EncodeDerInteger(_bytes.AsSpan(32, 32));
            var der = new byte[2 + r.Length + s.Length];
            der[0] = 0x30;
            der[1] = (byte)(r.Length + s.Length);
            r.CopyTo(der.AsSpan(2));
            s.CopyTo(der.AsSpan(2 + r.Length));
            return der;
        }

        /// <summary>Lowercase hex of <see cref="ToDer"/>.</summary>
        public string ToDerHex()
        {
            return Encoders.Hex.EncodeData(ToDer());
        }

        /// <summary>Parses a strict DER ECDSA signature into the 64-byte compact form.</summary>
        public static ECDSASignature FromDer(ReadOnlySpan<byte> der)
        {
            if (!TryFromDer(der, out var signature) || signature == null)
            {
                throw new FormatException("ECDSA signature is not strict DER.");
            }

            return signature;
        }

        /// <summary>Parses strict DER. R and S must be non-zero and below the secp256k1 order.</summary>
        public static bool TryFromDer(ReadOnlySpan<byte> der, out ECDSASignature? signature)
        {
            signature = null;
            if (der.Length < 8 || der[0] != 0x30)
            {
                return false;
            }

            var length = der[1];
            if (length != der.Length - 2)
            {
                return false;
            }

            var offset = 2;
            if (!ReadDerInteger(der, ref offset, out var r) || !ReadDerInteger(der, ref offset, out var s) || offset != der.Length)
            {
                return false;
            }

            if (!IsValidScalar(r) || !IsValidScalar(s))
            {
                return false;
            }

            var compact = new byte[Length];
            r.CopyTo(compact.AsSpan(0));
            s.CopyTo(compact.AsSpan(32));
            signature = new ECDSASignature(compact);
            return true;
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

        private static byte[] EncodeDerInteger(ReadOnlySpan<byte> scalar)
        {
            var start = 0;
            while (start < 31 && scalar[start] == 0)
            {
                start++;
            }

            var pad = scalar[start] >= 0x80;
            var length = 32 - start;
            var encoded = new byte[2 + length + (pad ? 1 : 0)];
            encoded[0] = 0x02;
            encoded[1] = (byte)(length + (pad ? 1 : 0));
            var offset = 2;
            if (pad)
            {
                encoded[offset++] = 0x00;
            }

            scalar.Slice(start).CopyTo(encoded.AsSpan(offset));
            return encoded;
        }

        private static bool ReadDerInteger(ReadOnlySpan<byte> der, ref int offset, out byte[] scalar)
        {
            scalar = Array.Empty<byte>();
            if (offset >= der.Length || der[offset++] != 0x02)
            {
                return false;
            }

            if (offset >= der.Length)
            {
                return false;
            }

            var length = der[offset++];
            if (length == 0 || offset + length > der.Length)
            {
                return false;
            }

            var body = der.Slice(offset, length);
            offset += length;
            if ((body[0] & 0x80) != 0)
            {
                return false;
            }

            if (body.Length > 1 && body[0] == 0x00 && body[1] < 0x80)
            {
                return false;
            }

            if (body[0] == 0x00)
            {
                body = body.Slice(1);
            }

            if (body.Length > 32)
            {
                return false;
            }

            scalar = new byte[32];
            body.CopyTo(scalar.AsSpan(32 - body.Length));
            return true;
        }

        private static bool IsValidScalar(ReadOnlySpan<byte> scalar)
        {
            var zero = true;
            for (var i = 0; i < scalar.Length; i++)
            {
                if (scalar[i] != 0)
                {
                    zero = false;
                    break;
                }
            }

            if (zero)
            {
                return false;
            }

            for (var i = 0; i < 32; i++)
            {
                if (scalar[i] < Secp256k1Order[i])
                {
                    return true;
                }

                if (scalar[i] > Secp256k1Order[i])
                {
                    return false;
                }
            }

            return false;
        }
    }
}
