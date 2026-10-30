using NKaspa.DataEncoders;
using Xunit;

namespace NKaspa.Tests
{
    public class Uint256Tests
    {
        [Fact]
        public void One_is_little_endian_and_display_is_not_reversed()
        {
            // Kaspa shows wire order. Integer 1 is byte 0 = 0x01, so the hex starts with 01.
            // NBitcoin.uint256.One.ToString() would be 63 zeros followed by 1.
            var text = uint256.One.ToString();
            Assert.Equal(64, text.Length);
            Assert.StartsWith("01", text);
            Assert.EndsWith("00", text);
            Assert.Equal(uint256.One, uint256.Parse(text));
            Assert.Equal(1, uint256.One.GetByte(0));
            Assert.Equal(0, uint256.One.GetByte(31));
        }

        [Fact]
        public void Parse_accepts_0x_and_uppercase_and_rejects_the_wrong_length()
        {
            var parsed = uint256.Parse("0x" + new string('A', 2) + new string('0', 62));
            Assert.Equal(0xAA, parsed.GetByte(0));
            Assert.False(uint256.TryParse("01", out _));
            Assert.False(uint256.TryParse(null, out _));
            Assert.Throws<FormatException>(() => uint256.Parse("zz"));
        }

        [Fact]
        public void Comparison_uses_little_endian_integer_order()
        {
            var low = new uint256(new byte[] { 0xFF }.Concat(new byte[31]).ToArray());
            var high = new byte[32];
            high[31] = 0x01;
            var highValue = new uint256(high);
            Assert.True(uint256.Zero < uint256.One);
            Assert.True(uint256.One < low);
            Assert.True(low < highValue);
            Assert.Equal(0, uint256.One.CompareTo(uint256.Parse(uint256.One.ToString())));
        }

        [Fact]
        public void Big_endian_constructor_reverses_into_wire_order()
        {
            var bigEndian = new byte[32];
            bigEndian[31] = 0x01;
            var value = new uint256(bigEndian, littleEndian: false);
            Assert.Equal(uint256.One, value);
            Assert.Equal(bigEndian, value.ToBytes(littleEndian: false));
            Assert.Equal(uint256.One.ToBytes(), value.ToBytes());
        }

        [Fact]
        public void Stream_roundtrip_keeps_wire_order()
        {
            var original = HashesProbe();
            using var stream = new MemoryStream();
            original.WriteTo(stream);
            stream.Position = 0;
            var read = uint256.ReadFrom(stream);
            Assert.Equal(original, read);
            Assert.Equal(original.ToString(), Encoders.Hex.EncodeData(read.ToBytes()));
        }

        private static uint256 HashesProbe()
        {
            var bytes = new byte[32];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(255 - i);
            }

            return new uint256(bytes);
        }
    }
}
