namespace NKaspa.DataEncoders
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.DataEncoders.Encoders.
    /// Shared encoder instances.
    /// </summary>
    public static class Encoders
    {
        private static readonly HexEncoder HexEncoder = new HexEncoder();
        private static readonly Base58Encoder Base58EncoderInstance = new Base58Encoder();
        private static readonly Base58CheckEncoder Base58CheckEncoderInstance = new Base58CheckEncoder();

        /// <summary>Lowercase hexadecimal encoder.</summary>
        public static DataEncoder Hex
        {
            get { return HexEncoder; }
        }

        /// <summary>
        /// Cashaddr-style bech32 encoder bound to <paramref name="prefix"/>.
        /// Kaspa counterpart of NBitcoin.DataEncoders.Encoders.Bech32, for Kaspa prefixes.
        /// </summary>
        public static KaspaBech32Encoder KaspaBech32(string prefix)
        {
            return new KaspaBech32Encoder(prefix);
        }

        /// <summary>Base58 encoder. Used by extended keys, not by Kaspa addresses.</summary>
        public static DataEncoder Base58
        {
            get { return Base58EncoderInstance; }
        }

        /// <summary>Base58Check encoder. The checksum is SHA256(SHA256(payload)).</summary>
        public static DataEncoder Base58Check
        {
            get { return Base58CheckEncoderInstance; }
        }
    }
}
