namespace NKaspa
{
    /// <summary>
    /// 4-byte Base58Check version prefixes for extended keys.
    /// Kaspa mainnet is <c>kprv</c>/<c>kpub</c>. Every other Kaspa network is <c>ktrv</c>/<c>ktub</c>.
    /// Those four values match rusty-kaspa <c>wallet/bip32</c> <c>Prefix</c>.
    /// Bitcoin <c>xprv</c>/<c>xpub</c> and the testnet and SLIP-132 prefixes are accepted so BIP32 vectors can be parsed.
    /// </summary>
    public static class HdKeyVersions
    {
        /// <summary>Bitcoin mainnet extended public key, <c>xpub</c>.</summary>
        public const uint Xpub = 0x0488B21E;

        /// <summary>Bitcoin mainnet extended private key, <c>xprv</c>.</summary>
        public const uint Xprv = 0x0488ADE4;

        /// <summary>Bitcoin P2SH-P2WPKH extended public key, <c>ypub</c>.</summary>
        public const uint Ypub = 0x049D7CB2;

        /// <summary>Bitcoin P2SH-P2WPKH extended private key, <c>yprv</c>.</summary>
        public const uint Yprv = 0x049D7878;

        /// <summary>Bitcoin P2WPKH extended public key, <c>zpub</c>.</summary>
        public const uint Zpub = 0x04B24746;

        /// <summary>Bitcoin P2WPKH extended private key, <c>zprv</c>.</summary>
        public const uint Zprv = 0x04B2430C;

        /// <summary>Bitcoin testnet extended public key, <c>tpub</c>.</summary>
        public const uint Tpub = 0x043587CF;

        /// <summary>Bitcoin testnet extended private key, <c>tprv</c>.</summary>
        public const uint Tprv = 0x04358394;

        /// <summary>Kaspa mainnet extended public key, <c>kpub</c>. rusty-kaspa <c>0x038f332e</c>.</summary>
        public const uint Kpub = 0x038F332E;

        /// <summary>Kaspa mainnet extended private key, <c>kprv</c>. rusty-kaspa <c>0x038f2ef4</c>.</summary>
        public const uint Kprv = 0x038F2EF4;

        /// <summary>Kaspa testnet, simnet, and devnet extended public key, <c>ktub</c>. rusty-kaspa <c>0x0390a241</c>.</summary>
        public const uint Ktub = 0x0390A241;

        /// <summary>Kaspa testnet, simnet, and devnet extended private key, <c>ktrv</c>. rusty-kaspa <c>0x03909e07</c>.</summary>
        public const uint Ktrv = 0x03909E07;

        /// <summary>True when <paramref name="version"/> is a known private prefix.</summary>
        public static bool IsPrivate(uint version)
        {
            return version == Xprv || version == Yprv || version == Zprv || version == Tprv || version == Kprv || version == Ktrv;
        }

        /// <summary>True when <paramref name="version"/> is a known public prefix.</summary>
        public static bool IsPublic(uint version)
        {
            return version == Xpub || version == Ypub || version == Zpub || version == Tpub || version == Kpub || version == Ktub;
        }

        /// <summary>Public prefix that pairs with a known private prefix.</summary>
        public static uint ToPublic(uint privateVersion)
        {
            if (privateVersion == Xprv)
            {
                return Xpub;
            }

            if (privateVersion == Yprv)
            {
                return Ypub;
            }

            if (privateVersion == Zprv)
            {
                return Zpub;
            }

            if (privateVersion == Tprv)
            {
                return Tpub;
            }

            if (privateVersion == Kprv)
            {
                return Kpub;
            }

            if (privateVersion == Ktrv)
            {
                return Ktub;
            }

            throw new ArgumentException("Unknown extended private key version.", nameof(privateVersion));
        }
    }
}
