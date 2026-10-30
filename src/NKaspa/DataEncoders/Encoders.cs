namespace NKaspa.DataEncoders
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.DataEncoders.Encoders.
    /// Shared encoder instances.
    /// </summary>
    public static class Encoders
    {
        private static readonly HexEncoder HexEncoder = new HexEncoder();

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
    }
}
