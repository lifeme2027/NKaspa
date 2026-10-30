using NKaspa.DataEncoders;
using Xunit;

namespace NKaspa.Tests
{
    public class NetworkAndHexTests
    {
        [Fact]
        public void Networks_expose_kaspa_prefixes()
        {
            Assert.Equal("kaspa", Network.Mainnet.AddressPrefix);
            Assert.Equal("kaspatest", Network.Testnet.AddressPrefix);
            Assert.Equal("kaspasim", Network.Simnet.AddressPrefix);
            Assert.Equal("kaspadev", Network.Devnet.AddressPrefix);
            Assert.Equal(16111, Network.Mainnet.P2PPort);
            Assert.Equal(16211, Network.Testnet.P2PPort);
            Assert.Equal(Network.Testnet, Network.GetNetwork("testnet-10"));
            Assert.Equal(Network.Mainnet, Network.GetNetwork("KASPA"));
            Assert.False(Network.TryGetNetwork("regtest", out _));
            Assert.Equal(4, Network.All.Count);
        }

        [Fact]
        public void Hex_encoder_roundtrips_and_rejects_odd_length()
        {
            var encoder = (HexEncoder)Encoders.Hex;
            Assert.Equal("00ff0a", encoder.EncodeData(new byte[] { 0x00, 0xFF, 0x0A }));
            Assert.Equal(new byte[] { 0x0A, 0xBC }, encoder.DecodeData("0aBC"));
            Assert.False(encoder.IsValid("abc"));
            Assert.False(encoder.IsValid("zz"));
            Assert.Throws<FormatException>(() => encoder.DecodeData("0"));
        }
    }
}
