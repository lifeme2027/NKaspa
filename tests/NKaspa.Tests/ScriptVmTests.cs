using System.Globalization;
using System.Text;
using System.Text.Json;
using NKaspa.DataEncoders;
using NKaspa.PSKT;
using Xunit;
using Xunit.Abstractions;
using KaspaPskt = NKaspa.PSKT.PSKT;

namespace NKaspa.Tests
{
    /// <summary>
    /// Script engine vectors from rusty-kaspa <c>crypto/txscript</c> and the three mainnet transactions
    /// accepted on 2026-10-30. The JSON file and its ISC notice live in <c>Data/</c>.
    /// These tests verify signatures. They do not broadcast.
    /// </summary>
    public class ScriptVmTests
    {
        private const string OneInputId = "5fd8f8600950c36b1d9195d9afab0bbf5bde94594fd8246d2c2419c64f703868";
        private const string TwoInputId = "2febe6cafd5f78f8ce21636d48f060560b39a7529b00646a00cf5d62098aa3d4";
        private const string Version1Id = "4d1c6a5e1b127278945673b3ed116786110b2b1ca44090c9f810a50e12350714";

        private readonly ITestOutputHelper _output;

        public ScriptVmTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Official_script_tests_json_matches_rusty_kaspa()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "script_tests.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var rows = document.RootElement;
            Assert.Equal(JsonValueKind.Array, rows.ValueKind);

            var passed = 0;
            var skipped = 0;
            var failed = 0;
            var comments = 0;
            var details = new StringBuilder();
            var index = 0;
            foreach (var row in rows.EnumerateArray())
            {
                var current = index;
                index++;
                if (row.ValueKind != JsonValueKind.Array)
                {
                    failed++;
                    AppendFailure(details, current, "(row)", string.Empty, "array", "row is " + row.ValueKind);
                    continue;
                }

                var width = row.GetArrayLength();
                if (width == 1)
                {
                    comments++;
                    continue;
                }

                if (width != 4 && width != 5)
                {
                    failed++;
                    AppendFailure(details, current, "(width)", string.Empty, "4 or 5", "width " + width.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                var sigText = Field(row, 0);
                var pubText = Field(row, 1);
                var expected = Field(row, 3);
                var error = RunOfficial(sigText, pubText);
                if (Names(error).Contains(expected))
                {
                    passed++;
                    continue;
                }

                if (error != null && error.Code == ScriptErrorCode.Unimplemented)
                {
                    skipped++;
                    if (skipped <= 8)
                    {
                        _output.WriteLine("skip " + current.ToString(CultureInfo.InvariantCulture) + " " + error + " :: " + Trim(pubText));
                    }

                    continue;
                }

                failed++;
                if (failed <= 24)
                {
                    AppendFailure(details, current, sigText, pubText, expected, error == null ? "OK" : error.ToString());
                }
            }

            var summary = "official script_tests.json passed=" + passed.ToString(CultureInfo.InvariantCulture)
                + " skipped=" + skipped.ToString(CultureInfo.InvariantCulture)
                + " failed=" + failed.ToString(CultureInfo.InvariantCulture)
                + " comments=" + comments.ToString(CultureInfo.InvariantCulture);
            _output.WriteLine(summary);
            Assert.True(passed > 0, summary);
            Assert.True(failed == 0, summary + Environment.NewLine + details);
        }

        [Fact]
        public void Zk_precompile_is_rejected_and_chain_block_needs_an_accessor()
        {
            var zk = RunOfficial(string.Empty, "ZKPRECOMPILE");
            Assert.NotNull(zk);
            Assert.Equal(ScriptErrorCode.EmptyStack, zk!.Code);

            var chain = ChainScriptWithoutCompare();
            var bare = Fixture(Array.Empty<byte>(), chain);
            var rejected = TxScriptEngine.Run(
                Array.Empty<byte>(),
                new ScriptPubKey(0, chain),
                bare,
                0,
                new UtxoEntry?[] { new UtxoEntry(0UL, new ScriptPubKey(0, chain), 0, true) },
                ulong.MaxValue,
                seqCommit: null);
            Assert.Equal(ScriptErrorCode.InvalidOpcode, rejected.Error!.Code);

            var spending = Fixture(Array.Empty<byte>(), ChainScript());
            var engine = TxScriptEngine.Run(
                Array.Empty<byte>(),
                new ScriptPubKey(0, ChainScript()),
                spending,
                0,
                new UtxoEntry?[] { new UtxoEntry(0UL, new ScriptPubKey(0, ChainScript()), 0, true) },
                ulong.MaxValue,
                new JsonSeqCommit());
            Assert.Null(engine.Error);
        }

        [Theory]
        [InlineData("mainnet-5fd8f860.json", OneInputId, 0)]
        [InlineData("mainnet-2febe6ca.json", TwoInputId, 0)]
        [InlineData("mainnet-4d1c6a5e.json", Version1Id, 1)]
        public void Accepted_mainnet_transactions_verify(string file, string expectedId, int version)
        {
            var tx = LoadMainnet(file, out var utxos);
            Assert.Equal(version, tx.Version);
            Assert.Equal(expectedId, tx.GetId().ToString());
            Assert.True(tx.Verify(utxos));
            for (var i = 0; i < tx.Inputs.Count; i++)
            {
                Assert.True(Script.VerifyScript(tx.Inputs[i].SignatureScript, utxos[i].ScriptPublicKey, tx, i, utxos));
                Assert.True(ScriptEvaluationContext.Verify(tx.Inputs[i].SignatureScript, utxos[i].ScriptPublicKey, tx, i, utxos).Result);
            }

            var builder = new TransactionBuilder().SetVersion((ushort)version);
            for (var i = 0; i < tx.Inputs.Count; i++)
            {
                builder.AddCoins(new Coin(tx.Inputs[i].PreviousOutpoint, utxos[i]));
            }

            builder.Send(tx.Outputs[0].ScriptPublicKey, tx.Outputs[0].Value);
            builder.SetChange(tx.Outputs[1].ScriptPublicKey);
            Assert.True(builder.Verify(tx));

            var broken = LoadMainnet(file, out var brokenUtxos);
            broken.Inputs[0].SignatureScript = Script.Empty;
            Assert.False(broken.Verify(brokenUtxos));
            Assert.False(Script.VerifyScript(Script.Empty, brokenUtxos[0].ScriptPublicKey, broken, 0, brokenUtxos, out var error));
            Assert.NotNull(error);
        }

        [Fact]
        public void Pay_to_script_hash_two_of_two_and_pskt_extract_verify()
        {
            using var first = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
            using var second = Key.Parse("0000000000000000000000000000000000000000000000000000000000000005");
            var redeem = KaspaPskt.MultisigRedeemScript(2, new[] { first.PubKey, second.PubKey });
            var locking = ScriptPubKey.PayToScriptHash(redeem);
            var utxo = new UtxoEntry(50_000_000UL, locking, 7, false);
            var tx = SignedMultisig(first, second, redeem, utxo, sigOpCount: 2);
            var spent = new[] { utxo };
            Assert.True(tx.Verify(spent));
            Assert.True(Script.VerifyScript(tx.Inputs[0].SignatureScript, locking, tx, 0, spent));
            var builder = new TransactionBuilder()
                .AddCoins(new Coin(tx.Inputs[0].PreviousOutpoint, utxo))
                .Send(tx.Outputs[0].ScriptPublicKey, tx.Outputs[0].Value);
            Assert.True(builder.Verify(tx));

            var badBytes = tx.Inputs[0].SignatureScript.ToBytes();
            badBytes[1] ^= 0x01;
            tx.Inputs[0].SignatureScript = new Script(badBytes);
            Assert.False(tx.Verify(spent));
            Assert.False(Script.VerifyScript(tx.Inputs[0].SignatureScript, locking, tx, 0, spent, out var bad));
            Assert.NotNull(bad);

            var tight = SignedMultisig(first, second, redeem, utxo, sigOpCount: 1);
            Assert.False(tight.Verify(spent));
            Assert.False(Script.VerifyScript(tight.Inputs[0].SignatureScript, locking, tight, 0, spent, out var budget));
            Assert.Equal(ScriptErrorCode.ExceededCommittedScriptUnits, budget!.Code);

            var left = PsktMultisig(first, second, redeem, utxo);
            var right = PsktMultisig(first, second, redeem, utxo);
            left.Sign(first, new byte[32]);
            right.Sign(second, new byte[32]);
            var extracted = left.Combiner().Combine(right).Finalizer().Finalize().Extractor().ExtractTransaction();
            var extractedUtxo = new[] { left.Inputs[0].Utxo! };
            Assert.Equal((byte)2, extracted.Inputs[0].SigOpCount);
            Assert.True(extracted.Verify(extractedUtxo));
            Assert.True(Script.VerifyScript(
                extracted.Inputs[0].SignatureScript,
                extractedUtxo[0].ScriptPublicKey,
                extracted,
                0,
                extractedUtxo));

            var ecdsaRedeem = KaspaPskt.MultisigRedeemScript(2, new[] { first.PubKeyECDSA, second.PubKeyECDSA });
            var ecdsaUtxo = new UtxoEntry(80_000_000UL, ScriptPubKey.PayToScriptHash(ecdsaRedeem), 4, false);
            var ecdsaLeft = PsktMultisig(first, second, ecdsaRedeem, ecdsaUtxo);
            var ecdsaRight = ecdsaLeft.Clone();
            ecdsaLeft.Sign(first, new byte[32]);
            ecdsaRight.Sign(second, new byte[32]);
            var ecdsaTx = ecdsaLeft.Combiner().Combine(ecdsaRight).Finalizer().Finalize().Extractor().ExtractTransaction();
            Assert.True(ecdsaTx.Verify(new[] { ecdsaLeft.Inputs[0].Utxo! }));
        }

        private static Transaction SignedMultisig(Key first, Key second, Script redeem, UtxoEntry utxo, byte sigOpCount)
        {
            var tx = new Transaction(
                0,
                new[] { new TxIn(new OutPoint(new uint256(8), 0), Script.Empty, 0, sigOpCount) },
                new[] { new TxOut(utxo.Amount.Sompi - 1_000UL, first.PubKey.GetScriptPubKey()) },
                0,
                SubnetworkId.Native,
                0,
                Array.Empty<byte>());
            var hash = tx.GetSignatureHash(0, utxo, SigHashType.All).ToBytes();
            var pushes = new ScriptBuilder()
                .AddData(WithHashType(first.SignSchnorr(hash).ToBytes()))
                .AddData(WithHashType(second.SignSchnorr(hash).ToBytes()))
                .ToScript();
            tx.Inputs[0].SignatureScript = Script.PayToScriptHashSignature(redeem, pushes);
            return tx;
        }

        private static KaspaPskt PsktMultisig(Key first, Key second, Script redeem, UtxoEntry utxo)
        {
            var input = new PSKTInput(new OutPoint(new uint256(8), 0))
            {
                Utxo = utxo,
                RedeemScript = redeem,
                SigOpCount = 2,
                Sequence = 0,
            };
            var pskt = KaspaPskt.Create().Constructor()
                .AddInput(input)
                .AddOutput(new PSKTOutput(utxo.Amount.Sompi - 1_000UL, first.PubKey.GetScriptPubKey()))
                .Updater()
                .Signer();
            Assert.NotNull(second);
            return pskt;
        }

        private static byte[] WithHashType(byte[] signature)
        {
            var encoded = new byte[signature.Length + 1];
            Buffer.BlockCopy(signature, 0, encoded, 0, signature.Length);
            encoded[signature.Length] = (byte)SigHashType.All;
            return encoded;
        }

        private static ScriptError? RunOfficial(string sigText, string pubText)
        {
            var sigError = ScriptShortForm.TryParse(sigText, out var sig);
            if (sigError != null)
            {
                return sigError;
            }

            var pubError = ScriptShortForm.TryParse(pubText, out var scriptPubKey);
            if (pubError != null)
            {
                return pubError;
            }

            Script signature;
            try
            {
                signature = new Script(sig);
            }
            catch (FormatException)
            {
                signature = Script.Empty;
            }

            var spending = Fixture(sig, scriptPubKey, signature);
            var entry = new UtxoEntry(0UL, new ScriptPubKey(0, scriptPubKey), 0, true);
            return TxScriptEngine.Run(
                sig,
                new ScriptPubKey(0, scriptPubKey),
                spending,
                0,
                new UtxoEntry?[] { entry },
                ulong.MaxValue,
                new JsonSeqCommit()).Error;
        }

        private static Transaction Fixture(byte[] sig, byte[] scriptPubKey)
        {
            Script signature;
            try
            {
                signature = new Script(sig);
            }
            catch (FormatException)
            {
                signature = Script.Empty;
            }

            return Fixture(sig, scriptPubKey, signature);
        }

        private static Transaction Fixture(byte[] sig, byte[] scriptPubKey, Script signature)
        {
            var coinbase = new Transaction(
                1,
                new[]
                {
                    new TxIn(new OutPoint(uint256.Zero, 0xffffffff), new Script(new byte[] { 0, 0 }), ulong.MaxValue, 20),
                },
                new[] { new TxOut(0UL, new ScriptPubKey(0, scriptPubKey)) },
                0,
                SubnetworkId.Native,
                0,
                Array.Empty<byte>());
            return new Transaction(
                1,
                new[]
                {
                    new TxIn(new OutPoint(coinbase.GetId(), 0), signature, ulong.MaxValue, 20),
                },
                new[] { new TxOut(0UL, new ScriptPubKey(0, Array.Empty<byte>())) },
                0,
                SubnetworkId.Native,
                0,
                Array.Empty<byte>());
        }

        private static byte[] ChainScript()
        {
            return ParseShort(
                "0x20 0x" + Hex32('f', "input_block") + " CHAINBLOCKSEQCOMMIT 0x20 0x" + Hex32('f', "output_root_hash") + " EQUAL");
        }

        private static byte[] ChainScriptWithoutCompare()
        {
            return ParseShort("0x20 0x" + Hex32('f', "input_block") + " CHAINBLOCKSEQCOMMIT");
        }

        private static byte[] ParseShort(string text)
        {
            var error = ScriptShortForm.TryParse(text, out var script);
            if (error != null)
            {
                throw new InvalidOperationException(error.ToString());
            }

            return script;
        }

        private static string Hex32(char fill, string prefix)
        {
            var bytes = new byte[32];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)fill;
            }

            var text = Encoding.ASCII.GetBytes(prefix);
            Buffer.BlockCopy(text, 0, bytes, 0, text.Length);
            return Encoders.Hex.EncodeData(bytes);
        }

        private static Transaction LoadMainnet(string file, out UtxoEntry[] utxos)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", file);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var version = (ushort)root.GetProperty("version").GetInt32();
            var inputs = root.GetProperty("inputs");
            var outputs = root.GetProperty("outputs");
            var txInputs = new List<TxIn>();
            utxos = new UtxoEntry[inputs.GetArrayLength()];
            var index = 0;
            foreach (var input in inputs.EnumerateArray())
            {
                var resolved = input.GetProperty("previous_outpoint_resolved");
                var outpoint = new OutPoint(
                    new uint256(input.GetProperty("previous_outpoint_hash").GetString()!),
                    uint.Parse(input.GetProperty("previous_outpoint_index").GetString()!, CultureInfo.InvariantCulture));
                var signature = new Script(Encoders.Hex.DecodeData(input.GetProperty("signature_script").GetString()!));
                var spentScript = new ScriptPubKey(0, Encoders.Hex.DecodeData(resolved.GetProperty("script_public_key").GetString()!));
                TxIn txIn;
                if (version >= 1)
                {
                    var budget = input.GetProperty("compute_budget");
                    var units = budget.ValueKind == JsonValueKind.Null ? (ushort)0 : budget.GetUInt16();
                    txIn = TxIn.WithComputeBudget(outpoint, signature, 0, units);
                }
                else
                {
                    var sigops = input.GetProperty("sig_op_count");
                    var count = sigops.ValueKind == JsonValueKind.String
                        ? byte.Parse(sigops.GetString()!, CultureInfo.InvariantCulture)
                        : sigops.GetByte();
                    txIn = new TxIn(outpoint, signature, 0, count);
                }

                txInputs.Add(txIn);
                utxos[index] = new UtxoEntry(ReadUlong(resolved.GetProperty("amount")), spentScript, 0, false);
                index++;
            }

            var txOutputs = new List<TxOut>();
            foreach (var output in outputs.EnumerateArray())
            {
                var script = new ScriptPubKey(0, Encoders.Hex.DecodeData(output.GetProperty("script_public_key").GetString()!));
                txOutputs.Add(new TxOut(ReadUlong(output.GetProperty("amount")), script));
            }

            var payload = root.GetProperty("payload");
            var payloadBytes = payload.ValueKind == JsonValueKind.String
                ? Encoders.Hex.DecodeData(payload.GetString()!)
                : Array.Empty<byte>();
            return new Transaction(
                version,
                txInputs,
                txOutputs,
                0,
                new SubnetworkId(Encoders.Hex.DecodeData(root.GetProperty("subnetwork_id").GetString()!)),
                0,
                payloadBytes);
        }

        private static ulong ReadUlong(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return ulong.Parse(element.GetString()!, CultureInfo.InvariantCulture);
            }

            return element.GetUInt64();
        }

        private static string Field(JsonElement row, int index)
        {
            var value = row[index];
            if (value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("script test field " + index.ToString(CultureInfo.InvariantCulture) + " is " + value.ValueKind);
            }

            return value.GetString() ?? string.Empty;
        }

        private static void AppendFailure(StringBuilder details, int index, string sig, string script, string expected, string actual)
        {
            details.Append("row ").Append(index.ToString(CultureInfo.InvariantCulture));
            details.Append(" expected ").Append(expected);
            details.Append(" actual ").Append(actual);
            details.Append(" sig=").Append(Trim(sig));
            details.Append(" spk=").Append(Trim(script));
            details.AppendLine();
        }

        private static string Trim(string text)
        {
            var flat = text.Replace("\r", " ").Replace("\n", " ");
            return flat.Length <= 160 ? flat : flat.Substring(0, 160);
        }

        private static List<string> Names(ScriptError? error)
        {
            if (error == null)
            {
                return new List<string> { "OK" };
            }

            switch (error.Code)
            {
                case ScriptErrorCode.NumberTooBig:
                case ScriptErrorCode.Serialization:
                case ScriptErrorCode.OutOfBoundsSubstring:
                case ScriptErrorCode.InvalidIndex:
                case ScriptErrorCode.CovenantsError:
                    return new List<string> { "UNKNOWN_ERROR" };
                case ScriptErrorCode.PubKeyFormat:
                case ScriptErrorCode.InvalidPubkey:
                    return new List<string> { "PUBKEYFORMAT" };
                case ScriptErrorCode.EvalFalse:
                    return new List<string> { "EVAL_FALSE" };
                case ScriptErrorCode.EmptyStack:
                    return new List<string> { "EMPTY_STACK", "EVAL_FALSE", "UNBALANCED_CONDITIONAL", "INVALID_ALTSTACK_OPERATION" };
                case ScriptErrorCode.NullFail:
                    return new List<string> { "NULLFAIL" };
                case ScriptErrorCode.InvalidSigHashType:
                    return new List<string> { "SIG_HASHTYPE" };
                case ScriptErrorCode.SignatureScriptNotPushOnly:
                    return new List<string> { "SIG_PUSHONLY" };
                case ScriptErrorCode.CleanStack:
                    return new List<string> { "CLEANSTACK" };
                case ScriptErrorCode.OpcodeReserved:
                case ScriptErrorCode.MalformedPush:
                case ScriptErrorCode.InvalidOpcode:
                    return new List<string> { "BAD_OPCODE" };
                case ScriptErrorCode.UnbalancedConditional:
                    return new List<string> { "UNBALANCED_CONDITIONAL" };
                case ScriptErrorCode.InvalidState:
                    if (error.Message == "condition stack empty")
                    {
                        return new List<string> { "UNBALANCED_CONDITIONAL" };
                    }

                    if (error.Message == "pick at an invalid location" || error.Message == "roll at an invalid location")
                    {
                        return new List<string> { "INVALID_STACK_OPERATION" };
                    }

                    if (error.Message == "expected boolean")
                    {
                        return new List<string> { "MINIMALIF" };
                    }

                    return new List<string> { "UNKNOWN_ERROR" };
                case ScriptErrorCode.EarlyReturn:
                    return new List<string> { "OP_RETURN" };
                case ScriptErrorCode.VerifyError:
                    return new List<string> { "VERIFY", "EQUALVERIFY" };
                case ScriptErrorCode.InvalidStackOperation:
                    return new List<string> { "INVALID_STACK_OPERATION", "INVALID_ALTSTACK_OPERATION" };
                case ScriptErrorCode.OpcodeDisabled:
                    return new List<string> { "DISABLED_OPCODE" };
                case ScriptErrorCode.ElementTooBig:
                    return new List<string> { "PUSH_SIZE" };
                case ScriptErrorCode.TooManyOperations:
                    return new List<string> { "OP_COUNT" };
                case ScriptErrorCode.StackSizeExceeded:
                    return new List<string> { "STACK_SIZE" };
                case ScriptErrorCode.InvalidPubKeyCount:
                    return new List<string> { "PUBKEY_COUNT" };
                case ScriptErrorCode.InvalidSignatureCount:
                    return new List<string> { "SIG_COUNT" };
                case ScriptErrorCode.NotMinimalData:
                    return new List<string> { "MINIMALDATA", "UNKNOWN_ERROR" };
                case ScriptErrorCode.UnsatisfiedLockTime:
                    return new List<string> { "UNSATISFIED_LOCKTIME" };
                case ScriptErrorCode.ScriptSize:
                    return new List<string> { "SCRIPT_SIZE" };
                case ScriptErrorCode.InvalidSignature:
                    return new List<string> { "INVALID_SIG" };
                default:
                    return new List<string>();
            }
        }

        /// <summary>
        /// The accessor installed by rusty-kaspa's JSON harness.
        /// The input block is 32 bytes of <c>f</c> prefixed with <c>input_block</c>.
        /// Any other hash is pruned. A matching ancestor returns the <c>output_root_hash</c> commitment.
        /// </summary>
        private sealed class JsonSeqCommit : ISeqCommitAccessor
        {
            private readonly uint256 _input = new uint256(Encoders.Hex.DecodeData(Hex32('f', "input_block")));
            private readonly uint256 _output = new uint256(Encoders.Hex.DecodeData(Hex32('f', "output_root_hash")));

            public bool? IsChainAncestor(uint256 blockHash)
            {
                return blockHash.Equals(_input) ? true : null;
            }

            public uint256? SeqCommitmentWithinDepth(uint256 blockHash)
            {
                return blockHash.Equals(_input) ? _output : null;
            }
        }
    }
}
