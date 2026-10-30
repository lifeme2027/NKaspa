using NKaspa;

namespace NKaspa.Crypto
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.Crypto.Hashes.
    /// Script hashes are unkeyed BLAKE2b-256. Transaction ids use a keyed digest.
    /// SHA256 and HASH160 exist only for BIP32 fingerprints and Base58Check.
    /// They are not Kaspa address hashes and they are not consensus hashes.
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

        /// <summary>
        /// SHA-256. Used by BIP39 checksums, BIP32 Base58Check, and HASH160.
        /// Not a Kaspa consensus hash.
        /// </summary>
        public static byte[] SHA256(ReadOnlySpan<byte> data)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            return hash.ComputeHash(data.ToArray());
        }

        /// <summary>
        /// RIPEMD160(SHA256(data)). BIP32 parent fingerprint only.
        /// Kaspa addresses do not use HASH160.
        /// </summary>
        public static byte[] Hash160(ReadOnlySpan<byte> data)
        {
            return Ripemd160.Compute(SHA256(data));
        }
    }
}
