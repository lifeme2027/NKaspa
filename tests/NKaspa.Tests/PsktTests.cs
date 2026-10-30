using NKaspa.Crypto;
using NKaspa.DataEncoders;
using NKaspa.PSKT;
using KaspaPskt = NKaspa.PSKT.PSKT;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// PSKT role flow, combine rules, finalize failures, and JSON layout.
    /// rusty-kaspa wallet/pskt tests and the multisig example use random keys and do not pin bytes.
    /// The empty document is pinned from the serde field order. Populated documents round-trip through this writer.
    /// </summary>
    public class PsktTests
    {
        private const string EmptyJson = "{\"global\":{\"version\":1,\"txVersion\":0,\"fallbackLockTime\":null,\"inputsModifiable\":false,\"outputsModifiable\":false,\"inputCount\":0,\"outputCount\":0,\"xpubs\":{},\"id\":null,\"proprietaries\":{},\"payload\":null},\"inputs\":[],\"outputs\":[]}";
        private static readonly byte[] Aux = new byte[32];

        [Fact]
        public void Empty_pskt_matches_the_serde_document_and_round_trips()
        {
            var created = KaspaPskt.Create();
            Assert.Equal(PSKTRole.Creator, created.Role);
            Assert.Equal(EmptyJson, created.ToJson());
            Assert.StartsWith("PSKT", created.ToHex());
            Assert.DoesNotContain(" ", created.ToJson());

            var parsed = KaspaPskt.FromHex(created.ToHex());
            Assert.Equal(PSKTRole.Combiner, parsed.Role);
            Assert.Equal(EmptyJson, parsed.ToJson());
            Assert.Equal(created.ToHex(), parsed.ToHex());
            Assert.Throws<FormatException>(() => KaspaPskt.FromHex("PSKB"));
            Assert.Throws<FormatException>(() => KaspaPskt.FromHex(Encoders.Hex.EncodeData(new byte[] { 1 })));
        }

        [Fact]
        public void Bundle_is_a_json_array_with_the_pskb_prefix()
        {
            var bundle = new PSKTBundle();
            Assert.Equal("[]", bundle.ToJson());
            Assert.StartsWith("PSKB", bundle.ToHex());
            Assert.Empty(PSKTBundle.FromHex(bundle.ToHex()).Items);

            var one = new PSKTBundle(KaspaPskt.Create());
            var other = new PSKTBundle(KaspaPskt.Create().SetVersion(PSKTVersion.Zero));
            one.Merge(other);
            var parsed = PSKTBundle.FromHex(one.ToHex());
            Assert.Equal(2, parsed.Items.Count);
            Assert.Equal(PSKTVersion.One, parsed.Items[0].Global.Version);
            Assert.Equal(PSKTVersion.Zero, parsed.Items[1].Global.Version);
            Assert.Throws<FormatException>(() => PSKTBundle.FromHex(KaspaPskt.Create().ToHex()));
        }

        [Fact]
        public void Populated_json_round_trips_schnorr_and_ecdsa_partial_signatures()
        {
            using var schnorrKey = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
            using var ecdsaKey = Key.Parse("0000000000000000000000000000000000000000000000000000000000000007");
            var redeem = new Script(new byte[] { 0x51, 0xae });
            var spk = ScriptPubKey.PayToScriptHash(redeem);
            var covenantId = new uint256(9);
            var input = new PSKTInput(new OutPoint(new uint256(4), 3))
            {
                Utxo = new UtxoEntry(50UL, spk, 11, true, covenantId),
                Sequence = 6,
                MinTime = 8,
                SighashType = SigHashType.None,
                RedeemScript = redeem,
                SigOpCount = 2,
                FinalScriptSig = Script.SignatureScript(new SchnorrSignature(new byte[64]), SigHashType.All),
            };
            input.PartialSignatures.Add(PSKTSignature.Schnorr(schnorrKey.PubKeyECDSA, new SchnorrSignature(Repeat(0x11, 64))));
            input.PartialSignatures.Add(PSKTSignature.Ecdsa(ecdsaKey.PubKeyECDSA, ecdsaKey.SignECDSA(Repeat(0x22))));
            input.Derivations.Add(new PSKTDerivation(schnorrKey.PubKeyECDSA, new PSKTKeySource(new HDFingerprint(new byte[] { 1, 2, 3, 4 }), new KeyPath("m/44'/111111'/0'/0/1"))));
            input.Derivations.Add(new PSKTDerivation(ecdsaKey.PubKeyECDSA, null));
            input.Proprietaries["note"] = "\"alpha\"";
            input.Unknowns["zzExtra"] = "7";

            var output = new PSKTOutput(40, schnorrKey.PubKey.GetScriptPubKey())
            {
                RedeemScript = redeem,
                Covenant = new CovenantBinding(1, covenantId),
            };
            output.Proprietaries["tag"] = "true";

            using var master = ExtKey.CreateFromSeed(Encoders.Hex.DecodeData("000102030405060708090a0b0c0d0e0f"));
            var pskt = KaspaPskt.Create()
                .SetVersion(PSKTVersion.Two)
                .FallbackLockTime(1)
                .InputsModifiable()
                .Constructor()
                .SetTransactionVersion(1)
                .SetPayload(new byte[] { 9, 8 })
                .AddInput(input)
                .AddOutput(output);
            pskt.Global.Xpubs.Add(new PSKTXpub(master.Neuter(), new PSKTKeySource(new HDFingerprint(new byte[] { 9, 9, 9, 9 }), new KeyPath("m/0"))));
            pskt.Global.Proprietaries["wallet"] = "\"nkaspa\"";
            pskt.Global.Unknowns["vendor"] = "{\"ok\":true}";
            pskt.Global.Id = new uint256(2);

            var json = pskt.ToJson();
            Assert.Contains("\"eCDSA\":", json);
            Assert.Contains("\"schnorr\":", json);
            Assert.Contains("\"sighashType\":2", json);
            Assert.DoesNotContain(" ", json);
            Assert.StartsWith("0000", json.Substring(json.IndexOf("scriptPublicKey", StringComparison.Ordinal) + "scriptPublicKey".Length + 3));

            var parsed = KaspaPskt.FromJson(json);
            Assert.Equal(json, parsed.ToJson());
            Assert.Equal(pskt.ToHex(), parsed.ToHex());
            Assert.Equal(2, parsed.Inputs[0].PartialSignatures.Count);
            Assert.Equal((byte)2, parsed.Inputs[0].SigOpCount);
            Assert.Null(parsed.Inputs[0].ComputeBudget);
            Assert.Equal("\"alpha\"", parsed.Inputs[0].Proprietaries["note"]);
            Assert.Equal("7", parsed.Inputs[0].Unknowns["zzExtra"]);
            Assert.Equal("{\"ok\":true}", parsed.Global.Unknowns["vendor"]);
            Assert.StartsWith("xpub", parsed.Global.Xpubs[0].ToXpub());
            Assert.Equal("m/44'/111111'/0'/0/1", parsed.Inputs[0].Derivations.Find(item => item.Source != null)!.Source!.Path.ToString());
            Assert.Contains(parsed.Inputs[0].Derivations, item => item.Source == null);
        }

        [Fact]
        public void Ecdsa_der_round_trips_to_the_compact_form()
        {
            using var key = Key.Parse("B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF");
            var compact = key.SignECDSA(Repeat(0x33));
            var der = compact.ToDer();
            Assert.Equal(0x30, der[0]);
            Assert.Equal(compact.ToBytes(), ECDSASignature.FromDer(der).ToBytes());
            Assert.False(ECDSASignature.TryFromDer(new byte[] { 0x30, 0x00 }, out _));
        }

        [Fact]
        public void Two_of_two_schnorr_multisig_combines_finalizes_and_extracts()
        {
            using var first = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
            using var second = Key.Parse("0000000000000000000000000000000000000000000000000000000000000005");
            var keys = new[] { first.PubKey, second.PubKey };
            var redeem = KaspaPskt.MultisigRedeemScript(2, keys);
            Assert.Equal((byte)OpcodeType.OP_2, redeem.ToBytes()[0]);
            Assert.Equal((byte)OpcodeType.OP_CHECKMULTISIG, redeem.ToBytes()[redeem.Length - 1]);
            var utxo = new UtxoEntry(100_000_000UL, ScriptPubKey.PayToScriptHash(redeem), 20, false);
            var left = Multisig(first, second, redeem, utxo);
            var right = Multisig(first, second, redeem, utxo);
            left.Sign(first, Aux);
            right.Sign(second, Aux);
            Assert.Single(left.Inputs[0].PartialSignatures);
            Assert.Single(right.Inputs[0].PartialSignatures);

            var combined = left.Combiner().Combine(right);
            Assert.Equal(2, combined.Inputs[0].PartialSignatures.Count);
            Assert.Equal(combined.ToJson(), KaspaPskt.FromHex(combined.ToHex()).ToJson());

            var extracted = combined.Finalizer().Finalize().Extractor().ExtractTransaction();
            Assert.Equal(combined.Global.Id, extracted.GetId());
            Assert.True(extracted.StorageMass > 0 || extracted.Outputs.Count > 0);
            var ops = extracted.Inputs[0].SignatureScript.Ops;
            Assert.Equal(3, ops.Count);
            Assert.Equal(OpcodeType.OP_DATA_65, ops[0].Code);
            Assert.Equal(OpcodeType.OP_DATA_65, ops[1].Code);
            var hash = extracted.GetSignatureHash(0, utxo, SigHashType.All).ToBytes();
            Assert.True(keys[0].Verify(new SchnorrSignature(ops[0].Data!.AsSpan(0, 64)), hash));
            Assert.True(keys[1].Verify(new SchnorrSignature(ops[1].Data!.AsSpan(0, 64)), hash));
            Assert.Equal(redeem.ToBytes(), ops[2].Data);
        }

        [Fact]
        public void Two_of_two_ecdsa_multisig_and_single_sig_p2pk_finalize()
        {
            using var first = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
            using var second = Key.Parse("0000000000000000000000000000000000000000000000000000000000000005");
            var redeem = KaspaPskt.MultisigRedeemScript(2, new[] { first.PubKeyECDSA, second.PubKeyECDSA });
            Assert.Equal((byte)OpcodeType.OP_CHECKMULTISIGECDSA, redeem.ToBytes()[redeem.Length - 1]);
            var utxo = new UtxoEntry(80_000_000UL, ScriptPubKey.PayToScriptHash(redeem), 4, false);
            var left = Multisig(first, second, redeem, utxo);
            var right = left.Clone();
            left.Sign(first, Aux);
            right.Sign(second, Aux);
            var extracted = left.Combiner().Combine(right).Finalizer().Finalize().Extractor().ExtractTransaction();
            var hash = extracted.GetEcdsaSignatureHash(0, utxo, SigHashType.All).ToBytes();
            var ops = extracted.Inputs[0].SignatureScript.Ops;
            Assert.True(first.PubKeyECDSA.Verify(new ECDSASignature(ops[0].Data!.AsSpan(0, 64)), hash));
            Assert.True(second.PubKeyECDSA.Verify(new ECDSASignature(ops[1].Data!.AsSpan(0, 64)), hash));

            var p2pk = PsktFor(first.PubKey.GetScriptPubKey(), 25_000_000);
            p2pk.Sign(first, Aux);
            var schnorrTx = p2pk.Finalizer().Finalize().Extractor().ExtractTransaction();
            Assert.True(schnorrTx.VerifyInput(0, schnorrTx.Inputs[0].PreviousOutpoint == null ? utxo : p2pk.Inputs[0].Utxo!));

            var ecdsa = PsktFor(first.PubKeyECDSA.GetScriptPubKey(), 25_000_000);
            ecdsa.Sign(first, Aux);
            var ecdsaTx = ecdsa.Finalizer().Finalize().Extractor().ExtractTransaction();
            Assert.True(ecdsaTx.VerifyInput(0, ecdsa.Inputs[0].Utxo!));
            Assert.Contains("\"eCDSA\":", ecdsa.ToJson());
        }

        [Fact]
        public void Builder_pskt_signs_version_0_and_version_1_without_broadcasting()
        {
            using var payer = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
            using var payee = Key.Parse("B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF");
            var coin = new Coin(new OutPoint(new uint256(11), 0), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 10, false));
            var version0 = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(40_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1_000_000));
            var direct0 = version0.BuildTransaction(true);
            var pskt0 = version0.Sign(version0.BuildPSKT()).Finalizer().Finalize().Extractor().ExtractTransaction();
            Assert.Equal(direct0.GetId(), pskt0.GetId());
            Assert.Equal(direct0.StorageMass, pskt0.StorageMass);
            Assert.Equal((byte)1, pskt0.Inputs[0].SigOpCount);
            Assert.True(pskt0.VerifyInput(0, coin.Utxo));

            var version1 = new TransactionBuilder()
                .SetVersion(1)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(40_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1_000_000));
            var direct1 = version1.BuildTransaction(true);
            var built = version1.BuildPSKT();
            Assert.Equal((ushort)10, built.Inputs[0].ComputeBudget);
            var pskt1 = version1.Sign(built).Finalizer().Finalize().Extractor().ExtractTransaction();
            Assert.Equal(direct1.GetId(), pskt1.GetId());
            Assert.Equal(direct1.StorageMass, pskt1.StorageMass);
            Assert.Equal((ushort)10, pskt1.Inputs[0].ComputeBudget);
            Assert.True(pskt1.VerifyInput(0, coin.Utxo));
            Assert.Equal(pskt1.GetId(), KaspaPskt.FromHex(built.ToHex()).Signer().Sign(payer, Aux).Finalizer().Finalize().Global.Id);
        }

        [Fact]
        public void Combine_merges_compatible_fields_and_rejects_conflicts()
        {
            var baseInput = SampleInput();
            var left = CombinerWith(baseInput, new PSKTOutput(10, Payee()));
            left.Inputs[0].SighashType = SigHashType.All;
            left.Inputs[0].Sequence = 3;
            left.Inputs[0].PartialSignatures.Add(PSKTSignature.Schnorr(Key.Parse("0000000000000000000000000000000000000000000000000000000000000003").PubKeyECDSA, new SchnorrSignature(Repeat(0x41, 64))));
            left.Global.FallbackLockTime = 5;
            left.Global.Proprietaries["k"] = "1";

            var rightInput = SampleInput();
            rightInput.Sequence = 9;
            rightInput.SighashType = SigHashType.None;
            rightInput.RedeemScript = new Script(new byte[] { 0x51 });
            var right = CombinerWith(rightInput, new PSKTOutput(10, Payee()));
            right.Inputs[0].PartialSignatures.Add(PSKTSignature.Schnorr(Key.Parse("0000000000000000000000000000000000000000000000000000000000000005").PubKeyECDSA, new SchnorrSignature(Repeat(0x42, 64))));
            var merged = left.Combine(right);
            Assert.Equal(2, merged.Inputs[0].PartialSignatures.Count);
            Assert.Equal((ulong)9, merged.Inputs[0].Sequence);
            Assert.Equal(SigHashType.None, merged.Inputs[0].SighashType);
            Assert.Equal("51", merged.Inputs[0].RedeemScript!.ToHex());
            Assert.Equal((ulong)5, merged.Global.FallbackLockTime);

            var otherVersion = CombinerWith(SampleInput(), new PSKTOutput(10, Payee()));
            otherVersion.Global.TxVersion = 1;
            Assert.Throws<PSKTException>(() => left.Combine(otherVersion));

            var otherLock = CombinerWith(SampleInput(), new PSKTOutput(10, Payee()));
            otherLock.Global.FallbackLockTime = 6;
            Assert.Throws<PSKTException>(() => left.Combine(otherLock));

            var otherOutpoint = SampleInput();
            otherOutpoint = new PSKTInput(new OutPoint(otherOutpoint.PreviousOutpoint.TransactionId, 1)) { Utxo = otherOutpoint.Utxo };
            Assert.Throws<PSKTException>(() => left.Combine(CombinerWith(otherOutpoint, new PSKTOutput(10, Payee()))));

            var otherUtxo = SampleInput();
            otherUtxo.Utxo = new UtxoEntry(99UL, Payee(), 1, false);
            Assert.Throws<PSKTException>(() => left.Combine(CombinerWith(otherUtxo, new PSKTOutput(10, Payee()))));

            var otherRedeem = SampleInput();
            otherRedeem.RedeemScript = new Script(new byte[] { 0x52 });
            left.Inputs[0].RedeemScript = new Script(new byte[] { 0x51 });
            Assert.Throws<PSKTException>(() => left.Combine(CombinerWith(otherRedeem, new PSKTOutput(10, Payee()))));

            Assert.Throws<PSKTException>(() => left.Combine(CombinerWith(SampleInput(), new PSKTOutput(11, Payee()))));
            Assert.Throws<PSKTException>(() => left.Combine(CombinerWith(SampleInput(), new PSKTOutput(10, ScriptPubKey.PayToPubKey(new byte[32])))));

            var otherProprietary = CombinerWith(SampleInput(), new PSKTOutput(10, Payee()));
            otherProprietary.Global.Proprietaries["k"] = "2";
            Assert.Throws<PSKTException>(() => left.Combine(otherProprietary));
        }

        [Fact]
        public void Finalize_fails_without_utxo_redeem_script_or_enough_signatures()
        {
            var missingUtxo = KaspaPskt.Create().Constructor().AddInput(new PSKTInput(new OutPoint(new uint256(1), 0))).AddOutput(new PSKTOutput(1, Payee())).Updater().Signer().Finalizer();
            Assert.Throws<PSKTException>(() => missingUtxo.Finalize());

            using var key = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
            var redeem = KaspaPskt.MultisigRedeemScript(2, new[] { key.PubKey, Key.Parse("0000000000000000000000000000000000000000000000000000000000000005").PubKey });
            var bare = PsktFor(ScriptPubKey.PayToScriptHash(redeem), 10);
            bare.Inputs[0].RedeemScript = null;
            Assert.Throws<PSKTException>(() => bare.Finalizer().Finalize());

            var oneSig = PsktFor(ScriptPubKey.PayToScriptHash(redeem), 10);
            oneSig.Inputs[0].RedeemScript = redeem;
            oneSig.Sign(key, Aux);
            Assert.Throws<PSKTException>(() => oneSig.Finalizer().Finalize());

            var p2pk = PsktFor(key.PubKey.GetScriptPubKey(), 10);
            p2pk.Sign(key, Aux);
            Assert.Throws<PSKTException>(() => p2pk.Finalizer().Finalize(_ => new Script[0]));
            Assert.Throws<PSKTException>(() => p2pk.Finalizer().Finalize(_ => new[] { Script.Empty }));
            Assert.Throws<PSKTException>(() => p2pk.Extractor());
            var finalized = p2pk.Finalizer().Finalize();
            Assert.NotNull(finalized.Global.Id);
            Assert.Equal(ulong.MaxValue, finalized.Inputs[0].Sequence);
        }

        [Fact]
        public void Xpub_paths_keep_the_longest_suffix_and_reject_a_conflict()
        {
            using var master = ExtKey.CreateFromSeed(Encoders.Hex.DecodeData("000102030405060708090a0b0c0d0e0f"));
            var xpub = master.Neuter();
            var fingerprint = new HDFingerprint(new byte[] { 1, 2, 3, 4 });
            var left = KaspaPskt.Create().Constructor().Combiner();
            left.Global.Xpubs.Add(new PSKTXpub(xpub, new PSKTKeySource(fingerprint, new KeyPath("m/44'/0/1"))));
            var right = KaspaPskt.Create().Constructor().Combiner();
            right.Global.Xpubs.Add(new PSKTXpub(xpub, new PSKTKeySource(fingerprint, new KeyPath("m/0/1"))));
            Assert.Equal("m/44'/0/1", left.Combine(right).Global.Xpubs[0].Source.Path.ToString());

            var conflict = KaspaPskt.Create().Constructor().Combiner();
            conflict.Global.Xpubs.Add(new PSKTXpub(xpub, new PSKTKeySource(fingerprint, new KeyPath("m/1/2"))));
            Assert.Throws<PSKTException>(() => left.Combine(conflict));
        }

        [Fact]
        public void Payload_and_covenant_follow_the_version_gates()
        {
            Assert.Throws<PSKTException>(() => KaspaPskt.Create().SetVersion(PSKTVersion.Zero).Constructor().SetPayload(new byte[] { 1 }));
            Assert.Throws<PSKTException>(() => KaspaPskt.Create().Constructor().SetTransactionVersion(1).AddOutput(new PSKTOutput(1, Payee()) { Covenant = new CovenantBinding(0, new uint256(1)) }));
            var allowed = KaspaPskt.Create().SetVersion(PSKTVersion.Two).Constructor().SetTransactionVersion(1).AddOutput(new PSKTOutput(1, Payee()) { Covenant = new CovenantBinding(0, new uint256(1)) });
            Assert.NotNull(KaspaPskt.FromHex(allowed.ToHex()).Outputs[0].Covenant);
        }

        private static KaspaPskt Multisig(Key first, Key second, Script redeem, UtxoEntry utxo)
        {
            var pskt = PsktFor(utxo.ScriptPublicKey, utxo.Amount.Sompi - 1_000);
            pskt.Inputs[0].Utxo = utxo;
            pskt.Inputs[0].RedeemScript = redeem;
            pskt.Inputs[0].SigOpCount = 2;
            pskt.Inputs[0].Sequence = 0;
            Assert.NotNull(first);
            Assert.NotNull(second);
            return pskt;
        }

        private static KaspaPskt PsktFor(ScriptPubKey spent, ulong amount)
        {
            var input = new PSKTInput(new OutPoint(new uint256(8), 0))
            {
                Utxo = new UtxoEntry(amount + 1_000UL, spent, 1, false),
                SigOpCount = 1,
            };
            return KaspaPskt.Create().Constructor().AddInput(input).AddOutput(new PSKTOutput(amount, Payee())).Updater().Signer();
        }

        private static KaspaPskt CombinerWith(PSKTInput input, PSKTOutput output)
        {
            return KaspaPskt.Create().Constructor().AddInput(input).AddOutput(output).Combiner();
        }

        private static PSKTInput SampleInput()
        {
            return new PSKTInput(new OutPoint(new uint256(6), 0))
            {
                Utxo = new UtxoEntry(20UL, Payee(), 1, false),
            };
        }

        private static ScriptPubKey Payee()
        {
            return ScriptPubKey.PayToPubKey(Encoders.Hex.DecodeData("0279be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798").AsSpan(1));
        }

        private static byte[] Repeat(byte value, int length = 32)
        {
            var bytes = new byte[length];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = value;
            }

            return bytes;
        }
    }
}
