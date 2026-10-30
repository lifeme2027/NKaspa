using System.Text;
using NKaspa.DataEncoders;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Short vectors copied from kaswallet <c>KaspaBech32Tests.Encodes_rusty_kaspa_vectors</c>
    /// (D:\Project2026\kaswallet\dotnet\tests\KasWallet.Core.Tests\KaspaBech32Tests.cs),
    /// which records the rusty-kaspa cashaddr examples.
    /// </summary>
    public class KaspaBech32Tests
    {
        [Theory]
        [InlineData("a", 0, "", "a:qqeq69uvrh")]
        [InlineData("a", 8, "", "a:pq99546ray")]
        [InlineData("b", 8, " ", "b:pqsqzsjd64fv")]
        [InlineData("b", 8, "-", "b:pqksmhczf8ud")]
        [InlineData("b", 8, "0", "b:pqcq53eqrk0e")]
        [InlineData("b", 8, "1", "b:pqcshg75y0vf")]
        [InlineData("b", 8, "-1", "b:pqknzl4e9y0zy")]
        [InlineData("b", 8, "11", "b:pqcnzt888ytdg")]
        [InlineData("b", 8, "abc", "b:ppskycc8txxxn2w")]
        [InlineData("b", 8, "1234598760", "b:pqcnyve5x5unsdekxqeusxeyu2")]
        [InlineData("b", 8, "abcdefghijklmnopqrstuvwxyz", "b:ppskycmyv4nxw6rfdf4kcmtwdac8zunnw36hvamc09aqtpppz8lk")]
        [InlineData("b", 8, "000000000000000000000000000000000000000000", "b:pqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrq7ag684l3")]
        public void Encodes_rusty_kaspa_vectors(string prefix, int version, string payloadText, string expected)
        {
            var payload = Encoding.ASCII.GetBytes(payloadText);
            var encoder = new KaspaBech32Encoder(prefix);
            Assert.Equal(expected, encoder.Encode((byte)version, payload));

            Assert.True(KaspaBech32Encoder.TryDecode(expected, out var decodedPrefix, out var decodedVersion, out var decodedPayload, out _));
            Assert.Equal(prefix, decodedPrefix);
            Assert.Equal((byte)version, decodedVersion);
            Assert.Equal(payload, decodedPayload);
            Assert.Equal(payload, encoder.Decode(expected, out var versionOut));
            Assert.Equal((byte)version, versionOut);
        }
    }
}
