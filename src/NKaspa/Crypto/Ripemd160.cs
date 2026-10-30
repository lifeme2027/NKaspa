namespace NKaspa.Crypto
{
    /// <summary>
    /// RIPEMD-160 (ISO/IEC 10118-3).
    /// Kaspa addresses and consensus hashes do not use this function.
    /// BIP32 parent fingerprints do: they are the first four bytes of HASH160.
    /// </summary>
    internal static class Ripemd160
    {
        private const int DigestLength = 20;

        private static readonly int[] R1 =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            7, 4, 13, 1, 10, 6, 15, 3, 12, 0, 9, 5, 2, 14, 11, 8,
            3, 10, 14, 4, 9, 15, 8, 1, 2, 7, 0, 6, 13, 11, 5, 12,
            1, 9, 11, 10, 0, 8, 12, 4, 13, 3, 7, 15, 14, 5, 6, 2,
            4, 0, 5, 9, 7, 12, 2, 10, 14, 1, 3, 8, 11, 6, 15, 13,
        };

        private static readonly int[] R2 =
        {
            5, 14, 7, 0, 9, 2, 11, 4, 13, 6, 15, 8, 1, 10, 3, 12,
            6, 11, 3, 7, 0, 13, 5, 10, 14, 15, 8, 12, 4, 9, 1, 2,
            15, 5, 1, 3, 7, 14, 6, 9, 11, 8, 12, 2, 10, 0, 4, 13,
            8, 6, 4, 1, 3, 11, 15, 0, 5, 12, 2, 13, 9, 7, 10, 14,
            12, 15, 10, 4, 1, 5, 8, 7, 6, 2, 13, 14, 0, 3, 9, 11,
        };

        private static readonly int[] S1 =
        {
            11, 14, 15, 12, 5, 8, 7, 9, 11, 13, 14, 15, 6, 7, 9, 8,
            7, 6, 8, 13, 11, 9, 7, 15, 7, 12, 15, 9, 11, 7, 13, 12,
            11, 13, 6, 7, 14, 9, 13, 15, 14, 8, 13, 6, 5, 12, 7, 5,
            11, 12, 14, 15, 14, 15, 9, 8, 9, 14, 5, 6, 8, 6, 5, 12,
            9, 15, 5, 11, 6, 8, 13, 12, 5, 12, 13, 14, 11, 8, 5, 6,
        };

        private static readonly int[] S2 =
        {
            8, 9, 9, 11, 13, 15, 15, 5, 7, 7, 8, 11, 14, 14, 12, 6,
            9, 13, 15, 7, 12, 8, 9, 11, 7, 7, 12, 7, 6, 15, 13, 11,
            9, 7, 15, 11, 8, 6, 6, 14, 12, 13, 5, 14, 13, 13, 7, 5,
            15, 5, 8, 11, 14, 14, 6, 14, 6, 9, 12, 9, 12, 5, 15, 8,
            8, 5, 12, 9, 12, 5, 14, 6, 8, 13, 6, 5, 15, 13, 11, 11,
        };

        /// <summary>20-byte RIPEMD-160 digest.</summary>
        public static byte[] Compute(ReadOnlySpan<byte> data)
        {
            var message = Pad(data);
            uint h0 = 0x67452301;
            uint h1 = 0xEFCDAB89;
            uint h2 = 0x98BADCFE;
            uint h3 = 0x10325476;
            uint h4 = 0xC3D2E1F0;
            var block = new uint[16];

            for (var offset = 0; offset < message.Length; offset += 64)
            {
                for (var i = 0; i < 16; i++)
                {
                    var p = offset + (i * 4);
                    block[i] = (uint)message[p]
                        | ((uint)message[p + 1] << 8)
                        | ((uint)message[p + 2] << 16)
                        | ((uint)message[p + 3] << 24);
                }

                uint a = h0, b = h1, c = h2, d = h3, e = h4;
                uint aa = h0, bb = h1, cc = h2, dd = h3, ee = h4;

                for (var j = 0; j < 80; j++)
                {
                    uint t = Rol(a + F(j, b, c, d) + block[R1[j]] + K1(j), S1[j]) + e;
                    a = e;
                    e = d;
                    d = Rol(c, 10);
                    c = b;
                    b = t;

                    t = Rol(aa + G(j, bb, cc, dd) + block[R2[j]] + K2(j), S2[j]) + ee;
                    aa = ee;
                    ee = dd;
                    dd = Rol(cc, 10);
                    cc = bb;
                    bb = t;
                }

                var next = h1 + c + dd;
                h1 = h2 + d + ee;
                h2 = h3 + e + aa;
                h3 = h4 + a + bb;
                h4 = h0 + b + cc;
                h0 = next;
            }

            var digest = new byte[DigestLength];
            WriteLE(digest, 0, h0);
            WriteLE(digest, 4, h1);
            WriteLE(digest, 8, h2);
            WriteLE(digest, 12, h3);
            WriteLE(digest, 16, h4);
            return digest;
        }

        private static uint F(int j, uint x, uint y, uint z)
        {
            if (j < 16)
            {
                return x ^ y ^ z;
            }

            if (j < 32)
            {
                return (x & y) | (~x & z);
            }

            if (j < 48)
            {
                return (x | ~y) ^ z;
            }

            if (j < 64)
            {
                return (x & z) | (y & ~z);
            }

            return x ^ (y | ~z);
        }

        private static uint G(int j, uint x, uint y, uint z)
        {
            if (j < 16)
            {
                return x ^ (y | ~z);
            }

            if (j < 32)
            {
                return (x & z) | (y & ~z);
            }

            if (j < 48)
            {
                return (x | ~y) ^ z;
            }

            if (j < 64)
            {
                return (x & y) | (~x & z);
            }

            return x ^ y ^ z;
        }

        private static uint K1(int j)
        {
            if (j < 16)
            {
                return 0x00000000;
            }

            if (j < 32)
            {
                return 0x5A827999;
            }

            if (j < 48)
            {
                return 0x6ED9EBA1;
            }

            if (j < 64)
            {
                return 0x8F1BBCDC;
            }

            return 0xA953FD4E;
        }

        private static uint K2(int j)
        {
            if (j < 16)
            {
                return 0x50A28BE6;
            }

            if (j < 32)
            {
                return 0x5C4DD124;
            }

            if (j < 48)
            {
                return 0x6D703EF3;
            }

            if (j < 64)
            {
                return 0x7A6D76E9;
            }

            return 0x00000000;
        }

        private static uint Rol(uint value, int bits)
        {
            return (value << bits) | (value >> (32 - bits));
        }

        private static byte[] Pad(ReadOnlySpan<byte> data)
        {
            var bitLength = (ulong)data.Length * 8UL;
            var padLength = (data.Length % 64) < 56 ? 56 - (data.Length % 64) : 120 - (data.Length % 64);
            var padded = new byte[data.Length + padLength + 8];
            data.CopyTo(padded);
            padded[data.Length] = 0x80;
            for (var i = 0; i < 8; i++)
            {
                padded[padded.Length - 8 + i] = (byte)(bitLength >> (8 * i));
            }

            return padded;
        }

        private static void WriteLE(byte[] destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }
    }
}
