using System.Text;
using NKaspa.Crypto;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Official BLAKE3 test vectors from
    /// https://github.com/BLAKE3-team/BLAKE3/blob/master/test_vectors/test_vectors.json .
    /// Each input is bytes 0, 1, 2, ... modulo 251. Compared digests are the first 32 bytes.
    /// The keyed key is the 32-byte ASCII string "whats the Elvish word for friend".
    /// The empty PayloadDigest is rusty-kaspa ZERO_PAYLOAD_DIGEST in hashing/tx.rs.
    /// </summary>
    public class Blake3Tests
    {
        private static readonly byte[] Key = Encoding.ASCII.GetBytes("whats the Elvish word for friend");

        [Theory]
        [InlineData(0, "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262", "92b2b75604ed3c761f9d6f62392c8a9227ad0ea3f09573e783f1498a4ed60d26")]
        [InlineData(1, "2d3adedff11b61f14c886e35afa036736dcd87a74d27b5c1510225d0f592e213", "6d7878dfff2f485635d39013278ae14f1454b8c0a3a2d34bc1ab38228a80c95b")]
        [InlineData(63, "e9bc37a594daad83be9470df7f7b3798297c3d834ce80ba85d6e207627b7db7b", "bb1eb5d4afa793c1ebdd9fb08def6c36d10096986ae0cfe148cd101170ce37ae")]
        [InlineData(64, "4eed7141ea4a5cd4b788606bd23f46e212af9cacebacdc7d1f4c6dc7f2511b98", "ba8ced36f327700d213f120b1a207a3b8c04330528586f414d09f2f7d9ccb7e6")]
        [InlineData(65, "de1e5fa0be70df6d2be8fffd0e99ceaa8eb6e8c93a63f2d8d1c30ecb6b263dee", "c0a4edefa2d2accb9277c371ac12fcdbb52988a86edc54f0716e1591b4326e72")]
        [InlineData(127, "d81293fda863f008c09e92fc382a81f5a0b4a1251cba1634016a0f86a6bd640d", "c64200ae7dfaf35577ac5a9521c47863fb71514a3bcad18819218b818de85818")]
        [InlineData(128, "f17e570564b26578c33bb7f44643f539624b05df1a76c81f30acd548c44b45ef", "b04fe15577457267ff3b6f3c947d93be581e7e3a4b018679125eaf86f6a628ec")]
        [InlineData(1023, "10108970eeda3eb932baac1428c7a2163b0e924c9a9e25b35bba72b28f70bd11", "c951ecdf03288d0fcc96ee3413563d8a6d3589547f2c2fb36d9786470f1b9d6e")]
        [InlineData(1024, "42214739f095a406f3fc83deb889744ac00df831c10daa55189b5d121c855af7", "75c46f6f3d9eb4f55ecaaee480db732e6c2105546f1e675003687c31719c7ba4")]
        [InlineData(1025, "d00278ae47eb27b34faecf67b4fe263f82d5412916c1ffd97c8cb7fb814b8444", "357dc55de0c7e382c900fd6e320acc04146be01db6a8ce7210b7189bd664ea69")]
        [InlineData(2048, "e776b6028c7cd22a4d0ba182a8bf62205d2ef576467e838ed6f2529b85fba24a", "879cf1fa2ea0e79126cb1063617a05b6ad9d0b696d0d757cf053439f60a99dd1")]
        [InlineData(4096, "015094013f57a5277b59d8475c0501042c0b642e531b0a1c8f58d2163229e969", "befc660aea2f1718884cd8deb9902811d332f4fc4a38cf7c7300d597a081bfc0")]
        [InlineData(8192, "aae792484c8efe4f19e2ca7d371d8c467ffb10748d8a5a1ae579948f718a2a63", "dc9637c8845a770b4cbf76b8daec0eebf7dc2eac11498517f08d44c8fc00d58a")]
        [InlineData(102400, "bc3e3d41a1146b069abffad3c0d44860cf664390afce4d9661f7902e7943e085", "1c35d1a5811083fd7119f5d5d1ba027b4d01c0c6c49fb6ff2cf75393ea5db4a7")]
        public void Official_vectors(int length, string hash, string keyed)
        {
            var input = Input(length);
            Assert.Equal(hash, ToHex(Blake3.Hash(input)));
            Assert.Equal(keyed, ToHex(Blake3.KeyedHash(Key, input)));
        }

        [Fact]
        public void Empty_payload_digest_matches_rusty_kaspa()
        {
            var digest = Blake3.DomainHash("PayloadDigest", ReadOnlySpan<byte>.Empty);
            Assert.Equal(ToHex(SigHash.ZeroPayloadDigest), ToHex(digest));
            Assert.Equal("9c0ca2acb45e92ffe6ceb4ae29188b35c82d9676cdd3ce067fd6ccc30a9c4a38", ToHex(digest));
        }

        private static byte[] Input(int length)
        {
            var data = new byte[length];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 251);
            }

            return data;
        }

        private static string ToHex(byte[] data)
        {
            var chars = new char[data.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (var i = 0; i < data.Length; i++)
            {
                chars[i * 2] = alphabet[data[i] >> 4];
                chars[(i * 2) + 1] = alphabet[data[i] & 0x0F];
            }

            return new string(chars);
        }
    }
}
