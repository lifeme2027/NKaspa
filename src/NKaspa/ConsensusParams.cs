namespace NKaspa
{
    /// <summary>
    /// Coinbase maturity for one Kaspa network, measured in DAA score.
    /// rusty-kaspa <c>consensus/core/src/config/params.rs</c> stores <c>blockrate: BlockrateParams::new::&lt;10&gt;()</c>
    /// on mainnet, testnet (suffix 10), simnet, and devnet.
    /// <c>BlockrateParams::new</c> sets <c>coinbase_maturity</c> from <c>Bps::coinbase_maturity</c>
    /// (<c>consensus/core/src/config/bps.rs</c>), which is <c>BPS * COINBASE_MATURITY_SECONDS</c>.
    /// <c>COINBASE_MATURITY_SECONDS</c> is 100 (<c>consensus/core/src/config/constants.rs</c>).
    /// At 10 blocks per second that value is 1000. At 1 block per second it is 100.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mainnet <c>crescendo_activation</c> is <c>ForkActivation::new(110_165_000)</c> (<c>params.rs</c> <c>MAINNET_PARAMS</c>).
    /// <c>pre_crescendo_target_time_per_block</c> is 1000 milliseconds, so maturity before that score is 100.
    /// Testnet-10 uses <c>ForkActivation::new(88_657_000)</c> and the same 1000 millisecond pre-Crescendo target (<c>TESTNET_PARAMS</c>).
    /// Simnet and devnet use <c>ForkActivation::always()</c> and a 10 BPS pre-Crescendo target, so maturity is 1000 at every score.
    /// </para>
    /// <para>
    /// Current master has no <c>TESTNET11_PARAMS</c>. <c>From&lt;NetworkId&gt;</c> accepts testnet suffix 10 only.
    /// Historical testnet-11 was the 10 BPS network. kaspanet/kaspad <c>kaspawallet</c> commit
    /// <c>25c2dd86709b5a0b889785e7e6b0c00c8827a8b7</c> used coinbase maturity 1000 when the network name was <c>kaspa-testnet-11</c>.
    /// <see cref="Testnet11"/> therefore uses 1000 at every DAA score. It does not change <see cref="Network.Testnet"/>,
    /// whose address prefix <c>kaspatest</c> is shared by every testnet. <see cref="ForNetwork"/> maps <see cref="Network.Testnet"/> to testnet-10.
    /// </para>
    /// <para>
    /// A current node's <c>TransactionValidator</c> is built with one <c>params.coinbase_maturity()</c>, the post-blockrate value 1000
    /// (<c>consensus/src/processes/transaction_validator/tx_validation_in_utxo_context.rs</c>).
    /// <see cref="CoinbaseMaturityAt"/> still applies the BPS-scaled value at the point-of-view DAA, so pre-Crescendo boundaries stay testable.
    /// On a live post-Crescendo node, pass a virtual DAA at or above <see cref="CrescendoActivationDaaScore"/>, or use <see cref="CoinbaseMaturity"/>.
    /// The check is immature when <c>is_coinbase &amp;&amp; block_daa_score + coinbase_maturity &gt; pov_daa_score</c>.
    /// Equality is mature. This helper subtracts instead of adding, so a sum that would wrap is immature.
    /// </para>
    /// </remarks>
    public sealed class ConsensusParams
    {
        /// <summary>Coinbase maturity in DAA score before Crescendo, when the network ran at 1 block per second.</summary>
        public const ulong PreCrescendoMaturity = 100;

        /// <summary>Coinbase maturity in DAA score at 10 blocks per second. This is <c>params.coinbase_maturity()</c> on a current binary.</summary>
        public const ulong PostCrescendoMaturity = 1000;

        /// <summary>Mainnet Crescendo activation DAA score, 110165000. <c>params.rs</c> <c>MAINNET_PARAMS.crescendo_activation</c>.</summary>
        public const ulong MainnetCrescendoActivationDaaScore = 110_165_000;

        /// <summary>Testnet-10 Crescendo activation DAA score, 88657000. <c>params.rs</c> <c>TESTNET_PARAMS.crescendo_activation</c>.</summary>
        public const ulong Testnet10CrescendoActivationDaaScore = 88_657_000;

        private ConsensusParams(string name, ulong crescendoActivationDaaScore, ulong preCrescendoCoinbaseMaturity, ulong coinbaseMaturity)
        {
            Name = name;
            CrescendoActivationDaaScore = crescendoActivationDaaScore;
            PreCrescendoCoinbaseMaturity = preCrescendoCoinbaseMaturity;
            CoinbaseMaturity = coinbaseMaturity;
        }

        /// <summary>Mainnet. Maturity is 100 before DAA 110165000 and 1000 at and after it.</summary>
        public static ConsensusParams Mainnet { get; } = new ConsensusParams("mainnet", MainnetCrescendoActivationDaaScore, PreCrescendoMaturity, PostCrescendoMaturity);

        /// <summary>Testnet-10. Maturity is 100 before DAA 88657000 and 1000 at and after it. This is <see cref="Network.Testnet"/>.</summary>
        public static ConsensusParams Testnet10 { get; } = new ConsensusParams("testnet-10", Testnet10CrescendoActivationDaaScore, PreCrescendoMaturity, PostCrescendoMaturity);

        /// <summary>Historical testnet-11. Maturity is 1000 at every DAA score. Current rusty-kaspa master has no parameter set for suffix 11.</summary>
        public static ConsensusParams Testnet11 { get; } = new ConsensusParams("testnet-11", 0, PostCrescendoMaturity, PostCrescendoMaturity);

        /// <summary>Simnet. <c>crescendo_activation</c> is <c>ForkActivation::always()</c>. Maturity is 1000 at every DAA score.</summary>
        public static ConsensusParams Simnet { get; } = new ConsensusParams("simnet", 0, PostCrescendoMaturity, PostCrescendoMaturity);

        /// <summary>Devnet. <c>crescendo_activation</c> is <c>ForkActivation::always()</c>. Maturity is 1000 at every DAA score.</summary>
        public static ConsensusParams Devnet { get; } = new ConsensusParams("devnet", 0, PostCrescendoMaturity, PostCrescendoMaturity);

        /// <summary>Short name: mainnet, testnet-10, testnet-11, simnet, or devnet.</summary>
        public string Name { get; }

        /// <summary>DAA score at which <see cref="CoinbaseMaturity"/> replaces <see cref="PreCrescendoCoinbaseMaturity"/>. Zero means every score uses <see cref="CoinbaseMaturity"/>.</summary>
        public ulong CrescendoActivationDaaScore { get; }

        /// <summary>Maturity used while the virtual DAA score is below <see cref="CrescendoActivationDaaScore"/>.</summary>
        public ulong PreCrescendoCoinbaseMaturity { get; }

        /// <summary>Post-blockrate coinbase maturity. 1000 on every built-in network.</summary>
        public ulong CoinbaseMaturity { get; }

        /// <summary>Maturity in DAA score at <paramref name="virtualDaaScore"/>.</summary>
        public ulong CoinbaseMaturityAt(ulong virtualDaaScore)
        {
            if (virtualDaaScore < CrescendoActivationDaaScore)
            {
                return PreCrescendoCoinbaseMaturity;
            }

            return CoinbaseMaturity;
        }

        /// <summary>
        /// Parameters for <paramref name="network"/>.
        /// <see cref="Network.Testnet"/> is testnet-10. Testnet-11 is <see cref="Testnet11"/> only.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Unknown Kaspa network.</exception>
        public static ConsensusParams ForNetwork(Network network)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            switch (network.Name)
            {
                case "mainnet":
                    return Mainnet;
                case "testnet":
                    return Testnet10;
                case "simnet":
                    return Simnet;
                case "devnet":
                    return Devnet;
                default:
                    throw new ArgumentException("Unknown Kaspa network '" + network.Name + "'.", nameof(network));
            }
        }

        /// <summary>
        /// True when <paramref name="entry"/> may be spent at <paramref name="virtualDaaScore"/> on <paramref name="network"/>.
        /// A non-coinbase entry is mature. A coinbase entry is mature when <paramref name="virtualDaaScore"/> is at least
        /// <see cref="UtxoEntry.BlockDaaScore"/> plus <see cref="CoinbaseMaturityAt"/>.
        /// <see cref="Network.Testnet"/> selects testnet-10.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="entry"/> is null. <paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Unknown Kaspa network.</exception>
        public static bool IsCoinbaseMature(UtxoEntry entry, ulong virtualDaaScore, Network network)
        {
            return IsCoinbaseMature(entry, virtualDaaScore, ForNetwork(network));
        }

        /// <summary>
        /// True when <paramref name="entry"/> may be spent at <paramref name="virtualDaaScore"/> under <paramref name="parameters"/>.
        /// A non-coinbase entry is mature. A coinbase entry is mature when the DAA age is at least <see cref="CoinbaseMaturityAt"/>.
        /// A virtual score below <see cref="UtxoEntry.BlockDaaScore"/> is immature. The comparison does not wrap.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="entry"/> is null. <paramref name="parameters"/> is null.</exception>
        public static bool IsCoinbaseMature(UtxoEntry entry, ulong virtualDaaScore, ConsensusParams parameters)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (!entry.IsCoinbase)
            {
                return true;
            }

            if (virtualDaaScore < entry.BlockDaaScore)
            {
                return false;
            }

            var maturity = parameters.CoinbaseMaturityAt(virtualDaaScore);
            return virtualDaaScore - entry.BlockDaaScore >= maturity;
        }

        /// <summary>
        /// First virtual DAA score at which <paramref name="entry"/> is spendable on <paramref name="network"/>.
        /// A non-coinbase entry returns <see cref="UtxoEntry.BlockDaaScore"/>.
        /// <see cref="ulong.MaxValue"/> means the coinbase age never reaches maturity inside a <see cref="ulong"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="entry"/> is null. <paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Unknown Kaspa network.</exception>
        public static ulong SpendableAtDaaScore(UtxoEntry entry, Network network)
        {
            return SpendableAtDaaScore(entry, ForNetwork(network));
        }

        /// <summary>
        /// First virtual DAA score at which <see cref="IsCoinbaseMature(UtxoEntry, ulong, ConsensusParams)"/> is true for <paramref name="entry"/>.
        /// A non-coinbase entry returns <see cref="UtxoEntry.BlockDaaScore"/>.
        /// A coinbase entry tries <see cref="UtxoEntry.BlockDaaScore"/> plus <see cref="PreCrescendoCoinbaseMaturity"/>,
        /// then plus <see cref="CoinbaseMaturity"/>. A sum that does not fit saturates to <see cref="ulong.MaxValue"/>.
        /// <see cref="ulong.MaxValue"/> is returned when that saturated score is still immature.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="entry"/> is null. <paramref name="parameters"/> is null.</exception>
        public static ulong SpendableAtDaaScore(UtxoEntry entry, ConsensusParams parameters)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (!entry.IsCoinbase)
            {
                return entry.BlockDaaScore;
            }

            var pre = AddSaturating(entry.BlockDaaScore, parameters.PreCrescendoCoinbaseMaturity);
            if (IsCoinbaseMature(entry, pre, parameters))
            {
                return pre;
            }

            var post = AddSaturating(entry.BlockDaaScore, parameters.CoinbaseMaturity);
            if (IsCoinbaseMature(entry, post, parameters))
            {
                return post;
            }

            return ulong.MaxValue;
        }

        private static ulong AddSaturating(ulong blockDaaScore, ulong maturity)
        {
            if (blockDaaScore > ulong.MaxValue - maturity)
            {
                return ulong.MaxValue;
            }

            return blockDaaScore + maturity;
        }
    }
}
