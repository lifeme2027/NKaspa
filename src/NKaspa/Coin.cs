namespace NKaspa
{
    /// <summary>
    /// A spendable output. Kaspa counterpart of NBitcoin.Coin.
    /// The outpoint identifies the output. <see cref="Utxo"/> is the entry sighash and mass need.
    /// </summary>
    public sealed class Coin
    {
        /// <summary>Creates a coin from an outpoint and the UTXO entry it spends.</summary>
        public Coin(OutPoint outpoint, UtxoEntry utxo)
        {
            Outpoint = outpoint ?? throw new ArgumentNullException(nameof(outpoint));
            Utxo = utxo ?? throw new ArgumentNullException(nameof(utxo));
        }

        /// <summary>Outpoint of the output.</summary>
        public OutPoint Outpoint { get; }

        /// <summary>Amount, script, DAA score, and coinbase flag.</summary>
        public UtxoEntry Utxo { get; }

        /// <summary>Amount in sompi.</summary>
        public Money Amount
        {
            get { return Utxo.Amount; }
        }

        /// <summary>Locking script.</summary>
        public ScriptPubKey ScriptPubKey
        {
            get { return Utxo.ScriptPublicKey; }
        }
    }
}
