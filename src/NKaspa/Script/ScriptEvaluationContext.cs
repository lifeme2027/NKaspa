namespace NKaspa
{
    /// <summary>
    /// Result of running one Kaspa input. Kaspa counterpart of NBitcoin.ScriptEvaluationContext.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Verify(Script, ScriptPubKey, Transaction, int, IReadOnlyList{UtxoEntry})"/> enforces the input's
    /// sigop count or compute budget. A null <see cref="Error"/> means the input is valid.
    /// </para>
    /// <para>
    /// The script public key passed here replaces the spent output's script for both execution and the signature hash.
    /// Amount, DAA score, coinbase flag, and covenant id stay on the <see cref="UtxoEntry"/>.
    /// </para>
    /// </remarks>
    public sealed class ScriptEvaluationContext
    {
        private ScriptEvaluationContext(TxScriptEngine engine)
        {
            Error = engine.Error;
            UsedSigOps = engine.UsedSigOps;
            UsedScriptUnits = engine.UsedScriptUnits;
        }

        /// <summary>Failure from the script, or null when the input is valid.</summary>
        public ScriptError? Error { get; }

        /// <summary>True when <see cref="Error"/> is null.</summary>
        public bool Result
        {
            get { return Error == null; }
        }

        /// <summary>Signature checks charged, including failed attempts until a later attempt matched.</summary>
        public ushort UsedSigOps { get; }

        /// <summary>Script units charged. A charge that did not fit is not included.</summary>
        public ulong UsedScriptUnits { get; }

        /// <summary>Executes <paramref name="scriptSig"/> against <paramref name="scriptPubKey"/>.</summary>
        public static ScriptEvaluationContext Verify(
            Script scriptSig,
            ScriptPubKey scriptPubKey,
            Transaction tx,
            int inputIndex,
            IReadOnlyList<UtxoEntry> utxos)
        {
            if (scriptSig == null)
            {
                throw new ArgumentNullException(nameof(scriptSig));
            }

            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            if (utxos == null)
            {
                throw new ArgumentNullException(nameof(utxos));
            }

            var copy = new UtxoEntry?[utxos.Count];
            for (var i = 0; i < utxos.Count; i++)
            {
                copy[i] = utxos[i];
            }

            var engine = TxScriptEngine.Run(scriptSig.ToBytes(), scriptPubKey, tx, inputIndex, copy);
            return new ScriptEvaluationContext(engine);
        }

        /// <summary>Executes <paramref name="scriptSig"/> against a version-0 <paramref name="scriptPubKey"/>.</summary>
        public static ScriptEvaluationContext Verify(
            Script scriptSig,
            Script scriptPubKey,
            Transaction tx,
            int inputIndex,
            IReadOnlyList<UtxoEntry> utxos)
        {
            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            return Verify(scriptSig, new ScriptPubKey(0, scriptPubKey), tx, inputIndex, utxos);
        }
    }
}
