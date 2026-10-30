using NKaspa;
using NKaspa.Crypto;
using KaspaPskt = NKaspa.PSKT.PSKT;

namespace NKaspa.Samples.PsktMultisig
{
    /// <summary>
    /// 2-of-2 Schnorr PSKT. Kaspa does not use BIP174 PSBT.
    /// The roles follow rusty-kaspa <c>wallet/pskt</c>: create, both parties sign, combine, finalize, extract.
    /// 2-of-2 Schnorr 多签。创建、双方签名、合并、完成、提取。不广播。
    /// </summary>
    public static class Program
    {
        // Public test scalars from the library tests. Not a wallet.
        // 库测试里的公开标量，不是钱包。
        private const string PartyA = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string PartyB = "0000000000000000000000000000000000000000000000000000000000000005";

        public static int Main(string[] args)
        {
            return Run(args, Console.Out);
        }

        /// <summary>Arguments are ignored. Amounts are local and are not submitted. 参数被忽略。金额只在本地，不会提交。</summary>
        public static int Run(string[] args, TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            _ = args;
            using var first = Key.Parse(PartyA);
            using var second = Key.Parse(PartyB);
            using var payeeKey = Key.Parse("0000000000000000000000000000000000000000000000000000000000000007");

            // Keys stay in this order. Schnorr multisig uses OP_CHECKMULTISIG, not the ECDSA opcode.
            // 公钥保持这个顺序。Schnorr 多签用 OP_CHECKMULTISIG。
            var redeem = KaspaPskt.MultisigRedeemScript(2, new[] { first.PubKey, second.PubKey });
            var utxo = new UtxoEntry(100_000_000UL, ScriptPubKey.PayToScriptHash(redeem), 20, false);
            var input = new NKaspa.PSKT.PSKTInput(new OutPoint(new uint256(8), 0))
            {
                Utxo = utxo,
                RedeemScript = redeem,
                SigOpCount = 2,
            };

            // Inside namespace NKaspa the bare name PSKT is the namespace, so this file aliases the type.
            // 写在 namespace NKaspa 里时裸名 PSKT 是命名空间，所以这里用别名 KaspaPskt。
            writer.WriteLine("step: create");
            var created = KaspaPskt.Create()
                .Constructor()
                .AddInput(input)
                .AddOutput(new NKaspa.PSKT.PSKTOutput(90_000_000UL, payeeKey.PubKey.GetScriptPubKey()))
                .Updater()
                .Signer();
            var unsignedHex = created.ToHex();
            writer.WriteLine("pskt-json: " + created.ToJson());

            // Each party only sees the unsigned hex, then returns its own signed hex.
            // 32 zero auxiliary bytes make the two Schnorr signatures deterministic.
            // 每一方只拿到未签名的十六进制，再交回自己签过的那一份。
            // 辅助随机数 32 字节全 0，签名可以重复。正式使用时不要传，让它随机。
            var aux = new byte[32];
            writer.WriteLine("step: sign");
            var signedA = KaspaPskt.FromHex(unsignedHex).Signer().Sign(first, aux);
            var signedB = KaspaPskt.FromHex(unsignedHex).Signer().Sign(second, aux);
            writer.WriteLine("party-a-sigs: " + signedA.Inputs[0].PartialSignatures.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteLine("party-b-sigs: " + signedB.Inputs[0].PartialSignatures.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

            writer.WriteLine("step: combine");
            var combined = KaspaPskt.FromHex(signedA.ToHex()).Combiner().Combine(KaspaPskt.FromHex(signedB.ToHex()));
            writer.WriteLine("combined-sigs: " + combined.Inputs[0].PartialSignatures.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

            writer.WriteLine("step: finalize");
            var finalized = combined.Finalizer().Finalize();
            writer.WriteLine("step: extract");
            var tx = finalized.Extractor().ExtractTransaction();
            writer.WriteLine("id: " + tx.GetId());
            writer.WriteLine("hex: " + tx.ToHex());
            writer.WriteLine("broadcast: no");

            if (signedA.Inputs[0].PartialSignatures.Count != 1
                || signedB.Inputs[0].PartialSignatures.Count != 1
                || combined.Inputs[0].PartialSignatures.Count != 2
                || finalized.Global.Id == null
                || !tx.GetId().Equals(finalized.Global.Id))
            {
                writer.WriteLine("error: pskt flow check failed");
                return 1;
            }

            var hash = tx.GetSignatureHash(0, utxo, SigHashType.All).ToBytes();
            var ops = tx.Inputs[0].SignatureScript.Ops;
            if (ops.Count != 3
                || ops[0].Data == null
                || ops[1].Data == null
                || ops[2].Data == null
                || !first.PubKey.Verify(new SchnorrSignature(ops[0].Data.AsSpan(0, 64)), hash)
                || !second.PubKey.Verify(new SchnorrSignature(ops[1].Data.AsSpan(0, 64)), hash)
                || !ops[2].Data.AsSpan().SequenceEqual(redeem.ToBytes()))
            {
                writer.WriteLine("error: extracted signatures did not verify");
                return 1;
            }

            writer.WriteLine("verified: true");
            writer.WriteLine("ok");
            return 0;
        }
    }
}
