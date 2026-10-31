using NKaspa.Policy;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Activation scores: mainnet 474165565 from docs/toccata-guide.md line 3 at 4edcbbcc.
    /// Testnet-10 467579632 is params.rs line 783 on tag tn10-toc2 (97415b68) and line 784 on tn10-toc3 (1015a623).
    /// 476232000 is zk_hardening_activation on tn10-toc3 params.rs line 786, not the Toccata score.
    /// Block limits are the single triple in current params.rs. Activation changes the cap. The relay rate stays 100.
    /// </summary>
    public class MassPolicyTests
    {
        private const string SecretA = "0000000000000000000000000000000000000000000000000000000000000003";

        [Fact]
        public void Built_in_networks_share_post_toccata_block_limits_and_record_activation()
        {
            Assert.Equal(474_165_565UL, Network.MainnetToccataActivationDaaScore);
            Assert.Equal(467_579_632UL, Network.TestnetToccataActivationDaaScore);
            Assert.Equal(Network.MainnetToccataActivationDaaScore, Network.Mainnet.ToccataActivationDaaScore);
            Assert.Equal(Network.TestnetToccataActivationDaaScore, Network.Testnet.ToccataActivationDaaScore);
            Assert.Equal(0UL, Network.Simnet.ToccataActivationDaaScore);
            Assert.Equal(0UL, Network.Devnet.ToccataActivationDaaScore);

            foreach (var network in Network.All)
            {
                Assert.Equal(BlockMassLimits.Default, network.MassLimits);
                Assert.Equal(500_000UL, network.MassLimits.Compute);
                Assert.Equal(500_000UL, network.MassLimits.Storage);
                Assert.Equal(1_000_000UL, network.MassLimits.Transient);
                var current = new TransactionBuilder(network);
                Assert.Equal(MassPolicy.PostToccata.MaximumTransactionMass, current.MaximumMass);
                Assert.Equal(2_000_000UL, current.MinimumChange.Sompi);
                Assert.Equal(100UL, current.MinimumRelayFeeRate);
            }
        }

        [Fact]
        public void Virtual_daa_score_selects_the_pre_toccata_cap_only_below_activation()
        {
            var mainnetBefore = Network.Mainnet.MassPolicyAt(Network.MainnetToccataActivationDaaScore - 1);
            var mainnetAt = Network.Mainnet.MassPolicyAt(Network.MainnetToccataActivationDaaScore);
            Assert.False(mainnetBefore.ToccataActive);
            Assert.True(mainnetAt.ToccataActive);
            Assert.Equal(100_000UL, mainnetBefore.MaximumTransactionMass);
            Assert.Equal(100UL, mainnetBefore.MinimumRelayFeeRate);
            Assert.Equal(10_000_000UL, mainnetBefore.MinimumChange.Sompi);
            Assert.Equal(500_000UL, mainnetAt.MaximumTransactionMass);
            Assert.Equal(100UL, mainnetAt.MinimumRelayFeeRate);
            Assert.Equal(2_000_000UL, mainnetAt.MinimumChange.Sompi);

            Assert.False(Network.Testnet.IsToccataActive(Network.TestnetToccataActivationDaaScore - 1));
            Assert.True(Network.Testnet.IsToccataActive(Network.TestnetToccataActivationDaaScore));
            Assert.True(Network.Devnet.IsToccataActive(0));
            Assert.True(Network.Simnet.IsToccataActive(0));
            Assert.Equal(MassPolicy.PreToccata.MaximumTransactionMass, Network.Testnet.MassPolicyAt(0).MaximumTransactionMass);
            Assert.Equal(MassPolicy.PostToccata.MinimumRelayFeeRate, Network.Devnet.MassPolicyAt(0).MinimumRelayFeeRate);
        }

        [Fact]
        public void Builder_below_testnet_activation_rejects_mass_the_post_toccata_policy_allows()
        {
            using var payer = Key.Parse(SecretA);
            var coin = new Coin(new OutPoint(new uint256(9), 0), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var before = new TransactionBuilder(Network.Testnet, Network.TestnetToccataActivationDaaScore - 1)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Testnet), new Money(4_761_904))
                .SendFees(new Money(95_238_096));
            Assert.Equal(100_000UL, before.MaximumMass);
            Assert.Equal(10_000_000UL, before.MinimumChange.Sompi);
            Assert.Equal(100UL, before.MinimumRelayFeeRate);
            var error = Assert.Throws<InvalidOperationException>(() => before.BuildTransaction(true));
            Assert.Contains("mass", error.Message, StringComparison.OrdinalIgnoreCase);

            var at = new TransactionBuilder(Network.Testnet, Network.TestnetToccataActivationDaaScore)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Testnet), new Money(4_761_904))
                .SendFees(new Money(95_238_096));
            var tx = at.BuildTransaction(true);
            Assert.InRange(tx.StorageMass, 100_001UL, 500_000UL);
            Assert.True(at.Verify(tx));
        }

        [Fact]
        public void Pre_toccata_minimum_relay_fee_matches_the_post_toccata_rate()
        {
            Assert.Equal(100UL, MassPolicy.PreToccataMinimumRelayFeeRate);
            Assert.Equal(MassPolicy.PostToccataMinimumRelayFeeRate, MassPolicy.PreToccataMinimumRelayFeeRate);

            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse("B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF");
            var coin = new Coin(new OutPoint(new uint256(12), 0), new UtxoEntry(1_000_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder(Network.Mainnet, toccataActive: false)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), Money.Coins(1))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendMinimumRelayFee();

            var tx = builder.BuildTransaction(true);
            var paid = coin.Amount.Sompi - tx.Outputs[0].Value.Sompi - tx.Outputs[1].Value.Sompi;
            var mass = MassCalculator.Calculate(tx, new[] { coin.Utxo }, builder.MassLimits);
            Assert.Equal(MassCalculator.RelayMass(mass) * 100, paid);
            Assert.Equal(0UL, builder.ChangeFoldedIntoFee.Sompi);
        }

        [Fact]
        public void Toccata_activation_score_can_be_replaced_without_changing_the_network_identity()
        {
            var overridden = Network.Testnet.WithToccataActivationDaaScore(Network.TestnetZkHardeningActivationDaaScore);
            Assert.Equal(476_232_000UL, Network.TestnetZkHardeningActivationDaaScore);
            Assert.Equal(476_232_000UL, overridden.ToccataActivationDaaScore);
            Assert.Equal(Network.Testnet.AddressPrefix, overridden.AddressPrefix);
            Assert.Equal(Network.Testnet.P2PPort, overridden.P2PPort);
            Assert.Equal(Network.Testnet.ExtPubKeyVersion, overridden.ExtPubKeyVersion);
            Assert.Equal(467_579_632UL, Network.Testnet.ToccataActivationDaaScore);
            Assert.False(overridden.IsToccataActive(467_579_632));
            Assert.True(overridden.IsToccataActive(476_232_000));
            Assert.Equal(100UL, overridden.MassPolicyAt(467_579_632).MinimumRelayFeeRate);
            Assert.Equal(100_000UL, overridden.MassPolicyAt(467_579_632).MaximumTransactionMass);
            Assert.Equal(500_000UL, overridden.MassPolicyAt(476_232_000).MaximumTransactionMass);
            Assert.True(Network.Testnet.IsToccataActive(467_579_632));
        }

        [Fact]
        public void Custom_policy_sets_the_cap_and_the_dust_floor()
        {
            var limits = new BlockMassLimits(250_000, 250_000, 500_000);
            var policy = new MassPolicy(true, limits, 250_000, 50);
            var builder = new TransactionBuilder(Network.Devnet, policy);
            Assert.Equal(limits, builder.MassLimits);
            Assert.Equal(250_000UL, builder.MaximumMass);
            Assert.Equal(50UL, builder.MinimumRelayFeeRate);
            Assert.Equal(MassCalculator.StorageMassParameter / 250_000, builder.MinimumChange.Sompi);
            Assert.Throws<ArgumentOutOfRangeException>(() => new MassPolicy(true, limits, 0, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MassPolicy(false, default, 100, 1));
        }
    }
}
