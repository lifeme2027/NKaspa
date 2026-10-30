using System.Globalization;

namespace NKaspa
{
    /// <summary>Rusty-kaspa opcode type names used by <c>script_to_str</c>.</summary>
    internal static class OpcodeRust
    {
        internal static string Name(byte code)
        {
            if (code >= 1 && code <= 75)
            {
                return "OpData" + code.ToString(CultureInfo.InvariantCulture);
            }

            switch (code)
            {
                case 0x00: return "OpFalse";
                case 0x4c: return "OpPushData1";
                case 0x4d: return "OpPushData2";
                case 0x4e: return "OpPushData4";
                case 0x4f: return "Op1Negate";
                case 0x50: return "OpReserved";
                case 0x51: return "OpTrue";
                case 0x52: return "Op2";
                case 0x53: return "Op3";
                case 0x54: return "Op4";
                case 0x55: return "Op5";
                case 0x56: return "Op6";
                case 0x57: return "Op7";
                case 0x58: return "Op8";
                case 0x59: return "Op9";
                case 0x5a: return "Op10";
                case 0x5b: return "Op11";
                case 0x5c: return "Op12";
                case 0x5d: return "Op13";
                case 0x5e: return "Op14";
                case 0x5f: return "Op15";
                case 0x60: return "Op16";
                case 0x61: return "OpNop";
                case 0x62: return "OpVer";
                case 0x63: return "OpIf";
                case 0x64: return "OpNotIf";
                case 0x65: return "OpVerIf";
                case 0x66: return "OpVerNotIf";
                case 0x67: return "OpElse";
                case 0x68: return "OpEndIf";
                case 0x69: return "OpVerify";
                case 0x6a: return "OpReturn";
                case 0x6b: return "OpToAltStack";
                case 0x6c: return "OpFromAltStack";
                case 0x6d: return "Op2Drop";
                case 0x6e: return "Op2Dup";
                case 0x6f: return "Op3Dup";
                case 0x70: return "Op2Over";
                case 0x71: return "Op2Rot";
                case 0x72: return "Op2Swap";
                case 0x73: return "OpIfDup";
                case 0x74: return "OpDepth";
                case 0x75: return "OpDrop";
                case 0x76: return "OpDup";
                case 0x77: return "OpNip";
                case 0x78: return "OpOver";
                case 0x79: return "OpPick";
                case 0x7a: return "OpRoll";
                case 0x7b: return "OpRot";
                case 0x7c: return "OpSwap";
                case 0x7d: return "OpTuck";
                case 0x7e: return "OpCat";
                case 0x7f: return "OpSubstr";
                case 0x80: return "OpLeft";
                case 0x81: return "OpRight";
                case 0x82: return "OpSize";
                case 0x83: return "OpInvert";
                case 0x84: return "OpAnd";
                case 0x85: return "OpOr";
                case 0x86: return "OpXor";
                case 0x87: return "OpEqual";
                case 0x88: return "OpEqualVerify";
                case 0x89: return "OpReserved1";
                case 0x8a: return "OpReserved2";
                case 0x8b: return "Op1Add";
                case 0x8c: return "Op1Sub";
                case 0x8d: return "Op2Mul";
                case 0x8e: return "Op2Div";
                case 0x8f: return "OpNegate";
                case 0x90: return "OpAbs";
                case 0x91: return "OpNot";
                case 0x92: return "Op0NotEqual";
                case 0x93: return "OpAdd";
                case 0x94: return "OpSub";
                case 0x95: return "OpMul";
                case 0x96: return "OpDiv";
                case 0x97: return "OpMod";
                case 0x98: return "OpLShift";
                case 0x99: return "OpRShift";
                case 0x9a: return "OpBoolAnd";
                case 0x9b: return "OpBoolOr";
                case 0x9c: return "OpNumEqual";
                case 0x9d: return "OpNumEqualVerify";
                case 0x9e: return "OpNumNotEqual";
                case 0x9f: return "OpLessThan";
                case 0xa0: return "OpGreaterThan";
                case 0xa1: return "OpLessThanOrEqual";
                case 0xa2: return "OpGreaterThanOrEqual";
                case 0xa3: return "OpMin";
                case 0xa4: return "OpMax";
                case 0xa5: return "OpWithin";
                case 0xa6: return "OpZkPrecompile";
                case 0xa7: return "OpBlake2bWithKey";
                case 0xa8: return "OpSHA256";
                case 0xa9: return "OpCheckMultiSigECDSA";
                case 0xaa: return "OpBlake2b";
                case 0xab: return "OpCheckSigECDSA";
                case 0xac: return "OpCheckSig";
                case 0xad: return "OpCheckSigVerify";
                case 0xae: return "OpCheckMultiSig";
                case 0xaf: return "OpCheckMultiSigVerify";
                case 0xb0: return "OpCheckLockTimeVerify";
                case 0xb1: return "OpCheckSequenceVerify";
                case 0xb2: return "OpTxVersion";
                case 0xb3: return "OpTxInputCount";
                case 0xb4: return "OpTxOutputCount";
                case 0xb5: return "OpTxLockTime";
                case 0xb6: return "OpTxSubnetId";
                case 0xb7: return "OpTxGas";
                case 0xb8: return "OpTxPayloadSubstr";
                case 0xb9: return "OpTxInputIndex";
                case 0xba: return "OpOutpointTxId";
                case 0xbb: return "OpOutpointIndex";
                case 0xbc: return "OpTxInputScriptSigSubstr";
                case 0xbd: return "OpTxInputSeq";
                case 0xbe: return "OpTxInputAmount";
                case 0xbf: return "OpTxInputSpk";
                case 0xc0: return "OpTxInputDaaScore";
                case 0xc1: return "OpTxInputIsCoinbase";
                case 0xc2: return "OpTxOutputAmount";
                case 0xc3: return "OpTxOutputSpk";
                case 0xc4: return "OpTxPayloadLen";
                case 0xc5: return "OpTxInputSpkLen";
                case 0xc6: return "OpTxInputSpkSubstr";
                case 0xc7: return "OpTxOutputSpkLen";
                case 0xc8: return "OpTxOutputSpkSubstr";
                case 0xc9: return "OpTxInputScriptSigLen";
                case 0xca: return "OpUnknown202";
                case 0xcb: return "OpAuthOutputCount";
                case 0xcc: return "OpAuthOutputIdx";
                case 0xcd: return "OpNum2Bin";
                case 0xce: return "OpBin2Num";
                case 0xcf: return "OpInputCovenantId";
                case 0xd0: return "OpCovInputCount";
                case 0xd1: return "OpCovInputIdx";
                case 0xd2: return "OpCovOutputCount";
                case 0xd3: return "OpCovOutputIdx";
                case 0xd4: return "OpChainblockSeqCommit";
                case 0xd5: return "OpOutputCovenantId";
                case 0xd6: return "OpOutputAuthorizingInput";
                case 0xd7: return "OpCheckSigFromStack";
                case 0xd8: return "OpCheckSigFromStackECDSA";
                case 0xd9: return "OpBlake3";
                case 0xda: return "OpBlake3WithKey";
                case 0xfa: return "OpSmallInteger";
                case 0xfb: return "OpPubKeys";
                case 0xfc: return "OpUnknown252";
                case 0xfd: return "OpPubKeyHash";
                case 0xfe: return "OpPubKey";
                case 0xff: return "OpInvalidOpCode";
                default:
                    return "OpUnknown" + code.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
