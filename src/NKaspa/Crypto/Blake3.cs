using System.Text;

namespace NKaspa.Crypto
{
    /// <summary>
    /// BLAKE3, 32-byte output, unkeyed and keyed.
    /// Kaspa version-1 transaction ids use the keyed mode.
    /// The compression function follows the BLAKE3 specification
    /// (7 rounds, the standard message schedule, little-endian words).
    /// </summary>
    /// <remarks>
    /// There is no small MIT package dedicated to a 32-byte keyed BLAKE3,
    /// so the hash lives in this library. Elliptic-curve math is not implemented here.
    /// Extended output and <c>derive_key</c> are not exposed. Kaspa does not use them.
    /// </remarks>
    public sealed class Blake3
    {
        /// <summary>Digest size in bytes.</summary>
        public const int HashSize = 32;

        /// <summary>Keyed-mode key size in bytes.</summary>
        public const int KeySize = 32;

        private const int BlockLength = 64;
        private const int ChunkLength = 1024;
        private const int Rounds = 7;

        private const uint ChunkStart = 1u << 0;
        private const uint ChunkEnd = 1u << 1;
        private const uint Parent = 1u << 2;
        private const uint Root = 1u << 3;
        private const uint KeyedHashFlag = 1u << 4;

        private static readonly uint[] Iv =
        {
            0x6A09E667u,
            0xBB67AE85u,
            0x3C6EF372u,
            0xA54FF53Au,
            0x510E527Fu,
            0x9B05688Cu,
            0x1F83D9ABu,
            0x5BE0CD19u,
        };

        private static readonly int[] MessagePermutation = { 2, 6, 3, 10, 7, 0, 4, 13, 1, 11, 12, 5, 9, 14, 15, 8 };

        private readonly uint[] _key = new uint[8];
        private readonly uint _flags;
        private readonly List<uint[]> _stack = new List<uint[]>();
        private Chunk _chunk;

        private Blake3(ReadOnlySpan<byte> key)
        {
            if (key.IsEmpty)
            {
                for (var i = 0; i < 8; i++)
                {
                    _key[i] = Iv[i];
                }

                _flags = 0;
            }
            else
            {
                if (key.Length != KeySize)
                {
                    throw new ArgumentException("BLAKE3 key must be 32 bytes.", nameof(key));
                }

                LoadWords(key, _key);
                _flags = KeyedHashFlag;
            }

            _chunk = new Chunk(_key, 0, _flags);
        }

        /// <summary>Unkeyed BLAKE3-256.</summary>
        /// <exception cref="System.ArgumentException">BLAKE3 key must be 32 bytes.</exception>
        public static byte[] Hash(ReadOnlySpan<byte> data)
        {
            var hasher = new Blake3(ReadOnlySpan<byte>.Empty);
            hasher.Update(data);
            return hasher.FinalizeHash();
        }

        /// <summary>Keyed BLAKE3-256. <paramref name="key"/> is 32 bytes.</summary>
        /// <exception cref="System.ArgumentException">BLAKE3 key must be 32 bytes.</exception>
        public static byte[] KeyedHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
        {
            var hasher = new Blake3(key);
            hasher.Update(data);
            return hasher.FinalizeHash();
        }

        /// <summary>
        /// Kaspa domain-separated keyed BLAKE3.
        /// The 32-byte key is the ASCII domain followed by zeros.
        /// This matches rusty-kaspa <c>blake3_hasher!</c> in <c>crypto/hashes/src/hashers.rs</c>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="domain"/> is null.</exception>
        /// <exception cref="System.ArgumentException">BLAKE3 domain must fit in 32 bytes. BLAKE3 key must be 32 bytes.</exception>
        public static byte[] DomainHash(string domain, ReadOnlySpan<byte> data)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            var domainBytes = Encoding.ASCII.GetBytes(domain);
            if (domainBytes.Length > KeySize)
            {
                throw new ArgumentException("BLAKE3 domain must fit in 32 bytes.", nameof(domain));
            }

            var key = new byte[KeySize];
            domainBytes.CopyTo(key, 0);
            return KeyedHash(key, data);
        }

        private void Update(ReadOnlySpan<byte> input)
        {
            if (input.IsEmpty)
            {
                return;
            }

            if (_chunk.Length > 0)
            {
                var want = ChunkLength - _chunk.Length;
                var take = Math.Min(want, input.Length);
                _chunk.Update(input.Slice(0, take));
                input = input.Slice(take);
                if (input.IsEmpty)
                {
                    return;
                }

                var completed = _chunk.Output().ChainingValue();
                var totalChunks = _chunk.Counter + 1;
                AddChunk(completed, totalChunks);
                _chunk = new Chunk(_key, totalChunks, _flags);
            }

            while (input.Length > ChunkLength)
            {
                var full = Chunk.HashFull(_key, _chunk.Counter, _flags, input.Slice(0, ChunkLength));
                var totalChunks = _chunk.Counter + 1;
                AddChunk(full, totalChunks);
                _chunk = new Chunk(_key, totalChunks, _flags);
                input = input.Slice(ChunkLength);
            }

            _chunk.Update(input);
        }

        private byte[] FinalizeHash()
        {
            var output = _chunk.Output();
            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                output = ParentOutput(_stack[i], output.ChainingValue(), _key, _flags);
            }

            return output.RootBytes();
        }

        private void AddChunk(uint[] chainingValue, ulong totalChunks)
        {
            var cv = chainingValue;
            while ((totalChunks & 1) == 0)
            {
                var left = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
                cv = ParentOutput(left, cv, _key, _flags).ChainingValue();
                totalChunks >>= 1;
            }

            _stack.Add(cv);
        }

        private static Output ParentOutput(uint[] left, uint[] right, uint[] key, uint flags)
        {
            var block = new byte[BlockLength];
            StoreWords(left, block, 0);
            StoreWords(right, block, 32);
            return new Output(key, block, BlockLength, 0, Parent | flags);
        }

        private static uint RotateRight(uint value, int bits)
        {
            return (value >> bits) | (value << (32 - bits));
        }

        private static void G(uint[] state, int a, int b, int c, int d, uint mx, uint my)
        {
            state[a] = state[a] + state[b] + mx;
            state[d] = RotateRight(state[d] ^ state[a], 16);
            state[c] = state[c] + state[d];
            state[b] = RotateRight(state[b] ^ state[c], 12);
            state[a] = state[a] + state[b] + my;
            state[d] = RotateRight(state[d] ^ state[a], 8);
            state[c] = state[c] + state[d];
            state[b] = RotateRight(state[b] ^ state[c], 7);
        }

        private static void Round(uint[] state, uint[] message)
        {
            G(state, 0, 4, 8, 12, message[0], message[1]);
            G(state, 1, 5, 9, 13, message[2], message[3]);
            G(state, 2, 6, 10, 14, message[4], message[5]);
            G(state, 3, 7, 11, 15, message[6], message[7]);
            G(state, 0, 5, 10, 15, message[8], message[9]);
            G(state, 1, 6, 11, 12, message[10], message[11]);
            G(state, 2, 7, 8, 13, message[12], message[13]);
            G(state, 3, 4, 9, 14, message[14], message[15]);
        }

        private static void Permute(uint[] message)
        {
            var copy = new uint[16];
            for (var i = 0; i < 16; i++)
            {
                copy[i] = message[i];
            }

            for (var i = 0; i < 16; i++)
            {
                message[i] = copy[MessagePermutation[i]];
            }
        }

        internal static uint[] Compress(uint[] chainingValue, ReadOnlySpan<byte> block, int blockLength, ulong counter, uint flags)
        {
            var state = new uint[16];
            for (var i = 0; i < 8; i++)
            {
                state[i] = chainingValue[i];
            }

            state[8] = Iv[0];
            state[9] = Iv[1];
            state[10] = Iv[2];
            state[11] = Iv[3];
            state[12] = (uint)counter;
            state[13] = (uint)(counter >> 32);
            state[14] = (uint)blockLength;
            state[15] = flags;

            var message = new uint[16];
            var words = Math.Min(16, block.Length / 4);
            for (var i = 0; i < words; i++)
            {
                message[i] = (uint)block[i * 4]
                    | ((uint)block[(i * 4) + 1] << 8)
                    | ((uint)block[(i * 4) + 2] << 16)
                    | ((uint)block[(i * 4) + 3] << 24);
            }

            if ((block.Length % 4) != 0)
            {
                var index = words;
                var start = words * 4;
                uint partial = 0;
                var shift = 0;
                for (var i = start; i < block.Length; i++)
                {
                    partial |= (uint)block[i] << shift;
                    shift += 8;
                }

                message[index] = partial;
            }

            for (var round = 0; round < Rounds; round++)
            {
                Round(state, message);
                Permute(message);
            }

            for (var i = 0; i < 8; i++)
            {
                state[i] ^= state[i + 8];
                state[i + 8] ^= chainingValue[i];
            }

            return state;
        }

        private static void LoadWords(ReadOnlySpan<byte> bytes, uint[] destination)
        {
            for (var i = 0; i < 8; i++)
            {
                destination[i] = (uint)bytes[i * 4]
                    | ((uint)bytes[(i * 4) + 1] << 8)
                    | ((uint)bytes[(i * 4) + 2] << 16)
                    | ((uint)bytes[(i * 4) + 3] << 24);
            }
        }

        private static void StoreWords(uint[] words, byte[] destination, int offset)
        {
            for (var i = 0; i < 8; i++)
            {
                var word = words[i];
                destination[offset + (i * 4)] = (byte)word;
                destination[offset + (i * 4) + 1] = (byte)(word >> 8);
                destination[offset + (i * 4) + 2] = (byte)(word >> 16);
                destination[offset + (i * 4) + 3] = (byte)(word >> 24);
            }
        }

        private sealed class Chunk
        {
            private readonly uint[] _cv = new uint[8];
            private readonly byte[] _buffer = new byte[BlockLength];
            private readonly uint _flags;
            private int _bufferLength;
            private int _blocksCompressed;

            public Chunk(uint[] key, ulong counter, uint flags)
            {
                key.CopyTo(_cv, 0);
                Counter = counter;
                _flags = flags;
            }

            public ulong Counter { get; }

            public int Length
            {
                get { return (_blocksCompressed * BlockLength) + _bufferLength; }
            }

            public void Update(ReadOnlySpan<byte> input)
            {
                while (!input.IsEmpty)
                {
                    if (_bufferLength == BlockLength)
                    {
                        var compressed = Compress(_cv, _buffer, BlockLength, Counter, _flags | StartFlag());
                        for (var i = 0; i < 8; i++)
                        {
                            _cv[i] = compressed[i];
                        }

                        _blocksCompressed++;
                        _bufferLength = 0;
                    }

                    var want = BlockLength - _bufferLength;
                    var take = Math.Min(want, input.Length);
                    input.Slice(0, take).CopyTo(_buffer.AsSpan(_bufferLength));
                    _bufferLength += take;
                    input = input.Slice(take);
                }
            }

            public Output Output()
            {
                var block = new byte[BlockLength];
                _buffer.AsSpan(0, _bufferLength).CopyTo(block);
                var cv = new uint[8];
                _cv.CopyTo(cv, 0);
                return new Output(cv, block, _bufferLength, Counter, _flags | StartFlag() | ChunkEnd);
            }

            public static uint[] HashFull(uint[] key, ulong counter, uint flags, ReadOnlySpan<byte> chunk)
            {
                var state = new Chunk(key, counter, flags);
                state.Update(chunk);
                return state.Output().ChainingValue();
            }

            private uint StartFlag()
            {
                return _blocksCompressed == 0 ? ChunkStart : 0;
            }
        }

        private sealed class Output
        {
            private readonly uint[] _chainingValue;
            private readonly byte[] _block;
            private readonly int _blockLength;
            private readonly ulong _counter;
            private readonly uint _flags;

            public Output(uint[] chainingValue, byte[] block, int blockLength, ulong counter, uint flags)
            {
                _chainingValue = chainingValue;
                _block = block;
                _blockLength = blockLength;
                _counter = counter;
                _flags = flags;
            }

            public uint[] ChainingValue()
            {
                var state = Compress(_chainingValue, _block.AsSpan(0, _blockLength), _blockLength, _counter, _flags);
                var cv = new uint[8];
                for (var i = 0; i < 8; i++)
                {
                    cv[i] = state[i];
                }

                return cv;
            }

            public byte[] RootBytes()
            {
                var state = Compress(_chainingValue, _block.AsSpan(0, _blockLength), _blockLength, _counter, _flags | Root);
                var hash = new byte[HashSize];
                for (var i = 0; i < 8; i++)
                {
                    var word = state[i];
                    hash[i * 4] = (byte)word;
                    hash[(i * 4) + 1] = (byte)(word >> 8);
                    hash[(i * 4) + 2] = (byte)(word >> 16);
                    hash[(i * 4) + 3] = (byte)(word >> 24);
                }

                return hash;
            }
        }
    }
}
