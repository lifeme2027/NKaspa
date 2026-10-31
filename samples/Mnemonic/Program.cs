using System.Globalization;
using NKaspa;
using WalletMnemonic = NKaspa.Mnemonic;

namespace NKaspa.Samples.Mnemonic
{
    /// <summary>
    /// BIP39 mnemonic and BIP32 receive addresses.
    /// Kaspa counterpart of the NBitcoin <c>Mnemonic</c> / <c>ExtKey</c> how-to.
    /// 生成助记词，并按 m/44'/111111'/0'/0/i 派生主网和测试网地址。
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            return Run(args, Console.Out);
        }

        /// <summary>
        /// Pass a 12, 15, 18, 21, or 24 word sentence to reuse it.
        /// With no words, this draws a new 12-word English mnemonic.
        /// 传入助记词就用那一句；不传则新生成 12 个英文词。口令为空。
        /// </summary>
        public static int Run(string[] args, TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            WalletMnemonic mnemonic;
            try
            {
                mnemonic = args == null || args.Length == 0
                    ? new WalletMnemonic(Wordlist.English, WordCount.Twelve)
                    : new WalletMnemonic(string.Join(" ", args), Wordlist.English);
            }
            catch (FormatException ex)
            {
                writer.WriteLine("error: " + ex.Message);
                return 1;
            }

            // A bad checksum still parses, the same way NBitcoin does. DeriveSeed would throw.
            // 校验和错误时宽松构造仍能返回对象，和 NBitcoin 一样。DeriveSeed 会拒绝它。这里不往下派生。
            if (!mnemonic.IsValidChecksum)
            {
                writer.WriteLine("checksum: invalid");
                return 1;
            }

            writer.WriteLine("mnemonic: " + mnemonic.ToPhrase());
            using var master = mnemonic.DeriveExtKey();
            var account = master.Neuter();
            writer.WriteLine("kpub: " + account.ToString(Network.Mainnet));
            writer.WriteLine("ktub: " + account.ToString(Network.Testnet));

            for (uint i = 0; i < 3; i++)
            {
                // KeyPath.KaspaReceive(i) is m/44'/111111'/0'/0/i. Coin type 111111.
                // 收款链是 0。找零链是 KeyPath.KaspaChange，这里不打印。
                var path = KeyPath.KaspaReceive(i);
                using var child = master.Derive(path);
                using var key = child.PrivateKey;
                var main = key.GetAddress(Network.Mainnet);
                var test = key.GetAddress(Network.Testnet);
                if (!string.Equals(path.ToString(), "m/44'/111111'/0'/0/" + i.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    || !KaspaAddress.IsValid(main.ToString())
                    || !KaspaAddress.IsValid(test.ToString())
                    || main.Network != Network.Mainnet
                    || test.Network != Network.Testnet
                    || !main.ScriptPubKey.IsPayToPubKey
                    || !test.ScriptPubKey.IsPayToPubKey
                    || main.ToString() == test.ToString())
                {
                    writer.WriteLine("error: derived address failed its own check");
                    return 1;
                }

                var index = i.ToString(CultureInfo.InvariantCulture);
                writer.WriteLine("path[" + index + "]: " + path);
                writer.WriteLine("mainnet[" + index + "]: " + main);
                writer.WriteLine("testnet[" + index + "]: " + test);
            }

            writer.WriteLine("ok");
            return 0;
        }
    }
}
