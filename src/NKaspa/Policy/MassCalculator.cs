using System.Numerics;

namespace NKaspa.Policy
{
    /// <summary>
    /// One UTXO storage cell for KIP-9. <see cref="Plurality"/> is the number of 100-byte storage units.
    /// A standard script has plurality 1.
    /// </summary>
    public readonly struct UtxoCell
    {
        /// <summary>Creates a cell.</summary>
        public UtxoCell(ulong plurality, ulong amount)
        {
            Plurality = plurality;
            Amount = amount;
        }

        /// <summary>Number of 100-byte storage units. At least 1 for a real UTXO.</summary>
        public ulong Plurality { get; }

        /// <summary>Amount in sompi.</summary>
        public ulong Amount { get; }
    }

    /// <summary>Per-dimension block mass limits. The Kaspa counterpart of the rusty-kaspa <c>BlockMassLimits</c>.</summary>
    public readonly struct BlockMassLimits
    {
        /// <summary>Creates limits for the three mass dimensions.</summary>
        public BlockMassLimits(ulong storage, ulong compute, ulong transient)
        {
            Storage = storage;
            Compute = compute;
            Transient = transient;
        }

        /// <summary>Mainnet and testnet-10 limits: compute 500000, storage 500000, transient 1000000.</summary>
        public static BlockMassLimits Default { get; } = new BlockMassLimits(500_000, 500_000, 1_000_000);

        /// <summary>Storage-mass limit of a block.</summary>
        public ulong Storage { get; }

        /// <summary>Compute-mass limit of a block.</summary>
        public ulong Compute { get; }

        /// <summary>Transient-mass limit of a block.</summary>
        public ulong Transient { get; }
    }

    /// <summary>Compute and transient mass. No UTXO context is required.</summary>
    public readonly struct NonContextualMasses
    {
        /// <summary>Creates the pair.</summary>
        public NonContextualMasses(ulong computeMass, ulong transientMass)
        {
            ComputeMass = computeMass;
            TransientMass = transientMass;
        }

        /// <summary>Bytes plus script-public-key mass plus signature-operation mass.</summary>
        public ulong ComputeMass { get; }

        /// <summary>Serialized size times 4, before normalization.</summary>
        public ulong TransientMass { get; }
    }

    /// <summary>Every mass dimension of one transaction, plus the fee-relevant gram count.</summary>
    public readonly struct TransactionMass
    {
        /// <summary>Creates the report.</summary>
        public TransactionMass(
            ulong computeMass,
            ulong transientMass,
            ulong storageMass,
            ulong normalizedTransientMass,
            ulong normalizedStorageMass,
            ulong feeMass)
        {
            ComputeMass = computeMass;
            TransientMass = transientMass;
            StorageMass = storageMass;
            NormalizedTransientMass = normalizedTransientMass;
            NormalizedStorageMass = normalizedStorageMass;
            FeeMass = feeMass;
        }

        /// <summary>Compute mass.</summary>
        public ulong ComputeMass { get; }

        /// <summary>Raw transient mass.</summary>
        public ulong TransientMass { get; }

        /// <summary>KIP-9 storage mass, before normalization.</summary>
        public ulong StorageMass { get; }

        /// <summary>Transient mass scaled onto the compute limit.</summary>
        public ulong NormalizedTransientMass { get; }

        /// <summary>Storage mass scaled onto the compute limit.</summary>
        public ulong NormalizedStorageMass { get; }

        /// <summary>Maximum of compute, normalized transient, and normalized storage. The fee multiplies this value.</summary>
        public ulong FeeMass { get; }
    }

    /// <summary>
    /// Kaspa transaction mass for version-0 and version-1 native transactions.
    /// Port of rusty-kaspa <c>consensus/core/src/mass/mod.rs</c> and KIP-9.
    /// Compute mass is bytes plus 10 per script-public-key byte, plus 1000 per version-0 signature operation,
    /// or 100 per version-1 compute-budget unit.
    /// Transient mass is 4 per byte (KIP-13). Storage mass uses C = 10^12.
    /// The mass size counts a 32-byte payload hash and does not count the signature-operation byte or the covenant presence byte.
    /// A version-1 input adds 2 bytes for the compute budget. A covenant adds 34 bytes.
    /// It is not equal to <see cref="Transaction.ToBytes"/> length.
    /// </summary>
    public static class MassCalculator
    {
        /// <summary>Compute mass charged for one byte of the mass serialization.</summary>
        public const ulong MassPerTxByte = 1;

        /// <summary>Compute mass charged for one script-public-key byte, including the version.</summary>
        public const ulong MassPerScriptPubKeyByte = 10;

        /// <summary>Compute mass charged for one signature operation. Version 0 only.</summary>
        public const ulong MassPerSigOp = 1000;

        /// <summary>Compute mass charged for one version-1 compute-budget unit. rusty-kaspa <c>GRAMS_PER_COMPUTE_BUDGET_UNIT</c>.</summary>
        public const ulong MassPerComputeBudget = 100;

        /// <summary>KIP-9 storage mass parameter. sompi per KAS times 10000.</summary>
        public const ulong StorageMassParameter = 100_000_000UL * 10_000UL;

        /// <summary>KIP-13 transient mass per serialized byte.</summary>
        public const ulong TransientByteToMassFactor = 4;

        /// <summary>
        /// Pre-Toccata standard relay maximum for one transaction's fee mass.
        /// rusty-kaspa removed this per-transaction cap (consensus PR 1086). Block limits remain.
        /// The builder keeps this value as an optional policy.
        /// </summary>
        public const ulong MaxStandardTransactionMass = 100_000;

        /// <summary>Minimum relay fee rate, sompi per gram.</summary>
        public const ulong MinimumFeeRate = 100;

        private const ulong UtxoConstStorage = 32 + 4 + 8 + 8 + 1 + 2 + 8;
        private const ulong UtxoUnitSize = 100;

        /// <summary>rusty-kaspa <c>transaction_estimated_serialized_size</c>.</summary>
        public static ulong EstimatedSerializedSize(Transaction transaction)
        {
            RequireSupported(transaction);
            ulong size = 2 + 8;
            for (var i = 0; i < transaction.Inputs.Count; i++)
            {
                var input = transaction.Inputs[i];
                size += 32 + 4 + 8 + (ulong)input.SignatureScript.Length + 8;
                if (transaction.Version >= 1)
                {
                    size += 2;
                }
            }

            size += 8;
            for (var i = 0; i < transaction.Outputs.Count; i++)
            {
                var output = transaction.Outputs[i];
                size += 8 + 2 + 8 + (ulong)output.ScriptPublicKey.Length;
                if (output.Covenant != null)
                {
                    size += 2 + 32;
                }
            }

            size += 8 + 20 + 8 + 32 + 8;
            return size + (ulong)transaction.Payload.Length;
        }

        /// <summary>Compute and transient mass. A coinbase transaction is free.</summary>
        public static NonContextualMasses CalcNonContextualMasses(Transaction transaction)
        {
            RequireSupported(transaction);
            if (IsCoinbase(transaction))
            {
                return new NonContextualMasses(0, 0);
            }

            var size = EstimatedSerializedSize(transaction);
            ulong scriptBytes = 0;
            for (var i = 0; i < transaction.Outputs.Count; i++)
            {
                scriptBytes += 2 + (ulong)transaction.Outputs[i].ScriptPublicKey.Length;
            }

            ulong scriptMass;
            if (transaction.Version >= 1)
            {
                ulong budgets = 0;
                for (var i = 0; i < transaction.Inputs.Count; i++)
                {
                    budgets += transaction.Inputs[i].ComputeBudget ?? 0;
                }

                scriptMass = budgets * MassPerComputeBudget;
            }
            else
            {
                ulong sigOps = 0;
                for (var i = 0; i < transaction.Inputs.Count; i++)
                {
                    sigOps += transaction.Inputs[i].SigOpCount;
                }

                scriptMass = sigOps * MassPerSigOp;
            }

            var compute = size * MassPerTxByte + scriptBytes * MassPerScriptPubKeyByte + scriptMass;
            return new NonContextualMasses(compute, size * TransientByteToMassFactor);
        }

        /// <summary>Compute mass.</summary>
        public static ulong ComputeMass(Transaction transaction)
        {
            return CalcNonContextualMasses(transaction).ComputeMass;
        }

        /// <summary>Raw transient mass, 4 per byte, before normalization.</summary>
        public static ulong TransientMass(Transaction transaction)
        {
            return CalcNonContextualMasses(transaction).TransientMass;
        }

        /// <summary>How many 100-byte units a UTXO with this script occupies. The result is at least 1.</summary>
        public static ulong UtxoPlurality(int scriptLength, bool hasCovenantId = false)
        {
            if (scriptLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scriptLength));
            }

            var bytes = UtxoConstStorage + (ulong)scriptLength + (hasCovenantId ? 32UL : 0UL);
            return (bytes + UtxoUnitSize - 1) / UtxoUnitSize;
        }

        /// <summary>Storage cell of a spent output.</summary>
        public static UtxoCell CellOf(UtxoEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return new UtxoCell(UtxoPlurality(entry.ScriptPublicKey.Length, entry.CovenantId != null), entry.Amount.Sompi);
        }

        /// <summary>Storage cell of a created output.</summary>
        public static UtxoCell CellOf(TxOut output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            return new UtxoCell(UtxoPlurality(output.ScriptPublicKey.Length, output.Covenant != null), output.Value.Sompi);
        }

        /// <summary>
        /// KIP-9 storage mass. The relaxed harmonic input form is used when the output plurality is 1,
        /// the input plurality is 1, or both are 2. Otherwise the inputs use their arithmetic mean.
        /// A zero amount throws <see cref="ArgumentException"/>.
        /// </summary>
        public static ulong StorageMass(
            IReadOnlyList<UtxoCell> inputs,
            IReadOnlyList<UtxoCell> outputs,
            ulong storageMassParameter = StorageMassParameter)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (outputs == null)
            {
                throw new ArgumentNullException(nameof(outputs));
            }

            if (inputs.Count == 0)
            {
                throw new ArgumentException("A non-coinbase transaction needs at least one input.", nameof(inputs));
            }

            if (outputs.Count == 0)
            {
                throw new ArgumentException("A transaction needs at least one output.", nameof(outputs));
            }

            for (var i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].Amount == 0 || inputs[i].Plurality == 0)
                {
                    throw new ArgumentException("Input amount and plurality must be non-zero.", nameof(inputs));
                }
            }

            for (var i = 0; i < outputs.Count; i++)
            {
                if (outputs[i].Amount == 0 || outputs[i].Plurality == 0)
                {
                    throw new ArgumentException("Output amount and plurality must be non-zero.", nameof(outputs));
                }
            }

            checked
            {
                ulong outsPlurality = 0;
                ulong harmonicOuts = 0;
                for (var i = 0; i < outputs.Count; i++)
                {
                    var output = outputs[i];
                    outsPlurality += output.Plurality;
                    harmonicOuts += storageMassParameter * output.Plurality * output.Plurality / output.Amount;
                }

                ulong insPlurality = 0;
                for (var i = 0; i < inputs.Count; i++)
                {
                    insPlurality += inputs[i].Plurality;
                }

                var relaxed = outsPlurality == 1
                    || insPlurality == 1
                    || (outsPlurality == 2 && insPlurality == 2);
                if (relaxed)
                {
                    ulong harmonicIns = 0;
                    for (var i = 0; i < inputs.Count; i++)
                    {
                        var input = inputs[i];
                        harmonicIns = SaturatingAdd(harmonicIns, storageMassParameter * input.Plurality * input.Plurality / input.Amount);
                    }

                    return SaturatingSub(harmonicOuts, harmonicIns);
                }

                BigInteger sumIns = 0;
                for (var i = 0; i < inputs.Count; i++)
                {
                    sumIns += inputs[i].Amount;
                }

                var meanIns = sumIns / insPlurality;
                if (meanIns < 1)
                {
                    meanIns = 1;
                }

                if (meanIns > ulong.MaxValue)
                {
                    throw new OverflowException("Input amount mean does not fit in 64 bits.");
                }

                var arithmeticIns = SaturatingMul(insPlurality, storageMassParameter / (ulong)meanIns);
                return SaturatingSub(harmonicOuts, arithmeticIns);
            }
        }

        /// <summary>Storage mass of plurality-1 cells.</summary>
        public static ulong StorageMass(IReadOnlyList<ulong> inputAmounts, IReadOnlyList<ulong> outputAmounts)
        {
            if (inputAmounts == null)
            {
                throw new ArgumentNullException(nameof(inputAmounts));
            }

            if (outputAmounts == null)
            {
                throw new ArgumentNullException(nameof(outputAmounts));
            }

            var inputs = new UtxoCell[inputAmounts.Count];
            for (var i = 0; i < inputAmounts.Count; i++)
            {
                inputs[i] = new UtxoCell(1, inputAmounts[i]);
            }

            var outputs = new UtxoCell[outputAmounts.Count];
            for (var i = 0; i < outputAmounts.Count; i++)
            {
                outputs[i] = new UtxoCell(1, outputAmounts[i]);
            }

            return StorageMass(inputs, outputs);
        }

        /// <summary>Storage mass of a transaction. A coinbase transaction is 0. <paramref name="spentEntries"/> lines up with the inputs.</summary>
        public static ulong StorageMass(Transaction transaction, IReadOnlyList<UtxoEntry> spentEntries)
        {
            RequireSupported(transaction);
            if (spentEntries == null)
            {
                throw new ArgumentNullException(nameof(spentEntries));
            }

            if (IsCoinbase(transaction))
            {
                return 0;
            }

            if (transaction.Inputs.Count == 0)
            {
                throw new ArgumentException("A non-coinbase transaction needs at least one input.", nameof(transaction));
            }

            if (spentEntries.Count != transaction.Inputs.Count)
            {
                throw new ArgumentException("One spent UTXO entry is required per input.", nameof(spentEntries));
            }

            var inputs = new UtxoCell[spentEntries.Count];
            for (var i = 0; i < spentEntries.Count; i++)
            {
                inputs[i] = CellOf(spentEntries[i]);
            }

            var outputs = new UtxoCell[transaction.Outputs.Count];
            for (var i = 0; i < transaction.Outputs.Count; i++)
            {
                outputs[i] = CellOf(transaction.Outputs[i]);
            }

            return StorageMass(inputs, outputs);
        }

        /// <summary>
        /// Scales <paramref name="mass"/> from a dimension limited by <paramref name="dimensionLimit"/>
        /// onto the compute limit: ceil(mass * compute / limit).
        /// </summary>
        public static ulong Normalize(ulong mass, ulong dimensionLimit, ulong computeLimit)
        {
            if (dimensionLimit == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensionLimit));
            }

            var numerator = (BigInteger)mass * computeLimit;
            var result = (numerator + dimensionLimit - 1) / dimensionLimit;
            if (result > ulong.MaxValue)
            {
                throw new OverflowException("Normalized mass does not fit in 64 bits.");
            }

            return (ulong)result;
        }

        /// <summary>Normalized maximum of the three dimensions. With the default limits, transient mass is halved.</summary>
        public static TransactionMass Calculate(
            Transaction transaction,
            IReadOnlyList<UtxoEntry> spentEntries,
            BlockMassLimits? limits = null)
        {
            var chosen = limits ?? BlockMassLimits.Default;
            var contextual = CalcNonContextualMasses(transaction);
            var storage = StorageMass(transaction, spentEntries);
            var normTransient = Normalize(contextual.TransientMass, chosen.Transient, chosen.Compute);
            var normStorage = Normalize(storage, chosen.Storage, chosen.Compute);
            var feeMass = Math.Max(contextual.ComputeMass, Math.Max(normTransient, normStorage));
            return new TransactionMass(
                contextual.ComputeMass,
                contextual.TransientMass,
                storage,
                normTransient,
                normStorage,
                feeMass);
        }

        /// <summary>Normalized maximum of explicit masses.</summary>
        public static ulong NormalizedMax(ulong computeMass, ulong transientMass, ulong storageMass, BlockMassLimits limits)
        {
            return Math.Max(
                Math.Max(computeMass, Normalize(transientMass, limits.Transient, limits.Compute)),
                Normalize(storageMass, limits.Storage, limits.Compute));
        }

        /// <summary>
        /// Minimum relay fee from the Toccata upgrade guide: 100 sompi times the larger of compute mass and normalized transient mass.
        /// Storage mass is not part of this floor. With the default block limits, normalized transient mass is <c>2 * serialized size</c>.
        /// </summary>
        public static ulong MinimumRelayFee(TransactionMass mass)
        {
            var grams = Math.Max(mass.ComputeMass, mass.NormalizedTransientMass);
            return checked(grams * MinimumFeeRate);
        }

        /// <summary>Fee in sompi. A rate below <see cref="MinimumFeeRate"/> throws <see cref="ArgumentOutOfRangeException"/>.</summary>
        public static ulong EstimateFee(ulong mass, ulong feeRate)
        {
            if (feeRate < MinimumFeeRate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(feeRate),
                    "Fee rate " + feeRate.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is below the network minimum of " + MinimumFeeRate.ToString(System.Globalization.CultureInfo.InvariantCulture) + " sompi/gram.");
            }

            return checked(mass * feeRate);
        }

        /// <summary>True when the subnetwork id is the coinbase id (first byte 1, the rest zero).</summary>
        public static bool IsCoinbase(Transaction transaction)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            return transaction.SubnetworkId.Equals(SubnetworkId.Coinbase);
        }

        private static void RequireSupported(Transaction transaction)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }
        }

        private static ulong SaturatingAdd(ulong left, ulong right)
        {
            return ulong.MaxValue - left < right ? ulong.MaxValue : left + right;
        }

        private static ulong SaturatingSub(ulong left, ulong right)
        {
            return left > right ? left - right : 0;
        }

        private static ulong SaturatingMul(ulong left, ulong right)
        {
            return right != 0 && left > ulong.MaxValue / right ? ulong.MaxValue : left * right;
        }
    }
}
