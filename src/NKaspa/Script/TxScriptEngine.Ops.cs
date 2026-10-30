using System.Security.Cryptography;
using NKaspa.Crypto;

namespace NKaspa
{
    public sealed partial class TxScriptEngine
    {
        private ScriptError? ExecuteOpcode(ParsedOp op)
        {
            var code = op.Code;
            if (IsDisabled(code))
            {
                return ScriptError.OpcodeDisabled(code);
            }

            if (code == (byte)OpcodeType.OP_0)
            {
                return _dstack.Push(Array.Empty<byte>(), meter: false);
            }

            if (code >= (byte)OpcodeType.OP_DATA_1 && code <= (byte)OpcodeType.OP_PUSHDATA4)
            {
                return _dstack.Push((byte[])op.Data.Clone(), meter: false);
            }

            if (code == (byte)OpcodeType.OP_1NEGATE)
            {
                return _dstack.PushNumber(-1, meter: false);
            }

            if (code >= (byte)OpcodeType.OP_1 && code <= (byte)OpcodeType.OP_16)
            {
                return _dstack.PushNumber(code - (byte)OpcodeType.OP_1 + 1, meter: false);
            }

            switch (code)
            {
                case (byte)OpcodeType.OP_RESERVED:
                case (byte)OpcodeType.OP_VER:
                case (byte)OpcodeType.OP_VERIF:
                case (byte)OpcodeType.OP_VERNOTIF:
                case (byte)OpcodeType.OP_RESERVED1:
                case (byte)OpcodeType.OP_RESERVED2:
                    return ScriptError.OpcodeReserved(code);
                case (byte)OpcodeType.OP_NOP:
                    return null;
                case (byte)OpcodeType.OP_IF:
                    return OpIf(invert: false);
                case (byte)OpcodeType.OP_NOTIF:
                    return OpIf(invert: true);
                case (byte)OpcodeType.OP_ELSE:
                    return OpElse();
                case (byte)OpcodeType.OP_ENDIF:
                    return OpEndIf();
                case (byte)OpcodeType.OP_VERIFY:
                    return OpVerify();
                case (byte)OpcodeType.OP_RETURN:
                    return ScriptError.EarlyReturn;
                case (byte)OpcodeType.OP_TOALTSTACK:
                    return MoveStack(_dstack, _astack);
                case (byte)OpcodeType.OP_FROMALTSTACK:
                    return FromAltStack();
                case (byte)OpcodeType.OP_2DROP:
                    return _dstack.Drop(2);
                case (byte)OpcodeType.OP_2DUP:
                    return _dstack.Dup(2);
                case (byte)OpcodeType.OP_3DUP:
                    return _dstack.Dup(3);
                case (byte)OpcodeType.OP_2OVER:
                    return _dstack.Over(2);
                case (byte)OpcodeType.OP_2ROT:
                    return _dstack.Rot(2);
                case (byte)OpcodeType.OP_2SWAP:
                    return _dstack.Swap(2);
                case (byte)OpcodeType.OP_IFDUP:
                    return OpIfDup();
                case (byte)OpcodeType.OP_DEPTH:
                    return _dstack.PushNumber(_dstack.Count, meter: true);
                case (byte)OpcodeType.OP_DROP:
                    return _dstack.Drop(1);
                case (byte)OpcodeType.OP_DUP:
                    return _dstack.Dup(1);
                case (byte)OpcodeType.OP_NIP:
                    return _dstack.RemoveSecond();
                case (byte)OpcodeType.OP_OVER:
                    return _dstack.Over(1);
                case (byte)OpcodeType.OP_PICK:
                    return OpPick();
                case (byte)OpcodeType.OP_ROLL:
                    return OpRoll();
                case (byte)OpcodeType.OP_ROT:
                    return _dstack.Rot(1);
                case (byte)OpcodeType.OP_SWAP:
                    return _dstack.Swap(1);
                case (byte)OpcodeType.OP_TUCK:
                    return _dstack.Tuck();
                case (byte)OpcodeType.OP_SIZE:
                    return OpSize();
                case (byte)OpcodeType.OP_CAT:
                    return OpCat();
                case (byte)OpcodeType.OP_SUBSTR:
                    return OpSubstr();
                case (byte)OpcodeType.OP_INVERT:
                    return OpInvert();
                case (byte)OpcodeType.OP_AND:
                    return Bitwise("AND", 0);
                case (byte)OpcodeType.OP_OR:
                    return Bitwise("OR", 1);
                case (byte)OpcodeType.OP_XOR:
                    return Bitwise("XOR", 2);
                case (byte)OpcodeType.OP_EQUAL:
                    return OpEqual(verify: false);
                case (byte)OpcodeType.OP_EQUALVERIFY:
                    return OpEqual(verify: true);
                case (byte)OpcodeType.OP_1ADD:
                    return Unary(1, "Result of addition exceeds 64-bit signed integer range", checkedAdd: true);
                case (byte)OpcodeType.OP_1SUB:
                    return Unary(-1, "Result of subtraction exceeds 64-bit signed integer range", checkedAdd: true);
                case (byte)OpcodeType.OP_NEGATE:
                    return OpNegate();
                case (byte)OpcodeType.OP_ABS:
                    return OpAbs();
                case (byte)OpcodeType.OP_NOT:
                    return OpNot(equalZero: true);
                case (byte)OpcodeType.OP_0NOTEQUAL:
                    return OpNot(equalZero: false);
                case (byte)OpcodeType.OP_ADD:
                    return BinaryArithmetic(0);
                case (byte)OpcodeType.OP_SUB:
                    return BinaryArithmetic(1);
                case (byte)OpcodeType.OP_MUL:
                    return BinaryArithmetic(2);
                case (byte)OpcodeType.OP_DIV:
                    return BinaryArithmetic(3);
                case (byte)OpcodeType.OP_MOD:
                    return BinaryArithmetic(4);
                case (byte)OpcodeType.OP_BOOLAND:
                    return BinaryFlag(0);
                case (byte)OpcodeType.OP_BOOLOR:
                    return BinaryFlag(1);
                case (byte)OpcodeType.OP_NUMEQUAL:
                    return BinaryFlag(2);
                case (byte)OpcodeType.OP_NUMEQUALVERIFY:
                    return OpNumEqualVerify();
                case (byte)OpcodeType.OP_NUMNOTEQUAL:
                    return BinaryFlag(3);
                case (byte)OpcodeType.OP_LESSTHAN:
                    return BinaryFlag(4);
                case (byte)OpcodeType.OP_GREATERTHAN:
                    return BinaryFlag(5);
                case (byte)OpcodeType.OP_LESSTHANOREQUAL:
                    return BinaryFlag(6);
                case (byte)OpcodeType.OP_GREATERTHANOREQUAL:
                    return BinaryFlag(7);
                case (byte)OpcodeType.OP_MIN:
                    return BinaryFlag(8);
                case (byte)OpcodeType.OP_MAX:
                    return BinaryFlag(9);
                case (byte)OpcodeType.OP_WITHIN:
                    return OpWithin();
                case (byte)OpcodeType.OP_ZKPRECOMPILE:
                    return ZkPrecompile.Execute(_dstack, _meter);
                case (byte)OpcodeType.OP_BLAKE2BWITHKEY:
                    return OpBlake2b(keyed: true);
                case (byte)OpcodeType.OP_SHA256:
                    return OpSha256();
                case (byte)OpcodeType.OP_BLAKE2B:
                    return OpBlake2b(keyed: false);
                case (byte)OpcodeType.OP_CHECKSIGECDSA:
                    return OpCheckSig(ecdsa: true, verify: false);
                case (byte)OpcodeType.OP_CHECKSIG:
                    return OpCheckSig(ecdsa: false, verify: false);
                case (byte)OpcodeType.OP_CHECKSIGVERIFY:
                    return OpCheckSig(ecdsa: false, verify: true);
                case (byte)OpcodeType.OP_CHECKMULTISIGECDSA:
                    return OpCheckMultisig(ecdsa: true, verify: false);
                case (byte)OpcodeType.OP_CHECKMULTISIG:
                    return OpCheckMultisig(ecdsa: false, verify: false);
                case (byte)OpcodeType.OP_CHECKMULTISIGVERIFY:
                    return OpCheckMultisig(ecdsa: false, verify: true);
                case (byte)OpcodeType.OP_CHECKLOCKTIMEVERIFY:
                    return OpCheckLockTime();
                case (byte)OpcodeType.OP_CHECKSEQUENCEVERIFY:
                    return OpCheckSequence();
                case (byte)OpcodeType.OP_TXVERSION:
                    return PushTxNumber(_tx.Version, "OpTxVersion only applies to transaction inputs");
                case (byte)OpcodeType.OP_TXINPUTCOUNT:
                    return PushTxNumber(_tx.Inputs.Count, "OpInputCount only applies to transaction inputs");
                case (byte)OpcodeType.OP_TXOUTPUTCOUNT:
                    return PushTxNumber(_tx.Outputs.Count, "OpOutputCount only applies to transaction inputs");
                case (byte)OpcodeType.OP_TXLOCKTIME:
                    return PushTxNumber(unchecked((long)_tx.LockTime), "OpTxLockTime only applies to transaction inputs");
                case (byte)OpcodeType.OP_TXSUBNETID:
                    return PushTxBytes(_tx.SubnetworkId.ToBytes(), "OpTxSubnetId only applies to transaction inputs");
                case (byte)OpcodeType.OP_TXGAS:
                    return PushTxNumber(unchecked((long)_tx.Gas), "OpTxGas only applies to transaction inputs");
                case (byte)OpcodeType.OP_TXPAYLOADSUBSTR:
                    return OpPayloadSubstr();
                case (byte)OpcodeType.OP_TXINPUTINDEX:
                    return PushTxNumber(_inputIndex, "OpInputIndex only applies to transaction inputs");
                case (byte)OpcodeType.OP_OUTPOINTTXID:
                    return OpOutpointTxId();
                case (byte)OpcodeType.OP_OUTPOINTINDEX:
                    return OpOutpointIndex();
                case (byte)OpcodeType.OP_TXINPUTSCRIPTSIGSUBSTR:
                    return OpScriptSigSubstr();
                case (byte)OpcodeType.OP_TXINPUTSEQ:
                    return OpInputSequence();
                case (byte)OpcodeType.OP_TXINPUTAMOUNT:
                    return OpInputAmount();
                case (byte)OpcodeType.OP_TXINPUTSPK:
                    return OpInputSpk(lengthOnly: false, substring: false);
                case (byte)OpcodeType.OP_TXINPUTDAASCORE:
                    return OpInputDaa();
                case (byte)OpcodeType.OP_TXINPUTISCOINBASE:
                    return OpInputCoinbase();
                case (byte)OpcodeType.OP_TXOUTPUTAMOUNT:
                    return OpOutputAmount();
                case (byte)OpcodeType.OP_TXOUTPUTSPK:
                    return OpOutputSpk(lengthOnly: false, substring: false);
                case (byte)OpcodeType.OP_TXPAYLOADLEN:
                    return PushTxNumber(_tx.Payload.Length, "OpTxPayloadLen only applies to transaction inputs");
                case (byte)OpcodeType.OP_TXINPUTSPKLEN:
                    return OpInputSpk(lengthOnly: true, substring: false);
                case (byte)OpcodeType.OP_TXINPUTSPKSUBSTR:
                    return OpInputSpk(lengthOnly: false, substring: true);
                case (byte)OpcodeType.OP_TXOUTPUTSPKLEN:
                    return OpOutputSpk(lengthOnly: true, substring: false);
                case (byte)OpcodeType.OP_TXOUTPUTSPKSUBSTR:
                    return OpOutputSpk(lengthOnly: false, substring: true);
                case (byte)OpcodeType.OP_TXINPUTSCRIPTSIGLEN:
                    return OpScriptSigLen();
                case (byte)OpcodeType.OP_AUTHOUTPUTCOUNT:
                    return OpAuthOutputCount();
                case (byte)OpcodeType.OP_AUTHOUTPUTIDX:
                    return OpAuthOutputIdx();
                case (byte)OpcodeType.OP_NUM2BIN:
                    return OpNum2Bin();
                case (byte)OpcodeType.OP_BIN2NUM:
                    return OpBin2Num();
                case (byte)OpcodeType.OP_INPUTCOVENANTID:
                    return OpInputCovenantId();
                case (byte)OpcodeType.OP_COVINPUTCOUNT:
                    return OpCovCount(outputs: false);
                case (byte)OpcodeType.OP_COVINPUTIDX:
                    return OpCovIndex(outputs: false);
                case (byte)OpcodeType.OP_COVOUTPUTCOUNT:
                    return OpCovCount(outputs: true);
                case (byte)OpcodeType.OP_COVOUTPUTIDX:
                    return OpCovIndex(outputs: true);
                case (byte)OpcodeType.OP_CHAINBLOCKSEQCOMMIT:
                    return OpChainBlock();
                case (byte)OpcodeType.OP_OUTPUTCOVENANTID:
                    return OpOutputCovenantId();
                case (byte)OpcodeType.OP_OUTPUTAUTHORIZINGINPUT:
                    return OpOutputAuthorizing();
                case (byte)OpcodeType.OP_CHECKSIGFROMSTACK:
                    return OpCheckSigFromStack(ecdsa: false);
                case (byte)OpcodeType.OP_CHECKSIGFROMSTACKECDSA:
                    return OpCheckSigFromStack(ecdsa: true);
                case (byte)OpcodeType.OP_BLAKE3:
                    return OpBlake3(keyed: false);
                case (byte)OpcodeType.OP_BLAKE3WITHKEY:
                    return OpBlake3(keyed: true);
                default:
                    return ScriptError.InvalidOpcode(code);
            }
        }

        private ScriptError? OpIf(bool invert)
        {
            if (!IsExecuting)
            {
                _cond.Add(Cond.Skip);
                return null;
            }

            var error = PopBool(out var value);
            if (error != null)
            {
                return error;
            }

            if (invert)
            {
                value = !value;
            }

            _cond.Add(value ? Cond.True : Cond.False);
            return null;
        }

        private ScriptError? OpElse()
        {
            if (_cond.Count == 0)
            {
                return ScriptError.InvalidState("condition stack empty");
            }

            var index = _cond.Count - 1;
            var cond = _cond[index];
            if (cond == Cond.True)
            {
                _cond[index] = Cond.False;
            }
            else if (cond == Cond.False)
            {
                _cond[index] = Cond.True;
            }

            return null;
        }

        private ScriptError? OpEndIf()
        {
            if (_cond.Count == 0)
            {
                return ScriptError.InvalidState("condition stack empty");
            }

            _cond.RemoveAt(_cond.Count - 1);
            return null;
        }

        private ScriptError? OpVerify()
        {
            var error = PopBool(out var value);
            if (error != null)
            {
                return error;
            }

            return value ? null : ScriptError.VerifyError;
        }

        private ScriptError? MoveStack(ScriptStack from, ScriptStack to)
        {
            var error = from.PopRaw(1, out var items);
            if (error != null)
            {
                return error;
            }

            return to.Push(items[0], meter: false);
        }

        private ScriptError? FromAltStack()
        {
            var error = _astack.Pop(out var item);
            if (error != null)
            {
                return error;
            }

            return _dstack.Push(item, meter: false);
        }

        private ScriptError? OpIfDup()
        {
            if (_dstack.Count < 1)
            {
                return ScriptError.InvalidStackOperation(1, 0);
            }

            var top = _dstack[_dstack.Count - 1];
            if (!ScriptNum.AsBool(top))
            {
                return null;
            }

            return _dstack.Push((byte[])top.Clone(), meter: true);
        }

        private ScriptError? OpPick()
        {
            var error = PopI32(out var loc);
            if (error != null)
            {
                return error;
            }

            if (loc < 0 || loc >= _dstack.Count)
            {
                return ScriptError.InvalidState("pick at an invalid location");
            }

            return _dstack.Push((byte[])_dstack[_dstack.Count - loc - 1].Clone(), meter: true);
        }

        private ScriptError? OpRoll()
        {
            var error = PopI32(out var loc);
            if (error != null)
            {
                return error;
            }

            if (loc < 0 || loc >= _dstack.Count)
            {
                return ScriptError.InvalidState("roll at an invalid location");
            }

            return _dstack.Roll(loc);
        }

        private ScriptError? OpSize()
        {
            if (_dstack.Count < 1)
            {
                return ScriptError.InvalidStackOperation(1, 0);
            }

            return _dstack.PushNumber(_dstack[_dstack.Count - 1].Length, meter: true);
        }

        private ScriptError? OpCat()
        {
            var error = _dstack.Pop(out var b);
            if (error != null)
            {
                return error;
            }

            error = _dstack.Pop(out var a);
            if (error != null)
            {
                return error;
            }

            var joined = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, joined, 0, a.Length);
            Buffer.BlockCopy(b, 0, joined, a.Length, b.Length);
            return _dstack.Push(joined, meter: true);
        }

        private ScriptError? OpSubstr()
        {
            var error = PopI32(2, out var bounds);
            if (error != null)
            {
                return error;
            }

            error = _dstack.Pop(out var data);
            if (error != null)
            {
                return error;
            }

            error = ToIndexes(bounds, out var start, out var end);
            if (error != null)
            {
                return error;
            }

            error = Slice(data, start, end, out var piece);
            if (error != null)
            {
                return error;
            }

            return _dstack.Push(piece, meter: true);
        }

        private ScriptError? OpInvert()
        {
            var error = _dstack.Pop(out var data);
            if (error != null)
            {
                return error;
            }

            var flipped = new byte[data.Length];
            for (var i = 0; i < data.Length; i++)
            {
                flipped[i] = (byte)~data[i];
            }

            return _dstack.Push(flipped, meter: true);
        }

        private ScriptError? Bitwise(string name, int kind)
        {
            var error = _dstack.Pop(out var b);
            if (error != null)
            {
                return error;
            }

            error = _dstack.Pop(out var a);
            if (error != null)
            {
                return error;
            }

            if (a.Length != b.Length)
            {
                return ScriptError.InvalidState(name + " operands must be of equal length");
            }

            var mixed = new byte[a.Length];
            for (var i = 0; i < a.Length; i++)
            {
                if (kind == 0)
                {
                    mixed[i] = (byte)(a[i] & b[i]);
                }
                else if (kind == 1)
                {
                    mixed[i] = (byte)(a[i] | b[i]);
                }
                else
                {
                    mixed[i] = (byte)(a[i] ^ b[i]);
                }
            }

            return _dstack.Push(mixed, meter: true);
        }

        private ScriptError? OpEqual(bool verify)
        {
            var error = _dstack.PopRaw(2, out var pair);
            if (error != null)
            {
                return error;
            }

            var same = pair[0].AsSpan().SequenceEqual(pair[1]);
            if (verify)
            {
                return same ? null : ScriptError.VerifyError;
            }

            return _dstack.PushBool(same, meter: true);
        }

        private ScriptError? Unary(long delta, string message, bool checkedAdd)
        {
            var error = PopI64(out var value);
            if (error != null)
            {
                return error;
            }

            long result;
            try
            {
                checked
                {
                    result = value + delta;
                }
            }
            catch (OverflowException)
            {
                return ScriptError.NumberTooBig(message);
            }

            if (!checkedAdd)
            {
                return ScriptError.InvalidState("internal arithmetic");
            }

            return _dstack.PushNumber(result, meter: true);
        }

        private ScriptError? OpNegate()
        {
            var error = PopI64(out var value);
            if (error != null)
            {
                return error;
            }

            if (value == long.MinValue)
            {
                return ScriptError.NumberTooBig("Negation result exceeds 64-bit signed integer range");
            }

            return _dstack.PushNumber(-value, meter: true);
        }

        private ScriptError? OpAbs()
        {
            var error = PopI64(out var value);
            if (error != null)
            {
                return error;
            }

            if (value == long.MinValue)
            {
                return ScriptError.NumberTooBig("Absolute value exceeds 64-bit signed integer range");
            }

            return _dstack.PushNumber(value < 0 ? -value : value, meter: true);
        }

        private ScriptError? OpNot(bool equalZero)
        {
            var error = PopI64(out var value);
            if (error != null)
            {
                return error;
            }

            var flag = equalZero ? value == 0 : value != 0;
            return _dstack.PushNumber(flag ? 1 : 0, meter: true);
        }

        private ScriptError? BinaryArithmetic(int kind)
        {
            var error = PopI64(2, out var values);
            if (error != null)
            {
                return error;
            }

            var a = values[0];
            var b = values[1];
            long result;
            if (kind == 3 || kind == 4)
            {
                if (b == 0 || (a == long.MinValue && b == -1))
                {
                    var message = kind == 3
                        ? "Quotient exceeds 64-bit signed integer range, or there was a division by zero"
                        : "Illegal modulo by zero";
                    return ScriptError.InvalidState(message);
                }

                result = kind == 3 ? a / b : a % b;
                return _dstack.PushNumber(result, meter: true);
            }

            try
            {
                checked
                {
                    if (kind == 0)
                    {
                        result = a + b;
                    }
                    else if (kind == 1)
                    {
                        result = a - b;
                    }
                    else
                    {
                        result = a * b;
                    }
                }
            }
            catch (OverflowException)
            {
                var message = kind == 0
                    ? "Sum exceeds 64-bit signed integer range"
                    : kind == 1
                        ? "Difference exceeds 64-bit signed integer range"
                        : "Product exceeds 64-bit signed integer range";
                return ScriptError.NumberTooBig(message);
            }

            return _dstack.PushNumber(result, meter: true);
        }

        private ScriptError? BinaryFlag(int kind)
        {
            var error = PopI64(2, out var values);
            if (error != null)
            {
                return error;
            }

            var a = values[0];
            var b = values[1];
            long result;
            switch (kind)
            {
                case 0:
                    result = a != 0 && b != 0 ? 1 : 0;
                    break;
                case 1:
                    result = a != 0 || b != 0 ? 1 : 0;
                    break;
                case 2:
                    result = a == b ? 1 : 0;
                    break;
                case 3:
                    result = a != b ? 1 : 0;
                    break;
                case 4:
                    result = a < b ? 1 : 0;
                    break;
                case 5:
                    result = a > b ? 1 : 0;
                    break;
                case 6:
                    result = a <= b ? 1 : 0;
                    break;
                case 7:
                    result = a >= b ? 1 : 0;
                    break;
                case 8:
                    result = a < b ? a : b;
                    break;
                default:
                    result = a > b ? a : b;
                    break;
            }

            return _dstack.PushNumber(result, meter: true);
        }

        private ScriptError? OpNumEqualVerify()
        {
            var error = PopI64(2, out var values);
            if (error != null)
            {
                return error;
            }

            return values[0] == values[1] ? null : ScriptError.VerifyError;
        }

        private ScriptError? OpWithin()
        {
            var error = PopI64(3, out var values);
            if (error != null)
            {
                return error;
            }

            var inside = values[0] >= values[1] && values[0] < values[2];
            return _dstack.PushNumber(inside ? 1 : 0, meter: true);
        }

        private ScriptError? OpBlake2b(bool keyed)
        {
            byte[] data;
            byte[] key;
            ScriptError? error;
            if (keyed)
            {
                error = _dstack.PopRaw(2, out var items);
                if (error != null)
                {
                    return error;
                }

                data = items[0];
                key = items[1];
                if (key.Length > ScriptLimits.Blake2bMaxKeySize)
                {
                    return ScriptError.ElementTooBig(key.Length, ScriptLimits.Blake2bMaxKeySize);
                }
            }
            else
            {
                error = _dstack.PopRaw(1, out var items);
                if (error != null)
                {
                    return error;
                }

                data = items[0];
                key = Array.Empty<byte>();
            }

            error = _meter.Consume((ulong)data.Length * ScriptLimits.Blake2bUnitsPerByte);
            if (error != null)
            {
                return error;
            }

            return _dstack.Push(Blake2b.ComputeHash(data, 32, key), meter: true);
        }

        private ScriptError? OpSha256()
        {
            var error = _dstack.PopRaw(1, out var items);
            if (error != null)
            {
                return error;
            }

            error = _meter.Consume((ulong)items[0].Length);
            if (error != null)
            {
                return error;
            }

            byte[] digest;
            using (var sha = SHA256.Create())
            {
                digest = sha.ComputeHash(items[0]);
            }

            return _dstack.Push(digest, meter: true);
        }

        private ScriptError? OpBlake3(bool keyed)
        {
            byte[] data;
            byte[] key = Array.Empty<byte>();
            ScriptError? error;
            if (keyed)
            {
                error = _dstack.PopRaw(2, out var items);
                if (error != null)
                {
                    return error;
                }

                data = items[0];
                key = items[1];
                if (key.Length != ScriptLimits.Blake3KeySize)
                {
                    return ScriptError.MalformedPush(ScriptLimits.Blake3KeySize, key.Length);
                }
            }
            else
            {
                error = _dstack.PopRaw(1, out var items);
                if (error != null)
                {
                    return error;
                }

                data = items[0];
            }

            error = _meter.Consume((ulong)data.Length);
            if (error != null)
            {
                return error;
            }

            var digest = keyed ? Blake3.KeyedHash(key, data) : Blake3.Hash(data);
            return _dstack.Push(digest, meter: true);
        }

        private ScriptError? OpCheckSig(bool ecdsa, bool verify)
        {
            var error = _dstack.PopRaw(2, out var items);
            if (error != null)
            {
                return error;
            }

            var signature = items[0];
            var key = items[1];
            if (signature.Length == 0)
            {
                return FinishCheck(false, verify);
            }

            var hashType = signature[signature.Length - 1];
            if (!((SigHashType)hashType).IsDefined())
            {
                return ScriptError.InvalidSigHashType(hashType);
            }

            var bare = new byte[signature.Length - 1];
            Buffer.BlockCopy(signature, 0, bare, 0, bare.Length);
            var check = ecdsa
                ? CheckEcdsa((SigHashType)hashType, key, bare)
                : CheckSchnorr((SigHashType)hashType, key, bare);
            if (check.Error != null)
            {
                return check.Error;
            }

            return FinishCheck(check.Valid, verify);
        }

        private ScriptError? OpCheckSigFromStack(bool ecdsa)
        {
            var error = _dstack.PopRaw(3, out var items);
            if (error != null)
            {
                return error;
            }

            if (items[1].Length != 32)
            {
                return ScriptError.InvalidState("message hash must be 32 bytes");
            }

            var check = ecdsa
                ? CheckEcdsaMessage(items[2], items[0], items[1])
                : CheckSchnorrMessage(items[2], items[0], items[1]);
            if (check.Error != null)
            {
                return check.Error;
            }

            return _dstack.PushBool(check.Valid, meter: true);
        }

        private ScriptError? FinishCheck(bool valid, bool verify)
        {
            var pushed = _dstack.PushBool(valid, meter: true);
            if (pushed != null)
            {
                return pushed;
            }

            if (!verify)
            {
                return null;
            }

            var error = PopBool(out var value);
            if (error != null)
            {
                return error;
            }

            return value ? null : ScriptError.VerifyError;
        }

        private ScriptError? OpCheckMultisig(bool ecdsa, bool verify)
        {
            var error = PopI32(out var numKeys);
            if (error != null)
            {
                return error;
            }

            if (numKeys < 0)
            {
                return ScriptError.InvalidPubKeyCount("number of pubkeys " + numKeys.ToString() + " is negative");
            }

            if (numKeys > ScriptLimits.MaxPubKeysPerMultisig)
            {
                return ScriptError.InvalidPubKeyCount("too many pubkeys " + numKeys.ToString() + " > " + ScriptLimits.MaxPubKeysPerMultisig.ToString());
            }

            _numOps += numKeys;
            if (_numOps > ScriptLimits.MaxOpsPerScript)
            {
                return ScriptError.TooManyOperations;
            }

            error = _dstack.PopRaw(numKeys, out var pubKeys);
            if (error != null)
            {
                return error;
            }

            error = PopI32(out var numSigs);
            if (error != null)
            {
                return error;
            }

            if (numSigs < 0)
            {
                return ScriptError.InvalidSignatureCount("number of signatures " + numSigs.ToString() + " is negative");
            }

            if (numSigs > numKeys)
            {
                return ScriptError.InvalidSignatureCount("more signatures than pubkeys " + numSigs.ToString() + " > " + numKeys.ToString());
            }

            error = _dstack.PopRaw(numSigs, out var signatures);
            if (error != null)
            {
                return error;
            }

            var failed = false;
            var keyIndex = 0;
            for (var sigIndex = 0; sigIndex < signatures.Length; sigIndex++)
            {
                var signature = signatures[sigIndex];
                if (signature.Length == 0)
                {
                    failed = true;
                    break;
                }

                var hashType = signature[signature.Length - 1];
                if (!((SigHashType)hashType).IsDefined())
                {
                    return ScriptError.InvalidSigHashType(hashType);
                }

                var bare = new byte[signature.Length - 1];
                Buffer.BlockCopy(signature, 0, bare, 0, bare.Length);
                while (true)
                {
                    if (pubKeys.Length - keyIndex < signatures.Length - sigIndex)
                    {
                        failed = true;
                        goto finished;
                    }

                    var pubKey = pubKeys[keyIndex++];
                    var check = ecdsa
                        ? CheckEcdsa((SigHashType)hashType, pubKey, bare)
                        : CheckSchnorr((SigHashType)hashType, pubKey, bare);
                    if (check.Error != null)
                    {
                        return check.Error;
                    }

                    if (check.Valid)
                    {
                        break;
                    }
                }
            }

        finished:
            if (failed)
            {
                for (var i = 0; i < signatures.Length; i++)
                {
                    if (signatures[i].Length != 0)
                    {
                        return ScriptError.NullFail;
                    }
                }
            }

            return FinishCheck(!failed, verify);
        }

        private ScriptError? OpCheckLockTime()
        {
            var source = NeedInput("LockTimeVerify only applies to transaction inputs");
            if (source != null)
            {
                return source;
            }

            var error = _dstack.PopRaw(1, out var items);
            if (error != null)
            {
                return error;
            }

            error = ReadLockNumber(items[0], out var stackLock);
            if (error != null)
            {
                return error;
            }

            var txLock = _tx.LockTime;
            var stackIsTime = stackLock >= ScriptLimits.LockTimeThreshold;
            var txIsTime = txLock >= ScriptLimits.LockTimeThreshold;
            if (stackIsTime != txIsTime)
            {
                return ScriptError.UnsatisfiedLockTime("mismatched locktime types");
            }

            if (stackLock > txLock)
            {
                return ScriptError.UnsatisfiedLockTime("locktime requirement not satisfied");
            }

            if (_tx.Inputs[_inputIndex].Sequence == ScriptLimits.MaxTxInSequenceNum)
            {
                return ScriptError.UnsatisfiedLockTime("transaction input is finalized");
            }

            return null;
        }

        private ScriptError? OpCheckSequence()
        {
            var source = NeedInput("LockTimeVerify only applies to transaction inputs");
            if (source != null)
            {
                return source;
            }

            var error = _dstack.PopRaw(1, out var items);
            if (error != null)
            {
                return error;
            }

            error = ReadLockNumber(items[0], out var stackSequence);
            if (error != null)
            {
                return error;
            }

            if ((stackSequence & ScriptLimits.SequenceLockTimeDisabled) != 0)
            {
                return null;
            }

            var inputSequence = _tx.Inputs[_inputIndex].Sequence;
            if ((inputSequence & ScriptLimits.SequenceLockTimeDisabled) != 0)
            {
                return ScriptError.UnsatisfiedLockTime("transaction sequence has sequence locktime disabled bit set");
            }

            if ((stackSequence & ScriptLimits.SequenceLockTimeMask) > (inputSequence & ScriptLimits.SequenceLockTimeMask))
            {
                return ScriptError.UnsatisfiedLockTime("locktime requirement not satisfied");
            }

            return null;
        }

        private ScriptError? OpPayloadSubstr()
        {
            var source = NeedInput("OpTxPayloadSubstr only applies to transaction inputs");
            if (source != null)
            {
                return source;
            }

            var error = PopI32(2, out var bounds);
            if (error != null)
            {
                return error;
            }

            error = ToIndexes(bounds, out var start, out var end);
            if (error != null)
            {
                return error;
            }

            error = Slice(_tx.Payload, start, end, out var piece);
            if (error != null)
            {
                return error;
            }

            return _dstack.Push(piece, meter: true);
        }

        private ScriptError? OpOutpointTxId()
        {
            var error = RequireInput(out var input, "OpOutpointTxId only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            return _dstack.Push(input!.PreviousOutpoint.TransactionId.ToBytes(), meter: true);
        }

        private ScriptError? OpOutpointIndex()
        {
            var error = RequireInput(out var input, "OpOutpointIndex only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            return _dstack.PushNumber(input!.PreviousOutpoint.Index, meter: true);
        }

        private ScriptError? OpScriptSigSubstr()
        {
            var source = NeedInput("OpTxInputScriptSigSubstr only applies to transaction inputs");
            if (source != null)
            {
                return source;
            }

            var error = PopI32(3, out var values);
            if (error != null)
            {
                return error;
            }

            error = ToIndex(values[0], out var index);
            if (error != null)
            {
                return error;
            }

            if ((uint)index >= (uint)_tx.Inputs.Count)
            {
                return ScriptError.InvalidInputIndex(index, _tx.Inputs.Count);
            }

            error = ToIndexes(new[] { values[1], values[2] }, out var start, out var end);
            if (error != null)
            {
                return error;
            }

            error = Slice(_tx.Inputs[index].SignatureScript.ToBytes(), start, end, out var piece);
            if (error != null)
            {
                return error;
            }

            return _dstack.Push(piece, meter: true);
        }

        private ScriptError? OpInputSequence()
        {
            var error = RequireInput(out var input, "OpTxInputSeq only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            return _dstack.Push(Le64(input!.Sequence), meter: true);
        }

        private ScriptError? OpInputAmount()
        {
            var error = UtxoByAmountIndex(out var utxo, inputs: true);
            if (error != null)
            {
                return error;
            }

            if (utxo!.Amount.Sompi > (ulong)long.MaxValue)
            {
                return ScriptError.NumberTooBig("amount does not fit an i64");
            }

            return _dstack.PushNumber((long)utxo.Amount.Sompi, meter: true);
        }

        private ScriptError? OpInputSpk(bool lengthOnly, bool substring)
        {
            var message = substring
                ? "OpTxInputSpkSubstr only applies to transaction inputs"
                : lengthOnly
                    ? "OpTxInputSpkLen only applies to transaction inputs"
                    : "OpInputSpk only applies to transaction inputs";
            UtxoEntry? utxo;
            ScriptError? error;
            if (substring)
            {
                error = NeedInput(message);
                if (error != null)
                {
                    return error;
                }

                error = PopI32(3, out var values);
                if (error != null)
                {
                    return error;
                }

                error = UtxoAt(values[0], out utxo);
                if (error != null)
                {
                    return error;
                }

                error = ToIndexes(new[] { values[1], values[2] }, out var start, out var end);
                if (error != null)
                {
                    return error;
                }

                var encoded = EncodeSpk(utxo!.ScriptPublicKey);
                error = Slice(encoded, start, end, out var piece);
                if (error != null)
                {
                    return error;
                }

                return _dstack.Push(piece, meter: true);
            }

            if (!lengthOnly)
            {
                error = UtxoByAmountIndex(out utxo, inputs: true);
            }
            else
            {
                error = NeedInput(message);
                if (error != null)
                {
                    return error;
                }

                error = PopI32(out var raw);
                if (error != null)
                {
                    return error;
                }

                error = UtxoAt(raw, out utxo);
            }

            if (error != null)
            {
                return error;
            }

            var bytes = EncodeSpk(utxo!.ScriptPublicKey);
            if (lengthOnly)
            {
                return _dstack.PushNumber(bytes.Length, meter: true);
            }

            return _dstack.Push(bytes, meter: true);
        }

        private ScriptError? OpInputDaa()
        {
            var error = NeedInput("OpTxInputDaaScore only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            error = UtxoAt(raw, out var utxo);
            if (error != null)
            {
                return error;
            }

            return _dstack.PushNumber(unchecked((long)utxo!.BlockDaaScore), meter: true);
        }

        private ScriptError? OpInputCoinbase()
        {
            var error = NeedInput("OpTxInputIsCoinbase only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            error = UtxoAt(raw, out var utxo);
            if (error != null)
            {
                return error;
            }

            return _dstack.PushNumber(utxo!.IsCoinbase ? 1 : 0, meter: true);
        }

        private ScriptError? OpOutputAmount()
        {
            var error = OutputByAmountIndex(out var output);
            if (error != null)
            {
                return error;
            }

            if (output!.Value.Sompi > (ulong)long.MaxValue)
            {
                return ScriptError.NumberTooBig("amount does not fit an i64");
            }

            return _dstack.PushNumber((long)output.Value.Sompi, meter: true);
        }

        private ScriptError? OpOutputSpk(bool lengthOnly, bool substring)
        {
            var message = substring
                ? "OpTxOutputSpkSubstr only applies to transaction inputs"
                : lengthOnly
                    ? "OpTxOutputSpkLen only applies to transaction inputs"
                    : "OpTxOutputSpk only applies to transaction inputs";
            var source = NeedInput(message);
            if (source != null)
            {
                return source;
            }

            if (substring)
            {
                var error = PopI32(3, out var values);
                if (error != null)
                {
                    return error;
                }

                error = OutputAt(values[0], out var output);
                if (error != null)
                {
                    return error;
                }

                error = ToIndexes(new[] { values[1], values[2] }, out var start, out var end);
                if (error != null)
                {
                    return error;
                }

                var encoded = EncodeSpk(output!.ScriptPublicKey);
                error = Slice(encoded, start, end, out var piece);
                if (error != null)
                {
                    return error;
                }

                return _dstack.Push(piece, meter: true);
            }

            if (!lengthOnly)
            {
                var error = OutputByAmountIndex(out var output);
                if (error != null)
                {
                    return error;
                }

                return _dstack.Push(EncodeSpk(output!.ScriptPublicKey), meter: true);
            }

            var popped = PopI32(out var raw);
            if (popped != null)
            {
                return popped;
            }

            popped = OutputAt(raw, out var indexed);
            if (popped != null)
            {
                return popped;
            }

            return _dstack.PushNumber(EncodeSpk(indexed!.ScriptPublicKey).Length, meter: true);
        }

        private ScriptError? OpScriptSigLen()
        {
            var error = RequireInput(out var input, "OpTxInputScriptSigLen only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            return _dstack.PushNumber(input!.SignatureScript.ToBytes().Length, meter: true);
        }

        private ScriptError? OpAuthOutputCount()
        {
            var source = NeedInput("OpAuthOutputCount only applies to transaction inputs");
            if (source != null)
            {
                return source;
            }

            var error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            error = ToIndex(raw, out var index);
            if (error != null)
            {
                return error;
            }

            if ((uint)index >= (uint)_tx.Inputs.Count)
            {
                return ScriptError.InvalidInputIndex(index, _tx.Inputs.Count);
            }

            return _dstack.PushNumber(_covenants.AuthOutputCount(index), meter: true);
        }

        private ScriptError? OpAuthOutputIdx()
        {
            var source = NeedInput("OpAuthOutputIdx only applies to transaction inputs");
            if (source != null)
            {
                return source;
            }

            var error = PopI32(2, out var values);
            if (error != null)
            {
                return error;
            }

            error = ToIndex(values[0], out var inputIndex);
            if (error != null)
            {
                return error;
            }

            error = ToIndex(values[1], out var k);
            if (error != null)
            {
                return error;
            }

            if ((uint)inputIndex >= (uint)_tx.Inputs.Count)
            {
                return ScriptError.InvalidInputIndex(inputIndex, _tx.Inputs.Count);
            }

            error = _covenants.AuthOutputIndex(inputIndex, k, out var outputIndex);
            if (error != null)
            {
                return error;
            }

            return _dstack.PushNumber(outputIndex, meter: true);
        }

        private ScriptError? OpNum2Bin()
        {
            var error = PopI32(out var size);
            if (error != null)
            {
                return error;
            }

            error = ToIndex(size, out var width);
            if (error != null)
            {
                return error;
            }

            if (width > 8)
            {
                return ScriptError.NotMinimalData("NUM2BIN target size " + width.ToString() + " exceeds 8 bytes");
            }

            error = PopI64(out var number);
            if (error != null)
            {
                return error;
            }

            error = ScriptNum.TrySerializeFixed(number, width, out var encoded);
            if (error != null)
            {
                return error;
            }

            return _dstack.Push(encoded, meter: true);
        }

        private ScriptError? OpBin2Num()
        {
            var error = PopI64(out var number);
            if (error != null)
            {
                return error;
            }

            return _dstack.PushNumber(number, meter: true);
        }

        private ScriptError? OpInputCovenantId()
        {
            var error = NeedInput("OpInputCovenantId only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            error = UtxoAt(raw, out var utxo);
            if (error != null)
            {
                return error;
            }

            var id = utxo!.CovenantId ?? uint256.Zero;
            return _dstack.Push(id.ToBytes(), meter: true);
        }

        private ScriptError? OpCovCount(bool outputs)
        {
            var message = outputs
                ? "OpCovOutputCount only applies to transaction inputs"
                : "OpCovInputCount only applies to transaction inputs";
            var source = NeedInput(message);
            if (source != null)
            {
                return source;
            }

            var error = PopHash(out var id);
            if (error != null)
            {
                return error;
            }

            var count = outputs ? _covenants.CovenantOutputCount(id) : _covenants.CovenantInputCount(id);
            return _dstack.PushNumber(count, meter: true);
        }

        private ScriptError? OpCovIndex(bool outputs)
        {
            var message = outputs
                ? "OpCovOutputIdx only applies to transaction inputs"
                : "OpCovInputIdx only applies to transaction inputs";
            var source = NeedInput(message);
            if (source != null)
            {
                return source;
            }

            var error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            error = ToIndex(raw, out var k);
            if (error != null)
            {
                return error;
            }

            error = PopHash(out var id);
            if (error != null)
            {
                return error;
            }

            int index;
            error = outputs
                ? _covenants.CovenantOutputIndex(id, k, out index)
                : _covenants.CovenantInputIndex(id, k, out index);
            if (error != null)
            {
                return error;
            }

            return _dstack.PushNumber(index, meter: true);
        }

        private ScriptError? OpChainBlock()
        {
            if (_seq == null)
            {
                return ScriptError.InvalidOpcode((byte)OpcodeType.OP_CHAINBLOCKSEQCOMMIT);
            }

            var error = PopHash(out var block);
            if (error != null)
            {
                return error;
            }

            var ancestor = _seq.IsChainAncestor(block);
            if (ancestor == null)
            {
                return ScriptError.BlockAlreadyPruned();
            }

            if (ancestor == false)
            {
                return ScriptError.BlockNotSelected();
            }

            var commitment = _seq.SeqCommitmentWithinDepth(block);
            if (commitment == null)
            {
                return ScriptError.BlockIsTooDeep();
            }

            return _dstack.Push(commitment.ToBytes(), meter: true);
        }

        private ScriptError? OpOutputCovenantId()
        {
            var error = NeedInput("OpOutputCovenantId only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            error = OutputAt(raw, out var output);
            if (error != null)
            {
                return error;
            }

            var id = output!.Covenant == null ? uint256.Zero : output.Covenant.CovenantId;
            return _dstack.Push(id.ToBytes(), meter: true);
        }

        private ScriptError? OpOutputAuthorizing()
        {
            var error = NeedInput("OpOutputAuthorizingInput only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            error = OutputAt(raw, out var output);
            if (error != null)
            {
                return error;
            }

            var authorizing = output!.Covenant == null ? -1L : output.Covenant.AuthorizingInput;
            return _dstack.PushNumber(authorizing, meter: true);
        }

        private ScriptError? PushTxNumber(long value, string sourceMessage)
        {
            var error = NeedInput(sourceMessage);
            if (error != null)
            {
                return error;
            }

            return _dstack.PushNumber(value, meter: true);
        }

        private ScriptError? PushTxBytes(byte[] value, string sourceMessage)
        {
            var error = NeedInput(sourceMessage);
            if (error != null)
            {
                return error;
            }

            return _dstack.Push(value, meter: true);
        }

        private ScriptError? NeedInput(string message)
        {
            if (!_hasInput)
            {
                return ScriptError.InvalidSource(message);
            }

            return null;
        }

        private ScriptError? RequireInput(out TxIn? input, string message)
        {
            input = null;
            var error = NeedInput(message);
            if (error != null)
            {
                return error;
            }

            error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            error = ToIndex(raw, out var index);
            if (error != null)
            {
                return error;
            }

            if ((uint)index >= (uint)_tx.Inputs.Count)
            {
                return ScriptError.InvalidInputIndex(index, _tx.Inputs.Count);
            }

            input = _tx.Inputs[index];
            return null;
        }

        private ScriptError? UtxoByAmountIndex(out UtxoEntry? utxo, bool inputs)
        {
            utxo = null;
            var error = NeedInput(inputs
                ? "OpInputAmount only applies to transaction inputs"
                : "OpTxOutputAmount only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            if (raw < 0 || (uint)raw >= (uint)_tx.Inputs.Count || (uint)raw >= (uint)_utxos.Length || _utxos[raw] == null)
            {
                return ScriptError.InvalidInputIndex(raw, _tx.Inputs.Count);
            }

            utxo = _utxos[raw];
            return null;
        }

        private ScriptError? OutputByAmountIndex(out TxOut? output)
        {
            output = null;
            var error = NeedInput("OpTxOutputAmount only applies to transaction inputs");
            if (error != null)
            {
                return error;
            }

            error = PopI32(out var raw);
            if (error != null)
            {
                return error;
            }

            if (raw < 0 || (uint)raw >= (uint)_tx.Outputs.Count)
            {
                return ScriptError.InvalidOutputIndex(raw, _tx.Outputs.Count);
            }

            output = _tx.Outputs[raw];
            return null;
        }

        private ScriptError? UtxoAt(int raw, out UtxoEntry? utxo)
        {
            utxo = null;
            var error = ToIndex(raw, out var index);
            if (error != null)
            {
                return error;
            }

            if ((uint)index >= (uint)_tx.Inputs.Count || (uint)index >= (uint)_utxos.Length || _utxos[index] == null)
            {
                return ScriptError.InvalidInputIndex(index, _tx.Inputs.Count);
            }

            utxo = _utxos[index];
            return null;
        }

        private ScriptError? OutputAt(int raw, out TxOut? output)
        {
            output = null;
            var error = ToIndex(raw, out var index);
            if (error != null)
            {
                return error;
            }

            if ((uint)index >= (uint)_tx.Outputs.Count)
            {
                return ScriptError.InvalidOutputIndex(index, _tx.Outputs.Count);
            }

            output = _tx.Outputs[index];
            return null;
        }

        private ScriptError? PopBool(out bool value)
        {
            var error = _dstack.PopRaw(1, out var items);
            if (error != null)
            {
                value = false;
                return error;
            }

            value = ScriptNum.AsBool(items[0]);
            return null;
        }

        private ScriptError? PopI64(out long value)
        {
            var error = PopI64(1, out var values);
            value = error == null ? values[0] : 0;
            return error;
        }

        private ScriptError? PopI64(int count, out long[] values)
        {
            values = new long[count];
            var error = _dstack.PopRaw(count, out var items);
            if (error != null)
            {
                return error;
            }

            for (var i = 0; i < count; i++)
            {
                error = ScriptNum.TryDeserializeI64(items[i], out values[i]);
                if (error != null)
                {
                    return error;
                }
            }

            return null;
        }

        private ScriptError? PopI32(out int value)
        {
            var error = PopI32(1, out var values);
            value = error == null ? values[0] : 0;
            return error;
        }

        private ScriptError? PopI32(int count, out int[] values)
        {
            values = new int[count];
            var error = _dstack.PopRaw(count, out var items);
            if (error != null)
            {
                return error;
            }

            for (var i = 0; i < count; i++)
            {
                error = ScriptNum.TryDeserializeI32(items[i], out values[i]);
                if (error != null)
                {
                    return error;
                }
            }

            return null;
        }

        private ScriptError? PopHash(out uint256 hash)
        {
            var error = _dstack.PopRaw(1, out var items);
            if (error != null)
            {
                hash = uint256.Zero;
                return error;
            }

            if (items[0].Length != uint256.Length)
            {
                hash = uint256.Zero;
                return ScriptError.InvalidLengthOfBlockHash(items[0].Length);
            }

            hash = new uint256(items[0]);
            return null;
        }

        private static ScriptError? ToIndex(int value, out int index)
        {
            if (value < 0)
            {
                index = 0;
                return ScriptError.InvalidIndex(value);
            }

            index = value;
            return null;
        }

        private static ScriptError? ToIndexes(int[] values, out int start, out int end)
        {
            start = 0;
            end = 0;
            var error = ToIndex(values[0], out start);
            if (error != null)
            {
                return error;
            }

            return ToIndex(values[1], out end);
        }

        private static ScriptError? Slice(byte[] data, int start, int end, out byte[] piece)
        {
            piece = Array.Empty<byte>();
            if (end < start)
            {
                return ScriptError.InvalidRange(start, end);
            }

            var length = (long)end - start;
            if (length > ScriptLimits.MaxScriptElementSize)
            {
                return ScriptError.ElementTooBig((int)length, ScriptLimits.MaxScriptElementSize);
            }

            if ((uint)start > (uint)data.Length || (uint)end > (uint)data.Length)
            {
                return ScriptError.OutOfBoundsSubstring(start, end, data.Length);
            }

            piece = new byte[length];
            if (length > 0)
            {
                Buffer.BlockCopy(data, start, piece, 0, (int)length);
            }

            return null;
        }

        private static ScriptError? ReadLockNumber(byte[] raw, out ulong value)
        {
            if (raw.Length > 8)
            {
                value = 0;
                return ScriptError.NumberTooBig("lockTime value is longer than 8 bytes");
            }

            value = 0;
            for (var i = 0; i < raw.Length; i++)
            {
                value |= (ulong)raw[i] << (8 * i);
            }

            return null;
        }

        private static byte[] Le64(ulong value)
        {
            var bytes = new byte[8];
            for (var i = 0; i < 8; i++)
            {
                bytes[i] = (byte)(value & 0xff);
                value >>= 8;
            }

            return bytes;
        }

        private static byte[] EncodeSpk(ScriptPubKey scriptPubKey)
        {
            var script = scriptPubKey.Script;
            var bytes = new byte[2 + script.Length];
            bytes[0] = (byte)(scriptPubKey.Version >> 8);
            bytes[1] = (byte)scriptPubKey.Version;
            Buffer.BlockCopy(script, 0, bytes, 2, script.Length);
            return bytes;
        }
    }
}
