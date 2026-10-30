namespace NKaspa
{
    /// <summary>
    /// Kaspa script numbers. Little-endian, sign bit in the high bit of the last byte, at most 8 bytes when read as an <c>i64</c>.
    /// Minimal encoding is not required. Zero is an empty array. Negative zero (<c>0x80</c>) is false.
    /// </summary>
    internal static class ScriptNum
    {
        internal static bool AsBool(byte[] value)
        {
            if (value.Length == 0)
            {
                return false;
            }

            var last = value[value.Length - 1];
            if ((last & 0x7f) != 0)
            {
                return true;
            }

            for (var i = 0; i < value.Length - 1; i++)
            {
                if (value[i] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static ScriptError? TryDeserializeI64(byte[] value, out long number)
        {
            if (value.Length > 8)
            {
                number = 0;
                return ScriptError.NumberTooBig("numeric value is " + value.Length.ToString() + " bytes which exceeds the max allowed of 8");
            }

            number = Decode(value);
            return null;
        }

        internal static ScriptError? TryDeserializeI32(byte[] value, out int number)
        {
            var error = TryDeserializeI64(value, out var wide);
            if (error != null)
            {
                number = 0;
                return error;
            }

            if (wide < int.MinValue || wide > int.MaxValue)
            {
                number = 0;
                return ScriptError.NumberTooBig("numeric value does not fit an i32");
            }

            number = (int)wide;
            return null;
        }

        internal static long Decode(byte[] value)
        {
            if (value.Length == 0)
            {
                return 0;
            }

            var msb = value[value.Length - 1];
            var sign = 1 - (2 * (msb >> 7));
            long accum = msb & 0x7f;
            for (var i = value.Length - 2; i >= 0; i--)
            {
                accum = unchecked((accum << 8) + value[i]);
            }

            return accum * sign;
        }

        /// <summary>Minimal encoding. <see cref="long.MinValue"/> is 9 bytes and is rejected by <see cref="TrySerializeI64"/>.</summary>
        internal static byte[] SerializeMinimal(long value)
        {
            return Finish(Unsigned(value, out var negative), null, negative);
        }

        internal static ScriptError? TrySerializeI64(long value, out byte[] encoded)
        {
            var bytes = SerializeMinimal(value);
            if (bytes.Length > 8)
            {
                encoded = Array.Empty<byte>();
                return ScriptError.Serialization("number does not fit in 8 bytes");
            }

            encoded = bytes;
            return null;
        }

        internal static ScriptError? TrySerializeFixed(long value, int size, out byte[] encoded)
        {
            if (size > 8)
            {
                encoded = Array.Empty<byte>();
                return ScriptError.NotMinimalData("NUM2BIN target size " + size.ToString() + " exceeds 8 bytes");
            }

            var unsigned = Unsigned(value, out var negative);
            if (unsigned.Length > size)
            {
                encoded = Array.Empty<byte>();
                return ScriptError.Serialization("number does not fit in " + size.ToString() + " bytes");
            }

            encoded = Finish(unsigned, size, negative);
            return null;
        }

        private static byte[] Unsigned(long value, out bool negative)
        {
            negative = value < 0;
            ulong magnitude;
            if (value == long.MinValue)
            {
                magnitude = 1UL << 63;
            }
            else
            {
                magnitude = (ulong)(negative ? -value : value);
            }

            if (magnitude == 0)
            {
                return Array.Empty<byte>();
            }

            var bytes = new List<byte>(9);
            var lastSaturated = false;
            while (true)
            {
                if (magnitude == 0)
                {
                    if (lastSaturated)
                    {
                        bytes.Add(0);
                    }

                    break;
                }

                var piece = (byte)magnitude;
                lastSaturated = (piece & 0x80) != 0;
                bytes.Add(piece);
                magnitude >>= 8;
            }

            return bytes.ToArray();
        }

        private static byte[] Finish(byte[] unsigned, int? size, bool negative)
        {
            var length = unsigned.Length;
            if (size.HasValue)
            {
                length = size.Value;
            }

            var encoded = new byte[length];
            Buffer.BlockCopy(unsigned, 0, encoded, 0, unsigned.Length);
            if (negative)
            {
                encoded[encoded.Length - 1] |= 0x80;
            }

            return encoded;
        }
    }
}
