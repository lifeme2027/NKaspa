namespace NKaspa
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.SigHash.
    /// The byte values are Kaspa's, from rusty-kaspa <c>sighash_type.rs</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The low 3 bits select which outputs are signed. Bit 7 is anyone-can-pay.
    /// The six accepted values are 0x01, 0x02, 0x04, 0x81, 0x82, and 0x84.
    /// </para>
    /// <para>
    /// Bitcoin uses the same three base names, but its bytes differ:
    /// Bitcoin SIGHASH_ALL is 0x01, SIGHASH_NONE is 0x02, SIGHASH_SINGLE is 0x03,
    /// and SIGHASH_ANYONECANPAY is 0x80. Kaspa SIGHASH_SINGLE is 0x04, not 0x03.
    /// </para>
    /// </remarks>
    public enum SigHashType : byte
    {
        /// <summary>Sign every input and every output. Byte 0x01.</summary>
        All = 0x01,

        /// <summary>Sign every input and no output. Byte 0x02.</summary>
        None = 0x02,

        /// <summary>Sign every input and the output at the same index. Byte 0x04.</summary>
        Single = 0x04,

        /// <summary>Sign this input and every output. Byte 0x81.</summary>
        AllAnyOneCanPay = 0x81,

        /// <summary>Sign this input and no output. Byte 0x82.</summary>
        NoneAnyOneCanPay = 0x82,

        /// <summary>Sign this input and the output at the same index. Byte 0x84.</summary>
        SingleAnyOneCanPay = 0x84,
    }

    /// <summary>Checks and classifies <see cref="SigHashType"/> the way Kaspa consensus does.</summary>
    public static class SigHashTypeExtensions
    {
        /// <summary>Low bits that select All, None, or Single.</summary>
        public const byte Mask = 0x07;

        /// <summary>Anyone-can-pay bit.</summary>
        public const byte AnyOneCanPayBit = 0x80;

        /// <summary>True when <paramref name="hashType"/> is one of the six consensus values.</summary>
        public static bool IsDefined(this SigHashType hashType)
        {
            return hashType switch
            {
                SigHashType.All or SigHashType.None or SigHashType.Single
                    or SigHashType.AllAnyOneCanPay or SigHashType.NoneAnyOneCanPay
                    or SigHashType.SingleAnyOneCanPay => true,
                _ => false,
            };
        }

        /// <summary>True when the base type is <see cref="SigHashType.All"/>.</summary>
        public static bool IsAll(this SigHashType hashType)
        {
            return ((byte)hashType & Mask) == (byte)SigHashType.All;
        }

        /// <summary>True when the base type is <see cref="SigHashType.None"/>.</summary>
        public static bool IsNone(this SigHashType hashType)
        {
            return ((byte)hashType & Mask) == (byte)SigHashType.None;
        }

        /// <summary>True when the base type is <see cref="SigHashType.Single"/>.</summary>
        public static bool IsSingle(this SigHashType hashType)
        {
            return ((byte)hashType & Mask) == (byte)SigHashType.Single;
        }

        /// <summary>True when anyone-can-pay is set.</summary>
        public static bool IsAnyOneCanPay(this SigHashType hashType)
        {
            return ((byte)hashType & AnyOneCanPayBit) == AnyOneCanPayBit;
        }
    }
}
