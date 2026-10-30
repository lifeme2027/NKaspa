using System.Text;
using NKaspa;
using NKaspa.Crypto;
using NKaspa.DataEncoders;

namespace NKaspa.Samples.SignMessage
{
    /// <summary>
    /// Offline Schnorr and ECDSA signatures over a 32-byte digest.
    /// This is not NBitcoin <c>Key.SignMessage</c> and it is not BIP322.
    /// The digest is unkeyed BLAKE2b-256 of the UTF-8 message.
    /// 离线签名。签的是 UTF-8 消息的 BLAKE2b-256，不是比特币那套消息前缀，也不是 BIP322。
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
            var digest = Hashes.Blake2b256(Encoding.UTF8.GetBytes(message)).ToBytes();

            // 32 explicit auxiliary bytes, here all zeros, make BIP-340 deterministic.
            // Omit them in an application that wants fresh randomness.
            // 传入 32 字节辅助随机数（这里全 0）后，Schnorr 签名可重复。
            // 要随机签名就不要传这 32 字节。ECDSA 走 RFC 6979，本来就可重复。
            var aux = new byte[32];
            using var key = Key.Parse(DemoSecret);
            var schnorr = key.SignSchnorr(digest, aux);
            var ecdsa = key.SignECDSA(digest);
            var schnorrOk = key.PubKey.Verify(schnorr, digest);
            var ecdsaOk = key.PubKeyECDSA.Verify(ecdsa, digest);
            var tampered = (byte[])digest.Clone();
            tampered[0] ^= 0x01;
            var schnorrBad = key.PubKey.Verify(schnorr, tampered);
            var ecdsaBad = key.PubKeyECDSA.Verify(ecdsa, tampered);
            var roundTrip = key.PubKey.Verify(SchnorrSignature.Parse(schnorr.ToString()), digest)
                && key.PubKeyECDSA.Verify(ECDSASignature.Parse(ecdsa.ToString()), digest);

            writer.WriteLine("message: " + message);
            writer.WriteLine("digest: " + Encoders.Hex.EncodeData(digest));
            writer.WriteLine("pubkey: " + key.PubKey);
            writer.WriteLine("pubkey-ecdsa: " + key.PubKeyECDSA);
            writer.WriteLine("schnorr: " + schnorr);
            writer.WriteLine("ecdsa: " + ecdsa);
            writer.WriteLine("schnorr-ok: " + (schnorrOk ? "true" : "false"));
            writer.WriteLine("ecdsa-ok: " + (ecdsaOk ? "true" : "false"));
            writer.WriteLine("tampered-schnorr-ok: " + (schnorrBad ? "true" : "false"));
            writer.WriteLine("tampered-ecdsa-ok: " + (ecdsaBad ? "true" : "false"));
            if (!schnorrOk || !ecdsaOk || schnorrBad || ecdsaBad || !roundTrip)
            {
                writer.WriteLine("error: verify check failed");
                return 1;
            }

            writer.WriteLine("ok");
            return 0;
        }
    }
}
