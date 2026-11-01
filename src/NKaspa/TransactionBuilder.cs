using NKaspa.Crypto;
using NKaspa.Policy;
using NKaspa.PSKT;

namespace NKaspa
{
    /// <summary>
    /// Builds and signs a native Kaspa payment.
    /// Kaspa counterpart of NBitcoin.TransactionBuilder, reduced to one native payment.
    /// Inputs are Schnorr or ECDSA pay-to-pubkey. Each selected coin is its own input and gets its own signature script.
    /// <see cref="DefaultCoinSelector"/> spends one coin when that coin covers the payment, the fee, and the minimum change.
    /// <see cref="MustSpend(Coin[])"/> and <see cref="SpendAllCoins"/> force every named coin into the transaction.
    /// Change below <see cref="MinimumChange"/> is added to the fee.
    /// A larger change output that cannot be built (mass over the limit, or a storage-mass fee the surplus cannot pay)
    /// is folded into the fee only when the amount is at most <see cref="MaxFoldedChange"/> and within <see cref="MaxFoldedFeeFactor"/> of the fee that transaction needs.
    /// Those two limits are public. A caller can raise them. They are wallet safety limits, not consensus rules. Raising them can burn more sompi into the fee.
    /// A larger unbuildable change throws. It is not burned as a fee.
    /// <see cref="ChangeFoldedIntoFee"/> is the sompi removed from change on the last successful build. It is not the total fee.
    /// <see cref="Policy.TransactionMass"/> does not repeat that amount.
    /// The fee mass must stay within <see cref="MaximumMass"/> and within <see cref="MassLimits"/>.
    /// Version 0 is the default. <see cref="SetVersion"/>(1) builds a Toccata payment and signs it with the version-1 sighash.
    /// <see cref="BuildPSKT"/> wraps the same unsigned payment. <see cref="Sign(NKaspa.PSKT.PSKT)"/> adds partial signatures for the builder keys.
    /// </summary>
    public sealed class TransactionBuilder
    {
        /// <summary>
        /// Compute budget used on a version-1 pay-to-pubkey input when the caller does not set one.
        /// One unit is 100 compute grams, so 10 units match one version-0 signature operation (1000 grams).
        /// </summary>
        public const ushort DefaultComputeBudget = 10;

        private static readonly Script PlaceholderSignature = Script.SignatureScript(new SchnorrSignature(new byte[64]), SigHashType.All);

        private readonly List<Coin> _coins = new List<Coin>();
        private readonly List<Key> _keys = new List<Key>();
        private readonly List<TxOut> _sends = new List<TxOut>();
        private readonly List<OutPoint> _mustSpend = new List<OutPoint>();
        private readonly Dictionary<OutPoint, ushort> _budgets = new Dictionary<OutPoint, ushort>();
        private ScriptPubKey? _change;
        private ushort _version;
        private bool _spendAll;

        /// <summary>
        /// Change below this amount is added to the fee instead of creating an output.
        /// The default is <c>C / storage limit</c>: 0.02 KAS when the storage limit is 500000.
        /// <see cref="MassPolicy.PreToccata"/> uses <c>C / 100000</c>, which is 0.1 KAS.
        /// A larger change is folded only when the output cannot be built and the amount passes <see cref="MaxFoldedChange"/> and <see cref="MaxFoldedFeeFactor"/>.
        /// </summary>
        public Money MinimumChange { get; set; }

        /// <summary>
        /// Largest change that may be folded into the fee when the change output cannot be built.
        /// The default is 0.1 KAS (10,000,000 sompi). The allowed amount is <c>max(MinimumChange, MaxFoldedChange)</c>.
        /// Change below <see cref="MinimumChange"/> still folds. That path is the dust rule and does not use this cap.
        /// This property is public. A caller can raise it. It is a wallet safety limit, not a consensus rule.
        /// Raising it can burn more sompi into the fee.
        /// </summary>
        public Money MaxFoldedChange { get; set; } = new Money(10_000_000);

        /// <summary>
        /// A change output that cannot be built is folded only when its value is at most this many times
        /// the fee of the same transaction without that output. The default is 100.
        /// Values below 1 reject every positive fold on this path.
        /// This property is public. A caller can raise it. It is a wallet safety limit, not a consensus rule.
        /// Raising it can burn more sompi into the fee.
        /// </summary>
        public int MaxFoldedFeeFactor { get; set; } = 100;

        /// <summary>
        /// Sompi removed from a change output and added to the fee by the last successful
        /// <see cref="BuildTransaction"/> or <see cref="BuildPSKT"/>.
        /// Zero when that build kept the change, folded nothing, or threw.
        /// This is the removed change, not the total fee. <see cref="Policy.TransactionMass"/> does not include it.
        /// An explicit fee F plus folded dust D reports D here and charges F + D.
        /// A fee estimate that starts at zero and folds the whole surplus S reports S, and the fee is S.
        /// </summary>
        public Money ChangeFoldedIntoFee { get; private set; }

        /// <summary>
        /// Maximum allowed fee mass.
        /// The default is the post-Toccata block compute limit, 500000.
        /// <see cref="MassPolicy.PreToccata"/> and <c>toccataActive: false</c> set this to <see cref="MassCalculator.MaxStandardTransactionMass"/> (100000).
        /// </summary>
        public ulong MaximumMass { get; set; }

        /// <summary>Block dimension limits used for normalization and for the mass check. The default is the post-Toccata mainnet limits.</summary>
        public BlockMassLimits MassLimits { get; set; }

        /// <summary>
        /// Sompi charged for one gram when <see cref="SendMinimumRelayFee"/> runs, and the lowest rate <see cref="SendEstimatedFees"/> accepts.
        /// The default is 100. <see cref="MassPolicy.PreToccata"/> uses 1.
        /// </summary>
        public ulong MinimumRelayFeeRate { get; set; }

        /// <summary>
        /// When false, an explicit <see cref="SendFees"/> below the minimum relay fee throws.
        /// The floor is 100 sompi times the larger of compute mass and normalized transient mass.
        /// Storage mass is not part of that floor. A standard one-input Schnorr payment is about 0.002 KAS.
        /// The default is false. <see cref="SendMinimumRelayFee"/> already pays this floor.
        /// </summary>
        public bool AllowBelowMinimumRelayFee { get; set; }

        /// <summary>
        /// Transaction version. 0 keeps the pre-Toccata sighash and signature-operation mass.
        /// 1 is a Toccata payment: each input carries <see cref="ComputeBudget"/> and the signature hash omits sigops.
        /// Values above 1 are rejected.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Transaction version must be 0 or 1.</exception>
        public ushort Version
        {
            get { return _version; }
            set
            {
                if (value > 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Transaction version must be 0 or 1.");
                }

                _version = value;
            }
        }

        /// <summary>
        /// Compute budget written on every version-1 input that has no per-outpoint override.
        /// The default is <see cref="DefaultComputeBudget"/> (10). Ignored when <see cref="Version"/> is 0.
        /// </summary>
        public ushort ComputeBudget { get; set; } = DefaultComputeBudget;

        private Money? _explicitFee;
        private FeeRate? _estimatedRate;
        private bool _payMinimumRelay;

        /// <summary>Creates a builder for mainnet after Toccata. Fee mass may use the full block compute limit.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        public TransactionBuilder()
            : this(Network.Mainnet, MassPolicy.PostToccata)
        {
        }

        /// <summary>
        /// Creates a builder for <paramref name="network"/> using the current post-cleanup policy when <paramref name="toccataActive"/> is true.
        /// False selects <see cref="MassPolicy.PreToccata"/>: fee-mass cap 100000 and 1 sompi per gram relay rate.
        /// Every built-in network publishes the same post-Toccata block limits. Activation changes the cap and the relay rate.
        /// <see cref="TransactionBuilder(Network, ulong)"/> picks the policy from the node's virtual DAA score.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        public TransactionBuilder(Network network, bool toccataActive = true)
            : this(network, toccataActive ? MassPolicy.PostToccata : MassPolicy.PreToccata)
        {
        }

        /// <summary>
        /// Creates a builder for <paramref name="network"/> at <paramref name="virtualDaaScore"/>.
        /// The policy is <see cref="MassPolicy.ForNetwork"/>.
        /// Scores below <see cref="Network.ToccataActivationDaaScore"/> use the pre-Toccata cap and 1 sompi per gram.
        /// Simnet and current devnet use score 0, so every score is post-Toccata.
        /// An old devnet node needs <see cref="MassPolicy.PreToccata"/> explicitly.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Unknown Kaspa network.</exception>
        public TransactionBuilder(Network network, ulong virtualDaaScore)
            : this(network, MassPolicy.ForNetwork(network, virtualDaaScore))
        {
        }

        /// <summary>
        /// Creates a builder for <paramref name="network"/> with an explicit <paramref name="policy"/>.
        /// The network selects address prefixes. The policy selects mass limits, the transaction cap, the relay rate, and minimum change.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        public TransactionBuilder(Network network, MassPolicy policy)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            MassLimits = policy.BlockLimits;
            MaximumMass = policy.MaximumTransactionMass;
            MinimumChange = policy.MinimumChange;
            MinimumRelayFeeRate = policy.MinimumRelayFeeRate;
        }

        /// <summary>Adds private keys used to sign pay-to-pubkey inputs.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="keys"/> is null.</exception>
        public TransactionBuilder AddKeys(params Key[] keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            for (var i = 0; i < keys.Length; i++)
            {
                if (keys[i] == null)
                {
                    throw new ArgumentNullException(nameof(keys));
                }

                _keys.Add(keys[i]);
            }

            return this;
        }

        /// <summary>Adds coins that may be spent.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="coins"/> is null.</exception>
        public TransactionBuilder AddCoins(params Coin[] coins)
        {
            if (coins == null)
            {
                throw new ArgumentNullException(nameof(coins));
            }

            for (var i = 0; i < coins.Length; i++)
            {
                if (coins[i] == null)
                {
                    throw new ArgumentNullException(nameof(coins));
                }

                if (ContainsOutpoint(coins[i].Outpoint))
                {
                    continue;
                }

                _coins.Add(coins[i]);
            }

            return this;
        }

        /// <summary>
        /// Spends every coin passed to <see cref="AddCoins"/>.
        /// Each coin stays a separate input and is signed. The payment amount is unchanged.
        /// Surplus still goes to change, or to the fee when change is below <see cref="MinimumChange"/>.
        /// </summary>
        public TransactionBuilder SpendAllCoins()
        {
            _spendAll = true;
            return this;
        }

        /// <summary>
        /// Requires these outpoints to be inputs. The coins must already have been added, or be added later with <see cref="AddCoins"/>.
        /// Every required input is signed. Coins are not merged when they share a script.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="outpoints"/> is null.</exception>
        public TransactionBuilder MustSpend(params OutPoint[] outpoints)
        {
            if (outpoints == null)
            {
                throw new ArgumentNullException(nameof(outpoints));
            }

            for (var i = 0; i < outpoints.Length; i++)
            {
                if (outpoints[i] == null)
                {
                    throw new ArgumentNullException(nameof(outpoints));
                }

                if (IndexOfMust(outpoints[i]) >= 0)
                {
                    continue;
                }

                _mustSpend.Add(outpoints[i]);
            }

            return this;
        }

        /// <summary>
        /// Requires these coins to be inputs, and adds any that are not already in the builder.
        /// Every required input is signed. Coins are not merged when they share a script.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="coins"/> is null.</exception>
        public TransactionBuilder MustSpend(params Coin[] coins)
        {
            AddCoins(coins);
            if (coins.Length == 0)
            {
                return this;
            }

            var outpoints = new OutPoint[coins.Length];
            for (var i = 0; i < coins.Length; i++)
            {
                outpoints[i] = coins[i].Outpoint;
            }

            return MustSpend(outpoints);
        }

        /// <summary>Sets <see cref="Version"/>. Only 0 and 1 are accepted.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Transaction version must be 0 or 1.</exception>
        public TransactionBuilder SetVersion(ushort version)
        {
            Version = version;
            return this;
        }

        /// <summary>Sets the compute budget used for version-1 inputs that have no per-outpoint override.</summary>
        public TransactionBuilder SetComputeBudget(ushort computeBudget)
        {
            ComputeBudget = computeBudget;
            return this;
        }

        /// <summary>Sets the compute budget of one version-1 input. The outpoint does not have to be added yet.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="outpoint"/> is null.</exception>
        public TransactionBuilder SetComputeBudget(OutPoint outpoint, ushort computeBudget)
        {
            if (outpoint == null)
            {
                throw new ArgumentNullException(nameof(outpoint));
            }

            _budgets[outpoint] = computeBudget;
            return this;
        }

        /// <summary>Adds a payment to an address.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="address"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Send amount must be greater than zero.</exception>
        /// <exception cref="System.ArgumentException">Schnorr x-only public key must be 32 bytes. ECDSA public key must be 33 bytes. Script hash must be 32 bytes.</exception>
        public TransactionBuilder Send(KaspaAddress address, Money amount)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            return Send(address.ScriptPubKey, amount);
        }

        /// <summary>Adds a payment to a script.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPubKey"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Send amount must be greater than zero.</exception>
        public TransactionBuilder Send(ScriptPubKey scriptPubKey, Money amount)
        {
            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            if (amount.Sompi == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Send amount must be greater than zero.");
            }

            _sends.Add(new TxOut(amount, scriptPubKey));
            return this;
        }

        /// <summary>Sends change to an address.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="address"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Schnorr x-only public key must be 32 bytes. ECDSA public key must be 33 bytes. Script hash must be 32 bytes.</exception>
        public TransactionBuilder SetChange(KaspaAddress address)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            return SetChange(address.ScriptPubKey);
        }

        /// <summary>Sends change to a script.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPubKey"/> is null.</exception>
        public TransactionBuilder SetChange(ScriptPubKey scriptPubKey)
        {
            _change = scriptPubKey ?? throw new ArgumentNullException(nameof(scriptPubKey));
            return this;
        }

        /// <summary>Pays an exact fee. Cannot be combined with <see cref="SendEstimatedFees"/> or <see cref="SendMinimumRelayFee"/>.</summary>
        public TransactionBuilder SendFees(Money fee)
        {
            _explicitFee = fee;
            return this;
        }

        /// <summary>
        /// Estimates the fee from the signed fee mass and <paramref name="feeRate"/>.
        /// Fee mass is the maximum of compute mass, normalized transient mass, and normalized storage mass.
        /// A rate below <see cref="MinimumRelayFeeRate"/> sompi per gram is rejected (100 after Toccata, 1 before).
        /// The relay mass is still narrower than fee mass. See <see cref="SendMinimumRelayFee"/>.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Fee rate is below the builder minimum relay rate.</exception>
        public TransactionBuilder SendEstimatedFees(FeeRate feeRate)
        {
            if (feeRate.SompiPerGram < MinimumRelayFeeRate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(feeRate),
                    "Fee rate is below " + MinimumRelayFeeRate.ToString(System.Globalization.CultureInfo.InvariantCulture) + " sompi per gram.");
            }

            _estimatedRate = feeRate;
            return this;
        }

        /// <summary>
        /// Pays the minimum relay fee, solved from the signed transaction.
        /// The fee is <see cref="MinimumRelayFeeRate"/> sompi times the larger of compute mass and normalized transient mass.
        /// The default rate is 100. Storage mass is not included. A standard one-input Schnorr payment with change is 2036 grams, 203600 sompi, about 0.002 KAS.
        /// Cannot be combined with <see cref="SendFees"/> or <see cref="SendEstimatedFees"/>.
        /// </summary>
        public TransactionBuilder SendMinimumRelayFee()
        {
            _payMinimumRelay = true;
            return this;
        }

        /// <summary>
        /// Fee for <paramref name="mass"/> grams at <paramref name="feeRate"/>.
        /// A rate below 100 sompi per gram is rejected.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Fee rate {feeRate.ToString(System.Globalization.CultureInfo.InvariantCulture)} is below the network
        /// minimum of {MinimumFeeRate.ToString(System.Globalization.CultureInfo.InvariantCulture)} sompi/gram.
        /// </exception>
        public static Money EstimateFee(FeeRate feeRate, ulong mass)
        {
            return MassCalculator.EstimateFee(feeRate, mass);
        }

        /// <summary>
        /// Minimum relay fee for a measured mass: 100 sompi times <see cref="MassCalculator.RelayMass"/>.
        /// A standard one-input Schnorr payment is about 0.002 KAS.
        /// </summary>
        public static Money EstimateMinimumRelayFee(TransactionMass mass)
        {
            return MassCalculator.EstimateMinimumRelayFee(mass);
        }

        /// <summary>
        /// Selects coins, adds change, checks the mass limit, and optionally signs.
        /// Unsigned inputs have an empty signature script and a zero signature-operation count.
        /// <see cref="Transaction.StorageMass"/> is set to the KIP-9 storage mass. It does not change the transaction id.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// BLAKE2b digest size must be from 1 to 64 bytes. Fee rate
        /// {feeRate.ToString(System.Globalization.CultureInfo.InvariantCulture)} is below the network minimum
        /// of {MinimumFeeRate.ToString(System.Globalization.CultureInfo.InvariantCulture)} sompi/gram. The
        /// selection target must be greater than zero.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Sighash type is not one of the six Kaspa values. Signature must be 64 bytes. BIP-340 auxiliary
        /// randomness must be empty or 32 bytes. Schnorr message must be 32 bytes. Schnorr signature must be 64
        /// bytes. BLAKE2b key must be at most 64 bytes. ECDSA message must be 32 bytes. ECDSA signature must be
        /// 64 bytes. ECDSA public key is not on the curve. ECDSA public key must be compressed (0x02 or 0x03).
        /// ECDSA public key must be 33 bytes. Schnorr x-only public key is not on the curve. Schnorr x-only
        /// public key must be 32 bytes. One spent UTXO entry is required per input. A non-coinbase transaction
        /// needs at least one input. Output amount and plurality must be non-zero. Input amount and plurality
        /// must be non-zero. A transaction needs at least one output. Further failures of this type use the
        /// same exception.
        /// </exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated. uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">
        /// Fee estimate did not converge. No outputs to send. No private key matches an input script. SignInput
        /// signs pay-to-pubkey scripts. Build a pay-to-script-hash signature with
        /// Script.PayToScriptHashSignature. Stored private key is no longer valid. Transaction input is missing
        /// an outpoint. BLAKE2b hasher is already finished. Transaction output is missing a script. SHA256 did
        /// not produce a digest. Fee is below the minimum relay rate of 100 sompi per gram. Transaction mass
        /// exceeds the standard maximum. Change would exceed the mass limit. Folding it into the fee would
        /// burn more than MaxFoldedChange allows. Split the send or add inputs so the change output fits.
        /// Change is due and no change script was set. Not enough funds. A
        /// must-spend outpoint was not added with AddCoins. Set SendFees, SendEstimatedFees, or
        /// SendMinimumRelayFee. Set only one of SendFees, SendEstimatedFees, or SendMinimumRelayFee.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        /// <exception cref="System.OverflowException">Input amount mean does not fit in 64 bits. Normalized mass does not fit in 64 bits.</exception>
        public Transaction BuildTransaction(bool sign)
        {
            ChangeFoldedIntoFee = Money.Zero;
            if (_sends.Count == 0)
            {
                throw new InvalidOperationException("No outputs to send.");
            }

            var mode = ResolveFeeMode();
            var sendSum = SumSends();
            List<Coin> selected;
            ulong fee;
            Transaction transaction;
            ulong foldedAmount = 0;
            if (mode == FeeChoice.Explicit)
            {
                fee = _explicitFee!.Value.Sompi;
                selected = Select(checked(sendSum + fee));
                transaction = Materialize(selected, sendSum, ref fee, out foldedAmount);
            }
            else
            {
                fee = 0;
                selected = new List<Coin>();
                transaction = new Transaction();
                var settled = false;
                for (var attempt = 0; attempt < 32; attempt++)
                {
                    selected = Select(checked(sendSum + fee));
                    transaction = Materialize(selected, sendSum, ref fee, out foldedAmount);
                    var required = RequiredFee(transaction, selected, mode);
                    if (fee >= required)
                    {
                        settled = true;
                        break;
                    }

                    fee = required;
                }

                if (!settled)
                {
                    throw new InvalidOperationException("Fee estimate did not converge.");
                }
            }

            EnsureMass(transaction, selected);
            EnsureRelayFee(transaction, selected, fee);
            transaction.StorageMass = MassCalculator.StorageMass(transaction, Entries(selected));
            if (sign)
            {
                Sign(transaction, selected);
            }
            else
            {
                for (var i = 0; i < transaction.Inputs.Count; i++)
                {
                    transaction.Inputs[i].SignatureScript = Script.Empty;
                    transaction.Inputs[i].SigOpCount = 0;
                }
            }

            ChangeFoldedIntoFee = new Money(foldedAmount);
            return transaction;
        }

        /// <summary>
        /// Builds the same payment as <see cref="BuildTransaction"/> with <c>sign: false</c>, wrapped as a signer PSKT.
        /// Version 0 inputs record one sigop so a later signature matches <see cref="BuildTransaction"/>.
        /// Version 1 keeps the builder compute budget in memory. That budget is not part of the PSKT JSON.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// BLAKE2b digest size must be from 1 to 64 bytes. Fee rate
        /// {feeRate.ToString(System.Globalization.CultureInfo.InvariantCulture)} is below the network minimum
        /// of {MinimumFeeRate.ToString(System.Globalization.CultureInfo.InvariantCulture)} sompi/gram. The
        /// selection target must be greater than zero.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Sighash type is not one of the six Kaspa values. Signature must be 64 bytes. BIP-340 auxiliary
        /// randomness must be empty or 32 bytes. Schnorr message must be 32 bytes. Schnorr signature must be 64
        /// bytes. BLAKE2b key must be at most 64 bytes. ECDSA message must be 32 bytes. ECDSA signature must be
        /// 64 bytes. ECDSA public key is not on the curve. ECDSA public key must be compressed (0x02 or 0x03).
        /// ECDSA public key must be 33 bytes. Schnorr x-only public key is not on the curve. Schnorr x-only
        /// public key must be 32 bytes. One spent UTXO entry is required per input. A non-coinbase transaction
        /// needs at least one input. Output amount and plurality must be non-zero. Input amount and plurality
        /// must be non-zero. A transaction needs at least one output. Further failures of this type use the
        /// same exception.
        /// </exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated. uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">
        /// Built input is not a known coin. Fee estimate did not converge. No outputs to send. No private key
        /// matches an input script. SignInput signs pay-to-pubkey scripts. Build a pay-to-script-hash signature
        /// with Script.PayToScriptHashSignature. Stored private key is no longer valid. Transaction input is
        /// missing an outpoint. BLAKE2b hasher is already finished. Transaction output is missing a script.
        /// SHA256 did not produce a digest. Fee is below the minimum relay rate of 100 sompi per gram.
        /// Transaction mass exceeds the standard maximum. Change is due and no change script was set. Not
        /// enough funds. A must-spend outpoint was not added with AddCoins. Set SendFees, SendEstimatedFees, or
        /// SendMinimumRelayFee. Set only one of SendFees, SendEstimatedFees, or SendMinimumRelayFee.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        /// <exception cref="System.OverflowException">Input amount mean does not fit in 64 bits. Normalized mass does not fit in 64 bits.</exception>
        /// <exception cref="NKaspa.PSKT.PSKTException">
        /// Signer is reached from the constructor, updater, or combiner. This operation belongs to the
        /// {role.ToString()} role. Outputs not allowed to contain covenant due to pskt or tx versions mismatch.
        /// </exception>
        public NKaspa.PSKT.PSKT BuildPSKT()
        {
            var transaction = BuildTransaction(false);
            var pskt = NKaspa.PSKT.PSKT.Create().Constructor().SetTransactionVersion(Version);
            for (var i = 0; i < transaction.Inputs.Count; i++)
            {
                var coin = FindCoin(transaction.Inputs[i].PreviousOutpoint);
                if (coin == null)
                {
                    throw new InvalidOperationException("Built input is not a known coin.");
                }

                var input = new PSKTInput(transaction.Inputs[i].PreviousOutpoint)
                {
                    Utxo = coin.Utxo,
                    Sequence = transaction.Inputs[i].Sequence,
                    SighashType = SigHashType.All,
                    SigOpCount = Version == 0 ? (byte)1 : transaction.Inputs[i].SigOpCount,
                };
                if (Version >= 1)
                {
                    input.ComputeBudget = transaction.Inputs[i].ComputeBudget ?? BudgetFor(coin.Outpoint);
                }

                pskt.AddInput(input);
            }

            for (var i = 0; i < transaction.Outputs.Count; i++)
            {
                var output = transaction.Outputs[i];
                pskt.AddOutput(new PSKTOutput(output.Value.Sompi, output.ScriptPublicKey)
                {
                    Covenant = output.Covenant,
                });
            }

            return pskt.Updater().Signer();
        }

        /// <summary>
        /// Adds a partial signature for every builder key that can spend an input.
        /// The PSKT is moved to the signer role. It is not finalized and it is not broadcast.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="pskt"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">
        /// ECDSA public key is not on the curve. ECDSA public key must be compressed (0x02 or 0x03). ECDSA
        /// public key must be 33 bytes. BIP-340 auxiliary randomness must be empty or 32 bytes. Schnorr message
        /// must be 32 bytes. Schnorr signature must be 64 bytes. Sighash type is not one of the six Kaspa
        /// values. BLAKE2b key must be at most 64 bytes. ECDSA message must be 32 bytes. ECDSA signature must
        /// be 64 bytes. Schnorr x-only public key is not on the curve. Schnorr x-only public key must be 32
        /// bytes.
        /// </exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes. Script push is too long. Script push is truncated.</exception>
        /// <exception cref="System.InvalidOperationException">
        /// Stored private key is no longer valid. Transaction input is missing an outpoint. BLAKE2b hasher is
        /// already finished. Transaction output is missing a script. SHA256 did not produce a digest.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        /// <exception cref="NKaspa.PSKT.PSKTException">Missing UTXO entry. This operation belongs to the {role.ToString()} role. Signer is reached from the constructor, updater, or combiner.</exception>
        public NKaspa.PSKT.PSKT Sign(NKaspa.PSKT.PSKT pskt)
        {
            if (pskt == null)
            {
                throw new ArgumentNullException(nameof(pskt));
            }

            var signed = pskt.Signer();
            for (var i = 0; i < _keys.Count; i++)
            {
                signed.Sign(_keys[i]);
            }

            return signed;
        }

        /// <summary>
        /// True when <paramref name="transaction"/> spends known coins, verifies every signature,
        /// pays every <see cref="Send(ScriptPubKey, Money)"/> output, and stays within <see cref="MaximumMass"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">
        /// One spent UTXO entry is required per input. A non-coinbase transaction needs at least one input.
        /// Output amount and plurality must be non-zero. Input amount and plurality must be non-zero. A
        /// transaction needs at least one output. Subnetwork id must be 20 bytes. Spent output {i.ToString()}
        /// is missing. The spent output list must have one entry per input. The spent output for this input is
        /// missing. BLAKE3 key must be 32 bytes. wRPC request is missing a numeric id. Set PointOfView or
        /// ChainBlock, not both. PointOfView is the selected parent P. ChainBlock is the chain block B whose
        /// selected parent becomes P. Sighash type is not one of the six Kaspa values. BLAKE2b key must be at
        /// most 64 bytes.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isChainBlock. Expected a hex hash for {name}. uint256 hex must be 32 bytes.
        /// uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a parent hash. Expected parent hashes. Expected an array for parents. Missing JSON string
        /// {name}. Missing JSON property {name}. Hash is missing. networkId is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// WebSocket is not connected. Call ConnectAsync first. Transaction input is missing an outpoint.
        /// BLAKE2b hasher is already finished. Transaction output is missing a script. SHA256 did not produce a
        /// digest.
        /// </exception>
        /// <exception cref="System.OverflowException">Normalized mass does not fit in 64 bits. Input amount mean does not fit in 64 bits.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        /// <exception cref="NKaspa.RPC.ConsensusSeqCommitException">
        /// acceptedIdMerkleRoot is missing for {target.Hash}. seq commitment was requested for {target.Hash},
        /// which is not a selected-parent ancestor of {pov.Hash}. header blueScore does not match verboseData
        /// blueScore for {block.Hash}. blueScore is missing for {block.Hash}. getBlockDagInfo.sink changed from
        /// {previous} to {dag.Sink} and the new selected chain does not contain {anchor}.
        /// verboseData.isChainBlock is missing for {current.Hash}. selected parent chain cycled at
        /// {current.Hash}. isChainBlock is false for {block.Hash} on the selected chain.
        /// verboseData.isChainBlock is missing for {block.Hash}. getBlock did not return {hash}. The node data
        /// is incomplete. verboseData.hash {block.VerboseHash} does not match the requested block {block.Hash}.
        /// header.hash {block.HeaderHash} does not match the requested block {block.Hash}. Request limit of
        /// {_maxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)} was reached before the
        /// selected parent chain could be checked. blue score does not decrease from {child.Hash}
        /// ({childScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}) to selected parent
        /// {parent.Hash} ({parentScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}). selected
        /// parent walk reached {block.Hash} but isChainBlock is false. isChainBlock is false for {block.Hash}
        /// and getBlockDagInfo.sink changed from {previous} to {again.Sink}. getBlockDagInfo failed:
        /// {ex.Message}. selectedParentHash {block.SelectedParentHash} does not match the first direct parent
        /// {block.DirectParent} of {block.Hash}. Further failures of this type use the same exception.
        /// </exception>
        public bool Verify(Transaction transaction)
        {
            if (transaction == null || transaction.Version != Version)
            {
                return false;
            }

            if (transaction.Inputs.Count == 0 || transaction.Outputs.Count < _sends.Count)
            {
                return false;
            }

            var spent = new List<UtxoEntry>(transaction.Inputs.Count);
            ulong inputSum = 0;
            for (var i = 0; i < transaction.Inputs.Count; i++)
            {
                var coin = FindCoin(transaction.Inputs[i].PreviousOutpoint);
                if (coin == null)
                {
                    return false;
                }

                for (var j = 0; j < i; j++)
                {
                    if (transaction.Inputs[j].PreviousOutpoint.Equals(transaction.Inputs[i].PreviousOutpoint))
                    {
                        return false;
                    }
                }

                spent.Add(coin.Utxo);
                inputSum += coin.Amount.Sompi;
            }

            if (!transaction.Verify(spent))
            {
                return false;
            }

            ulong outputSum = 0;
            var used = new bool[transaction.Outputs.Count];
            for (var s = 0; s < _sends.Count; s++)
            {
                var found = false;
                for (var o = 0; o < transaction.Outputs.Count; o++)
                {
                    if (used[o])
                    {
                        continue;
                    }

                    if (transaction.Outputs[o].Value.Sompi == _sends[s].Value.Sompi
                        && transaction.Outputs[o].ScriptPublicKey.Equals(_sends[s].ScriptPublicKey))
                    {
                        used[o] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            for (var o = 0; o < transaction.Outputs.Count; o++)
            {
                outputSum += transaction.Outputs[o].Value.Sompi;
                if (used[o])
                {
                    continue;
                }

                if (_change == null || !transaction.Outputs[o].ScriptPublicKey.Equals(_change))
                {
                    return false;
                }
            }

            if (outputSum > inputSum)
            {
                return false;
            }

            try
            {
                var mass = MassCalculator.Calculate(transaction, spent, MassLimits);
                return mass.FeeMass <= MaximumMass
                    && mass.ComputeMass <= MassLimits.Compute
                    && mass.StorageMass <= MassLimits.Storage
                    && mass.TransientMass <= MassLimits.Transient;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private ulong SumSends()
        {
            ulong sum = 0;
            for (var i = 0; i < _sends.Count; i++)
            {
                sum = checked(sum + _sends[i].Value.Sompi);
            }

            return sum;
        }

        private List<Coin> Select(ulong target)
        {
            var forced = ResolveForced();
            if (_spendAll || forced.Count > 0)
            {
                ulong forcedSum = 0;
                for (var i = 0; i < forced.Count; i++)
                {
                    forcedSum = checked(forcedSum + forced[i].Amount.Sompi);
                }

                if (forcedSum >= target || _spendAll)
                {
                    if (forcedSum < target)
                    {
                        throw new InvalidOperationException("Not enough funds.");
                    }

                    return forced;
                }

                var remaining = new List<Coin>();
                for (var i = 0; i < _coins.Count; i++)
                {
                    if (IndexOfOutpoint(forced, _coins[i].Outpoint) < 0)
                    {
                        remaining.Add(_coins[i]);
                    }
                }

                var extra = new DefaultCoinSelector(MinimumChange).Select(remaining, new Money(checked(target - forcedSum)));
                forced.AddRange(extra);
                return forced;
            }

            return new DefaultCoinSelector(MinimumChange).Select(_coins, new Money(target));
        }

        private List<Coin> ResolveForced()
        {
            for (var i = 0; i < _mustSpend.Count; i++)
            {
                if (FindCoin(_mustSpend[i]) == null)
                {
                    throw new InvalidOperationException("A must-spend outpoint was not added with AddCoins.");
                }
            }

            if (_spendAll)
            {
                var all = new List<Coin>(_coins);
                all.Sort(CompareCoins);
                return all;
            }

            var forced = new List<Coin>(_mustSpend.Count);
            for (var i = 0; i < _mustSpend.Count; i++)
            {
                forced.Add(FindCoin(_mustSpend[i])!);
            }

            return forced;
        }

        private Transaction Materialize(List<Coin> selected, ulong sendSum, ref ulong fee, out ulong foldedAmount)
        {
            var transaction = MaterializeOnce(selected, sendSum, ref fee, out var dust);
            foldedAmount = dust;
            if (!TryGetChange(transaction, out var change))
            {
                return transaction;
            }

            if (!ChangeOutputUnbuildable(transaction, selected, sendSum))
            {
                return transaction;
            }

            if (!TryFoldChange(selected, sendSum, change, ref fee, out transaction))
            {
                throw new InvalidOperationException(
                    "Change would exceed the mass limit. Folding it into the fee would burn more than MaxFoldedChange allows. Split the send or add inputs so the change output fits.");
            }

            foldedAmount = checked(foldedAmount + change);
            return transaction;
        }

        private bool ChangeOutputUnbuildable(Transaction transaction, List<Coin> selected, ulong sendSum)
        {
            if (ExceedsLimits(transaction, selected))
            {
                return true;
            }

            if (_explicitFee.HasValue)
            {
                return false;
            }

            TransactionMass mass;
            try
            {
                mass = MassCalculator.Calculate(transaction, Entries(selected), MassLimits);
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (OverflowException)
            {
                return true;
            }

            ulong required;
            try
            {
                if (_estimatedRate.HasValue)
                {
                    required = checked(mass.FeeMass * _estimatedRate.Value.SompiPerGram);
                }
                else
                {
                    required = checked(MassCalculator.RelayMass(mass) * MinimumRelayFeeRate);
                }
            }
            catch (OverflowException)
            {
                return true;
            }

            return required > InputSum(selected) - sendSum;
        }

        private bool TryFoldChange(List<Coin> selected, ulong sendSum, ulong change, ref ulong fee, out Transaction transaction)
        {
            transaction = new Transaction();
            var cap = MinimumChange.Sompi > MaxFoldedChange.Sompi ? MinimumChange.Sompi : MaxFoldedChange.Sompi;
            if (change > cap)
            {
                return false;
            }

            var candidate = checked(fee + change);
            var trialFee = candidate;
            Transaction trial;
            try
            {
                trial = MaterializeOnce(selected, sendSum, ref trialFee, out _);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (ExceedsLimits(trial, selected))
            {
                return false;
            }

            ulong needed;
            try
            {
                needed = NeededFee(trial, selected);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }

            if (candidate < needed || !WithinFoldFactor(change, needed))
            {
                return false;
            }

            fee = candidate;
            transaction = trial;
            return true;
        }

        private ulong NeededFee(Transaction transaction, List<Coin> selected)
        {
            var mass = MassCalculator.Calculate(transaction, Entries(selected), MassLimits);
            EnsureWithin(mass);
            if (_estimatedRate.HasValue)
            {
                return checked(mass.FeeMass * _estimatedRate.Value.SompiPerGram);
            }

            if (AllowBelowMinimumRelayFee && _explicitFee.HasValue)
            {
                return 0;
            }

            return checked(MassCalculator.RelayMass(mass) * MinimumRelayFeeRate);
        }

        private bool WithinFoldFactor(ulong folded, ulong needed)
        {
            if (MaxFoldedFeeFactor < 1)
            {
                return folded == 0;
            }

            if (needed == 0)
            {
                return true;
            }

            var factor = (ulong)MaxFoldedFeeFactor;
            if (needed > ulong.MaxValue / factor)
            {
                return true;
            }

            return folded <= needed * factor;
        }

        private static ulong InputSum(List<Coin> selected)
        {
            ulong inputSum = 0;
            for (var i = 0; i < selected.Count; i++)
            {
                inputSum = checked(inputSum + selected[i].Amount.Sompi);
            }

            return inputSum;
        }

        private Transaction MaterializeOnce(List<Coin> selected, ulong sendSum, ref ulong fee, out ulong dustFolded)
        {
            dustFolded = 0;
            var inputSum = InputSum(selected);

            if (inputSum < sendSum || inputSum - sendSum < fee)
            {
                throw new InvalidOperationException("Not enough funds.");
            }

            var change = inputSum - sendSum - fee;
            if (change > 0 && change < MinimumChange.Sompi)
            {
                dustFolded = change;
                fee += change;
                change = 0;
            }

            if (change > 0 && _change == null)
            {
                throw new InvalidOperationException("Change is due and no change script was set.");
            }

            var inputs = new TxIn[selected.Count];
            for (var i = 0; i < selected.Count; i++)
            {
                var placeholder = new Script(PlaceholderSignature.ToBytes());
                if (Version == 0)
                {
                    inputs[i] = new TxIn(selected[i].Outpoint, placeholder, 0, 1);
                }
                else
                {
                    inputs[i] = TxIn.WithComputeBudget(selected[i].Outpoint, placeholder, 0, BudgetFor(selected[i].Outpoint));
                }
            }

            var outputs = new List<TxOut>(_sends.Count + 1);
            outputs.AddRange(_sends);
            if (change > 0)
            {
                outputs.Add(new TxOut(new Money(change), _change!));
            }

            return new Transaction(Version, inputs, outputs, 0, SubnetworkId.Native, 0, Array.Empty<byte>());
        }

        private bool TryGetChange(Transaction transaction, out ulong change)
        {
            change = 0;
            if (_change == null || transaction.Outputs.Count != _sends.Count + 1)
            {
                return false;
            }

            var last = transaction.Outputs[transaction.Outputs.Count - 1];
            if (!last.ScriptPublicKey.Equals(_change) || last.Value.Sompi == 0)
            {
                return false;
            }

            change = last.Value.Sompi;
            return true;
        }

        private ulong RequiredFee(Transaction transaction, List<Coin> selected, FeeChoice mode)
        {
            var mass = MassCalculator.Calculate(transaction, Entries(selected), MassLimits);
            EnsureWithin(mass);
            if (mode == FeeChoice.Rate)
            {
                return checked(mass.FeeMass * _estimatedRate!.Value.SompiPerGram);
            }

            return checked(MassCalculator.RelayMass(mass) * MinimumRelayFeeRate);
        }

        private FeeChoice ResolveFeeMode()
        {
            var choices = 0;
            if (_explicitFee.HasValue)
            {
                choices++;
            }

            if (_estimatedRate.HasValue)
            {
                choices++;
            }

            if (_payMinimumRelay)
            {
                choices++;
            }

            if (choices > 1)
            {
                throw new InvalidOperationException("Set only one of SendFees, SendEstimatedFees, or SendMinimumRelayFee.");
            }

            if (choices == 0)
            {
                throw new InvalidOperationException("Set SendFees, SendEstimatedFees, or SendMinimumRelayFee.");
            }

            if (_explicitFee.HasValue)
            {
                return FeeChoice.Explicit;
            }

            if (_estimatedRate.HasValue)
            {
                return FeeChoice.Rate;
            }

            return FeeChoice.MinimumRelay;
        }

        private void EnsureMass(Transaction transaction, List<Coin> selected)
        {
            EnsureWithin(MassCalculator.Calculate(transaction, Entries(selected), MassLimits));
        }

        private void EnsureRelayFee(Transaction transaction, List<Coin> selected, ulong fee)
        {
            if (AllowBelowMinimumRelayFee || !_explicitFee.HasValue)
            {
                return;
            }

            var mass = MassCalculator.Calculate(transaction, Entries(selected), MassLimits);
            var floor = checked(MassCalculator.RelayMass(mass) * MinimumRelayFeeRate);
            if (fee < floor)
            {
                throw new InvalidOperationException(
                    "Fee is below the minimum relay rate of " + MinimumRelayFeeRate.ToString(System.Globalization.CultureInfo.InvariantCulture) + " sompi per gram.");
            }
        }

        private bool ExceedsLimits(Transaction transaction, List<Coin> selected)
        {
            TransactionMass mass;
            try
            {
                mass = MassCalculator.Calculate(transaction, Entries(selected), MassLimits);
            }
            catch (ArgumentException)
            {
                return true;
            }

            return mass.FeeMass > MaximumMass
                || mass.ComputeMass > MassLimits.Compute
                || mass.StorageMass > MassLimits.Storage
                || mass.TransientMass > MassLimits.Transient;
        }

        private void EnsureWithin(TransactionMass mass)
        {
            if (mass.FeeMass > MaximumMass
                || mass.ComputeMass > MassLimits.Compute
                || mass.StorageMass > MassLimits.Storage
                || mass.TransientMass > MassLimits.Transient)
            {
                throw new InvalidOperationException("Transaction mass exceeds the standard maximum.");
            }
        }

        private void Sign(Transaction transaction, List<Coin> selected)
        {
            for (var i = 0; i < selected.Count; i++)
            {
                var key = FindKey(selected[i].ScriptPubKey);
                if (key == null)
                {
                    throw new InvalidOperationException("No private key matches an input script.");
                }

                if (transaction.Version == 0)
                {
                    transaction.Inputs[i].SigOpCount = 1;
                }

                transaction.SignInput(i, key, selected[i].Utxo, SigHashType.All);
            }
        }

        private ushort BudgetFor(OutPoint outpoint)
        {
            if (_budgets.TryGetValue(outpoint, out var budget))
            {
                return budget;
            }

            return ComputeBudget;
        }

        private int IndexOfMust(OutPoint outpoint)
        {
            for (var i = 0; i < _mustSpend.Count; i++)
            {
                if (_mustSpend[i].Equals(outpoint))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfOutpoint(List<Coin> coins, OutPoint outpoint)
        {
            for (var i = 0; i < coins.Count; i++)
            {
                if (coins[i].Outpoint.Equals(outpoint))
                {
                    return i;
                }
            }

            return -1;
        }

        private Key? FindKey(ScriptPubKey scriptPubKey)
        {
            var script = scriptPubKey.ToScript();
            if (script.TryGetPayToPubKey(out var xOnly))
            {
                for (var i = 0; i < _keys.Count; i++)
                {
                    if (_keys[i].PubKey.ToBytes().AsSpan().SequenceEqual(xOnly))
                    {
                        return _keys[i];
                    }
                }
            }

            if (script.TryGetPayToPubKeyEcdsa(out var compressed))
            {
                for (var i = 0; i < _keys.Count; i++)
                {
                    if (_keys[i].PubKeyECDSA.ToBytes().AsSpan().SequenceEqual(compressed))
                    {
                        return _keys[i];
                    }
                }
            }

            return null;
        }

        private Coin? FindCoin(OutPoint outpoint)
        {
            for (var i = 0; i < _coins.Count; i++)
            {
                if (_coins[i].Outpoint.Equals(outpoint))
                {
                    return _coins[i];
                }
            }

            return null;
        }

        private bool ContainsOutpoint(OutPoint outpoint)
        {
            for (var i = 0; i < _coins.Count; i++)
            {
                if (_coins[i].Outpoint.Equals(outpoint))
                {
                    return true;
                }
            }

            return false;
        }

        private static UtxoEntry[] Entries(List<Coin> selected)
        {
            var entries = new UtxoEntry[selected.Count];
            for (var i = 0; i < selected.Count; i++)
            {
                entries[i] = selected[i].Utxo;
            }

            return entries;
        }

        private static int CompareCoins(Coin left, Coin right)
        {
            var byAmount = right.Amount.Sompi.CompareTo(left.Amount.Sompi);
            if (byAmount != 0)
            {
                return byAmount;
            }

            return string.CompareOrdinal(left.Outpoint.ToString(), right.Outpoint.ToString());
        }

        private static Network RequireNetwork(Network network)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            return network;
        }

        private enum FeeChoice
        {
            Explicit,
            Rate,
            MinimumRelay,
        }
    }
}
