using NKaspa.Policy;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// End-to-end builder coverage. Amounts are local. Mass numbers are checked against MassCalculator,
    /// which is itself checked against rusty-kaspa and the api.kaspa.org storage samples.
    /// </summary>
    public class TransactionBuilderTests
    {
        private const string SecretA = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string SecretB = "B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF";

        [Fact]
        public void Signed_schnorr_payment_has_change_and_verifies()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            var coin = new Coin(new OutPoint(new uint256(1), 0), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 10, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(40_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(FeeRate.MinimumRelay);

            var tx = builder.BuildTransaction(sign: true);
            Assert.Equal(2, tx.Outputs.Count);
            Assert.Equal(40_000_000UL, tx.Outputs[0].Value.Sompi);
            Assert.Equal(payee.PubKey.GetScriptPubKey(), tx.Outputs[0].ScriptPublicKey);
            Assert.Equal(payer.PubKey.GetScriptPubKey(), tx.Outputs[1].ScriptPublicKey);
            Assert.Equal(1, tx.Inputs[0].SigOpCount);
            Assert.Equal(66, tx.Inputs[0].SignatureScript.Length);
            Assert.True(tx.VerifyInput(0, coin.Utxo));
            Assert.True(builder.Verify(tx));

            var mass = MassCalculator.Calculate(tx, new[] { coin.Utxo });
            var fee = coin.Amount.Sompi - tx.Outputs[0].Value.Sompi - tx.Outputs[1].Value.Sompi;
            Assert.Equal(mass.FeeMass * FeeRate.MinimumRelay.SompiPerGram, fee);
            Assert.True(mass.FeeMass <= MassCalculator.MaxStandardTransactionMass);
            Assert.Equal(mass.StorageMass, tx.StorageMass);
            Assert.NotEqual(tx.GetId(), tx.GetHash());
        }

        [Fact]
        public void Change_below_the_minimum_is_folded_into_the_fee()
        {
            using var payer = Key.Parse(SecretA);
            var coin = new Coin(new OutPoint(new uint256(2), 1), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder
            {
                MinimumChange = new Money(1000),
            };
            builder.AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Testnet), new Money(50_000_000))
                .SetChange(payer.GetAddress(Network.Testnet))
                .SendFees(new Money(49_999_500));

            var tx = builder.BuildTransaction(sign: true);
            Assert.Single(tx.Outputs);
            Assert.Equal(50_000_000UL, tx.Outputs[0].Value.Sompi);
            Assert.Equal(50_000_000UL, coin.Amount.Sompi - tx.Outputs[0].Value.Sompi);
            Assert.True(builder.Verify(tx));
        }

        [Fact]
        public void Tiny_change_that_explodes_storage_mass_is_refused()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            var coin = new Coin(
                new OutPoint(new uint256(3), 0),
                new UtxoEntry(1_000_000_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(1000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1000));

            var error = Assert.Throws<InvalidOperationException>(() => builder.BuildTransaction(true));
            Assert.Contains("mass", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Missing_funds_missing_key_and_conflicting_fees_fail()
        {
            using var payer = Key.Parse(SecretA);
            var coin = new Coin(new OutPoint(new uint256(4), 0), new UtxoEntry(1000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var poor = new TransactionBuilder().AddKeys(payer).AddCoins(coin).Send(payer.GetAddress(Network.Mainnet), new Money(5000)).SendFees(new Money(100));
            Assert.Throws<InvalidOperationException>(() => poor.BuildTransaction(true));

            var rich = new Coin(new OutPoint(new uint256(5), 0), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var noKey = new TransactionBuilder().AddCoins(rich).Send(payer.GetAddress(Network.Mainnet), new Money(1000)).SendFees(new Money(100_000));
            Assert.Throws<InvalidOperationException>(() => noKey.BuildTransaction(true));

            var both = new TransactionBuilder().AddKeys(payer).AddCoins(rich).Send(payer.GetAddress(Network.Mainnet), new Money(1000)).SetChange(payer.GetAddress(Network.Mainnet));
            both.SendFees(new Money(1000));
            both.SendEstimatedFees(FeeRate.MinimumRelay);
            Assert.Throws<InvalidOperationException>(() => both.BuildTransaction(true));

            Assert.Throws<ArgumentOutOfRangeException>(() => new TransactionBuilder().SendEstimatedFees(new FeeRate(99)));
        }

        [Fact]
        public void Unsigned_transaction_does_not_verify()
        {
            using var payer = Key.Parse(SecretA);
            var coin = new Coin(new OutPoint(new uint256(6), 0), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Mainnet), new Money(40_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(200_000));
            var unsigned = builder.BuildTransaction(false);
            Assert.Equal(0, unsigned.Inputs[0].SignatureScript.Length);
            Assert.False(builder.Verify(unsigned));
        }
    }
}
