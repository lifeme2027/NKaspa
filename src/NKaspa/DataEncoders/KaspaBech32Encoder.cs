namespace NKaspa.DataEncoders
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.DataEncoders.Bech32Encoder for Kaspa addresses.
    /// This is the cashaddr-style bech32 used by Kaspa, not BIP-173.
    /// The checksum is 8 characters (40 bits). The human-readable prefixes are
    /// <c>kaspa</c>, <c>kaspatest</c>, <c>kaspasim</c>, and <c>kaspadev</c>.
    /// </summary>
    /// <remarks>
    /// The encoded payload is <c>version || data</c> converted from 8-bit bytes
    /// to 5-bit symbols, then the cashaddr polymod. Characters are lowercase.
    /// The charset is <c>qpzry9x8gf2tvdw0s3jn54khce6mua7l</c>.
    /// Polymod constants match rusty-kaspa <c>crypto/addresses</c> and the
    /// cashaddr specification used by kaswallet <c>KaspaBech32</c>.
    /// </remarks>
    public sealed class KaspaBech32Encoder
    {
        /// <summary>Kaspa bech32 alphabet. The characters 1, b, i, and o are absent.</summary>
        public const string Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

        /// <summary>Number of 5-bit checksum symbols. Eight symbols are 40 bits.</summary>
        public const int ChecksumLength = 8;

        private static readonly sbyte[] CharsetIndex = BuildCharsetIndex();

        /// <summary>Creates an encoder bound to one human-readable prefix, without the colon.</summary>
        /// <exception cref="System.ArgumentException">Address prefix must not contain a colon. Address prefix is required.</exception>
        public KaspaBech32Encoder(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentException("Address prefix is required.", nameof(prefix));
            }

            if (prefix.IndexOf(':') >= 0)
            {
                throw new ArgumentException("Address prefix must not contain a colon.", nameof(prefix));
            }

            Prefix = prefix;
        }

        /// <summary>Human-readable prefix without the colon.</summary>
        public string Prefix { get; }

        /// <summary>
        /// Encodes a version byte and payload as <c>{prefix}:{data}</c>.
        /// The version byte is part of the converted payload, which is how Kaspa addresses are formed.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">8-to-5 conversion produced an unexpected length.</exception>
        public string Encode(byte version, ReadOnlySpan<byte> payload)
        {
            var raw = new byte[payload.Length + 1];
            raw[0] = version;
            payload.CopyTo(raw.AsSpan(1));
            var data = Convert8To5(raw);
            var checksumValue = Checksum(Prefix, data);
            var checksum = Convert8To5(ChecksumBytes(checksumValue));
            var chars = new char[data.Length + checksum.Length];
            for (var i = 0; i < data.Length; i++)
            {
                chars[i] = Charset[data[i]];
            }

            for (var i = 0; i < checksum.Length; i++)
            {
                chars[data.Length + i] = Charset[checksum[i]];
            }

            return Prefix + ":" + new string(chars);
        }

        /// <summary>
        /// Decodes an address that uses this encoder's prefix.
        /// </summary>
        /// <exception cref="System.FormatException">
        /// Kaspa address prefix does not match this encoder. Kaspa address is empty. A Kaspa address must look
        /// like {prefix}:{payload}. Kaspa address length is outside the cashaddr limits. Kaspa address prefix
        /// contains a non-printable character. Kaspa address contains a character outside the bech32 charset.
        /// Kaspa address checksum does not match the prefix and payload. Kaspa address payload padding is
        /// invalid.
        /// </exception>
        public byte[] Decode(string address, out byte version)
        {
            if (!TryDecode(address, out var prefix, out version, out var payload, out var error))
            {
                throw new FormatException(error);
            }

            if (!string.Equals(prefix, Prefix, StringComparison.Ordinal))
            {
                throw new FormatException("Kaspa address prefix does not match this encoder.");
            }

            return payload;
        }

        /// <summary>
        /// Decodes any prefix. Does not check that the prefix is a known Kaspa network
        /// and does not check the version or payload length. Callers that want an address
        /// use <see cref="NKaspa.KaspaAddress"/>.
        /// </summary>
        public static bool TryDecode(
            string? address,
            out string prefix,
            out byte version,
            out byte[] payload,
            out string error)
        {
            prefix = string.Empty;
            version = 0;
            payload = Array.Empty<byte>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(address))
            {
                error = "Kaspa address is empty.";
                return false;
            }

            var text = address.Trim();
            var colon = text.IndexOf(':');
            if (colon <= 0 || colon != text.LastIndexOf(':') || colon == text.Length - 1)
            {
                error = "A Kaspa address must look like {prefix}:{payload}.";
                return false;
            }

            prefix = text.Substring(0, colon);
            var body = text.Substring(colon + 1);
            if (prefix.Length > 83 || body.Length < ChecksumLength || body.Length > 150)
            {
                error = "Kaspa address length is outside the cashaddr limits.";
                return false;
            }

            for (var i = 0; i < prefix.Length; i++)
            {
                var c = prefix[i];
                if (c < 33 || c > 126)
                {
                    error = "Kaspa address prefix contains a non-printable character.";
                    return false;
                }
            }

            var values = new byte[body.Length];
            for (var i = 0; i < body.Length; i++)
            {
                var symbol = body[i];
                var index = symbol < CharsetIndex.Length ? CharsetIndex[symbol] : (sbyte)-1;
                if (index < 0)
                {
                    error = "Kaspa address contains a character outside the bech32 charset.";
                    return false;
                }

                values[i] = (byte)index;
            }

            var payload5 = new byte[values.Length - ChecksumLength];
            var checksum5 = new byte[ChecksumLength];
            Buffer.BlockCopy(values, 0, payload5, 0, payload5.Length);
            Buffer.BlockCopy(values, payload5.Length, checksum5, 0, ChecksumLength);

            var found = ReadChecksum(checksum5);
            var expected = Checksum(prefix, payload5);
            if (found != expected)
            {
                error = "Kaspa address checksum does not match the prefix and payload.";
                return false;
            }

            var payload8 = Convert5To8(payload5, out var badPadding);
            if (badPadding || payload8.Length == 0)
            {
                error = "Kaspa address payload padding is invalid.";
                return false;
            }

            version = payload8[0];
            payload = new byte[payload8.Length - 1];
            Buffer.BlockCopy(payload8, 1, payload, 0, payload.Length);
            return true;
        }

        private static ulong Checksum(string prefix, byte[] payload5)
        {
            var values = new byte[prefix.Length + 1 + payload5.Length + ChecksumLength];
            var n = 0;
            for (var i = 0; i < prefix.Length; i++)
            {
                values[n++] = (byte)(prefix[i] & 0x1F);
            }

            values[n++] = 0;
            Buffer.BlockCopy(payload5, 0, values, n, payload5.Length);
            return Polymod(values);
        }

        private static ulong Polymod(byte[] values)
        {
            ulong c = 1;
            for (var i = 0; i < values.Length; i++)
            {
                var c0 = c >> 35;
                c = ((c & 0x07ffffffffUL) << 5) ^ values[i];
                if ((c0 & 0x01) != 0)
                {
                    c ^= 0x98f2bc8e61UL;
                }

                if ((c0 & 0x02) != 0)
                {
                    c ^= 0x79b76d99e2UL;
                }

                if ((c0 & 0x04) != 0)
                {
                    c ^= 0xf33e5fb3c4UL;
                }

                if ((c0 & 0x08) != 0)
                {
                    c ^= 0xae2eabe2a8UL;
                }

                if ((c0 & 0x10) != 0)
                {
                    c ^= 0x1e4f43e470UL;
                }
            }

            return c ^ 1;
        }

        private static byte[] ChecksumBytes(ulong checksum)
        {
            var wide = new byte[8];
            for (var i = 7; i >= 0; i--)
            {
                wide[i] = (byte)(checksum & 0xFF);
                checksum >>= 8;
            }

            var five = new byte[5];
            Buffer.BlockCopy(wide, 3, five, 0, 5);
            return five;
        }

        private static ulong ReadChecksum(byte[] checksum5)
        {
            var bytes = Convert5To8(checksum5, out var badPadding);
            if (badPadding || bytes.Length > 5)
            {
                return ulong.MaxValue;
            }

            ulong value = 0;
            for (var i = 0; i < bytes.Length; i++)
            {
                value = (value << 8) | bytes[i];
            }

            return value;
        }

        internal static byte[] Convert8To5(byte[] payload)
        {
            var padding = payload.Length % 5 == 0 ? 0 : 1;
            var five = new byte[(payload.Length * 8 / 5) + padding];
            var index = 0;
            var buffer = 0;
            var bits = 0;
            for (var i = 0; i < payload.Length; i++)
            {
                buffer = (buffer << 8) | payload[i];
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    five[index++] = (byte)((buffer >> bits) & 31);
                    buffer &= (1 << bits) - 1;
                }
            }

            if (bits > 0)
            {
                five[index++] = (byte)((buffer << (5 - bits)) & 31);
            }

            if (index != five.Length)
            {
                throw new InvalidOperationException("8-to-5 conversion produced an unexpected length.");
            }

            return five;
        }

        private static byte[] Convert5To8(byte[] payload, out bool badPadding)
        {
            var eight = new byte[payload.Length * 5 / 8];
            var index = 0;
            var buffer = 0;
            var bits = 0;
            for (var i = 0; i < payload.Length; i++)
            {
                buffer = (buffer << 5) | (payload[i] & 31);
                bits += 5;
                while (bits >= 8)
                {
                    bits -= 8;
                    eight[index++] = (byte)((buffer >> bits) & 0xFF);
                    buffer &= (1 << bits) - 1;
                }
            }

            badPadding = bits > 0 && buffer != 0;
            return eight;
        }

        private static sbyte[] BuildCharsetIndex()
        {
            var index = new sbyte[128];
            for (var i = 0; i < index.Length; i++)
            {
                index[i] = -1;
            }

            for (var i = 0; i < Charset.Length; i++)
            {
                index[Charset[i]] = (sbyte)i;
            }

            return index;
        }
    }
}
