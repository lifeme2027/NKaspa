namespace NKaspa.DataEncoders
{
    /// <summary>
    /// Base58 encoder. Kaspa counterpart of NBitcoin.DataEncoders.Base58Encoder.
    /// Kaspa addresses do not use Base58. Extended keys (BIP32) do.
    /// </summary>
    public sealed class Base58Encoder : DataEncoder
    {
        /// <summary>Bitcoin Base58 alphabet. Zero bytes become the character <c>1</c>.</summary>
        public const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        private static readonly int[] Map = BuildMap();

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
        /// <exception cref="System.FormatException">Base58 value does not fit in the decode buffer. Invalid Base58 character.</exception>
        public override byte[] DecodeData(string encoded)
        {
            return Decode(encoded);
        }

        /// <summary>Encodes raw bytes as Base58.</summary>
        public static string Encode(ReadOnlySpan<byte> data)
        {
            var zeros = 0;
            while (zeros < data.Length && data[zeros] == 0)
            {
                zeros++;
            }

            var size = (data.Length - zeros) * 138 / 100 + 2;
            var buffer = new byte[size];
            var length = 0;
            for (var i = zeros; i < data.Length; i++)
            {
                var carry = (int)data[i];
                var written = 0;
                for (var k = size - 1; k >= 0 && (carry != 0 || written < length); k--, written++)
                {
                    carry += 256 * buffer[k];
                    buffer[k] = (byte)(carry % 58);
                    carry /= 58;
                }

                length = written;
            }

            var start = size - length;
            var chars = new char[zeros + length];
            for (var i = 0; i < zeros; i++)
            {
                chars[i] = '1';
            }

            for (var i = 0; i < length; i++)
            {
                chars[zeros + i] = Alphabet[buffer[start + i]];
            }

            return new string(chars);
        }

        /// <summary>Decodes a Base58 string. An unknown character throws <see cref="FormatException"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="encoded"/> is null.</exception>
        /// <exception cref="System.FormatException">Base58 value does not fit in the decode buffer. Invalid Base58 character.</exception>
        public static byte[] Decode(string encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            var zeros = 0;
            var index = 0;
            while (index < encoded.Length && encoded[index] == '1')
            {
                zeros++;
                index++;
            }

            var size = encoded.Length * 733 / 1000 + 1;
            var buffer = new byte[size];
            var length = 0;
            for (; index < encoded.Length; index++)
            {
                var digit = encoded[index] < Map.Length ? Map[encoded[index]] : -1;
                if (digit < 0)
                {
                    throw new FormatException("Invalid Base58 character.");
                }

                var carry = digit;
                var written = 0;
                for (var k = size - 1; k >= 0 && (carry != 0 || written < length); k--, written++)
                {
                    carry += 58 * buffer[k];
                    buffer[k] = (byte)(carry % 256);
                    carry /= 256;
                }

                if (carry != 0)
                {
                    throw new FormatException("Base58 value does not fit in the decode buffer.");
                }

                length = written;
            }

            var start = size - length;
            var result = new byte[zeros + length];
            Buffer.BlockCopy(buffer, start, result, zeros, length);
            return result;
        }

        private static int[] BuildMap()
        {
            var map = new int[128];
            for (var i = 0; i < map.Length; i++)
            {
                map[i] = -1;
            }

            for (var i = 0; i < Alphabet.Length; i++)
            {
                map[Alphabet[i]] = i;
            }

            return map;
        }
    }
}
