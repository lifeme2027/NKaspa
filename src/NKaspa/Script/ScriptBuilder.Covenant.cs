namespace NKaspa
{
    public sealed partial class ScriptBuilder
    {
        /// <summary>Appends an introspection opcode <c>0xb2</c>–<c>0xc9</c> or a covenant opcode <c>0xcb</c>–<c>0xd6</c>.</summary>
        public ScriptBuilder AddCovenantOp(OpcodeType opcode)
        {
            var code = (byte)opcode;
            var introspection = code >= (byte)OpcodeType.OP_TXVERSION && code <= (byte)OpcodeType.OP_TXINPUTSCRIPTSIGLEN;
            var covenant = code >= (byte)OpcodeType.OP_AUTHOUTPUTCOUNT && code <= (byte)OpcodeType.OP_OUTPUTAUTHORIZINGINPUT;
            if (!introspection && !covenant)
            {
                throw new ArgumentException("Opcode is not an introspection or covenant opcode.", nameof(opcode));
            }

            return AddOp(opcode);
        }

        /// <summary><c>OP_TXVERSION</c> (0xb2).</summary>
        public ScriptBuilder TxVersion()
        {
            return AddOp(OpcodeType.OP_TXVERSION);
        }

        /// <summary><c>OP_TXINPUTCOUNT</c> (0xb3).</summary>
        public ScriptBuilder TxInputCount()
        {
            return AddOp(OpcodeType.OP_TXINPUTCOUNT);
        }

        /// <summary><c>OP_TXOUTPUTCOUNT</c> (0xb4).</summary>
        public ScriptBuilder TxOutputCount()
        {
            return AddOp(OpcodeType.OP_TXOUTPUTCOUNT);
        }

        /// <summary><c>OP_TXLOCKTIME</c> (0xb5).</summary>
        public ScriptBuilder TxLockTime()
        {
            return AddOp(OpcodeType.OP_TXLOCKTIME);
        }

        /// <summary><c>OP_TXSUBNETID</c> (0xb6).</summary>
        public ScriptBuilder TxSubnetId()
        {
            return AddOp(OpcodeType.OP_TXSUBNETID);
        }

        /// <summary><c>OP_TXGAS</c> (0xb7).</summary>
        public ScriptBuilder TxGas()
        {
            return AddOp(OpcodeType.OP_TXGAS);
        }

        /// <summary><c>OP_TXPAYLOADSUBSTR</c> (0xb8). Pops end and start, then pushes the payload slice.</summary>
        public ScriptBuilder TxPayloadSubstr()
        {
            return AddOp(OpcodeType.OP_TXPAYLOADSUBSTR);
        }

        /// <summary><c>OP_TXINPUTINDEX</c> (0xb9).</summary>
        public ScriptBuilder TxInputIndex()
        {
            return AddOp(OpcodeType.OP_TXINPUTINDEX);
        }

        /// <summary><c>OP_OUTPOINTTXID</c> (0xba). Pops an input index.</summary>
        public ScriptBuilder OutpointTxId()
        {
            return AddOp(OpcodeType.OP_OUTPOINTTXID);
        }

        /// <summary><c>OP_OUTPOINTINDEX</c> (0xbb). Pops an input index.</summary>
        public ScriptBuilder OutpointIndex()
        {
            return AddOp(OpcodeType.OP_OUTPOINTINDEX);
        }

        /// <summary><c>OP_TXINPUTSCRIPTSIGSUBSTR</c> (0xbc).</summary>
        public ScriptBuilder TxInputScriptSigSubstr()
        {
            return AddOp(OpcodeType.OP_TXINPUTSCRIPTSIGSUBSTR);
        }

        /// <summary><c>OP_TXINPUTSEQ</c> (0xbd). Pops an input index.</summary>
        public ScriptBuilder TxInputSeq()
        {
            return AddOp(OpcodeType.OP_TXINPUTSEQ);
        }

        /// <summary><c>OP_TXINPUTAMOUNT</c> (0xbe). Pops an input index.</summary>
        public ScriptBuilder TxInputAmount()
        {
            return AddOp(OpcodeType.OP_TXINPUTAMOUNT);
        }

        /// <summary><c>OP_TXINPUTSPK</c> (0xbf). Pops an input index.</summary>
        public ScriptBuilder TxInputSpk()
        {
            return AddOp(OpcodeType.OP_TXINPUTSPK);
        }

        /// <summary><c>OP_TXINPUTDAASCORE</c> (0xc0). Pops an input index.</summary>
        public ScriptBuilder TxInputDaaScore()
        {
            return AddOp(OpcodeType.OP_TXINPUTDAASCORE);
        }

        /// <summary><c>OP_TXINPUTISCOINBASE</c> (0xc1). Pops an input index.</summary>
        public ScriptBuilder TxInputIsCoinbase()
        {
            return AddOp(OpcodeType.OP_TXINPUTISCOINBASE);
        }

        /// <summary><c>OP_TXOUTPUTAMOUNT</c> (0xc2). Pops an output index.</summary>
        public ScriptBuilder TxOutputAmount()
        {
            return AddOp(OpcodeType.OP_TXOUTPUTAMOUNT);
        }

        /// <summary><c>OP_TXOUTPUTSPK</c> (0xc3). Pops an output index.</summary>
        public ScriptBuilder TxOutputSpk()
        {
            return AddOp(OpcodeType.OP_TXOUTPUTSPK);
        }

        /// <summary><c>OP_TXPAYLOADLEN</c> (0xc4).</summary>
        public ScriptBuilder TxPayloadLen()
        {
            return AddOp(OpcodeType.OP_TXPAYLOADLEN);
        }

        /// <summary><c>OP_TXINPUTSPKLEN</c> (0xc5). Pops an input index.</summary>
        public ScriptBuilder TxInputSpkLen()
        {
            return AddOp(OpcodeType.OP_TXINPUTSPKLEN);
        }

        /// <summary><c>OP_TXINPUTSPKSUBSTR</c> (0xc6).</summary>
        public ScriptBuilder TxInputSpkSubstr()
        {
            return AddOp(OpcodeType.OP_TXINPUTSPKSUBSTR);
        }

        /// <summary><c>OP_TXOUTPUTSPKLEN</c> (0xc7). Pops an output index.</summary>
        public ScriptBuilder TxOutputSpkLen()
        {
            return AddOp(OpcodeType.OP_TXOUTPUTSPKLEN);
        }

        /// <summary><c>OP_TXOUTPUTSPKSUBSTR</c> (0xc8).</summary>
        public ScriptBuilder TxOutputSpkSubstr()
        {
            return AddOp(OpcodeType.OP_TXOUTPUTSPKSUBSTR);
        }

        /// <summary><c>OP_TXINPUTSCRIPTSIGLEN</c> (0xc9). Pops an input index.</summary>
        public ScriptBuilder TxInputScriptSigLen()
        {
            return AddOp(OpcodeType.OP_TXINPUTSCRIPTSIGLEN);
        }

        /// <summary><c>OP_AUTHOUTPUTCOUNT</c> (0xcb). Pops a covenant id.</summary>
        public ScriptBuilder AuthOutputCount()
        {
            return AddOp(OpcodeType.OP_AUTHOUTPUTCOUNT);
        }

        /// <summary><c>OP_AUTHOUTPUTIDX</c> (0xcc).</summary>
        public ScriptBuilder AuthOutputIdx()
        {
            return AddOp(OpcodeType.OP_AUTHOUTPUTIDX);
        }

        /// <summary><c>OP_NUM2BIN</c> (0xcd). Pops the size, then the number.</summary>
        public ScriptBuilder Num2Bin()
        {
            return AddOp(OpcodeType.OP_NUM2BIN);
        }

        /// <summary><c>OP_BIN2NUM</c> (0xce).</summary>
        public ScriptBuilder Bin2Num()
        {
            return AddOp(OpcodeType.OP_BIN2NUM);
        }

        /// <summary><c>OP_INPUTCOVENANTID</c> (0xcf). Pops an input index, then pushes the 32-byte covenant id or 32 zero bytes.</summary>
        public ScriptBuilder InputCovenantId()
        {
            return AddOp(OpcodeType.OP_INPUTCOVENANTID);
        }

        /// <summary><c>OP_COVINPUTCOUNT</c> (0xd0). Pops a covenant id.</summary>
        public ScriptBuilder CovInputCount()
        {
            return AddOp(OpcodeType.OP_COVINPUTCOUNT);
        }

        /// <summary><c>OP_COVINPUTIDX</c> (0xd1).</summary>
        public ScriptBuilder CovInputIdx()
        {
            return AddOp(OpcodeType.OP_COVINPUTIDX);
        }

        /// <summary><c>OP_COVOUTPUTCOUNT</c> (0xd2). Pops a covenant id.</summary>
        public ScriptBuilder CovOutputCount()
        {
            return AddOp(OpcodeType.OP_COVOUTPUTCOUNT);
        }

        /// <summary><c>OP_COVOUTPUTIDX</c> (0xd3).</summary>
        public ScriptBuilder CovOutputIdx()
        {
            return AddOp(OpcodeType.OP_COVOUTPUTIDX);
        }

        /// <summary><c>OP_CHAINBLOCKSEQCOMMIT</c> (0xd4). Pops a 32-byte block hash. Requires <see cref="ISeqCommitAccessor"/>.</summary>
        public ScriptBuilder ChainBlockSeqCommit()
        {
            return AddOp(OpcodeType.OP_CHAINBLOCKSEQCOMMIT);
        }

        /// <summary><c>OP_OUTPUTCOVENANTID</c> (0xd5). Pops an output index.</summary>
        public ScriptBuilder OutputCovenantId()
        {
            return AddOp(OpcodeType.OP_OUTPUTCOVENANTID);
        }

        /// <summary><c>OP_OUTPUTAUTHORIZINGINPUT</c> (0xd6). Pops an output index.</summary>
        public ScriptBuilder OutputAuthorizingInput()
        {
            return AddOp(OpcodeType.OP_OUTPUTAUTHORIZINGINPUT);
        }
    }
}
