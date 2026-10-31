using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// One parsed script instruction. Kaspa counterpart of NBitcoin.Op.
    /// </summary>
    public sealed class Op
    {
        /// <summary>Creates an instruction. <paramref name="data"/> is the pushed bytes, or null when the opcode pushes nothing.</summary>
        public Op(OpcodeType code, byte[]? data)
        {
            Code = code;
            if (data != null)
            {
                Data = (byte[])data.Clone();
            }
        }

        /// <summary>Opcode byte.</summary>
        public OpcodeType Code { get; }

        /// <summary>
        /// Bytes pushed by a data opcode. Null for opcodes that are not data pushes.
        /// Small integers (<see cref="OpcodeType.OP_0"/>, <see cref="OpcodeType.OP_1NEGATE"/>, <see cref="OpcodeType.OP_1"/> through <see cref="OpcodeType.OP_16"/>) have no data bytes.
        /// </summary>
        public byte[]? Data { get; }

        /// <summary>True when this opcode is disabled by Kaspa, the same set Bitcoin disables among the bitwise and shift opcodes.</summary>
        public bool IsDisabled
        {
            get { return Code.IsDisabled(); }
        }

        /// <summary>
        /// The small integer this opcode pushes, when it is <see cref="OpcodeType.OP_0"/>,
        /// <see cref="OpcodeType.OP_1NEGATE"/>, or <see cref="OpcodeType.OP_1"/> through <see cref="OpcodeType.OP_16"/>.
        /// </summary>
        public bool TryGetSmallInt(out long value)
        {
            if (Code == OpcodeType.OP_0)
            {
                value = 0;
                return true;
            }

            if (Code == OpcodeType.OP_1NEGATE)
            {
                value = -1;
                return true;
            }

            var code = (byte)Code;
            if (code >= (byte)OpcodeType.OP_1 && code <= (byte)OpcodeType.OP_16)
            {
                value = code - (byte)OpcodeType.OP_1 + 1;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>Opcode name, then the pushed data as lowercase hex when this is a data push.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public override string ToString()
        {
            var name = Code.ToString();
            if (Data == null || Data.Length == 0)
            {
                return name;
            }

            return name + " " + Encoders.Hex.EncodeData(Data);
        }
    }

    /// <summary>Classifies <see cref="OpcodeType"/> values.</summary>
    public static class OpcodeTypeExtensions
    {
        /// <summary>
        /// True for the opcodes Kaspa leaves disabled:
        /// <see cref="OpcodeType.OP_LEFT"/>, <see cref="OpcodeType.OP_RIGHT"/>,
        /// <see cref="OpcodeType.OP_2MUL"/>, <see cref="OpcodeType.OP_2DIV"/>,
        /// <see cref="OpcodeType.OP_LSHIFT"/>, and <see cref="OpcodeType.OP_RSHIFT"/>.
        /// <see cref="OpcodeType.OP_CAT"/>, <see cref="OpcodeType.OP_SUBSTR"/>, and the bitwise opcodes are enabled.
        /// </summary>
        public static bool IsDisabled(this OpcodeType code)
        {
            return code == OpcodeType.OP_LEFT
                || code == OpcodeType.OP_RIGHT
                || code == OpcodeType.OP_2MUL
                || code == OpcodeType.OP_2DIV
                || code == OpcodeType.OP_LSHIFT
                || code == OpcodeType.OP_RSHIFT;
        }
    }
}
