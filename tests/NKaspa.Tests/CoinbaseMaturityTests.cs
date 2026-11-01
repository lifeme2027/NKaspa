using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Coinbase maturity boundaries. Equality with the DAA age is mature.
    /// </summary>
    public class CoinbaseMaturityTests
    {
        [Fact]
        public void Built_in_networks_use_the_upstream_post_blockrate_maturity()
        {
            Assert.Equal(1000UL, ConsensusParams.Mainnet.CoinbaseMaturity);
            Assert.Equal(1000UL, ConsensusParams.Testnet10.CoinbaseMaturity);
            Assert.Equal(1000UL, ConsensusParams.Testnet11.CoinbaseMaturity);
            Assert.Equal(1000UL, ConsensusParams.Simnet.CoinbaseMaturity);
            Assert.Equal(1000UL, ConsensusParams.Devnet.CoinbaseMaturity);
            Assert.Equal(ConsensusParams.Testnet10, ConsensusParams.ForNetwork(Network.Testnet));
            Assert.Equal(100UL, ConsensusParams.Mainnet.CoinbaseMaturityAt(ConsensusParams.MainnetCrescendoActivationDaaScore - 1));
            Assert.Equal(1000UL, ConsensusParams.Mainnet.CoinbaseMaturityAt(ConsensusParams.MainnetCrescendoActivationDaaScore));
            Assert.Equal(100UL, ConsensusParams.Testnet10.CoinbaseMaturityAt(ConsensusParams.Testnet10CrescendoActivationDaaScore - 1));
            Assert.Equal(1000UL, ConsensusParams.Testnet10.CoinbaseMaturityAt(ConsensusParams.Testnet10CrescendoActivationDaaScore));
            Assert.Equal(1000UL, ConsensusParams.Testnet11.CoinbaseMaturityAt(0));
            Assert.Equal(1000UL, ConsensusParams.Simnet.CoinbaseMaturityAt(0));
            Assert.Equal(1000UL, ConsensusParams.Devnet.CoinbaseMaturityAt(0));
        }

        [Theory]
        [InlineData("mainnet")]
        [InlineData("testnet")]
        public void Pre_crescendo_coinbase_matures_at_100_and_not_at_99(string networkName)
        {
            var network = Network.GetNetwork(networkName);
            var entry = Coinbase(1_000);
            Assert.False(ConsensusParams.IsCoinbaseMature(entry, 1_099, network));
            Assert.True(ConsensusParams.IsCoinbaseMature(entry, 1_100, network));
            Assert.Equal(1_100UL, ConsensusParams.SpendableAtDaaScore(entry, network));
        }

        [Fact]
        public void Mainnet_and_testnet_10_switch_at_their_own_activation_scores()
        {
            var activation = ConsensusParams.Testnet10CrescendoActivationDaaScore;
            var block = activation - 50;
            var entry = Coinbase(block);
            Assert.False(ConsensusParams.IsCoinbaseMature(entry, block + 100, ConsensusParams.Testnet10));
            Assert.True(ConsensusParams.IsCoinbaseMature(entry, block + 1000, ConsensusParams.Testnet10));
            Assert.Equal(block + 1000, ConsensusParams.SpendableAtDaaScore(entry, ConsensusParams.Testnet10));
            Assert.True(ConsensusParams.IsCoinbaseMature(entry, block + 100, ConsensusParams.Mainnet));
            Assert.Equal(block + 100, ConsensusParams.SpendableAtDaaScore(entry, Network.Mainnet));

            var mainnetActivation = ConsensusParams.MainnetCrescendoActivationDaaScore;
            var crossing = Coinbase(mainnetActivation - 100);
            Assert.False(ConsensusParams.IsCoinbaseMature(crossing, mainnetActivation - 1, Network.Mainnet));
            Assert.False(ConsensusParams.IsCoinbaseMature(crossing, mainnetActivation, Network.Mainnet));
            Assert.True(ConsensusParams.IsCoinbaseMature(crossing, mainnetActivation - 100 + 1000, Network.Mainnet));
            Assert.Equal(mainnetActivation - 100 + 1000, ConsensusParams.SpendableAtDaaScore(crossing, ConsensusParams.Mainnet));
        }

        [Theory]
        [InlineData("testnet11")]
        [InlineData("simnet")]
        [InlineData("devnet")]
        public void Ten_bps_networks_always_require_1000(string name)
        {
            var parameters = name == "testnet11"
                ? ConsensusParams.Testnet11
                : ConsensusParams.ForNetwork(Network.GetNetwork(name));
            var entry = Coinbase(5);
            Assert.False(ConsensusParams.IsCoinbaseMature(entry, 1_004, parameters));
            Assert.True(ConsensusParams.IsCoinbaseMature(entry, 1_005, parameters));
            Assert.Equal(1_005UL, ConsensusParams.SpendableAtDaaScore(entry, parameters));
            Assert.False(ConsensusParams.IsCoinbaseMature(Coinbase(0), 999, parameters));
            Assert.True(ConsensusParams.IsCoinbaseMature(Coinbase(0), 1000, parameters));
        }

        [Fact]
        public void Non_coinbase_is_mature_and_a_younger_virtual_score_is_not()
        {
            var ordinary = new UtxoEntry(1UL, new ScriptPubKey(0, Array.Empty<byte>()), 50, false);
            Assert.True(ConsensusParams.IsCoinbaseMature(ordinary, 0, Network.Mainnet));
            Assert.Equal(50UL, ConsensusParams.SpendableAtDaaScore(ordinary, Network.Devnet));

            var coinbase = Coinbase(10);
            Assert.False(ConsensusParams.IsCoinbaseMature(coinbase, 9, Network.Simnet));
            Assert.Throws<ArgumentNullException>(() => ConsensusParams.IsCoinbaseMature(null!, 1, Network.Mainnet));
            Assert.Throws<ArgumentNullException>(() => ConsensusParams.SpendableAtDaaScore(coinbase, (ConsensusParams)null!));
        }

        [Fact]
        public void Overflow_saturates_and_stays_immature()
        {
            var entry = Coinbase(ulong.MaxValue - 50);
            Assert.False(ConsensusParams.IsCoinbaseMature(entry, ulong.MaxValue, ConsensusParams.Mainnet));
            Assert.Equal(ulong.MaxValue, ConsensusParams.SpendableAtDaaScore(entry, ConsensusParams.Simnet));

            var almost = Coinbase(ulong.MaxValue - 999);
            Assert.Equal(ulong.MaxValue, ConsensusParams.SpendableAtDaaScore(almost, ConsensusParams.Devnet));
            Assert.False(ConsensusParams.IsCoinbaseMature(almost, ulong.MaxValue, ConsensusParams.Testnet11));
        }

        private static UtxoEntry Coinbase(ulong blockDaaScore)
        {
            return new UtxoEntry(1UL, new ScriptPubKey(0, Array.Empty<byte>()), blockDaaScore, true);
        }
    }
}
