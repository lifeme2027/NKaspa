using NKaspa;

namespace NKaspa.Crypto
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.Crypto.Hashes.
    /// Bitcoin double-SHA256 and HASH160 are not the Kaspa consensus hashes.
    /// Kaspa ids and script hashes are unkeyed BLAKE2b-256.
    /// </summary>
    public static class Hashes
    {
        /// <summary>BLAKE2b-256 of the whole buffer, returned as <see cref="uint256"/>.</summary>
        public static uint256 Blake2b256(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return Blake2b256(data, 0, data.Length);
        }

        /// <summary>BLAKE2b-256 of a slice, returned as <see cref="uint256"/>.</summary>
        public static uint256 Blake2b256(byte[] data, int offset, int count)
        {
            return new uint256(Blake2b256RawBytes(data, offset, count));
        }

        /// <summary>BLAKE2b-256 of a span, returned as <see cref="uint256"/>.</summary>
        public static uint256 Blake2b256(ReadOnlySpan<byte> data)
        {
            return new uint256(Blake2b.Hash256(data));
        }

        /// <summary>Raw 32-byte BLAKE2b-256 digest, in Kaspa wire order.</summary>
        public static byte[] Blake2b256RawBytes(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return Blake2b256RawBytes(data, 0, data.Length);
        }

        /// <summary>Raw 32-byte BLAKE2b-256 digest, in Kaspa wire order.</summary>
        public static byte[] Blake2b256RawBytes(byte[] data, int offset, int count)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || count < 0 || offset > data.Length - count)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            return Blake2b.Hash256(data.AsSpan(offset, count));
        }
    }
}
