using System.Text.Json;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Finalized <c>OP_ZKPRECOMPILE</c> checks, covenant script helpers, and the read-only wRPC accessor.
    /// No test opens a socket or submits a transaction.
    /// </summary>
    public class ZkCovenantTests
    {
        [Fact]
        public void Bare_precompile_is_an_empty_stack()
        {
            var engine = Run(new ScriptBuilder().AddOp(OpcodeType.OP_ZKPRECOMPILE).ToScript(), ulong.MaxValue);
            Assert.Equal(ScriptErrorCode.EmptyStack, engine.Error!.Code);
            Assert.Equal(0UL, engine.UsedScriptUnits);
        }

        [Fact]
        public void Tag_failures_are_zk_integrity_and_are_not_charged()
        {
            var missing = Run(new ScriptBuilder().AddOp(OpcodeType.OP_0).AddOp(OpcodeType.OP_ZKPRECOMPILE).ToScript(), 9999);
            Assert.Equal(ScriptErrorCode.ZkIntegrity, missing.Error!.Code);
            Assert.Equal("Tag byte is missing", missing.Error.Message);
            Assert.Equal(0UL, missing.UsedScriptUnits);

            var wide = Run(new ScriptBuilder().AddData(new byte[] { 0x20, 0x20 }).AddOp(OpcodeType.OP_ZKPRECOMPILE).ToScript(), 9999);
            Assert.Equal("Tag byte length 2 is invalid", wide.Error!.Message);

            var unknown = Run(new ScriptBuilder().AddData(new byte[] { 7 }).AddOp(OpcodeType.OP_ZKPRECOMPILE).ToScript(), 9999);
            Assert.Equal(ScriptErrorCode.ZkIntegrity, unknown.Error!.Code);
            Assert.Equal("Unknown tag: 7", unknown.Error.Message);
            Assert.Equal(0UL, unknown.UsedScriptUnits);
        }

        [Fact]
        public void Known_tag_cost_is_charged_before_the_proof_is_checked()
        {
            var engine = Run(new ScriptBuilder().AddData(new byte[] { ZkPrecompile.Groth16Tag }).AddOp(OpcodeType.OP_ZKPRECOMPILE).ToScript(), 9999);
            Assert.Equal(ScriptErrorCode.ExceededCommittedScriptUnits, engine.Error!.Code);
            Assert.Equal("used 14000000 limit 9999", engine.Error.Message);
            Assert.Equal(0UL, engine.UsedScriptUnits);
        }

        [Fact]
        public void Groth16_field_and_layout_failures_use_the_upstream_text()
        {
            var shortScript = GrothScript(new byte[10], Empty, 1, new byte[31]);
            var shortField = Run(shortScript, ulong.MaxValue);
            Assert.Equal(ScriptErrorCode.ZkIntegrity, shortField.Error!.Code);
            Assert.Contains("Invalid Fr length: expected 32 bytes, got 31", shortField.Error.Message, StringComparison.Ordinal);
            Assert.Equal(Spent(shortScript, ZkPrecompile.Groth16ScriptUnits), shortField.UsedScriptUnits);

            var modulusScript = GrothScript(new byte[10], Empty, 1, FrModulus());
            var modulus = Run(modulusScript, ulong.MaxValue);
            Assert.Contains("integer is larger than the field modulus", modulus.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(Spent(modulusScript, ZkPrecompile.Groth16ScriptUnits), modulus.UsedScriptUnits);

            var negative = Groth(new byte[10], Empty, -1);
            Assert.Contains("-1 cannot be used as an array index", negative.Error!.Message, StringComparison.Ordinal);

            var shortKey = Groth(new byte[10], Empty, 0);
            Assert.Equal("Groth16 error: Groth16 verifying key bytes are too short to read gamma_abc_g1 length", shortKey.Error!.Message);

            var emptyGamma = Groth(VerifyingKey(0, 232), Empty, 0);
            Assert.Equal("Groth16 error: Groth16 verifying key has empty gamma_abc_g1", emptyGamma.Error!.Message);

            var arityScript = GrothScript(VerifyingKey(2, 232), Empty, 0);
            var arityLimit = Spent(arityScript, ZkPrecompile.Groth16ScriptUnits);
            var arity = Run(arityScript, arityLimit);
            Assert.Equal("Groth16 error: ARK R1CS error: arity mismatch", arity.Error!.Message);
            Assert.Equal(arityLimit, arity.UsedScriptUnits);

            var gammaScript = GrothScript(VerifyingKey(1, 232), Empty, 0);
            var gammaLimit = Spent(gammaScript, ZkPrecompile.Groth16ScriptUnits);
            var gamma = Run(gammaScript, gammaLimit);
            var gammaUsed = gammaLimit + ZkPrecompile.GammaAbcElementScriptUnits;
            Assert.Equal(
                "Groth16 error: Kaspa txscript error: script units exceeded the amount committed in the input: used="
                + gammaUsed.ToString()
                + ", limit="
                + gammaLimit.ToString(),
                gamma.Error!.Message);

            var insideScript = GrothScript(VerifyingKey(2, 232), Empty, 1, new byte[32]);
            var inside = Run(insideScript, ulong.MaxValue);
            Assert.Equal("Groth16 error: ARK serialization error: verifying key ended inside gamma_abc_g1", inside.Error!.Message);
            Assert.Equal(Spent(insideScript, ZkPrecompile.Groth16ScriptUnits + (2 * ZkPrecompile.GammaAbcElementScriptUnits)), inside.UsedScriptUnits);

            var trailingKey = Groth(VerifyingKey(1, 265), Empty, 0);
            Assert.Equal("Groth16 error: Groth16 verifying key has trailing bytes", trailingKey.Error!.Message);

            var shortProof = Groth(VerifyingKey(1, 264), new byte[127], 0);
            Assert.Equal("Groth16 error: ARK serialization error: proof is shorter than the compressed Groth16 proof", shortProof.Error!.Message);

            var trailingProof = Groth(VerifyingKey(1, 264), new byte[129], 0);
            Assert.Equal("Groth16 error: Groth16 proof has trailing bytes", trailingProof.Error!.Message);
        }

        [Fact]
        public void Structurally_valid_proofs_stay_unimplemented_and_do_not_succeed()
        {
            var groth = Groth(VerifyingKey(1, 264), new byte[ZkPrecompile.Groth16ProofBytes], 0);
            Assert.Equal(ScriptErrorCode.Unimplemented, groth.Error!.Code);
            Assert.Equal("OP_ZKPRECOMPILE Groth16 pairing is not implemented", groth.Error.Message);

            var risc = R0(1, new byte[32], Empty, new byte[4]);
            Assert.Equal(ScriptErrorCode.Unimplemented, risc.Error!.Code);
            Assert.Equal("OP_ZKPRECOMPILE RISC0 succinct receipt is not implemented", risc.Error.Message);

            var blake = R0(0, new byte[32], Empty, new byte[4]);
            Assert.Equal("R0 error: Unsupported hash function: Blake2b", blake.Error!.Message);
            var sha = R0(2, new byte[32], Empty, new byte[4]);
            Assert.Equal("R0 error: Unsupported hash function: Sha256", sha.Error!.Message);
            var deep = R0(1, new byte[32], new byte[32 * 9], new byte[4]);
            Assert.Equal("R0 error: Control inclusion proof length 9 exceeds maximum 8", deep.Error!.Message);

            var bare = Run(new ScriptBuilder().AddData(new byte[] { ZkPrecompile.R0SuccinctTag }).AddOp(OpcodeType.OP_ZKPRECOMPILE).ToScript(), ulong.MaxValue);
            Assert.Equal("R0 error: Txscript error: opcode requires at least 8 but stack has only 0", bare.Error!.Message);
        }

        [Fact]
        public void Zk_script_unit_upper_bound_uses_the_tag_and_the_redeem_script()
        {
            Assert.Equal(ZkPrecompile.Groth16ScriptUnits, TxScriptEngine.GetZkScriptUnitsUpperBound(Empty, new byte[] { 0x01, 0x20, 0xa6 }));
            Assert.Equal(ZkPrecompile.R0SuccinctScriptUnits, TxScriptEngine.GetZkScriptUnitsUpperBound(Empty, new byte[] { 0x01, 0x21, 0xa6 }));
            Assert.Equal(ZkPrecompile.R0SuccinctScriptUnits, TxScriptEngine.GetZkScriptUnitsUpperBound(Empty, new byte[] { 0x01, 0xff, 0xa6 }));
            Assert.Equal(ZkPrecompile.R0SuccinctScriptUnits, TxScriptEngine.GetZkScriptUnitsUpperBound(Empty, new byte[] { 0xa6 }));
            Assert.Equal(ZkPrecompile.R0SuccinctScriptUnits, TxScriptEngine.GetZkScriptUnitsUpperBound(Empty, new byte[] { 0x00, 0xa6 }));
            Assert.Equal(
                ZkPrecompile.Groth16ScriptUnits + ZkPrecompile.R0SuccinctScriptUnits,
                TxScriptEngine.GetZkScriptUnitsUpperBound(Empty, new byte[] { 0x01, 0x20, 0xa6, 0x01, 0x21, 0xa6 }));
            Assert.Equal(ZkPrecompile.Groth16ScriptUnits, TxScriptEngine.GetZkScriptUnitsUpperBound(Empty, new byte[] { 0x01, 0x20, 0xa6, 0x05 }));

            var redeem = new Script(new byte[] { 0x01, 0x20, 0xa6 });
            var p2sh = Script.PayToScriptHash(redeem).ToBytes();
            var signature = new ScriptBuilder().AddData(redeem.ToBytes()).ToBytes();
            Assert.Equal(ZkPrecompile.Groth16ScriptUnits, TxScriptEngine.GetZkScriptUnitsUpperBound(signature, p2sh));
            Assert.Equal(0UL, TxScriptEngine.GetZkScriptUnitsUpperBound(Empty, p2sh));
            Assert.Equal(0UL, TxScriptEngine.GetZkScriptUnitsUpperBound(new byte[] { 0x02, 0x01 }, p2sh));
        }

        [Fact]
        public void Covenant_helpers_reject_other_opcodes_and_the_examples_execute()
        {
            Assert.Throws<ArgumentException>(() => new ScriptBuilder().AddCovenantOp(OpcodeType.OP_DUP));
            Assert.Equal(new byte[] { (byte)OpcodeType.OP_TXVERSION }, new ScriptBuilder().AddCovenantOp(OpcodeType.OP_TXVERSION).ToBytes());
            Assert.Equal(new byte[] { (byte)OpcodeType.OP_CHAINBLOCKSEQCOMMIT }, new ScriptBuilder().ChainBlockSeqCommit().ToBytes());

            var preserved = CovenantScripts.VersionOnePreservesInputAmount();
            var match = RunScript(preserved, AmountTx(1000, 1000), 0);
            Assert.Null(match);
            Assert.Equal(ScriptErrorCode.EvalFalse, RunScript(preserved, AmountTx(1000, 999), 0)!.Code);

            var id = new uint256(9);
            var bound = CovenantScripts.InputCovenantEquals(0, id);
            Assert.Null(RunScript(bound, AmountTx(1, 1), 0, id));
            Assert.Equal(ScriptErrorCode.EvalFalse, RunScript(bound, AmountTx(1, 1), 0, new uint256(8))!.Code);

            var spending = CovenantScripts.SpendingInputIs(0);
            var two = Sample(2);
            Assert.Null(RunScript(spending, two, 0));
            Assert.Equal(ScriptErrorCode.EvalFalse, RunScript(spending, two, 1)!.Code);
        }

        [Fact]
        public void Wrpc_accessor_reads_blocks_and_does_not_submit()
        {
            var target = new uint256(11);
            var sink = new uint256(22);
            var root = new uint256(33);
            var methods = new List<string>();
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [target.ToString()] = Block(target, 100000, root, true),
                [sink.ToString()] = Block(sink, 500000, new uint256(44), true),
            };
            var accessor = Accessor(methods, blocks, sink);

            Assert.True(accessor.IsChainAncestor(target));
            Assert.Equal(new[] { "getBlock" }, methods);
            Assert.Equal(root, accessor.SeqCommitmentWithinDepth(target));
            Assert.DoesNotContain("submitTransaction", methods);
            Assert.All(methods, method => Assert.True(method == "getBlock" || method == "getBlockDagInfo"));

            methods.Clear();
            blocks[target.ToString()] = Block(target, 1, root, true);
            accessor = Accessor(methods, blocks, sink);
            Assert.Null(accessor.SeqCommitmentWithinDepth(target));

            blocks[target.ToString()] = Block(target, 68001, root, true);
            accessor = Accessor(methods, blocks, sink);
            Assert.Equal(root, accessor.SeqCommitmentWithinDepth(target));
            blocks[target.ToString()] = Block(target, 68000, root, true);
            accessor = Accessor(methods, blocks, sink);
            Assert.Null(accessor.SeqCommitmentWithinDepth(target));

            blocks[target.ToString()] = Block(target, 100000, null, true);
            accessor = Accessor(methods, blocks, sink);
            Assert.Null(accessor.SeqCommitmentWithinDepth(target));

            blocks[target.ToString()] = Block(target, 100000, root, false);
            accessor = Accessor(methods, blocks, sink);
            Assert.False(accessor.IsChainAncestor(target));

            blocks.Remove(target.ToString());
            accessor = Accessor(methods, blocks, sink, missingIsError: true);
            Assert.Null(accessor.IsChainAncestor(target));
            Assert.DoesNotContain("submitTransaction", methods);
        }

        private static TxScriptEngine Run(Script script, ulong limit)
        {
            var tx = new Transaction(
                0,
                new[] { new TxIn(new OutPoint(uint256.Zero, 0), Script.Empty, 1, 0) },
                new[] { new TxOut(0UL, new ScriptPubKey(0, Script.Empty)) },
                0,
                SubnetworkId.Native,
                0,
                Empty);
            return TxScriptEngine.Run(Empty, new ScriptPubKey(0, script), tx, 0, new UtxoEntry?[] { new UtxoEntry(0UL, new ScriptPubKey(0, script), 0, false) }, limit);
        }

        private static TxScriptEngine Groth(byte[] verifyingKey, byte[] proof, int count, params byte[][] fields)
        {
            return Run(GrothScript(verifyingKey, proof, count, fields), ulong.MaxValue);
        }

        private static Script GrothScript(byte[] verifyingKey, byte[] proof, int count, params byte[][] fields)
        {
            var builder = new ScriptBuilder();
            for (var i = 0; i < fields.Length; i++)
            {
                builder.AddData(fields[i]);
            }

            return builder.AddInt64(count).AddData(proof).AddData(verifyingKey).AddData(new byte[] { ZkPrecompile.Groth16Tag }).AddOp(OpcodeType.OP_ZKPRECOMPILE).ToScript();
        }

        private static ulong Spent(Script script, ulong opcodeUnits)
        {
            var length = script.ToBytes().Length;
            var extra = length > ScriptLimits.StandardScriptPubKeyMaxSize
                ? (ulong)(length - ScriptLimits.StandardScriptPubKeyMaxSize) * ScriptLimits.ScriptUnitsPerGram
                : 0UL;
            return extra + opcodeUnits;
        }

        private static TxScriptEngine R0(byte hashFn, byte[] controlId, byte[] digests, byte[] index)
        {
            var digest = new byte[32];
            var builder = new ScriptBuilder()
                .AddData(digest)
                .AddData(index)
                .AddData(digests)
                .AddData(Empty)
                .AddData(digest)
                .AddData(digest)
                .AddData(controlId)
                .AddData(new byte[] { hashFn })
                .AddData(new byte[] { ZkPrecompile.R0SuccinctTag })
                .AddOp(OpcodeType.OP_ZKPRECOMPILE);
            return Run(builder.ToScript(), ulong.MaxValue);
        }

        private static byte[] VerifyingKey(ulong gammaCount, int totalLength)
        {
            var bytes = new byte[totalLength];
            if (totalLength >= 232)
            {
                for (var i = 0; i < 8; i++)
                {
                    bytes[224 + i] = (byte)(gammaCount >> (8 * i));
                }
            }

            return bytes;
        }

        private static byte[] FrModulus()
        {
            var bytes = new byte[32];
            Write(bytes, 0, 0x43e1f593f0000001UL);
            Write(bytes, 8, 0x2833e84879a97091UL);
            Write(bytes, 16, 0xb85045b68181585dUL);
            Write(bytes, 24, 0x30644e72e131a029UL);
            return bytes;
        }

        private static void Write(byte[] bytes, int offset, ulong value)
        {
            for (var i = 0; i < 8; i++)
            {
                bytes[offset + i] = (byte)(value >> (8 * i));
            }
        }

        private static ScriptError? RunScript(Script locking, Transaction tx, int input, uint256? covenant = null)
        {
            var utxos = new UtxoEntry?[tx.Inputs.Count];
            for (var i = 0; i < utxos.Length; i++)
            {
                utxos[i] = new UtxoEntry(tx.Inputs.Count == 0 ? 0 : 1000UL, new ScriptPubKey(0, Script.Empty), 0, false, i == input ? covenant : null);
            }

            if (tx.Inputs.Count > 0)
            {
                utxos[0] = new UtxoEntry(1000UL, new ScriptPubKey(0, Script.Empty), 0, false, covenant);
            }

            return TxScriptEngine.Run(Empty, new ScriptPubKey(0, locking), tx, input, utxos, ulong.MaxValue).Error;
        }

        private static Transaction AmountTx(ulong inputAmount, ulong outputAmount)
        {
            var tx = new Transaction(
                1,
                new[] { new TxIn(new OutPoint(uint256.Zero, 0), Script.Empty, 1, 0) },
                new[] { new TxOut(outputAmount, new ScriptPubKey(0, Script.Empty)) },
                0,
                SubnetworkId.Native,
                0,
                Empty);
            return tx;
        }

        private static Transaction Sample(int inputs)
        {
            var txins = new TxIn[inputs];
            for (var i = 0; i < inputs; i++)
            {
                txins[i] = new TxIn(new OutPoint(uint256.Zero, (uint)i), Script.Empty, 1, 0);
            }

            return new Transaction(1, txins, new[] { new TxOut(1UL, new ScriptPubKey(0, Script.Empty)) }, 0, SubnetworkId.Native, 0, Empty);
        }

        private static WrpcSeqCommitAccessor Accessor(List<string> methods, Dictionary<string, string> blocks, uint256 sink, bool missingIsError = false)
        {
            var channel = new ScriptedChannel();
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                var method = doc.RootElement.GetProperty("method").GetString() ?? string.Empty;
                methods.Add(method);
                if (method == "getBlockDagInfo")
                {
                    return "{\"id\":" + id + ",\"method\":\"getBlockDagInfo\",\"params\":{\"network\":\"mainnet\",\"blockCount\":1,\"headerCount\":1,\"tipHashes\":[\"" + sink + "\"],\"difficulty\":1,\"pastMedianTime\":1,\"virtualParentHashes\":[\"" + sink + "\"],\"pruningPointHash\":\"" + sink + "\",\"virtualDaaScore\":1,\"sink\":\"" + sink + "\"}}";
                }

                var hash = doc.RootElement.GetProperty("params").GetProperty("hash").GetString() ?? string.Empty;
                Assert.False(doc.RootElement.GetProperty("params").GetProperty("includeTransactions").GetBoolean());
                if (!blocks.TryGetValue(hash, out var block))
                {
                    if (missingIsError)
                    {
                        return "{\"id\":" + id + ",\"method\":\"getBlock\",\"error\":{\"code\":1,\"message\":\"missing\"}}";
                    }

                    return "{\"id\":" + id + ",\"method\":\"getBlock\",\"params\":{}}";
                }

                return "{\"id\":" + id + ",\"method\":\"getBlock\",\"params\":{\"block\":" + block + "}}";
            };
            return new WrpcSeqCommitAccessor(new KaspaWrpcJsonClient(channel));
        }

        private static string Block(uint256 hash, ulong score, uint256? root, bool chain)
        {
            var rootText = root == null ? string.Empty : root.ToString();
            return "{\"header\":{\"blueScore\":" + score.ToString() + ",\"acceptedIdMerkleRoot\":\"" + rootText + "\"},\"verboseData\":{\"isChainBlock\":" + (chain ? "true" : "false") + "},\"hash\":\"" + hash + "\"}";
        }

        private static readonly byte[] Empty = Array.Empty<byte>();

        private sealed class ScriptedChannel : IKaspaJsonChannel
        {
            public Func<string, string> Handler { get; set; } = _ => throw new InvalidOperationException("No handler.");

            public Task<string> ExchangeAsync(string requestJson, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Handler(requestJson));
            }
        }
    }
}
