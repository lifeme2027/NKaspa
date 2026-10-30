using NKaspa.Crypto;
using NKaspa.Policy;

namespace NKaspa
{
    /// <summary>
    /// Builds and signs a version-0 Kaspa transaction.
    /// Kaspa counterpart of NBitcoin.TransactionBuilder, reduced to one native payment.
    /// Inputs are Schnorr or ECDSA pay-to-pubkey. Coin selection is largest-first.
    /// Change below <see cref="MinimumChange"/> is added to the fee.
    /// The fee mass must stay within <see cref="MaximumMass"/>.
    /// </summary>
    public sealed class TransactionBuilder
    {
        private static readonly Script PlaceholderSignature = Script.SignatureScript(new SchnorrSignature(new byte[64]), SigHashType.All);

        private readonly List<Coin> _coins = new List<Coin>();
        private readonly List<Key> _keys = new List<Key>();
        private readonly List<TxOut> _sends = new List<TxOut>();
        private ScriptPubKey? _change;

        /// <summary>
        /// Change at or below this amount is added to the fee instead of creating an output.
        /// The default is zero, so any positive change becomes an output.
        /// </summary>
        public Money MinimumChange { get; set; } = Money.Zero;

        /// <summary>Maximum allowed fee mass. The default is the standard limit of 100000 grams.</summary>
        public ulong MaximumMass { get; set; } = MassCalculator.MaxStandardTransactionMass;

        private Money? _explicitFee;
        private FeeRate? _estimatedRate;

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

                _coins.Add(coins[i]);
            }

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

        /// <summary>Pays an exact fee. Cannot be combined with <see cref="SendEstimatedFees"/>.</summary>
        public TransactionBuilder SendFees(Money fee)
        {
            _explicitFee = fee;
            return this;
        }

        /// <summary>
        /// Estimates the fee from the signed mass and <paramref name="feeRate"/>.
        /// A rate below 100 sompi per gram is rejected.
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

            if (_explicitFee.HasValue && _estimatedRate.HasValue)
            {
                throw new InvalidOperationException("SendFees and SendEstimatedFees cannot both be set.");
            }

            if (!_explicitFee.HasValue && !_estimatedRate.HasValue)
            {
                throw new InvalidOperationException("Set SendFees or SendEstimatedFees.");
            }

            var sendSum = SumSends();
            List<Coin> selected;
            ulong fee;
            Transaction transaction;
            if (_explicitFee.HasValue)
            {
                fee = _explicitFee.Value.Sompi;
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
                    var required = RequiredFee(transaction, selected);
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
        /// True when <paramref name="transaction"/> spends known coins, verifies every signature,
        /// pays every <see cref="Send(ScriptPubKey, Money)"/> output, and stays within <see cref="MaximumMass"/>.
        /// </summary>
        public bool Verify(Transaction transaction)
        {
            if (transaction == null || transaction.Version != 0)
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
                var mass = MassCalculator.Calculate(transaction, spent);
                return mass.FeeMass <= MaximumMass;
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
            var ordered = new List<Coin>(_coins);
            ordered.Sort(CompareCoins);
            var selected = new List<Coin>();
            ulong sum = 0;
            for (var i = 0; i < ordered.Count; i++)
            {
                selected.Add(ordered[i]);
                sum = checked(sum + ordered[i].Amount.Sompi);
                if (sum >= target)
                {
                    return selected;
                }
            }

            throw new InvalidOperationException("Not enough funds.");
        }

        private Transaction Materialize(List<Coin> selected, ulong sendSum, ref ulong fee)
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
                inputs[i] = new TxIn(selected[i].Outpoint, PlaceholderSignature, 0, 1);
            }

            var outputs = new List<TxOut>(_sends.Count + 1);
            outputs.AddRange(_sends);
            if (change > 0)
            {
                outputs.Add(new TxOut(new Money(change), _change!));
            }

            return new Transaction(0, inputs, outputs, 0, SubnetworkId.Native, 0, Array.Empty<byte>());
        }

        private ulong RequiredFee(Transaction transaction, List<Coin> selected)
        {
            var mass = MassCalculator.Calculate(transaction, Entries(selected));
            if (mass.FeeMass > MaximumMass)
            {
                throw new InvalidOperationException("Transaction mass exceeds the standard maximum.");
            }

            return MassCalculator.EstimateFee(mass.FeeMass, _estimatedRate!.Value.SompiPerGram);
        }

        private void EnsureMass(Transaction transaction, List<Coin> selected)
        {
            var mass = MassCalculator.Calculate(transaction, Entries(selected));
            if (mass.FeeMass > MaximumMass)
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

                transaction.Inputs[i].SigOpCount = 1;
                transaction.SignInput(i, key, selected[i].Utxo, SigHashType.All);
            }
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
    }
}
