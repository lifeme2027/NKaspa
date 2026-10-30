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

        /// <summary>
        /// 200-byte message, longer than the 128-byte BLAKE2b block.
        /// Digest from kaswallet <c>ConsensusSigningTests.Blake2b_keyed_and_multiblock_match_reference</c>.
        /// The keyed digest uses the Kaspa domain <c>TransactionSigningHash</c>.
        /// </summary>
        [Fact]
        public void Blake2b256_multiblock_matches_kaswallet_reference()
        {
            var message = new byte[200];
            for (var i = 0; i < message.Length; i++)
            {
                message[i] = (byte)i;
            }

            var key = Encoding.ASCII.GetBytes("TransactionSigningHash");
            Assert.Equal("63c3d97a9f8894d5e043a707b0fee7f7ec4c049a23bbf1079df20b4165f9e22d", ToHex(Blake2b.Hash256(message)));
            Assert.Equal("160ae16e5f0bc71d5815d7f5f42bd36fa2b4dc23e1c8dc3b59144821d083cd58", ToHex(Blake2b.ComputeHash(message, 32, key)));
            Assert.Equal(
                "1d25f7b19571ef69f5a7fac4494f74bb9c410ec485570beefe5a7e8d11fcc465",
                ToHex(Blake2b.ComputeHash(Encoding.ASCII.GetBytes("abc"), 32, key)));
        }

        /// <summary>
        /// Incremental keyed BLAKE2b from rusty-kaspa <c>crypto/hashes/src/hashers.rs</c> <c>test_vectors</c>.
        /// Each digest is the hash of every chunk so far, without resetting the hasher.
        /// The domain is <c>TransactionSigningHash</c>.
        /// </summary>
        [Fact]
        public void TransactionSigningHash_incremental_matches_rusty_kaspa()
        {
            var chunks = new[]
            {
                Array.Empty<byte>(),
                new byte[] { 1 },
                DecodeHex("05c77e2c4720528b7ad92b30347028d1b4538be748308830a8e285073c04a0cd"),
                Repeat(0x2a, 64),
                new byte[8],
            };
            var expected = new[]
            {
                "34c75037ad62740d4b3228f88f844f7901c07bfacd55a045be518eabc15e52ce",
                "8523b0471bcbea04575ccaa635eef9f9114f2890bda54367e5ff8caa3878bf82",
                "a51c49d9eb3d13f9de16e1aa8d1ff17668d55633ce00f36a643ac714b0fb137f",
                "487f199ef74c3e893e85bd37770e6334575a2d4d113b2e10474593c49807de93",
                "6392adc33a8e24e9a0a0c4c5f07f9c1cc958ad40c16d7a9a276e374cebb4e32b",
            };

            var hasher = new Blake2b(32, Encoding.ASCII.GetBytes("TransactionSigningHash"));
            for (var i = 0; i < chunks.Length; i++)
            {
                hasher.Update(chunks[i]);
                Assert.Equal(expected[i], ToHex(hasher.Clone().FinalizeHash()));
            }
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

        private static byte[] Repeat(byte value, int length)
        {
            var bytes = new byte[length];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = value;
            }

            return bytes;
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
