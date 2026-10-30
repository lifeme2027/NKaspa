using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Canonical pushes follow rusty-kaspa <c>crypto/txscript/src/script_builder.rs</c>:
    /// 17 is <c>01 11</c>, -1 is <c>OP_1NEGATE</c>, 128 is <c>02 80 00</c>.
    /// Opcode numbers that differ from Bitcoin are pinned from
    /// rusty-kaspa <c>crypto/txscript/src/opcodes/mod.rs</c>.
    /// </summary>
    public class ScriptTests
    {
        [Fact]
        public void Standard_templates_match_the_address_scripts()
        {
            var xOnly = new byte[32];
            xOnly[0] = 0x5f;
            var schnorr = Script.PayToPubKey(xOnly);
            Assert.True(schnorr.IsPayToPubKey);
            Assert.False(schnorr.IsPayToPubKeyEcdsa);
            Assert.Equal("OP_DATA_32 5f" + new string('0', 62) + " OP_CHECKSIG", schnorr.ToString());
            Assert.Equal(schnorr.ToBytes(), ScriptPubKey.PayToPubKey(xOnly).Script);

            var ecdsa = Script.PayToPubKey(new PubKeyECDSA(GeneratorCompressed()));
            Assert.True(ecdsa.IsPayToPubKeyEcdsa);
            Assert.Equal(OpcodeType.OP_CHECKSIGECDSA, ecdsa.Ops[ecdsa.Ops.Count - 1].Code);

            var redeem = Script.PayToPubKey(xOnly);
            var p2sh = Script.PayToScriptHash(redeem);
            Assert.True(p2sh.IsPayToScriptHash);
            Assert.True(p2sh.TryGetPayToScriptHash(out var hash));
            Assert.Equal(redeem.GetScriptHash(), hash);
            Assert.Equal(0, ScriptPubKey.PayToScriptHash(redeem).Version);
            Assert.True(ScriptPubKey.PayToScriptHash(redeem).IsPayToScriptHash);
        }

        [Fact]
        public void Script_numbers_use_the_canonical_push()
        {
            Assert.Equal("00", new ScriptBuilder().AddInt64(0).ToScript().ToHex());
            Assert.Equal("4f", new ScriptBuilder().AddInt64(-1).ToScript().ToHex());
            Assert.Equal("51", new ScriptBuilder().AddInt64(1).ToScript().ToHex());
            Assert.Equal("60", new ScriptBuilder().AddInt64(16).ToScript().ToHex());
            Assert.Equal("0111", new ScriptBuilder().AddInt64(17).ToScript().ToHex());
            Assert.Equal("028000", new ScriptBuilder().AddInt64(128).ToScript().ToHex());
            Assert.Equal("0182", new ScriptBuilder().AddInt64(-2).ToScript().ToHex());
        }

        [Fact]
        public void Long_data_uses_pushdata_and_truncated_scripts_fail()
        {
            var data = new byte[80];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i;
            }

            var script = new ScriptBuilder().AddData(data).ToScript();
            Assert.Equal(OpcodeType.OP_PUSHDATA1, script.Ops[0].Code);
            Assert.Equal(data, script.Ops[0].Data);
            Assert.Equal(script.ToBytes(), Script.Parse(script.ToHex()).ToBytes());

            Assert.Throws<FormatException>(() => new Script(new byte[] { 0x02, 0x01 }));
            Assert.Throws<FormatException>(() => new Script(new byte[] { (byte)OpcodeType.OP_PUSHDATA1 }));
        }

        [Fact]
        public void Opcode_values_that_differ_from_bitcoin_are_the_kaspa_numbers()
        {
            Assert.Equal(0xac, (byte)OpcodeType.OP_CHECKSIG);
            Assert.Equal(0xab, (byte)OpcodeType.OP_CHECKSIGECDSA);
            Assert.Equal(0xaa, (byte)OpcodeType.OP_BLAKE2B);
            Assert.Equal(0xa9, (byte)OpcodeType.OP_CHECKMULTISIGECDSA);
            Assert.Equal(0xa7, (byte)OpcodeType.OP_BLAKE2BWITHKEY);
            Assert.Equal(0xa6, (byte)OpcodeType.OP_ZKPRECOMPILE);
            Assert.Equal(0xb0, (byte)OpcodeType.OP_CHECKLOCKTIMEVERIFY);
            Assert.Equal(0xb1, (byte)OpcodeType.OP_CHECKSEQUENCEVERIFY);
            Assert.Equal(0x7e, (byte)OpcodeType.OP_CAT);
            Assert.True(OpcodeType.OP_CAT.IsDisabled() == false);
            Assert.True(OpcodeType.OP_LEFT.IsDisabled());
            Assert.True(OpcodeType.OP_2MUL.IsDisabled());
            Assert.True(OpcodeType.OP_LSHIFT.IsDisabled());
        }

        [Fact]
        public void Signature_script_is_a_65_byte_push_and_p2sh_appends_the_redeem_script()
        {
            var signature = new Crypto.SchnorrSignature(new byte[64]);
            var sigScript = Script.SignatureScript(signature, SigHashType.All);
            Assert.Equal(66, sigScript.Length);
            Assert.Equal(0x41, sigScript.ToBytes()[0]);
            Assert.Equal(0x01, sigScript.ToBytes()[65]);
            Assert.True(sigScript.TryParseSignature(out var parsed, out var hashType));
            Assert.Equal(SigHashType.All, hashType);
            Assert.Equal(new byte[64], parsed);

            var redeem = Script.PayToPubKey(new byte[32]);
            var p2shSig = Script.PayToScriptHashSignature(redeem, sigScript);
            var bytes = p2shSig.ToBytes();
            Assert.Equal(66 + 1 + redeem.Length, bytes.Length);
            Assert.Equal((byte)redeem.Length, bytes[66]);
            Assert.Equal(redeem.ToBytes(), bytes.AsSpan(67).ToArray());
        }

        private static byte[] GeneratorCompressed()
        {
            var secret = new byte[32];
            secret[31] = 1;
            using var key = new Key(secret);
            return key.PubKeyECDSA.ToBytes();
        }
    }
}
