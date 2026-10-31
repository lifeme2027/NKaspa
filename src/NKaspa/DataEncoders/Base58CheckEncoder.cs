using NKaspa.Crypto;

namespace NKaspa.DataEncoders
{
    /// <summary>
    /// Base58Check encoder. Kaspa counterpart of NBitcoin.DataEncoders.Base58CheckEncoder.
    /// The checksum is the first four bytes of SHA256(SHA256(payload)).
    /// Kaspa addresses do not use this encoding. BIP32 extended keys do.
    /// </summary>
    public sealed class Base58CheckEncoder : DataEncoder
    {
        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException"><paramref name="data"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> is outside the allowed range.</exception>
        public override string EncodeData(byte[] data, int offset, int count)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || count < 0 || offset > data.Length - count)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            return Encode(data.AsSpan(offset, count));
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException"><paramref name="encoded"/> is null.</exception>
        /// <exception cref="System.FormatException">
        /// Invalid Base58Check checksum. Base58Check payload is shorter than the checksum. Base58 value does
        /// not fit in the decode buffer. Invalid Base58 character.
        /// </exception>
        public override byte[] DecodeData(string encoded)
        {
            return Decode(encoded);
        }

        /// <summary>Appends a 4-byte checksum and encodes the result as Base58.</summary>
        public static string Encode(ReadOnlySpan<byte> payload)
        {
            var body = new byte[payload.Length + 4];
            payload.CopyTo(body);
            var checksum = Checksum(payload);
            checksum.CopyTo(body.AsSpan(payload.Length));
            return Base58Encoder.Encode(body);
        }

        /// <summary>
        /// Decodes Base58Check. A bad checksum throws <see cref="FormatException"/>.
        /// The returned buffer is the payload without the checksum.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="encoded"/> is null.</exception>
        /// <exception cref="System.FormatException">
        /// Invalid Base58Check checksum. Base58Check payload is shorter than the checksum. Base58 value does
        /// not fit in the decode buffer. Invalid Base58 character.
        /// </exception>
        public static byte[] Decode(string encoded)
        {
            var full = Base58Encoder.Decode(encoded);
            if (full.Length < 4)
            {
                throw new FormatException("Base58Check payload is shorter than the checksum.");
            }

            var payloadLength = full.Length - 4;
            var payload = full.AsSpan(0, payloadLength);
            var expected = Checksum(payload);
            if (!expected.AsSpan().SequenceEqual(full.AsSpan(payloadLength)))
            {
                throw new FormatException("Invalid Base58Check checksum.");
            }

            return payload.ToArray();
        }

        private static byte[] Checksum(ReadOnlySpan<byte> payload)
        {
            var hash = Hashes.SHA256(Hashes.SHA256(payload));
            var checksum = new byte[4];
            hash.AsSpan(0, 4).CopyTo(checksum);
            return checksum;
        }
    }
}
