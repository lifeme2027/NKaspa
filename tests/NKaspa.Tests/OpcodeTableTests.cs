using System.Text.Json;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Table-driven opcode cases from rusty-kaspa <c>crypto/txscript/src/opcodes/mod.rs</c>.
    /// Each fact is one upstream test. Stack index 0 is the bottom. These tests do not broadcast.
    /// </summary>
    public class OpcodeTableTests
    {
        private static readonly byte[] Empty = Array.Empty<byte>();
        private static readonly byte[] Sha256Empty = Hex("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        private static readonly byte[] Blake2bEmpty = Hex("0e5751c026e543b2e8ab2eb06099daa1d1e5df47778f7787faab45cdf12fe3a8");

        [Fact]
        public void Disabled_opcodes_are_rejected()
        {
            byte[] codes =
            {
                (byte)OpcodeType.OP_LEFT,
                (byte)OpcodeType.OP_RIGHT,
                (byte)OpcodeType.OP_2MUL,
                (byte)OpcodeType.OP_2DIV,
                (byte)OpcodeType.OP_LSHIFT,
                (byte)OpcodeType.OP_RSHIFT,
            };
            for (var i = 0; i < codes.Length; i++)
            {
                AssertCode(codes[i], ScriptErrorCode.OpcodeDisabled);
            }
        }

        [Fact]
        public void Reserved_opcodes_are_rejected()
        {
            byte[] codes =
            {
                (byte)OpcodeType.OP_RESERVED,
                (byte)OpcodeType.OP_VER,
                (byte)OpcodeType.OP_VERIF,
                (byte)OpcodeType.OP_VERNOTIF,
                (byte)OpcodeType.OP_RESERVED1,
                (byte)OpcodeType.OP_RESERVED2,
            };
            for (var i = 0; i < codes.Length; i++)
            {
                AssertCode(codes[i], ScriptErrorCode.OpcodeReserved);
            }
        }

        [Fact]
        public void Invalid_opcodes_are_rejected()
        {
            AssertCode((byte)OpcodeType.OP_UNKNOWN202, ScriptErrorCode.InvalidOpcode);
            for (var code = (byte)OpcodeType.OP_UNKNOWN219; code <= (byte)OpcodeType.OP_UNKNOWN249; code++)
            {
                AssertCode(code, ScriptErrorCode.InvalidOpcode);
            }

            AssertCode((byte)OpcodeType.OP_SMALLINTEGER, ScriptErrorCode.InvalidOpcode);
            AssertCode((byte)OpcodeType.OP_PUBKEYS, ScriptErrorCode.InvalidOpcode);
            AssertCode((byte)OpcodeType.OP_UNKNOWN252, ScriptErrorCode.InvalidOpcode);
            AssertCode((byte)OpcodeType.OP_PUBKEYHASH, ScriptErrorCode.InvalidOpcode);
            AssertCode((byte)OpcodeType.OP_PUBKEY, ScriptErrorCode.InvalidOpcode);
            AssertCode((byte)OpcodeType.OP_INVALIDOPCODE, ScriptErrorCode.InvalidOpcode);
        }

        [Fact]
        public void True_uses_the_rust_opcode_name()
        {
            var script = new Script(new[] { (byte)OpcodeType.OP_TRUE });
            Assert.Equal("OP_TRUE", script.ToString());
            Assert.Equal("OpTrue", script.ToRustString());
        }

        [Fact]
        public void Data_push_uses_a_0x_hex_suffix()
        {
            var script = new Script(new byte[] { 0x02, 0xab, 0xcd });
            Assert.Equal("OP_DATA_2 abcd", script.ToString());
            Assert.Equal("OpData2 0xabcd", script.ToRustString());
        }

        [Fact]
        public void Script_builder_round_trips_through_rust_names()
        {
            var script = new ScriptBuilder()
                .AddOp(OpcodeType.OP_DUP)
                .AddData(new byte[] { 0x02, 0xab, 0xcd })
                .AddOp(OpcodeType.OP_BLAKE2B)
                .AddData(new byte[] { 0x6b, 0x61, 0x73, 0x70, 0x61 })
                .AddOp(OpcodeType.OP_EQUALVERIFY)
                .AddInt64(-1)
                .AddInt64(16)
                .AddOp(OpcodeType.OP_CHECKSIG)
                .AddData(new byte[] { 0xde, 0xad, 0xbe, 0xef })
                .AddOp(OpcodeType.OP_DROP)
                .AddOp(OpcodeType.OP_VERIFY)
                .ToScript();
            const string text = "OpDup OpData3 0x02abcd OpBlake2b OpData5 0x6b61737061 OpEqualVerify Op1Negate Op16 OpCheckSig OpData4 0xdeadbeef OpDrop OpVerify";
            Assert.Equal(text, script.ToRustString());
            Assert.Null(ScriptShortForm.TryParse(text, out var parsed));
            Assert.Equal(script.ToBytes(), parsed);
            Assert.Null(ScriptShortForm.TryParse("OpTrue", out var truth));
            Assert.Equal(new byte[] { (byte)OpcodeType.OP_TRUE }, truth);
            Assert.Null(ScriptShortForm.TryParse("OpData2 0xabcd", out var data));
            Assert.Equal(new byte[] { 0x02, 0xab, 0xcd }, data);
        }

        [Fact]
        public void Push_data_places_the_payload_on_the_stack()
        {
            AssertStack((byte)OpcodeType.OP_0, Empty, new[] { Empty });
            for (var length = 1; length <= 75; length++)
            {
                var payload = new byte[length];
                for (var i = 0; i < payload.Length; i++)
                {
                    payload[i] = 0x01;
                }

                AssertStack((byte)length, payload, new[] { payload });
            }

            var wide = new byte[0x10000];
            for (var i = 0; i < wide.Length; i++)
            {
                wide[i] = 0x01;
            }

            AssertStack((byte)OpcodeType.OP_PUSHDATA4, wide, new[] { wide });
        }

        [Fact]
        public void Push_num_encodes_small_integers()
        {
            AssertStack((byte)OpcodeType.OP_1NEGATE, Empty, new[] { new byte[] { 0x81 } });
            for (var n = 1; n <= 16; n++)
            {
                AssertStack((byte)((byte)OpcodeType.OP_1 + n - 1), Empty, new[] { new byte[] { (byte)n } });
            }
        }

        [Fact]
        public void Unary_arithmetic_matches_the_opcode_table()
        {
            AssertStack((byte)OpcodeType.OP_1ADD, Empty, new[] { new byte[] { 2 } }, new byte[] { 1 });
            AssertStack((byte)OpcodeType.OP_1ADD, Empty, new[] { Empty }, new byte[] { 0x81 });
            AssertStack((byte)OpcodeType.OP_1SUB, Empty, new[] { Empty }, new byte[] { 1 });
            AssertStack((byte)OpcodeType.OP_NEGATE, Empty, new[] { new byte[] { 0x85 } }, new byte[] { 5 });
            AssertStack((byte)OpcodeType.OP_NEGATE, Empty, new[] { Empty }, Empty);
            AssertStack((byte)OpcodeType.OP_ABS, Empty, new[] { new byte[] { 5 } }, new byte[] { 0x85 });
            AssertStack((byte)OpcodeType.OP_NOT, Empty, new[] { new byte[] { 1 } }, Empty);
            AssertStack((byte)OpcodeType.OP_NOT, Empty, new[] { Empty }, new byte[] { 1 });
            AssertStack((byte)OpcodeType.OP_0NOTEQUAL, Empty, new[] { Empty }, Empty);
            AssertStack((byte)OpcodeType.OP_0NOTEQUAL, Empty, new[] { new byte[] { 1 } }, new byte[] { 2 });
            AssertCode((byte)OpcodeType.OP_1ADD, ScriptErrorCode.InvalidStackOperation);
            AssertStack((byte)OpcodeType.OP_1ADD, Empty, null, ScriptErrorCode.NumberTooBig, Hex("ffffffffffffff7f"));
        }

        [Fact]
        public void Binary_arithmetic_matches_the_opcode_table()
        {
            AssertStack((byte)OpcodeType.OP_ADD, Empty, new[] { new byte[] { 5 } }, new byte[] { 2 }, new byte[] { 3 });
            AssertStack((byte)OpcodeType.OP_SUB, Empty, new[] { new byte[] { 2 } }, new byte[] { 5 }, new byte[] { 3 });
            AssertStack((byte)OpcodeType.OP_MUL, Empty, new[] { new byte[] { 12 } }, new byte[] { 4 }, new byte[] { 3 });
            AssertStack((byte)OpcodeType.OP_DIV, Empty, new[] { new byte[] { 2 } }, new byte[] { 7 }, new byte[] { 3 });
            AssertStack((byte)OpcodeType.OP_MOD, Empty, new[] { new byte[] { 1 } }, new byte[] { 7 }, new byte[] { 3 });
            AssertStack((byte)OpcodeType.OP_MIN, Empty, new[] { new byte[] { 3 } }, new byte[] { 5 }, new byte[] { 3 });
            AssertStack((byte)OpcodeType.OP_MAX, Empty, new[] { new byte[] { 5 } }, new byte[] { 3 }, new byte[] { 5 });
            AssertStack((byte)OpcodeType.OP_LESSTHAN, Empty, new[] { new byte[] { 1 } }, new byte[] { 3 }, new byte[] { 5 });
            AssertStack((byte)OpcodeType.OP_GREATERTHAN, Empty, new[] { new byte[] { 1 } }, new byte[] { 5 }, new byte[] { 3 });
            AssertStack((byte)OpcodeType.OP_LESSTHANOREQUAL, Empty, new[] { new byte[] { 1 } }, new byte[] { 3 }, new byte[] { 3 });
            AssertStack((byte)OpcodeType.OP_GREATERTHANOREQUAL, Empty, new[] { Empty }, new byte[] { 3 }, new byte[] { 5 });
            AssertStack((byte)OpcodeType.OP_BOOLAND, Empty, new[] { Empty }, new byte[] { 1 }, Empty);
            AssertStack((byte)OpcodeType.OP_BOOLOR, Empty, new[] { new byte[] { 1 } }, Empty, new byte[] { 2 });
            AssertStack((byte)OpcodeType.OP_NUMEQUAL, Empty, new[] { new byte[] { 1 } }, new byte[] { 4 }, new byte[] { 4 });
            AssertStack((byte)OpcodeType.OP_NUMNOTEQUAL, Empty, new[] { new byte[] { 1 } }, new byte[] { 4 }, new byte[] { 5 });
            AssertStack((byte)OpcodeType.OP_LESSTHAN, Empty, new[] { new byte[] { 1 } }, new byte[] { 0x00, 0x81 }, Empty);
            AssertStack((byte)OpcodeType.OP_DIV, Empty, null, ScriptErrorCode.InvalidState, new byte[] { 1 }, Empty);
            AssertStack((byte)OpcodeType.OP_ADD, Empty, null, ScriptErrorCode.NumberTooBig, Hex("ffffffffffffff7f"), new byte[] { 1 });
        }

        [Fact]
        public void Logical_bytes_match_the_opcode_table()
        {
            AssertStack((byte)OpcodeType.OP_AND, Empty, new[] { new byte[] { 0x0f, 0x00 } }, new byte[] { 0xff, 0x00 }, new byte[] { 0x0f, 0xff });
            AssertStack((byte)OpcodeType.OP_OR, Empty, new[] { new byte[] { 0xff, 0xff } }, new byte[] { 0xff, 0x00 }, new byte[] { 0x0f, 0xff });
            AssertStack((byte)OpcodeType.OP_XOR, Empty, new[] { new byte[] { 0xf0, 0xff } }, new byte[] { 0xff, 0x00 }, new byte[] { 0x0f, 0xff });
            AssertStack((byte)OpcodeType.OP_INVERT, Empty, new[] { new byte[] { 0x00 } }, new byte[] { 0xff });
            AssertStack((byte)OpcodeType.OP_EQUAL, Empty, new[] { new byte[] { 1 } }, Empty, Empty);
            AssertStack((byte)OpcodeType.OP_EQUAL, Empty, new[] { Empty }, new byte[] { 1 }, new byte[] { 2 });
            AssertStack((byte)OpcodeType.OP_AND, Empty, null, ScriptErrorCode.InvalidState, new byte[] { 1 }, new byte[] { 1, 2 });
        }

        [Fact]
        public void Depth_pushes_the_item_count()
        {
            AssertStack((byte)OpcodeType.OP_DEPTH, Empty, new[] { Empty });
            AssertStack((byte)OpcodeType.OP_DEPTH, Empty, new[] { new byte[] { 1 }, new byte[] { 2 }, new byte[] { 2 } }, new byte[] { 1 }, new byte[] { 2 });
        }

        [Fact]
        public void Drop_removes_the_top_item()
        {
            AssertStack((byte)OpcodeType.OP_DROP, Empty, new[] { new byte[] { 1 } }, new byte[] { 1 }, new byte[] { 2 });
            AssertCode((byte)OpcodeType.OP_DROP, ScriptErrorCode.InvalidStackOperation);
        }

        [Fact]
        public void Two_drop_removes_two_items()
        {
            AssertStack((byte)OpcodeType.OP_2DROP, Empty, new[] { new byte[] { 1 } }, new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 });
            AssertStack((byte)OpcodeType.OP_2DROP, Empty, null, ScriptErrorCode.InvalidStackOperation, new byte[] { 1 });
        }

        [Fact]
        public void Dup_copies_the_top_item()
        {
            AssertStack((byte)OpcodeType.OP_DUP, Empty, new[] { new byte[] { 1 }, new byte[] { 1 } }, new byte[] { 1 });
            AssertCode((byte)OpcodeType.OP_DUP, ScriptErrorCode.InvalidStackOperation);
        }

        [Fact]
        public void Two_dup_copies_the_top_pair()
        {
            AssertStack((byte)OpcodeType.OP_2DUP, Empty, new[] { new byte[] { 1 }, new byte[] { 2 }, new byte[] { 1 }, new byte[] { 2 } }, new byte[] { 1 }, new byte[] { 2 });
        }

        [Fact]
        public void Three_dup_copies_the_top_three()
        {
            AssertStack(
                (byte)OpcodeType.OP_3DUP,
                Empty,
                new[] { new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }, new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 } },
                new byte[] { 1 },
                new byte[] { 2 },
                new byte[] { 3 });
        }

        [Fact]
        public void Nip_removes_the_second_item()
        {
            AssertStack((byte)OpcodeType.OP_NIP, Empty, new[] { new byte[] { 2 } }, new byte[] { 1 }, new byte[] { 2 });
            AssertCode((byte)OpcodeType.OP_NIP, ScriptErrorCode.InvalidStackOperation);
        }

        [Fact]
        public void Over_copies_the_second_item()
        {
            AssertStack((byte)OpcodeType.OP_OVER, Empty, new[] { new byte[] { 1 }, new byte[] { 2 }, new byte[] { 1 } }, new byte[] { 1 }, new byte[] { 2 });
        }

        [Fact]
        public void Two_over_copies_the_pair_under_the_top_pair()
        {
            AssertStack(
                (byte)OpcodeType.OP_2OVER,
                Empty,
                new[] { new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }, new byte[] { 4 }, new byte[] { 1 }, new byte[] { 2 } },
                new byte[] { 1 },
                new byte[] { 2 },
                new byte[] { 3 },
                new byte[] { 4 });
        }

        [Fact]
        public void Pick_copies_the_indexed_item()
        {
            AssertStack((byte)OpcodeType.OP_PICK, Empty, new[] { new byte[] { 0xaa }, new byte[] { 0xaa } }, new byte[] { 0xaa }, Empty);
            AssertStack((byte)OpcodeType.OP_PICK, Empty, null, ScriptErrorCode.InvalidState, new byte[] { 0xaa }, new byte[] { 5 });
        }

        [Fact]
        public void Roll_moves_the_indexed_item()
        {
            AssertStack((byte)OpcodeType.OP_ROLL, Empty, new[] { new byte[] { 0xbb }, new byte[] { 0xaa } }, new byte[] { 0xaa }, new byte[] { 0xbb }, new byte[] { 1 });
            AssertStack((byte)OpcodeType.OP_ROLL, Empty, null, ScriptErrorCode.InvalidState, new byte[] { 0xaa }, new byte[] { 5 });
        }

        [Fact]
        public void Rot_cycles_three_items()
        {
            AssertStack((byte)OpcodeType.OP_ROT, Empty, new[] { new byte[] { 2 }, new byte[] { 3 }, new byte[] { 1 } }, new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 });
        }

        [Fact]
        public void Two_rot_cycles_two_triples()
        {
            AssertStack(
                (byte)OpcodeType.OP_2ROT,
                Empty,
                new[] { new byte[] { 3 }, new byte[] { 4 }, new byte[] { 5 }, new byte[] { 6 }, new byte[] { 1 }, new byte[] { 2 } },
                new byte[] { 1 },
                new byte[] { 2 },
                new byte[] { 3 },
                new byte[] { 4 },
                new byte[] { 5 },
                new byte[] { 6 });
        }

        [Fact]
        public void Swap_exchanges_the_top_pair()
        {
            AssertStack((byte)OpcodeType.OP_SWAP, Empty, new[] { new byte[] { 2 }, new byte[] { 1 } }, new byte[] { 1 }, new byte[] { 2 });
        }

        [Fact]
        public void Two_swap_exchanges_the_top_pairs()
        {
            AssertStack(
                (byte)OpcodeType.OP_2SWAP,
                Empty,
                new[] { new byte[] { 3 }, new byte[] { 4 }, new byte[] { 1 }, new byte[] { 2 } },
                new byte[] { 1 },
                new byte[] { 2 },
                new byte[] { 3 },
                new byte[] { 4 });
        }

        [Fact]
        public void Tuck_copies_the_top_under_the_second_item()
        {
            AssertStack((byte)OpcodeType.OP_TUCK, Empty, new[] { new byte[] { 2 }, new byte[] { 1 }, new byte[] { 2 } }, new byte[] { 1 }, new byte[] { 2 });
        }

        [Fact]
        public void Equal_verify_consumes_a_matching_pair()
        {
            AssertStack((byte)OpcodeType.OP_EQUALVERIFY, Empty, Array.Empty<byte[]>(), new byte[] { 1 }, new byte[] { 1 });
            AssertStack((byte)OpcodeType.OP_EQUALVERIFY, Empty, null, ScriptErrorCode.VerifyError, new byte[] { 1 }, new byte[] { 2 });
        }

        [Fact]
        public void Size_pushes_the_top_length()
        {
            AssertStack((byte)OpcodeType.OP_SIZE, Empty, new[] { new byte[] { 0xaa, 0xbb }, new byte[] { 2 } }, new byte[] { 0xaa, 0xbb });
            AssertCode((byte)OpcodeType.OP_SIZE, ScriptErrorCode.InvalidStackOperation);
        }

        [Fact]
        public void Within_uses_a_half_open_interval()
        {
            AssertStack((byte)OpcodeType.OP_WITHIN, Empty, new[] { new byte[] { 1 } }, new byte[] { 5 }, new byte[] { 3 }, new byte[] { 7 });
            AssertStack((byte)OpcodeType.OP_WITHIN, Empty, new[] { Empty }, new byte[] { 7 }, new byte[] { 3 }, new byte[] { 7 });
            AssertStack((byte)OpcodeType.OP_WITHIN, Empty, new[] { Empty }, new byte[] { 2 }, new byte[] { 3 }, new byte[] { 7 });
        }

        [Fact]
        public void Sha256_hashes_the_top_item()
        {
            AssertStack((byte)OpcodeType.OP_SHA256, Empty, new[] { Sha256Empty }, Empty);
        }

        [Fact]
        public void Blake2b_hashes_the_top_item()
        {
            AssertStack((byte)OpcodeType.OP_BLAKE2B, Empty, new[] { Blake2bEmpty }, Empty);
        }

        [Fact]
        public void Nop_leaves_the_stack_alone()
        {
            AssertStack((byte)OpcodeType.OP_NOP, Empty, new[] { new byte[] { 1 } }, new byte[] { 1 });
        }

        [Fact]
        public void Check_lock_time_verify_matches_the_opcode_table()
        {
            AssertStack((byte)OpcodeType.OP_CHECKLOCKTIMEVERIFY, Empty, Array.Empty<byte[]>(), LockTx(1, 1), Empty);
            AssertStack((byte)OpcodeType.OP_CHECKLOCKTIMEVERIFY, Empty, Array.Empty<byte[]>(), LockTx(0x800000, 1), new byte[] { 0x7f, 0x00, 0x00 });
            AssertStack((byte)OpcodeType.OP_CHECKLOCKTIMEVERIFY, Empty, Array.Empty<byte[]>(), LockTx(5, 1), new byte[] { 5 });
            AssertStack((byte)OpcodeType.OP_CHECKLOCKTIMEVERIFY, Empty, null, LockTx(5, 1), new byte[] { 6 }, ScriptErrorCode.UnsatisfiedLockTime);
            AssertStack((byte)OpcodeType.OP_CHECKLOCKTIMEVERIFY, Empty, null, LockTx(1, 1), Hex("0088526a74"), ScriptErrorCode.UnsatisfiedLockTime);
            AssertStack((byte)OpcodeType.OP_CHECKLOCKTIMEVERIFY, Empty, null, LockTx(1, ulong.MaxValue), Empty, ScriptErrorCode.UnsatisfiedLockTime);
        }

        [Fact]
        public void Check_sequence_verify_matches_the_opcode_table()
        {
            AssertStack((byte)OpcodeType.OP_CHECKSEQUENCEVERIFY, Empty, Array.Empty<byte[]>(), LockTx(0, 10), Hex("0000000000000080"));
            AssertStack((byte)OpcodeType.OP_CHECKSEQUENCEVERIFY, Empty, Array.Empty<byte[]>(), LockTx(0, 10), new byte[] { 5 });
            AssertStack((byte)OpcodeType.OP_CHECKSEQUENCEVERIFY, Empty, null, LockTx(0, 5), new byte[] { 10 }, ScriptErrorCode.UnsatisfiedLockTime);
            AssertStack((byte)OpcodeType.OP_CHECKSEQUENCEVERIFY, Empty, null, LockTx(0, 1UL << 63), new byte[] { 1 }, ScriptErrorCode.UnsatisfiedLockTime);
        }

        [Fact]
        public void Return_stops_the_script()
        {
            AssertCode((byte)OpcodeType.OP_RETURN, ScriptErrorCode.EarlyReturn);
        }

        [Fact]
        public void Verify_requires_a_true_top_item()
        {
            AssertStack((byte)OpcodeType.OP_VERIFY, Empty, Array.Empty<byte[]>(), new byte[] { 1 });
            AssertStack((byte)OpcodeType.OP_VERIFY, Empty, null, ScriptErrorCode.VerifyError, Empty);
            AssertCode((byte)OpcodeType.OP_VERIFY, ScriptErrorCode.InvalidStackOperation);
        }

        [Fact]
        public void If_dup_copies_only_a_true_top_item()
        {
            AssertStack((byte)OpcodeType.OP_IFDUP, Empty, new[] { new byte[] { 1 }, new byte[] { 1 } }, new byte[] { 1 });
            AssertStack((byte)OpcodeType.OP_IFDUP, Empty, new[] { Empty }, Empty);
            AssertCode((byte)OpcodeType.OP_IFDUP, ScriptErrorCode.InvalidStackOperation);
        }

        [Fact]
        public void Unary_introspection_ops_push_transaction_fields()
        {
            var tx = Sample(2, 3, lockTime: 9, gas: 4, payload: new byte[] { 1, 2, 3 }, version: 2);
            AssertStack((byte)OpcodeType.OP_TXVERSION, Empty, new[] { new byte[] { 2 } }, tx);
            AssertStack((byte)OpcodeType.OP_TXINPUTCOUNT, Empty, new[] { new byte[] { 2 } }, tx);
            AssertStack((byte)OpcodeType.OP_TXOUTPUTCOUNT, Empty, new[] { new byte[] { 3 } }, tx);
            AssertStack((byte)OpcodeType.OP_TXLOCKTIME, Empty, new[] { new byte[] { 9 } }, tx);
            AssertStack((byte)OpcodeType.OP_TXGAS, Empty, new[] { new byte[] { 4 } }, tx);
            AssertStack((byte)OpcodeType.OP_TXPAYLOADLEN, Empty, new[] { new byte[] { 3 } }, tx);
            AssertStack((byte)OpcodeType.OP_TXINPUTINDEX, Empty, new[] { Empty }, tx);
            AssertStack((byte)OpcodeType.OP_TXSUBNETID, Empty, new[] { new byte[20] }, tx);
        }

        [Fact]
        public void Input_and_output_counts_are_pushed_as_script_numbers()
        {
            var tx = Sample(2, 3, version: 1);
            AssertStack((byte)OpcodeType.OP_TXINPUTCOUNT, Empty, new[] { new byte[] { 2 } }, tx);
            AssertStack((byte)OpcodeType.OP_TXOUTPUTCOUNT, Empty, new[] { new byte[] { 3 } }, tx);
        }

        [Fact]
        public void Output_amount_matches_the_named_output()
        {
            var tx = Sample(1, 1, outputAmount: 1000);
            var utxos = Utxos(tx, 0);
            Assert.Null(Run(Lock(s => s.AddInt64(0).TxOutputAmount().AddInt64(1000).AddOp(OpcodeType.OP_NUMEQUAL)), tx, utxos, 0));
            Assert.Equal(ScriptErrorCode.EvalFalse, Run(Lock(s => s.AddInt64(0).TxOutputAmount().AddInt64(1001).AddOp(OpcodeType.OP_NUMEQUAL)), tx, utxos, 0)!.Code);
        }

        [Fact]
        public void Input_amount_matches_the_spent_output()
        {
            var tx = Sample(1, 1);
            var utxos = Utxos(tx, 1000);
            Assert.Null(Run(Lock(s => s.AddInt64(0).TxInputAmount().AddInt64(1000).AddOp(OpcodeType.OP_NUMEQUAL)), tx, utxos, 0));
            Assert.Equal(ScriptErrorCode.EvalFalse, Run(Lock(s => s.AddInt64(0).TxInputAmount().AddInt64(1).AddOp(OpcodeType.OP_NUMEQUAL)), tx, utxos, 0)!.Code);
        }

        [Fact]
        public void Input_script_basic_returns_the_versioned_script()
        {
            var tx = Sample(2, 1);
            var utxos = Utxos(tx, 0);
            utxos[0] = new UtxoEntry(0UL, new ScriptPubKey(0, new byte[] { 0x51 }), 0, false);
            var script = Lock(s => s.AddData(new byte[] { 0x00, 0x00, 0x51 }).AddInt64(0).TxInputSpk().AddOp(OpcodeType.OP_EQUAL));
            Assert.Null(Run(script, tx, utxos, 1));
        }

        [Fact]
        public void Input_scripts_that_differ_compare_false()
        {
            var tx = Sample(3, 1);
            var utxos = Utxos(tx, 0);
            utxos[0] = new UtxoEntry(0UL, new ScriptPubKey(0, new byte[] { 0x51 }), 0, false);
            utxos[1] = new UtxoEntry(0UL, new ScriptPubKey(0, new byte[] { 0x52 }), 0, false);
            var script = Lock(s => s.AddInt64(0).TxInputSpk().AddInt64(1).TxInputSpk().AddOp(OpcodeType.OP_EQUAL));
            Assert.Equal(ScriptErrorCode.EvalFalse, Run(script, tx, utxos, 2)!.Code);
        }

        [Fact]
        public void Input_scripts_that_match_compare_true()
        {
            var tx = Sample(3, 1);
            var utxos = Utxos(tx, 0);
            utxos[0] = new UtxoEntry(0UL, new ScriptPubKey(0, new byte[] { 0x51 }), 0, false);
            utxos[1] = new UtxoEntry(0UL, new ScriptPubKey(0, new byte[] { 0x51 }), 0, false);
            var script = Lock(s => s.AddInt64(0).TxInputSpk().AddInt64(1).TxInputSpk().AddOp(OpcodeType.OP_EQUAL));
            Assert.Null(Run(script, tx, utxos, 2));
        }

        [Fact]
        public void Output_script_returns_the_versioned_script()
        {
            var tx = Sample(1, 1);
            tx.Outputs[0] = new TxOut(0UL, new ScriptPubKey(0, new byte[] { 0x51 }));
            var utxos = Utxos(tx, 0);
            var script = Lock(s => s.AddData(new byte[] { 0x00, 0x00, 0x51 }).AddInt64(0).TxOutputSpk().AddOp(OpcodeType.OP_EQUAL));
            Assert.Null(Run(script, tx, utxos, 0));
        }

        [Fact]
        public void Input_index_is_the_input_being_executed()
        {
            var tx = Sample(2, 1, version: 1);
            var utxos = Utxos(tx, 0);
            var wrong = Lock(s => s.TxInputIndex().AddInt64(0).AddOp(OpcodeType.OP_NUMEQUAL));
            Assert.Equal(ScriptErrorCode.EvalFalse, Run(wrong, tx, utxos, 1)!.Code);
            var right = Lock(s => s.TxInputIndex().AddInt64(1).AddOp(OpcodeType.OP_NUMEQUAL));
            Assert.Null(Run(right, tx, utxos, 1));
        }

        [Fact]
        public void Counts_compare_inputs_and_outputs()
        {
            var tx = Sample(2, 3, version: 1);
            var utxos = Utxos(tx, 0);
            Assert.Null(Run(Lock(s => s.TxInputCount().AddInt64(2).AddOp(OpcodeType.OP_NUMEQUAL)), tx, utxos, 0));
            Assert.Null(Run(Lock(s => s.TxOutputCount().AddInt64(3).AddOp(OpcodeType.OP_NUMEQUAL)), tx, utxos, 0));
            Assert.Equal(
                ScriptErrorCode.EvalFalse,
                Run(Lock(s => s.TxInputCount().AddInt64(1).AddOp(OpcodeType.OP_NUMEQUAL)), tx, utxos, 0)!.Code);
        }

        [Fact]
        public void Input_daa_score_is_pushed_for_the_named_input()
        {
            var tx = Sample(1, 1, version: 1);
            var utxos = Utxos(tx, 0, daa: 42);
            Assert.Null(Run(Lock(s => s.AddInt64(0).TxInputDaaScore().AddInt64(42).AddOp(OpcodeType.OP_NUMEQUAL)), tx, utxos, 0));
        }

        [Fact]
        public void Input_daa_score_can_be_checked_inside_a_script()
        {
            var tx = Sample(1, 1, version: 1);
            var utxos = Utxos(tx, 0, daa: 42);
            var script = Lock(s => s.TxVersion().AddInt64(1).AddOp(OpcodeType.OP_NUMEQUALVERIFY).AddInt64(0).TxInputDaaScore().AddInt64(42).AddOp(OpcodeType.OP_NUMEQUAL));
            Assert.Null(Run(script, tx, utxos, 0));
            var wrong = Lock(s => s.AddInt64(0).TxInputDaaScore().AddInt64(43).AddOp(OpcodeType.OP_NUMEQUAL));
            Assert.Equal(ScriptErrorCode.EvalFalse, Run(wrong, tx, utxos, 0)!.Code);
        }

        [Fact]
        public void Script_pub_key_strings_round_trip_through_rust_names()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "script_tests.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var matched = 0;
            var skipped = 0;
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 2)
                {
                    skipped++;
                    continue;
                }

                var text = row[1].GetString() ?? string.Empty;
                if (ScriptShortForm.TryParse(text, out var bytes) != null)
                {
                    skipped++;
                    continue;
                }

                Script script;
                try
                {
                    script = new Script(bytes);
                }
                catch (FormatException)
                {
                    skipped++;
                    continue;
                }

                var rust = script.ToRustString();
                var again = ScriptShortForm.TryParse(rust, out var reparsed);
                Assert.Null(again);
                if (!bytes.AsSpan().SequenceEqual(reparsed))
                {
                    continue;
                }

                matched++;
            }

            Assert.True(matched > 0, "matched=" + matched.ToString() + " skipped=" + skipped.ToString());
        }

        private static void AssertCode(byte code, ScriptErrorCode expected)
        {
            AssertStack(code, Empty, null, expected);
        }

        private static void AssertStack(byte code, byte[] data, byte[][]? expected, ScriptErrorCode error, params byte[][] stack)
        {
            AssertStack(code, data, expected, null, stack, error);
        }

        private static void AssertStack(byte code, byte[] data, byte[][]? expected, params byte[][] stack)
        {
            AssertStack(code, data, expected, null, stack, null);
        }

        private static void AssertStack(byte code, byte[] data, byte[][]? expected, byte[][] stack, ScriptErrorCode error)
        {
            AssertStack(code, data, expected, null, stack, error);
        }

        private static void AssertStack(byte code, byte[] data, byte[][]? expected, Transaction tx, params byte[][] stack)
        {
            AssertStack(code, data, expected, tx, stack, null);
        }

        private static void AssertStack(byte code, byte[] data, byte[][]? expected, Transaction? tx, byte[] item, ScriptErrorCode error)
        {
            AssertStack(code, data, expected, tx, new[] { item }, error);
        }

        private static void AssertStack(byte code, byte[] data, byte[][]? expected, Transaction? tx, byte[][] stack, ScriptErrorCode? error)
        {
            var actual = tx == null
                ? TxScriptEngine.ExecuteOpcodeForTest(code, data, stack, out var result)
                : TxScriptEngine.ExecuteOpcodeForTest(code, data, stack, tx, out result);
            if (error == null)
            {
                Assert.Null(actual);
                Assert.NotNull(expected);
                Assert.Equal(expected!.Length, result.Length);
                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.Equal(expected[i], result[i]);
                }

                return;
            }

            Assert.NotNull(actual);
            Assert.Equal(error.Value, actual!.Code);
        }

        private static Transaction LockTx(ulong lockTime, ulong sequence)
        {
            var input = new TxIn(new OutPoint(uint256.Zero, 0), Script.Empty, sequence, 0);
            var output = new TxOut(0UL, new ScriptPubKey(0, Script.Empty));
            return new Transaction(0, new[] { input }, new[] { output }, lockTime, SubnetworkId.Native, 0, Empty);
        }

        private static Transaction Sample(int inputs, int outputs, ulong lockTime = 0, ulong gas = 0, byte[]? payload = null, ushort version = 0, ulong outputAmount = 0)
        {
            var txins = new TxIn[inputs];
            for (var i = 0; i < inputs; i++)
            {
                txins[i] = new TxIn(new OutPoint(uint256.Zero, (uint)i), Script.Empty, 1, 0);
            }

            var txouts = new TxOut[outputs];
            for (var i = 0; i < outputs; i++)
            {
                txouts[i] = new TxOut(outputAmount, new ScriptPubKey(0, Script.Empty));
            }

            return new Transaction(version, txins, txouts, lockTime, SubnetworkId.Native, gas, payload ?? Empty);
        }

        private static UtxoEntry?[] Utxos(Transaction tx, ulong amount, ulong daa = 0)
        {
            var utxos = new UtxoEntry?[tx.Inputs.Count];
            for (var i = 0; i < utxos.Length; i++)
            {
                utxos[i] = new UtxoEntry(amount, new ScriptPubKey(0, Script.Empty), daa, false);
            }

            return utxos;
        }

        private static Script Lock(Func<ScriptBuilder, ScriptBuilder> build)
        {
            return build(new ScriptBuilder()).ToScript();
        }

        private static ScriptError? Run(Script locking, Transaction tx, UtxoEntry?[] utxos, int input)
        {
            return TxScriptEngine.Run(Empty, new ScriptPubKey(0, locking), tx, input, utxos, ulong.MaxValue).Error;
        }

        private static byte[] Hex(string text)
        {
            var bytes = new byte[text.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
