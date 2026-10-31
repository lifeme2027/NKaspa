namespace NKaspa.Policy
{
    /// <summary>
    /// Standardness and relay limits for one activation state.
    /// Current rusty-kaspa master writes the same <see cref="BlockMassLimits"/> on mainnet, testnet, simnet, and devnet.
    /// The per-transaction cap and the relay rate change when Toccata activates.
    /// Before activation the cap is <see cref="MassCalculator.MaxStandardTransactionMass"/> (100000) and the rate is 1 sompi per gram.
    /// After activation the cap is the block compute limit and the rate is 100 sompi per gram.
    /// </summary>
    public readonly struct MassPolicy
    {
        /// <summary>
        /// Minimum relay rate after Toccata, sompi per gram.
        /// rusty-kaspa commit <c>90dbf074275d60c1fe74a3491883196f110970c0</c>,
        /// <c>mining/src/mempool/config.rs</c> lines 20–23:
        /// <c>DEFAULT_MINIMUM_RELAY_TRANSACTION_FEE = 100_000</c> sompi per kilogram, which is this rate.
        /// </summary>
        public const ulong PostToccataMinimumRelayFeeRate = 100;

        /// <summary>
        /// Minimum relay rate before Toccata, sompi per gram.
        /// rusty-kaspa commit <c>90dbf074275d60c1fe74a3491883196f110970c0</c>,
        /// <c>mining/src/mempool/config.rs</c> lines 25–28:
        /// <c>LEGACY_MINIMUM_RELAY_TRANSACTION_FEE = 1_000</c> sompi per kilogram, which is 1 sompi per gram.
        /// Current (post-Toccata) nodes may reject a 1 sompi/gram fee. Use <see cref="PostToccata"/> for live networks.
        /// </summary>
        public const ulong PreToccataMinimumRelayFeeRate = 1;

        /// <summary>Creates a policy. Every limit and the relay rate must be non-zero.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Block mass limits must be non-zero. Maximum transaction mass must be non-zero. Minimum relay fee rate
        /// must be non-zero.
        /// </exception>
        public MassPolicy(bool toccataActive, BlockMassLimits blockLimits, ulong maximumTransactionMass, ulong minimumRelayFeeRate)
        {
            if (blockLimits.Storage == 0 || blockLimits.Compute == 0 || blockLimits.Transient == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockLimits), "Block mass limits must be non-zero.");
            }

            if (maximumTransactionMass == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTransactionMass), "Maximum transaction mass must be non-zero.");
            }

            if (minimumRelayFeeRate == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumRelayFeeRate), "Minimum relay fee rate must be non-zero.");
            }

            ToccataActive = toccataActive;
            BlockLimits = blockLimits;
            MaximumTransactionMass = maximumTransactionMass;
            MinimumRelayFeeRate = minimumRelayFeeRate;
        }

        /// <summary>
        /// Policy of a node after Toccata. Block limits are 500000 / 500000 / 1000000.
        /// The transaction cap is the compute limit. The relay rate is 100 sompi per gram.
        /// Change below <c>C / storage limit</c> (0.02 KAS) is dust for KIP-9.
        /// </summary>
        public static MassPolicy PostToccata { get; } = new MassPolicy(
            true,
            BlockMassLimits.Default,
            BlockMassLimits.Default.Compute,
            PostToccataMinimumRelayFeeRate);

        /// <summary>
        /// Policy of a node before Toccata. Block dimension limits stay <see cref="BlockMassLimits.Default"/>
        /// because current rusty-kaspa master does not fork that triple.
        /// The transaction cap is 100000. The relay rate is 1 sompi per gram
        /// (<c>LEGACY_MINIMUM_RELAY_TRANSACTION_FEE</c> at rusty-kaspa <c>90dbf074</c> <c>mining/src/mempool/config.rs</c> line 28).
        /// Change below <c>C / 100000</c> (0.1 KAS) is folded into the fee.
        /// Use this for a node that has not activated Toccata. Current nodes may reject this rate.
        /// </summary>
        public static MassPolicy PreToccata { get; } = new MassPolicy(
            false,
            BlockMassLimits.Default,
            MassCalculator.MaxStandardTransactionMass,
            PreToccataMinimumRelayFeeRate);

        /// <summary>True when the post-Toccata cap and 100 sompi per gram relay rate apply.</summary>
        public bool ToccataActive { get; }

        /// <summary>Block dimension limits used to normalize mass.</summary>
        public BlockMassLimits BlockLimits { get; }

        /// <summary>Largest fee mass the builder will produce.</summary>
        public ulong MaximumTransactionMass { get; }

        /// <summary>Minimum sompi charged for one gram of relay mass.</summary>
        public ulong MinimumRelayFeeRate { get; }

        /// <summary>
        /// Change below this amount is folded into the fee.
        /// After Toccata the ceiling is the storage-mass limit. Before it, the ceiling is <see cref="MaximumTransactionMass"/>.
        /// </summary>
        public Money MinimumChange
        {
            get
            {
                var ceiling = ToccataActive ? BlockLimits.Storage : MaximumTransactionMass;
                return new Money(MassCalculator.StorageMassParameter / ceiling);
            }
        }
    }
}
