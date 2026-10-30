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
    /// Change below <see cref="MinimumChange"/>, or change that would push mass over the limit, is added to the fee.
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
        /// A change at or above this amount is still folded in when keeping it would exceed <see cref="MaximumMass"/> or <see cref="MassLimits"/>.
        /// </summary>
        public Money MinimumChange { get; set; }

        /// <summary>
        /// Maximum allowed fee mass.
        /// The default is the post-Toccata block compute limit, 500000.
        /// Set this to <see cref="MassCalculator.MaxStandardTransactionMass"/> (100000) for the pre-Toccata relay cap.
        /// </summary>
        public ulong MaximumMass { get; set; }

        /// <summary>Block dimension limits used for normalization and for the mass check. The default is the post-Toccata mainnet limits.</summary>
        public BlockMassLimits MassLimits { get; set; }

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
        public TransactionBuilder()
            : this(Network.Mainnet, toccataActive: true)
        {
        }

        /// <summary>
        /// Creates a builder for <paramref name="network"/>.
        /// <paramref name="toccataActive"/> false keeps the pre-Toccata fee-mass cap of 100000.
        /// Every built-in network currently publishes the same post-Toccata block limits.
        /// </summary>
        public TransactionBuilder(Network network, bool toccataActive = true)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            MassLimits = network.MassLimits;
            MaximumMass = toccataActive ? network.MassLimits.Compute : MassCalculator.MaxStandardTransactionMass;
            var storageCeiling = toccataActive ? network.MassLimits.Storage : MassCalculator.MaxStandardTransactionMass;
            MinimumChange = new Money(MassCalculator.StorageMassParameter / storageCeiling);
        }

        /// <summary>Adds private keys used to sign pay-to-pubkey inputs.</summary>
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
        public TransactionBuilder Send(KaspaAddress address, Money amount)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            return Send(address.ScriptPubKey, amount);
        }

        /// <summary>Adds a payment to a script.</summary>
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
        public TransactionBuilder SetChange(KaspaAddress address)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            return SetChange(address.ScriptPubKey);
        }

        /// <summary>Sends change to a script.</summary>
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
        /// A rate below 100 sompi per gram is rejected.
        /// The minimum relay floor is narrower. See <see cref="SendMinimumRelayFee"/>.
        /// </summary>
        public TransactionBuilder SendEstimatedFees(FeeRate feeRate)
        {
            if (feeRate.SompiPerGram < FeeRate.MinimumRelaySompiPerGram)
            {
                throw new ArgumentOutOfRangeException(nameof(feeRate), "Fee rate is below 100 sompi per gram.");
            }

            _estimatedRate = feeRate;
            return this;
        }

        /// <summary>
        /// Pays the minimum relay fee, solved from the signed transaction.
        /// The fee is 100 sompi times the larger of compute mass and normalized transient mass.
        /// Storage mass is not included. A standard one-input Schnorr payment with change is 2036 grams, 203600 sompi, about 0.002 KAS.
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
        public Transaction BuildTransaction(bool sign)
        {
            if (_sends.Count == 0)
            {
                throw new InvalidOperationException("No outputs to send.");
            }

            var mode = ResolveFeeMode();
            var sendSum = SumSends();
            List<Coin> selected;
            ulong fee;
            Transaction transaction;
            if (mode == FeeChoice.Explicit)
            {
                fee = _explicitFee!.Value.Sompi;
                selected = Select(checked(sendSum + fee));
                transaction = Materialize(selected, sendSum, ref fee);
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
                    transaction = Materialize(selected, sendSum, ref fee);
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

            return transaction;
        }

        /// <summary>
        /// Builds the same payment as <see cref="BuildTransaction"/> with <c>sign: false</c>, wrapped as a signer PSKT.
        /// Version 0 inputs record one sigop so a later signature matches <see cref="BuildTransaction"/>.
        /// Version 1 keeps the builder compute budget in memory. That budget is not part of the PSKT JSON.
        /// </summary>
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

                if (!transaction.VerifyInput(i, coin.Utxo))
                {
                    return false;
                }

                spent.Add(coin.Utxo);
                inputSum += coin.Amount.Sompi;
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

        private Transaction Materialize(List<Coin> selected, ulong sendSum, ref ulong fee)
        {
            var transaction = MaterializeOnce(selected, sendSum, ref fee);
            if (TryGetChange(transaction, out var change) && ExceedsLimits(transaction, selected))
            {
                fee = checked(fee + change);
                transaction = MaterializeOnce(selected, sendSum, ref fee);
            }

            return transaction;
        }

        private Transaction MaterializeOnce(List<Coin> selected, ulong sendSum, ref ulong fee)
        {
            ulong inputSum = 0;
            for (var i = 0; i < selected.Count; i++)
            {
                inputSum = checked(inputSum + selected[i].Amount.Sompi);
            }

            if (inputSum < sendSum || inputSum - sendSum < fee)
            {
                throw new InvalidOperationException("Not enough funds.");
            }

            var change = inputSum - sendSum - fee;
            if (change > 0 && change < MinimumChange.Sompi)
            {
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
                return MassCalculator.EstimateFee(mass.FeeMass, _estimatedRate!.Value.SompiPerGram);
            }

            return MassCalculator.MinimumRelayFee(mass);
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
            if (fee < MassCalculator.MinimumRelayFee(mass))
            {
                throw new InvalidOperationException("Fee is below the minimum relay rate of 100 sompi per gram.");
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

        private enum FeeChoice
        {
            Explicit,
            Rate,
            MinimumRelay,
        }
    }
}
