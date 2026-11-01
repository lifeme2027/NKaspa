using NKaspa;
using NKaspa.Crypto;
using NKaspa.DataEncoders;

namespace NKaspa.Samples.SignMessage
{
    /// <summary>
    /// Offline personal-message signature.
    /// The digest is BLAKE2b-256 of the UTF-8 message, keyed with <c>PersonalMessageSigningHash</c>.
    /// This is rusty-kaspa <c>sign_message</c>. It is not NBitcoin <c>Key.SignMessage</c> and it is not BIP322.
    /// 离线个人消息签名。摘要是以 PersonalMessageSigningHash 为密钥的 BLAKE2b-256。
    /// 这是 rusty-kaspa 的 sign_message，不是比特币消息前缀，也不是 BIP322。
    /// </summary>
    public static class Program
    {
        // Public test scalar used by the library tests. Not a wallet key.
        // 库测试里的公开标量，不是钱包密钥。
        private const string DemoSecret = "0000000000000000000000000000000000000000000000000000000000000003";

        public static int Main(string[] args)
        {
            return Run(args, Console.Out);
        }

        /// <summary>The arguments are the message. The default is <c>NKaspa</c>. 参数拼成消息，默认 NKaspa。</summary>
        public static int Run(string[] args, TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            var message = args == null || args.Length == 0 ? "NKaspa" : string.Join(" ", args);
            var digest = PersonalMessage.Hash(message);

            // 32 explicit auxiliary bytes, here all zeros, make BIP-340 deterministic.
            // Omit them in an application that wants fresh randomness.
            // 传入 32 字节辅助随机数（这里全 0）后，Schnorr 签名可重复。
            // 要随机签名就不要传这 32 字节。
            var aux = new byte[32];
            using var key = Key.Parse(DemoSecret);
            var schnorr = PersonalMessage.SignMessage(key, message, aux);
            var address = key.GetAddress(Network.Mainnet);
            var schnorrOk = PersonalMessage.VerifyMessage(key.PubKey, message, schnorr);
            var addressOk = PersonalMessage.VerifyMessage(address, message, schnorr);
            var tampered = PersonalMessage.VerifyMessage(key.PubKey, message + " ", schnorr);
            var roundTrip = PersonalMessage.VerifyMessage(key.PubKey, message, SchnorrSignature.Parse(schnorr.ToString()));

            writer.WriteLine("message: " + message);
            writer.WriteLine("digest: " + Encoders.Hex.EncodeData(digest));
            writer.WriteLine("pubkey: " + key.PubKey);
            writer.WriteLine("address: " + address);
            writer.WriteLine("schnorr: " + schnorr);
            writer.WriteLine("schnorr-ok: " + (schnorrOk ? "true" : "false"));
            writer.WriteLine("address-ok: " + (addressOk ? "true" : "false"));
            writer.WriteLine("tampered-ok: " + (tampered ? "true" : "false"));
            if (!schnorrOk || !addressOk || tampered || !roundTrip)
            {
                writer.WriteLine("error: verify check failed");
                return 1;
            }

            writer.WriteLine("ok");
            return 0;
        }
    }
}
