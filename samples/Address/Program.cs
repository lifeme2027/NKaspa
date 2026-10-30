using NKaspa;

namespace NKaspa.Samples.Address
{
    /// <summary>
    /// Parse, validate, and show <see cref="ScriptPubKey"/>.
    /// Kaspa counterpart of NBitcoin <c>BitcoinAddress.Create</c> and <c>ScriptPubKey</c>.
    /// 解析并校验地址，打印锁定脚本。地址是 cashaddr，不是 Base58。
    /// </summary>
    public static class Program
    {
        // Vectors shared with KaspaAddressTests. Payloads are public test keys, not a wallet.
        // 与地址测试同一组公开向量，不是钱包。
        private const string Schnorr = "kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva";
        private const string Ecdsa = "kaspatest:qypt5q0uta8fmxretxwxng76lkp45uj7tuhfxn5nytkd8tcep2c0vrsxarh9t02";
        private const string ScriptHash = "kaspa:pqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqlmtfk4dg";
        private const string BadChecksum = "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4l";

        public static int Main(string[] args)
        {
            return Run(args, Console.Out);
        }

        /// <summary>Arguments are ignored. The sample always prints the same vectors. 参数被忽略，每次打印同一组向量。</summary>
        public static int Run(string[] args, TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            _ = args;
            if (!Show(writer, Schnorr, "20dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac", true)
                || !Show(writer, Ecdsa, "2102ba01fc5f4e9d9879599c69a3dafd835a725e5f2e934e9322ecd3af190ab0f60eab", false)
                || !Show(writer, ScriptHash, "aa20" + new string('0', 64) + "87", false))
            {
                return 1;
            }

            // TryParse does not throw. Parse does. IsValid is the predicate.
            // TryParse 不抛。Parse 会抛 FormatException。IsValid 只回答是或否。
            writer.WriteLine("address: " + BadChecksum);
            writer.WriteLine("valid: " + (KaspaAddress.IsValid(BadChecksum) ? "true" : "false"));
            if (KaspaAddress.IsValid(BadChecksum) || KaspaAddress.TryParse(BadChecksum, out _))
            {
                writer.WriteLine("error: bad checksum was accepted");
                return 1;
            }

            try
            {
                KaspaAddress.Parse(BadChecksum);
                writer.WriteLine("error: Parse did not reject the bad checksum");
                return 1;
            }
            catch (FormatException ex)
            {
                writer.WriteLine("rejected: " + ex.Message);
            }

            // The prefix selects the network. A testnet string is not a mainnet address.
            // 前缀决定网络。测试网字符串不能当成主网地址。
            if (KaspaAddress.TryParse(Ecdsa, Network.Mainnet, out _))
            {
                writer.WriteLine("error: testnet address was accepted as mainnet");
                return 1;
            }

            try
            {
                KaspaAddress.Parse(Ecdsa, Network.Mainnet);
                writer.WriteLine("error: expected network was not enforced");
                return 1;
            }
            catch (FormatException)
            {
                writer.WriteLine("network-mismatch: " + Ecdsa + " is not " + Network.Mainnet.Name);
            }

            writer.WriteLine("ok");
            return 0;
        }

        private static bool Show(TextWriter writer, string text, string scriptHex, bool schnorr)
        {
            if (!KaspaAddress.IsValid(text) || !KaspaAddress.TryParse(text, out var parsed) || parsed == null)
            {
                writer.WriteLine("error: failed to parse " + text);
                return false;
            }

            var again = KaspaAddress.Parse(text);
            var script = parsed.ScriptPubKey;
            writer.WriteLine("address: " + text);
            writer.WriteLine("network: " + parsed.Network.Name);
            writer.WriteLine("type: " + parsed.GetType().Name);
            writer.WriteLine("version: " + parsed.Version);
            writer.WriteLine("script-version: " + script.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteLine("script: " + script.ToScript());
            writer.WriteLine("script-hex: " + script.ToHex());
            writer.WriteLine("valid: true");
            if (!string.Equals(again.ToString(), text, StringComparison.Ordinal)
                || !string.Equals(script.ToHex(), scriptHex, StringComparison.Ordinal)
                || script.Version != 0
                || script.IsPayToPubKey != schnorr
                || script.IsPayToPubKeyEcdsa != text.Contains("qypt5q0", StringComparison.Ordinal)
                || script.IsPayToScriptHash != text.StartsWith("kaspa:p", StringComparison.Ordinal))
            {
                writer.WriteLine("error: script check failed for " + text);
                return false;
            }

            return true;
        }
    }
}
