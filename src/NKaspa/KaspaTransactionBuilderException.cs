using System.Globalization;

namespace NKaspa
{
    /// <summary>Which <see cref="TransactionBuilder"/> or <see cref="DefaultCoinSelector"/> check failed.</summary>
    public enum KaspaTransactionBuilderReason
    {
        /// <summary>The coins cannot cover the payment and the fee. <see cref="KaspaTransactionBuilderException.Needed"/> and <see cref="KaspaTransactionBuilderException.Available"/> are set.</summary>
        InsufficientFunds,

        /// <summary>Fee mass, compute mass, storage mass, or transient mass is above its cap. <see cref="KaspaTransactionBuilderException.Mass"/> and <see cref="KaspaTransactionBuilderException.Cap"/> are set.</summary>
        MassExceeded,

        /// <summary>
        /// The change output cannot be built, and folding it into the fee would pass <see cref="KaspaTransactionBuilderException.MaxFoldedChange"/> or the mass limit.
        /// <see cref="KaspaTransactionBuilderException.Change"/> and <see cref="KaspaTransactionBuilderException.MaxFoldedChange"/> are set.
        /// </summary>
        ChangeUnfoldable,

        /// <summary>An explicit fee is below the minimum relay fee. <see cref="KaspaTransactionBuilderException.Fee"/> and <see cref="KaspaTransactionBuilderException.MinimumFee"/> are set.</summary>
        FeeBelowMinimum,

        /// <summary>No builder key can sign an input. <see cref="KaspaTransactionBuilderException.InputIndex"/> is set.</summary>
        NoMatchingKey,

        /// <summary>
        /// <see cref="TransactionBuilder.MustSpend(OutPoint[])"/> names an outpoint that <see cref="TransactionBuilder.AddCoins(Coin[])"/> did not add.
        /// <see cref="KaspaTransactionBuilderException.Outpoint"/> is set.
        /// </summary>
        MustSpendUnknown,

        /// <summary>No payment output was added.</summary>
        NoOutputs,

        /// <summary>The fee loop did not settle within its attempt limit.</summary>
        FeeEstimateDidNotConverge,

        /// <summary>A built input does not match a coin the builder holds. <see cref="KaspaTransactionBuilderException.Outpoint"/> is set.</summary>
        UnknownInput,

        /// <summary>Change is due and <see cref="TransactionBuilder.SetChange(ScriptPubKey)"/> was not called. <see cref="KaspaTransactionBuilderException.Change"/> is set.</summary>
        ChangeScriptMissing,

        /// <summary>More than one of <see cref="TransactionBuilder.SendFees"/>, <see cref="TransactionBuilder.SendEstimatedFees"/>, and <see cref="TransactionBuilder.SendMinimumRelayFee"/> was set.</summary>
        ConflictingFeeModes,

        /// <summary>None of <see cref="TransactionBuilder.SendFees"/>, <see cref="TransactionBuilder.SendEstimatedFees"/>, or <see cref="TransactionBuilder.SendMinimumRelayFee"/> was set.</summary>
        FeeModeMissing,
    }


    /// <summary>
    /// Failure from <see cref="TransactionBuilder"/> or <see cref="DefaultCoinSelector"/>.
    /// This type extends <see cref="InvalidOperationException"/>, so <c>catch (InvalidOperationException)</c> still handles it.
    /// <see cref="Reason"/> is a <see cref="KaspaTransactionBuilderReason"/> and says which check failed.
    /// </summary>
    /// <remarks>
    /// Each reason sets only the properties named here. The others stay null.
    /// <list type="bullet">
    /// <item><description><see cref="KaspaTransactionBuilderReason.InsufficientFunds"/> sets <see cref="Needed"/> and <see cref="Available"/> (sompi). <see cref="Available"/> is null when the coin sum does not fit in a <see cref="ulong"/>.</description></item>
    /// <item><description><see cref="KaspaTransactionBuilderReason.MassExceeded"/> sets <see cref="Mass"/> and <see cref="Cap"/> (grams). The first hit wins: fee mass against <see cref="TransactionBuilder.MaximumMass"/>, then compute, storage, and transient against <see cref="TransactionBuilder.MassLimits"/>.</description></item>
    /// <item><description><see cref="KaspaTransactionBuilderReason.ChangeUnfoldable"/> sets <see cref="Change"/> and <see cref="MaxFoldedChange"/> (sompi). <see cref="MaxFoldedChange"/> is <c>max(MinimumChange, TransactionBuilder.MaxFoldedChange)</c>.</description></item>
    /// <item><description><see cref="KaspaTransactionBuilderReason.FeeBelowMinimum"/> sets <see cref="Fee"/> and <see cref="MinimumFee"/> (sompi).</description></item>
    /// <item><description><see cref="KaspaTransactionBuilderReason.NoMatchingKey"/> sets <see cref="InputIndex"/>.</description></item>
    /// <item><description><see cref="KaspaTransactionBuilderReason.MustSpendUnknown"/> sets <see cref="Outpoint"/>.</description></item>
    /// <item><description><see cref="KaspaTransactionBuilderReason.UnknownInput"/> sets <see cref="Outpoint"/>.</description></item>
    /// <item><description><see cref="KaspaTransactionBuilderReason.ChangeScriptMissing"/> sets <see cref="Change"/> (sompi).</description></item>
    /// <item><description><see cref="KaspaTransactionBuilderReason.NoOutputs"/>, <see cref="KaspaTransactionBuilderReason.FeeEstimateDidNotConverge"/>, <see cref="KaspaTransactionBuilderReason.ConflictingFeeModes"/>, and <see cref="KaspaTransactionBuilderReason.FeeModeMissing"/> set no extra property.</description></item>
    /// </list>
    /// The builder does not reject an immature coinbase. <see cref="ConsensusParams.IsCoinbaseMature(UtxoEntry, ulong, Network)"/> is that check.
    /// <see cref="ArgumentNullException"/> and <see cref="ArgumentOutOfRangeException"/> still report a bad argument.
    /// </remarks>
    public class KaspaTransactionBuilderException : InvalidOperationException
    {
        /// <summary>
        /// Creates an exception.
        /// Properties that do not apply to <paramref name="reason"/> stay null.
        /// </summary>
        /// <param name="reason">Which check failed.</param>
        /// <param name="message">Text stored on <see cref="Exception.Message"/>.</param>
        /// <param name="needed">Sompi the payment and fee required.</param>
        /// <param name="available">Sompi in the coins that were considered.</param>
        /// <param name="mass">Grams of the dimension that exceeded its cap.</param>
        /// <param name="cap">Gram cap for <paramref name="mass"/>.</param>
        /// <param name="change">Change output, in sompi.</param>
        /// <param name="maxFoldedChange">Largest change, in sompi, that may be folded into the fee.</param>
        /// <param name="fee">Explicit fee, in sompi.</param>
        /// <param name="minimumFee">Minimum relay fee, in sompi.</param>
        /// <param name="outpoint">Outpoint the check named.</param>
        /// <param name="inputIndex">Index of the input that no key can sign.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaTransactionBuilderException(
            KaspaTransactionBuilderReason reason,
            string message,
            ulong? needed = null,
            ulong? available = null,
            ulong? mass = null,
            ulong? cap = null,
            ulong? change = null,
            ulong? maxFoldedChange = null,
            ulong? fee = null,
            ulong? minimumFee = null,
            OutPoint? outpoint = null,
            int? inputIndex = null)
            : base(message ?? throw new ArgumentNullException(nameof(message)))
        {
            Reason = reason;
            Needed = needed;
            Available = available;
            Mass = mass;
            Cap = cap;
            Change = change;
            MaxFoldedChange = maxFoldedChange;
            Fee = fee;
            MinimumFee = minimumFee;
            Outpoint = outpoint;
            InputIndex = inputIndex;
        }

        /// <summary>Which check failed. See each <see cref="KaspaTransactionBuilderReason"/> value for the properties it sets.</summary>
        public KaspaTransactionBuilderReason Reason { get; }

        /// <summary>Sompi the payment and fee required. Set for <see cref="KaspaTransactionBuilderReason.InsufficientFunds"/>.</summary>
        public ulong? Needed { get; }

        /// <summary>
        /// Sompi in the coins that were considered.
        /// Set for <see cref="KaspaTransactionBuilderReason.InsufficientFunds"/> when that sum fits in a <see cref="ulong"/>.
        /// Null when this reason does not use it, and null when the coin sum overflows.
        /// </summary>
        public ulong? Available { get; }

        /// <summary>
        /// Grams of the dimension that exceeded its cap.
        /// Set for <see cref="KaspaTransactionBuilderReason.MassExceeded"/>.
        /// Fee mass is compared with <see cref="TransactionBuilder.MaximumMass"/> first, then compute, storage, and transient.
        /// </summary>
        public ulong? Mass { get; }

        /// <summary>Gram cap for <see cref="Mass"/>. Set for <see cref="KaspaTransactionBuilderReason.MassExceeded"/>.</summary>
        public ulong? Cap { get; }

        /// <summary>Change output, in sompi. Set for <see cref="KaspaTransactionBuilderReason.ChangeUnfoldable"/> and <see cref="KaspaTransactionBuilderReason.ChangeScriptMissing"/>.</summary>
        public ulong? Change { get; }

        /// <summary>
        /// Largest change, in sompi, that may be folded into the fee.
        /// This is <c>max(MinimumChange, TransactionBuilder.MaxFoldedChange)</c>.
        /// Set for <see cref="KaspaTransactionBuilderReason.ChangeUnfoldable"/>.
        /// </summary>
        public ulong? MaxFoldedChange { get; }

        /// <summary>Explicit fee, in sompi. Set for <see cref="KaspaTransactionBuilderReason.FeeBelowMinimum"/>.</summary>
        public ulong? Fee { get; }

        /// <summary>Minimum relay fee for the built transaction, in sompi. Set for <see cref="KaspaTransactionBuilderReason.FeeBelowMinimum"/>.</summary>
        public ulong? MinimumFee { get; }

        /// <summary>Outpoint the check named. Set for <see cref="KaspaTransactionBuilderReason.MustSpendUnknown"/> and <see cref="KaspaTransactionBuilderReason.UnknownInput"/>.</summary>
        public OutPoint? Outpoint { get; }

        /// <summary>Index of the input that no key can sign. Set for <see cref="KaspaTransactionBuilderReason.NoMatchingKey"/>.</summary>
        public int? InputIndex { get; }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.InsufficientFunds"/>. The message is <c>Not enough funds.</c></summary>
        public static KaspaTransactionBuilderException InsufficientFunds(ulong? needed, ulong? available)
        {
            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.InsufficientFunds, "Not enough funds.", needed: needed, available: available);
        }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.MassExceeded"/>. The message is <c>Transaction mass exceeds the standard maximum.</c></summary>
        public static KaspaTransactionBuilderException MassExceeded(ulong mass, ulong cap)
        {
            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.MassExceeded, "Transaction mass exceeds the standard maximum.", mass: mass, cap: cap);
        }

        /// <summary>
        /// Creates <see cref="KaspaTransactionBuilderReason.ChangeUnfoldable"/>.
        /// The message says the change would exceed the mass limit and names <c>MaxFoldedChange</c>.
        /// </summary>
        public static KaspaTransactionBuilderException ChangeUnfoldable(ulong change, ulong maxFoldedChange)
        {
            return new KaspaTransactionBuilderException(
                KaspaTransactionBuilderReason.ChangeUnfoldable,
                "Change would exceed the mass limit. Folding it into the fee would burn more than MaxFoldedChange allows. Split the send or add inputs so the change output fits.",
                change: change,
                maxFoldedChange: maxFoldedChange);
        }

        /// <summary>
        /// Creates <see cref="KaspaTransactionBuilderReason.FeeBelowMinimum"/>.
        /// The message names <paramref name="sompiPerGram"/>.
        /// </summary>
        public static KaspaTransactionBuilderException FeeBelowMinimum(ulong fee, ulong minimumFee, ulong sompiPerGram)
        {
            return new KaspaTransactionBuilderException(
                KaspaTransactionBuilderReason.FeeBelowMinimum,
                "Fee is below the minimum relay rate of " + sompiPerGram.ToString(CultureInfo.InvariantCulture) + " sompi per gram.",
                fee: fee,
                minimumFee: minimumFee);
        }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.NoMatchingKey"/>. The message is <c>No private key matches an input script.</c></summary>
        public static KaspaTransactionBuilderException NoMatchingKey(int inputIndex)
        {
            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.NoMatchingKey, "No private key matches an input script.", inputIndex: inputIndex);
        }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.MustSpendUnknown"/>. The message is <c>A must-spend outpoint was not added with AddCoins.</c></summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="outpoint"/> is null.</exception>
        public static KaspaTransactionBuilderException MustSpendUnknown(OutPoint outpoint)
        {
            if (outpoint == null)
            {
                throw new ArgumentNullException(nameof(outpoint));
            }

            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.MustSpendUnknown, "A must-spend outpoint was not added with AddCoins.", outpoint: outpoint);
        }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.NoOutputs"/>. The message is <c>No outputs to send.</c></summary>
        public static KaspaTransactionBuilderException NoOutputs()
        {
            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.NoOutputs, "No outputs to send.");
        }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.FeeEstimateDidNotConverge"/>. The message is <c>Fee estimate did not converge.</c></summary>
        public static KaspaTransactionBuilderException FeeEstimateDidNotConverge()
        {
            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.FeeEstimateDidNotConverge, "Fee estimate did not converge.");
        }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.UnknownInput"/>. The message is <c>Built input is not a known coin.</c></summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="outpoint"/> is null.</exception>
        public static KaspaTransactionBuilderException UnknownInput(OutPoint outpoint)
        {
            if (outpoint == null)
            {
                throw new ArgumentNullException(nameof(outpoint));
            }

            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.UnknownInput, "Built input is not a known coin.", outpoint: outpoint);
        }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.ChangeScriptMissing"/>. The message is <c>Change is due and no change script was set.</c></summary>
        public static KaspaTransactionBuilderException ChangeScriptMissing(ulong change)
        {
            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.ChangeScriptMissing, "Change is due and no change script was set.", change: change);
        }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.ConflictingFeeModes"/>. The message names the three fee methods.</summary>
        public static KaspaTransactionBuilderException ConflictingFeeModes()
        {
            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.ConflictingFeeModes, "Set only one of SendFees, SendEstimatedFees, or SendMinimumRelayFee.");
        }

        /// <summary>Creates <see cref="KaspaTransactionBuilderReason.FeeModeMissing"/>. The message names the three fee methods.</summary>
        public static KaspaTransactionBuilderException FeeModeMissing()
        {
            return new KaspaTransactionBuilderException(KaspaTransactionBuilderReason.FeeModeMissing, "Set SendFees, SendEstimatedFees, or SendMinimumRelayFee.");
        }
    }
}
