namespace NKaspa.Policy
{
    /// <summary>
    /// Standardness and relay limits for one activation state.
    /// <see cref="ForNetwork"/> is the entry point that picks this state from a network and a virtual DAA score.
    /// Current rusty-kaspa master writes the same <see cref="BlockMassLimits"/> on mainnet, testnet, simnet, and devnet.
    /// The per-transaction cap and the relay rate change when Toccata activates.
    /// Before activation the cap is <see cref="MassCalculator.MaxStandardTransactionMass"/> (100000) and the rate is 1 sompi per gram.
    /// After activation the cap is the block compute limit and the rate is 100 sompi per gram.
    /// The storage-mass commitment field is a separate switch. It follows Crescendo, not Toccata.
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

        /// <summary>
        /// Creates a policy. Every limit and the relay rate must be non-zero.
        /// <paramref name="storageMassCommitmentRequired"/> defaults to true.
        /// <see cref="PreToccata"/> and <see cref="PostToccata"/> both require the commitment, because Crescendo
        /// is earlier than Toccata on mainnet and testnet-10. <see cref="ForNetwork"/> is the call that turns it off
        /// below the Crescendo score.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Block mass limits must be non-zero. Maximum transaction mass must be non-zero. Minimum relay fee rate
        /// must be non-zero.
        /// </exception>
        public MassPolicy(
            bool toccataActive,
            BlockMassLimits blockLimits,
            ulong maximumTransactionMass,
            ulong minimumRelayFeeRate,
            bool storageMassCommitmentRequired = true)
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
            StorageMassCommitmentRequired = storageMassCommitmentRequired;
        }

        /// <summary>
        /// Policy for <paramref name="network"/> at <paramref name="virtualDaaScore"/>.
        /// The transaction cap and the relay rate follow Toccata through <see cref="Network.MassPolicyAt"/>.
        /// Mainnet activates at 474165565 and testnet-10 at 467579632
        /// (rusty-kaspa <c>90dbf074</c> <c>consensus/core/src/config/params.rs</c>).
        /// Simnet and devnet use score 0, so every score is post-Toccata.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="StorageMassCommitmentRequired"/> follows Crescendo, not a separate storage-mass score.
        /// Mainnet is 110165000 and testnet-10 is 88657000 (<see cref="ConsensusParams"/>).
        /// Simnet and devnet use <c>ForkActivation::always()</c>, so the commitment is required at every score, including 0.
        /// </para>
        /// <para>
        /// rusty-kaspa never published a finite <c>storage_mass_activation</c> for mainnet or testnet-10.
        /// Pull request 588 set <c>MAINNET_PARAMS</c> and <c>TESTNET_PARAMS</c> to <c>ForkActivation::never()</c> (<c>u64::MAX</c>).
        /// Pull request 379 applied KIP-9 in the mempool on every network and in consensus only on testnet-11 (DAA 23325109, later always).
        /// Current master dropped <c>storage_mass_activation</c>.
        /// <c>consensus/core/src/tx.rs</c> <c>set_storage_mass</c>, still present at commit
        /// <c>f7d7877363fb97c2ec6871be0e8cbc351d44df10</c>, says the commitment field was activated on mainnet
        /// as part of the Crescendo hardfork.
        /// A fee quote still includes KIP-9 storage mass below that score. The mempool rule was not the <c>never()</c> consensus switch.
        /// </para>
        /// </remarks>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Unknown Kaspa network.</exception>
        public static MassPolicy ForNetwork(Network network, ulong virtualDaaScore)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            var relay = network.MassPolicyAt(virtualDaaScore);
            var crescendo = ConsensusParams.ForNetwork(network).CrescendoActivationDaaScore;
            return new MassPolicy(
                relay.ToccataActive,
                relay.BlockLimits,
                relay.MaximumTransactionMass,
                relay.MinimumRelayFeeRate,
                virtualDaaScore >= crescendo);
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
            PostToccataMinimumRelayFeeRate,
            true);

        /// <summary>
        /// Policy of a node before Toccata. Block dimension limits stay <see cref="BlockMassLimits.Default"/>
        /// because current rusty-kaspa master does not fork that triple.
        /// The transaction cap is 100000. The relay rate is 1 sompi per gram
        /// (<c>LEGACY_MINIMUM_RELAY_TRANSACTION_FEE</c> at rusty-kaspa <c>90dbf074</c> <c>mining/src/mempool/config.rs</c> line 28).
        /// Change below <c>C / 100000</c> (0.1 KAS) is folded into the fee.
        /// Use this for a node that has not activated Toccata. Current nodes may reject this rate.
        /// The storage-mass commitment stays required. Crescendo is earlier than Toccata on mainnet and testnet-10.
        /// </summary>
        public static MassPolicy PreToccata { get; } = new MassPolicy(
            false,
            BlockMassLimits.Default,
            MassCalculator.MaxStandardTransactionMass,
            PreToccataMinimumRelayFeeRate,
            true);

        /// <summary>True when the post-Toccata cap and 100 sompi per gram relay rate apply.</summary>
        public bool ToccataActive { get; }

        /// <summary>Block dimension limits used to normalize mass.</summary>
        public BlockMassLimits BlockLimits { get; }

        /// <summary>Largest fee mass the builder will produce.</summary>
        public ulong MaximumTransactionMass { get; }

        /// <summary>Minimum sompi charged for one gram of relay mass.</summary>
        public ulong MinimumRelayFeeRate { get; }

        /// <summary>
        /// True when a transaction must commit KIP-9 storage mass in the storage-mass field.
        /// <see cref="ForNetwork"/> sets this from <see cref="ConsensusParams.CrescendoActivationDaaScore"/>.
        /// rusty-kaspa <c>consensus/core/src/tx.rs</c> <c>set_storage_mass</c> says that field was activated on mainnet
        /// as part of the Crescendo hardfork (commit <c>f7d7877363fb97c2ec6871be0e8cbc351d44df10</c>).
        /// The KIP-9 formula used by a fee quote does not turn off when this is false.
        /// </summary>
        public bool StorageMassCommitmentRequired { get; }

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
