using System.Text;
using NKaspa.Crypto;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// RFC 7693 and published BLAKE2b-256 vectors.
    /// BLAKE2b-512("abc") is RFC 7693 Appendix A.
    /// BLAKE2b-512("") is the empty digest published with RFC 7693 implementations.
    /// BLAKE2b-256 vectors are the unkeyed digests used by BoringSSL and the Linux crypto testmgr
    /// (empty, 0x00, 0x0001, and "abc"). The keyed vector is the 64-byte key from the BLAKE2 test suite.
    /// </summary>
    public class Blake2bTests
    {
        [Fact]
        public void Blake2b512_empty_matches_rfc7693()
        {
            var hash = Blake2b.ComputeHash(ReadOnlySpan<byte>.Empty, 64);
            Assert.Equal(
                "786a02f742015903c6c6fd852552d272912f4740e15847618a86e217f71f5419d25e1031afee585313896444934eb04b903a685b1448b755d56f701afe9be2ce",
                ToHex(hash));
        }

        [Fact]
        public void Blake2b512_abc_matches_rfc7693_appendix_a()
        {
            var hash = Blake2b.ComputeHash(Encoding.ASCII.GetBytes("abc"), 64);
            Assert.Equal(
                "ba80a53f981c4d0d6a2797b69f12f6e94c212f14685ac4b74b12bb6fdbffa2d17d87c5392aab792dc252d5de4533cc9518d38aa8dbf1925ab92386edd4009923",
                ToHex(hash));
        }

        [Fact]
        public void Blake2b256_empty_and_abc_match_published_vectors()
        {
            Assert.Equal(
                "0e5751c026e543b2e8ab2eb06099daa1d1e5df47778f7787faab45cdf12fe3a8",
                ToHex(Blake2b.Hash256(ReadOnlySpan<byte>.Empty)));
            Assert.Equal(
                "bddd813c634239723171ef3fee98579b94964e3bb1cb3e427262c8c068d52319",
                ToHex(Blake2b.Hash256(Encoding.ASCII.GetBytes("abc"))));
        }

        [Theory]
        [InlineData("00", "03170a2e7597b7b7e3d84c05391d139a62b157e78786d8c082f29dcf4c111314")]
        [InlineData("0001", "01cf79da4945c370c68b265ef70641aaa65eaa8f5953e3900d97724c2c5aa095")]
        [InlineData("000102", "3d8c3d594928271f44aad7a04b177154806867bcf918e1549c0bc16f9da2b09b")]
        public void Blake2b256_short_inputs_match_linux_testmgr(string inputHex, string expected)
        {
            var input = DecodeHex(inputHex);
            Assert.Equal(expected, ToHex(Blake2b.Hash256(input)));
            Assert.Equal(expected, Hashes.Blake2b256(input).ToString());
        }

        [Fact]
        public void Blake2b512_keyed_empty_message_matches_blake2_suite()
        {
            var key = new byte[64];
            for (var i = 0; i < key.Length; i++)
            {
                key[i] = (byte)i;
            }

            var hash = Blake2b.ComputeHash(ReadOnlySpan<byte>.Empty, 64, key);
            Assert.Equal(
                "10ebb67700b1868efb4417987acf4690ae9d972fb7a590c2f02871799aaa4786b5e996e8f0f4eb981fc214b005f42d2ff4233499391653df7aefcbc13fc51568",
                ToHex(hash));
        }

        [Fact]
        public void Incremental_update_matches_one_shot_across_a_block_boundary()
        {
            var message = new byte[200];
            for (var i = 0; i < message.Length; i++)
            {
                message[i] = (byte)i;
            }

            var oneShot = Blake2b.Hash256(message);
            var hasher = new Blake2b(32);
            hasher.Update(message, 0, 1);
            hasher.Update(message, 1, 60);
            hasher.Update(message, 61, 70);
            hasher.Update(message, 131, message.Length - 131);
            Assert.Equal(oneShot, hasher.FinalizeHash());
        }

        private static string ToHex(byte[] data)
        {
            var chars = new char[data.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (var i = 0; i < data.Length; i++)
            {
                chars[i * 2] = alphabet[data[i] >> 4];
                chars[i * 2 + 1] = alphabet[data[i] & 0x0F];
            }

            return new string(chars);
        }

        private static byte[] DecodeHex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
