namespace NKaspa
{
    /// <summary>
    /// Scripts built with the introspection and covenant helpers.
    /// They are locking scripts. The engine runs them. Nothing here builds a transaction to broadcast.
    /// </summary>
    public static class CovenantScripts
    {
        /// <summary>
        /// Version must be 1, and input 0 must pay the same sompi amount as output 0.
        /// Stack shape: <c>OP_TXVERSION 1 OP_NUMEQUALVERIFY 0 OP_TXINPUTAMOUNT 0 OP_TXOUTPUTAMOUNT OP_NUMEQUAL</c>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">Use AddData for a push.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script VersionOnePreservesInputAmount()
        {
            return new ScriptBuilder()
                .TxVersion()
                .AddInt64(1)
                .AddOp(OpcodeType.OP_NUMEQUALVERIFY)
                .AddInt64(0)
                .TxInputAmount()
                .AddInt64(0)
                .TxOutputAmount()
                .AddOp(OpcodeType.OP_NUMEQUAL)
                .ToScript();
        }

        /// <summary>
        /// Input <paramref name="index"/> must carry <paramref name="covenantId"/>.
        /// <c>OP_INPUTCOVENANTID</c> pops the index before it pushes the 32-byte id.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="covenantId"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Use AddData for a push.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script InputCovenantEquals(int index, uint256 covenantId)
        {
            if (covenantId == null)
            {
                throw new ArgumentNullException(nameof(covenantId));
            }

            return new ScriptBuilder()
                .AddData(covenantId.ToBytes())
                .AddInt64(index)
                .InputCovenantId()
                .AddOp(OpcodeType.OP_EQUAL)
                .ToScript();
        }

        /// <summary>
        /// The spending input's index must be <paramref name="index"/>, then the script leaves true.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">Use AddData for a push.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script SpendingInputIs(int index)
        {
            return new ScriptBuilder()
                .TxInputIndex()
                .AddInt64(index)
                .AddOp(OpcodeType.OP_NUMEQUAL)
                .ToScript();
        }
    }
}
