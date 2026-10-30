namespace NKaspa.DataEncoders
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.DataEncoders.HexEncoder.
    /// Lowercase hexadecimal, no separators. Both cases are accepted on decode.
    /// </summary>
    public sealed class HexEncoder : DataEncoder
    {
        private const string Alphabet = "0123456789abcdef";

        /// <inheritdoc />
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

            return EncodeData(data.AsSpan(offset, count));
        }

        /// <inheritdoc />
        public override string EncodeData(ReadOnlySpan<byte> data)
        {
            var chars = new char[data.Length * 2];
            for (var i = 0; i < data.Length; i++)
            {
                chars[i * 2] = Alphabet[data[i] >> 4];
                chars[i * 2 + 1] = Alphabet[data[i] & 0x0F];
            }

            return new string(chars);
        }

        /// <inheritdoc />
        public override byte[] DecodeData(string encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            if ((encoded.Length & 1) != 0)
            {
                throw new FormatException("Hex string length must be even.");
            }

            var bytes = new byte[encoded.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var high = Value(encoded[i * 2]);
                var low = Value(encoded[i * 2 + 1]);
                if (high < 0 || low < 0)
                {
                    throw new FormatException("Hex string contains a non-hex character.");
                }

                bytes[i] = (byte)((high << 4) | low);
            }

            return bytes;
        }

        /// <summary>True when every character is hexadecimal and the length is even.</summary>
        public bool IsValid(string encoded)
        {
            if (encoded == null || (encoded.Length & 1) != 0)
            {
                return false;
            }

            for (var i = 0; i < encoded.Length; i++)
            {
                if (Value(encoded[i]) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int Value(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }

            return -1;
        }
    }
}
