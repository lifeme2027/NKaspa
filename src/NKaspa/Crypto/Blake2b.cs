namespace NKaspa.Crypto
{
    /// <summary>
    /// RFC 7693 BLAKE2b. Script hashes use the unkeyed 32-byte digest.
    /// Transaction ids, transaction hashes, and signature hashes use the same digest
    /// with a domain-separation key. There is no suitable small MIT NuGet package
    /// dedicated to this use, so the hash lives in this library.
    /// </summary>
    /// <remarks>
    /// Keyed mode pads the key to one 128-byte block, as specified by RFC 7693.
    /// Elliptic-curve math is not implemented here. It belongs to NBitcoin.Secp256k1.
    /// </remarks>
    public sealed class Blake2b
    {
        /// <summary>BLAKE2b block size in bytes.</summary>
        public const int BlockSize = 128;

        /// <summary>Largest BLAKE2b digest size in bytes.</summary>
        public const int MaxHashSize = 64;

        /// <summary>Largest BLAKE2b key size in bytes.</summary>
        public const int MaxKeySize = 64;

        private static readonly ulong[] Iv =
        {
            0x6A09E667F3BCC908UL,
            0xBB67AE8584CAA73BUL,
            0x3C6EF372FE94F82BUL,
            0xA54FF53A5F1D36F1UL,
            0x510E527FADE682D1UL,
            0x9B05688C2B3E6C1FUL,
            0x1F83D9ABFB41BD6BUL,
            0x5BE0CD19137E2179UL,
        };

        // Rounds 10 and 11 repeat rounds 0 and 1 (RFC 7693 section 2.7).
        private static readonly byte[] Sigma =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
            11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4,
            7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8,
            9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13,
            2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9,
            12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11,
            13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10,
            6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5,
            10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
        };

        private readonly int _hashSize;
        private readonly ulong[] _h = new ulong[8];
        private readonly byte[] _buffer = new byte[BlockSize];
        private int _bufferLength;
        private ulong _counterLow;
        private ulong _counterHigh;
        private bool _finished;

        /// <summary>Copies the hasher state, including a key block that has not been finalized.</summary>
        public Blake2b Clone()
        {
            return new Blake2b(this);
        }

        /// <summary>Creates a hasher. <paramref name="hashSize"/> is the digest length in bytes, from 1 to 64.</summary>
        public Blake2b(int hashSize = 32, ReadOnlySpan<byte> key = default)
        {
            if (hashSize < 1 || hashSize > MaxHashSize)
            {
                throw new ArgumentOutOfRangeException(nameof(hashSize), "BLAKE2b digest size must be from 1 to 64 bytes.");
            }

            if (key.Length > MaxKeySize)
            {
                throw new ArgumentException("BLAKE2b key must be at most 64 bytes.", nameof(key));
            }

            _hashSize = hashSize;
            Iv.AsSpan().CopyTo(_h);
            _h[0] ^= 0x01010000UL ^ ((ulong)key.Length << 8) ^ (ulong)hashSize;
            if (!key.IsEmpty)
            {
                key.CopyTo(_buffer);
                _bufferLength = BlockSize;
            }
        }

        private Blake2b(Blake2b other)
        {
            _hashSize = other._hashSize;
            other._h.AsSpan().CopyTo(_h);
            other._buffer.AsSpan().CopyTo(_buffer);
            _bufferLength = other._bufferLength;
            _counterLow = other._counterLow;
            _counterHigh = other._counterHigh;
            _finished = other._finished;
        }

        /// <summary>Absorbs the next slice of the message.</summary>
        public void Update(byte[] data, int offset, int count)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || count < 0 || offset > data.Length - count)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            Update(data.AsSpan(offset, count));
        }

        /// <summary>Absorbs the next slice of the message.</summary>
        public void Update(ReadOnlySpan<byte> data)
        {
            if (_finished)
            {
                throw new InvalidOperationException("BLAKE2b hasher is already finished.");
            }

            if (data.IsEmpty)
            {
                return;
            }

            if (_bufferLength > 0)
            {
                var fill = BlockSize - _bufferLength;
                if (data.Length > fill)
                {
                    data.Slice(0, fill).CopyTo(_buffer.AsSpan(_bufferLength));
                    _bufferLength = 0;
                    Increment(BlockSize);
                    Compress(_buffer, last: false);
                    data = data.Slice(fill);
                }
                else
                {
                    data.CopyTo(_buffer.AsSpan(_bufferLength));
                    _bufferLength += data.Length;
                    return;
                }
            }

            while (data.Length > BlockSize)
            {
                Increment(BlockSize);
                Compress(data.Slice(0, BlockSize), last: false);
                data = data.Slice(BlockSize);
            }

            if (!data.IsEmpty)
            {
                data.CopyTo(_buffer);
                _bufferLength = data.Length;
            }
        }

        /// <summary>Returns the digest and retires this instance.</summary>
        public byte[] FinalizeHash()
        {
            if (_finished)
            {
                throw new InvalidOperationException("BLAKE2b hasher is already finished.");
            }

            _finished = true;
            Increment((ulong)_bufferLength);
            _buffer.AsSpan(_bufferLength).Clear();
            Compress(_buffer, last: true);

            var output = new byte[_hashSize];
            for (var i = 0; i < _hashSize; i++)
            {
                output[i] = (byte)(_h[i >> 3] >> (8 * (i & 7)));
            }

            return output;
        }

        /// <summary>Unkeyed or keyed BLAKE2b of a whole message.</summary>
        public static byte[] ComputeHash(ReadOnlySpan<byte> data, int hashSize = 32, ReadOnlySpan<byte> key = default)
        {
            var hasher = new Blake2b(hashSize, key);
            hasher.Update(data);
            return hasher.FinalizeHash();
        }

        /// <summary>Unkeyed BLAKE2b-256, the digest Kaspa uses for ids and script hashes.</summary>
        public static byte[] Hash256(ReadOnlySpan<byte> data)
        {
            return ComputeHash(data, 32);
        }

        private void Increment(ulong length)
        {
            var sum = _counterLow + length;
            if (sum < _counterLow)
            {
                _counterHigh++;
            }

            _counterLow = sum;
        }

        private void Compress(ReadOnlySpan<byte> block, bool last)
        {
            Span<ulong> message = stackalloc ulong[16];
            Span<ulong> v = stackalloc ulong[16];
            for (var i = 0; i < 16; i++)
            {
                message[i] = ReadUInt64LittleEndian(block.Slice(i * 8, 8));
            }

            for (var i = 0; i < 8; i++)
            {
                v[i] = _h[i];
                v[i + 8] = Iv[i];
            }

            v[12] ^= _counterLow;
            v[13] ^= _counterHigh;
            if (last)
            {
                v[14] ^= ulong.MaxValue;
            }

            for (var round = 0; round < 12; round++)
            {
                var s = round * 16;
                Mix(v, 0, 4, 8, 12, message[Sigma[s + 0]], message[Sigma[s + 1]]);
                Mix(v, 1, 5, 9, 13, message[Sigma[s + 2]], message[Sigma[s + 3]]);
                Mix(v, 2, 6, 10, 14, message[Sigma[s + 4]], message[Sigma[s + 5]]);
                Mix(v, 3, 7, 11, 15, message[Sigma[s + 6]], message[Sigma[s + 7]]);
                Mix(v, 0, 5, 10, 15, message[Sigma[s + 8]], message[Sigma[s + 9]]);
                Mix(v, 1, 6, 11, 12, message[Sigma[s + 10]], message[Sigma[s + 11]]);
                Mix(v, 2, 7, 8, 13, message[Sigma[s + 12]], message[Sigma[s + 13]]);
                Mix(v, 3, 4, 9, 14, message[Sigma[s + 14]], message[Sigma[s + 15]]);
            }

            for (var i = 0; i < 8; i++)
            {
                _h[i] ^= v[i] ^ v[i + 8];
            }
        }

        private static void Mix(Span<ulong> v, int a, int b, int c, int d, ulong x, ulong y)
        {
            unchecked
            {
                v[a] = v[a] + v[b] + x;
                v[d] = RotateRight(v[d] ^ v[a], 32);
                v[c] += v[d];
                v[b] = RotateRight(v[b] ^ v[c], 24);
                v[a] = v[a] + v[b] + y;
                v[d] = RotateRight(v[d] ^ v[a], 16);
                v[c] += v[d];
                v[b] = RotateRight(v[b] ^ v[c], 63);
            }
        }

        private static ulong RotateRight(ulong value, int offset)
        {
            return (value >> offset) | (value << (64 - offset));
        }

        private static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> bytes)
        {
            return (ulong)bytes[0]
                | ((ulong)bytes[1] << 8)
                | ((ulong)bytes[2] << 16)
                | ((ulong)bytes[3] << 24)
                | ((ulong)bytes[4] << 32)
                | ((ulong)bytes[5] << 40)
                | ((ulong)bytes[6] << 48)
                | ((ulong)bytes[7] << 56);
        }
    }
}
