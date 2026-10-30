namespace NKaspa
{
    /// <summary>
    /// Number of words in a BIP39 mnemonic.
    /// Kaspa counterpart of NBitcoin.WordCount.
    /// </summary>
    public enum WordCount
    {
        /// <summary>12 words, 128 bits of entropy.</summary>
        Twelve = 12,

        /// <summary>15 words, 160 bits of entropy.</summary>
        Fifteen = 15,

        /// <summary>18 words, 192 bits of entropy.</summary>
        Eighteen = 18,

        /// <summary>21 words, 224 bits of entropy.</summary>
        TwentyOne = 21,

        /// <summary>24 words, 256 bits of entropy.</summary>
        TwentyFour = 24,
    }
}
