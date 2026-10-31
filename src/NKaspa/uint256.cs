using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.uint256. A 32-byte value used for hashes and script hashes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Byte order follows Kaspa, not the Bitcoin display convention.
    /// <see cref="ToBytes(bool)"/> and <see cref="ToString"/> use the same order:
    /// the raw BLAKE2b digest, which is also the order on the wire and in Kaspa RPC hex.
    /// Byte 0 is the first digest byte. NBitcoin reverses those bytes in <c>ToString()</c>
    /// so that the hex reads as a big-endian integer. This type does not reverse them.
    /// </para>
    /// <para>
    /// Numeric comparison treats the 32 bytes as a little-endian integer
    /// (byte 0 is the least significant byte). That matches Kaspa proof-of-work
    /// ordering, where a hash is a little-endian 256-bit integer, and it matches
    /// the integer order of NBitcoin.uint256. It does not match lexicographic
    /// order of the display hex.
    /// </para>
    /// </remarks>
    public sealed class uint256 : IComparable<uint256>, IComparable, IEquatable<uint256>
    {
        /// <summary>Number of bytes in the value.</summary>
        public const int Length = 32;

        private static readonly HexEncoder Hex = new HexEncoder();
        private readonly byte[] _bytes;

        /// <summary>The zero value.</summary>
        public static uint256 Zero { get; } = new uint256(new byte[Length]);

        /// <summary>
        /// The integer 1. Its wire and display form starts with <c>01</c> and ends with zeros,
        /// because byte 0 is the least significant byte and the display is not reversed.
        /// </summary>
        public static uint256 One { get; } = new uint256(1UL);

        /// <summary>Creates zero.</summary>
        public uint256()
        {
            _bytes = new byte[Length];
        }

        /// <summary>Copies another value.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        public uint256(uint256 other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            _bytes = (byte[])other._bytes.Clone();
        }

        /// <summary>Creates a value from an unsigned integer. The integer occupies the low bytes.</summary>
        public uint256(ulong value)
        {
            _bytes = new byte[Length];
            for (var i = 0; i < 8; i++)
            {
                _bytes[i] = (byte)(value & 0xFF);
                value >>= 8;
            }
        }

        /// <summary>
        /// Creates a value from 32 bytes.
        /// <paramref name="littleEndian"/> true (the default) stores Kaspa wire order as given.
        /// False treats <paramref name="bytes"/> as a big-endian integer and reverses it into wire order.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        public uint256(byte[] bytes, bool littleEndian = true)
            : this(bytes, 0, bytes == null ? 0 : bytes.Length, littleEndian)
        {
        }

        /// <summary>Creates a value from a 32-byte span in Kaspa wire order.</summary>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        public uint256(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != Length)
            {
                throw new FormatException("uint256 requires 32 bytes.");
            }

            _bytes = bytes.ToArray();
        }

        /// <summary>Creates a value from a slice. See <see cref="uint256(byte[], bool)"/> for endianness.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        public uint256(byte[] bytes, int offset, int length, bool littleEndian = true)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (offset < 0 || length != Length || offset > bytes.Length - length)
            {
                throw new FormatException("uint256 requires 32 bytes.");
            }

            _bytes = new byte[Length];
            Buffer.BlockCopy(bytes, offset, _bytes, 0, Length);
            if (!littleEndian)
            {
                Array.Reverse(_bytes);
            }
        }

        /// <summary>Parses a 64-digit hex string. An optional <c>0x</c> prefix is accepted. The hex is Kaspa wire order.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.FormatException">uint256 hex must be 32 bytes. uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.</exception>
        public uint256(string hex)
        {
            if (!TryParse(hex, out var parsed) || parsed == null)
            {
                throw new FormatException("uint256 hex must be 32 bytes.");
            }

            _bytes = parsed._bytes;
        }

        /// <summary>Parses Kaspa wire-order hex. Returns false when the text is not 32 bytes of hex.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.</exception>
        public static bool TryParse(string? hex, out uint256? result)
        {
            result = null;
            if (hex == null)
            {
                return false;
            }

            var text = hex;
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(2);
            }

            if (text.Length != Length * 2 || !Hex.IsValid(text))
            {
                return false;
            }

            result = new uint256(Hex.DecodeData(text));
            return true;
        }

        /// <summary>Parses Kaspa wire-order hex.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="hex"/> is null.</exception>
        /// <exception cref="System.FormatException">uint256 hex must be 32 bytes. uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.</exception>
        public static uint256 Parse(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            if (!TryParse(hex, out var result) || result == null)
            {
                throw new FormatException("uint256 hex must be 32 bytes.");
            }

            return result;
        }

        /// <summary>Byte at <paramref name="index"/> in wire order. Index 0 is the least significant byte.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="index"/> is outside the allowed range.</exception>
        public byte GetByte(int index)
        {
            if (index < 0 || index >= Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _bytes[index];
        }

        /// <summary>
        /// Copies the value.
        /// <paramref name="littleEndian"/> true returns Kaspa wire order.
        /// False returns the bytes reversed, most significant byte first.
        /// </summary>
        public byte[] ToBytes(bool littleEndian = true)
        {
            var copy = (byte[])_bytes.Clone();
            if (!littleEndian)
            {
                Array.Reverse(copy);
            }

            return copy;
        }

        /// <summary>Writes Kaspa wire order into <paramref name="destination"/>, which must hold 32 bytes.</summary>
        /// <exception cref="System.ArgumentException">Destination must hold 32 bytes.</exception>
        public void WriteTo(Span<byte> destination)
        {
            if (destination.Length < Length)
            {
                throw new ArgumentException("Destination must hold 32 bytes.", nameof(destination));
            }

            _bytes.CopyTo(destination);
        }

        /// <summary>Reads 32 wire-order bytes from a stream.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        /// <exception cref="System.IO.EndOfStreamException">Stream ended before 32 bytes of uint256.</exception>
        public static uint256 ReadFrom(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var bytes = new byte[Length];
            var read = 0;
            while (read < Length)
            {
                var n = stream.Read(bytes, read, Length - read);
                if (n == 0)
                {
                    throw new EndOfStreamException("Stream ended before 32 bytes of uint256.");
                }

                read += n;
            }

            return new uint256(bytes);
        }

        /// <summary>Writes 32 wire-order bytes to a stream.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public void WriteTo(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            stream.Write(_bytes, 0, Length);
        }

        /// <summary>Lowercase hex in Kaspa wire order. The bytes are not reversed.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public override string ToString()
        {
            return Hex.EncodeData(_bytes);
        }

        /// <inheritdoc />
        public int CompareTo(uint256? other)
        {
            if (other is null)
            {
                return 1;
            }

            for (var i = Length - 1; i >= 0; i--)
            {
                var compare = _bytes[i].CompareTo(other._bytes[i]);
                if (compare != 0)
                {
                    return compare;
                }
            }

            return 0;
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentException">Object is not a uint256.</exception>
        public int CompareTo(object? obj)
        {
            if (obj == null)
            {
                return 1;
            }

            if (obj is uint256 other)
            {
                return CompareTo(other);
            }

            throw new ArgumentException("Object is not a uint256.", nameof(obj));
        }

        /// <inheritdoc />
        public bool Equals(uint256? other)
        {
            if (other is null)
            {
                return false;
            }

            for (var i = 0; i < Length; i++)
            {
                if (_bytes[i] != other._bytes[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as uint256);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < Length; i++)
                {
                    hash = (hash * 31) + _bytes[i];
                }

                return hash;
            }
        }

        /// <summary>Returns whether two hashes have the same 32 bytes. Two null references are equal.</summary>
        /// <param name="left">Left hash, or null.</param>
        /// <param name="right">Right hash, or null.</param>
        /// <returns><see langword="true"/> when both are null or <see cref="Equals(uint256)"/> is true.</returns>
        /// <example>
        /// <para>Numeric order treats byte 0 as the least significant byte, so <c>new uint256(1)</c> is less than <c>new uint256(2)</c>.</para>
        /// </example>
        public static bool operator ==(uint256? left, uint256? right)
        {
            if (left is null)
            {
                return right is null;
            }

            return left.Equals(right);
        }

        /// <summary>Returns whether two hashes differ. A null reference differs from a non-null hash.</summary>
        /// <param name="left">Left hash, or null.</param>
        /// <param name="right">Right hash, or null.</param>
        /// <returns><see langword="true"/> when the hashes are not equal.</returns>
        public static bool operator !=(uint256? left, uint256? right)
        {
            return !(left == right);
        }

        /// <summary>Compares two hashes as little-endian integers. A null operand yields false.</summary>
        /// <param name="left">Left hash, or null.</param>
        /// <param name="right">Right hash, or null.</param>
        /// <returns><see langword="true"/> when neither hash is null and <paramref name="left"/> is strictly less than <paramref name="right"/>.</returns>
        public static bool operator <(uint256? left, uint256? right)
        {
            if (left is null || right is null)
            {
                return false;
            }

            return left.CompareTo(right) < 0;
        }

        /// <summary>Compares two hashes as little-endian integers. A null operand yields false.</summary>
        /// <param name="left">Left hash, or null.</param>
        /// <param name="right">Right hash, or null.</param>
        /// <returns><see langword="true"/> when neither hash is null and <paramref name="left"/> is strictly greater than <paramref name="right"/>.</returns>
        public static bool operator >(uint256? left, uint256? right)
        {
            if (left is null || right is null)
            {
                return false;
            }

            return left.CompareTo(right) > 0;
        }

        /// <summary>Compares two hashes as little-endian integers. A null operand yields false.</summary>
        /// <param name="left">Left hash, or null.</param>
        /// <param name="right">Right hash, or null.</param>
        /// <returns><see langword="true"/> when neither hash is null and <paramref name="left"/> is less than or equal to <paramref name="right"/>.</returns>
        public static bool operator <=(uint256? left, uint256? right)
        {
            if (left is null || right is null)
            {
                return false;
            }

            return left.CompareTo(right) <= 0;
        }

        /// <summary>Compares two hashes as little-endian integers. A null operand yields false.</summary>
        /// <param name="left">Left hash, or null.</param>
        /// <param name="right">Right hash, or null.</param>
        /// <returns><see langword="true"/> when neither hash is null and <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</returns>
        public static bool operator >=(uint256? left, uint256? right)
        {
            if (left is null || right is null)
            {
                return false;
            }

            return left.CompareTo(right) >= 0;
        }
    }
}
