using NKaspa.Policy;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// One case for each <see cref="KaspaTransactionBuilderReason"/>.
    /// Amounts are local. The hex keys are the same public test scalars as the builder tests.
    /// </summary>
    public class TransactionBuilderExceptionTests
    {
        private const string SecretA = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string SecretB = "B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF";

        [Fact]
        public void Insufficient_funds_reports_needed_and_available()
        {
            using var payer = Key.Parse(SecretA);
            var coin = CoinOf(payer, 1000, 4, 0);
            var poor = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Mainnet), new Money(5000))
                .SendFees(new Money(100));

            KaspaTransactionBuilderException? caught = null;
            try
            {
                poor.BuildTransaction(true);
            }
            catch (InvalidOperationException error)
            {
                caught = Assert.IsType<KaspaTransactionBuilderException>(error);
            }

            Assert.NotNull(caught);
            Assert.Equal(KaspaTransactionBuilderReason.InsufficientFunds, caught.Reason);
            Assert.Equal("Not enough funds.", caught.Message);
            Assert.Equal(5100UL, caught.Needed);
            Assert.Equal(1000UL, caught.Available);
            Assert.Equal(0UL, poor.ChangeFoldedIntoFee.Sompi);

            var shortfall = AssertBuilder(() => new DefaultCoinSelector().Select(new[] { coin }, new Money(80_000_000)));
            Assert.Equal(KaspaTransactionBuilderReason.InsufficientFunds, shortfall.Reason);
            Assert.Equal(80_000_000UL, shortfall.Needed);
            Assert.Equal(1000UL, shortfall.Available);

            var left = CoinOf(payer, (ulong.MaxValue / 2) + 1, 5, 0);
            var right = CoinOf(payer, (ulong.MaxValue / 2) + 1, 6, 0);
            var overflow = AssertBuilder(() => new DefaultCoinSelector().Select(new[] { left, right }, new Money(left.Amount.Sompi + 1)));
            Assert.Equal(KaspaTransactionBuilderReason.InsufficientFunds, overflow.Reason);
            Assert.Equal(left.Amount.Sompi + 1, overflow.Needed);
            Assert.Null(overflow.Available);
        }

        [Fact]
        public void Mass_above_the_cap_reports_mass_and_cap()
        {
            using var payer = Key.Parse(SecretA);
            var coin = CoinOf(payer, 100_000_000, 9, 0);
            var before = new TransactionBuilder(Network.Mainnet, toccataActive: false)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Mainnet), new Money(4_761_904))
                .SendFees(new Money(95_238_096));
            var error = AssertBuilder(() => before.BuildTransaction(true));
            Assert.Equal(KaspaTransactionBuilderReason.MassExceeded, error.Reason);
            Assert.Equal("Transaction mass exceeds the standard maximum.", error.Message);
            Assert.Equal(100_000UL, error.Cap);
            Assert.True(error.Mass > error.Cap);
            Assert.Null(error.Needed);
            Assert.Null(error.Change);

            var after = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Mainnet), new Money(4_761_904))
                .SendFees(new Money(95_238_096));
            var tx = after.BuildTransaction(true);
            var mass = MassCalculator.Calculate(tx, new[] { coin.Utxo }, before.MassLimits);
            Assert.Equal(mass.FeeMass, error.Mass);
        }

        [Fact]
        public void Unfoldable_change_reports_the_fold_cap()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            const ulong input = 5_000_000_000UL;
            const ulong send = 10_000_000UL;
            var coin = CoinOf(payer, input, 24, 0);
            var builder = new TransactionBuilder(Network.Mainnet, toccataActive: false)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(send))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(new FeeRate(150));
            var error = AssertBuilder(() => builder.BuildTransaction(true));
            var cap = builder.MinimumChange.Sompi > builder.MaxFoldedChange.Sompi
                ? builder.MinimumChange.Sompi
                : builder.MaxFoldedChange.Sompi;
            Assert.Equal(KaspaTransactionBuilderReason.ChangeUnfoldable, error.Reason);
            Assert.Equal(
                "Change would exceed the mass limit. Folding it into the fee would burn more than MaxFoldedChange allows. Split the send or add inputs so the change output fits.",
                error.Message);
            Assert.Equal(cap, error.MaxFoldedChange);
            Assert.Equal(10_000_000UL, error.MaxFoldedChange);
            Assert.True(error.Change > error.MaxFoldedChange);
            Assert.Null(error.Mass);
            Assert.Equal(0UL, builder.ChangeFoldedIntoFee.Sompi);
        }

        [Fact]
        public void Fee_below_the_relay_floor_reports_fee_and_minimum()
        {
            using var payer = Key.Parse(SecretA);
            var coin = CoinOf(payer, 100_000_000, 10, 0);
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Mainnet), new Money(50_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1));
            var error = AssertBuilder(() => builder.BuildTransaction(true));
            Assert.Equal(KaspaTransactionBuilderReason.FeeBelowMinimum, error.Reason);
            Assert.Equal("Fee is below the minimum relay rate of 100 sompi per gram.", error.Message);
            Assert.Equal(1UL, error.Fee);
            Assert.True(error.MinimumFee > error.Fee);

            builder.AllowBelowMinimumRelayFee = true;
            var tx = builder.BuildTransaction(true);
            var mass = MassCalculator.Calculate(tx, new[] { coin.Utxo });
            Assert.Equal(MassCalculator.RelayMass(mass) * builder.MinimumRelayFeeRate, error.MinimumFee);
            Assert.Equal(203_600UL, error.MinimumFee);
        }

        [Fact]
        public void Missing_key_reports_the_input_index()
        {
            using var payer = Key.Parse(SecretA);
            var coin = CoinOf(payer, 100_000_000, 5, 0);
            var builder = new TransactionBuilder()
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Mainnet), new Money(40_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1_000_000));
            var error = AssertBuilder(() => builder.BuildTransaction(true));
            Assert.Equal(KaspaTransactionBuilderReason.NoMatchingKey, error.Reason);
            Assert.Equal("No private key matches an input script.", error.Message);
            Assert.Equal(0, error.InputIndex);
            Assert.Null(error.Outpoint);
        }

        [Fact]
        public void Unknown_must_spend_reports_the_outpoint()
        {
            using var payer = Key.Parse(SecretA);
            var coin = CoinOf(payer, 100_000_000, 50, 0);
            var missing = new OutPoint(new uint256(99), 7);
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .MustSpend(missing)
                .Send(payer.GetAddress(Network.Mainnet), new Money(1_000_000))
                .SendFees(new Money(1_000_000));
            var error = AssertBuilder(() => builder.BuildTransaction(true));
            Assert.Equal(KaspaTransactionBuilderReason.MustSpendUnknown, error.Reason);
            Assert.Equal("A must-spend outpoint was not added with AddCoins.", error.Message);
            Assert.Equal(missing, error.Outpoint);
            Assert.Null(error.InputIndex);
        }

        [Fact]
        public void No_outputs_reports_that_reason()
        {
            var error = AssertBuilder(() => new TransactionBuilder().SendFees(new Money(1)).BuildTransaction(false));
            Assert.Equal(KaspaTransactionBuilderReason.NoOutputs, error.Reason);
            Assert.Equal("No outputs to send.", error.Message);
            Assert.Null(error.Needed);
            Assert.Null(error.Fee);
        }

        [Fact]
        public void Fee_estimate_that_does_not_settle_reports_that_reason()
        {
            using var payer = Key.Parse(SecretA);
            const ulong send = 1_000_000_000_000UL;
            const ulong surplus = 20_000_000UL;
            var builder = new TransactionBuilder
            {
                MinimumChange = Money.Zero,
            };
            builder.AddKeys(payer)
                .AddCoins(CoinOf(payer, send + surplus, 30, 0))
                .Send(payer.GetAddress(Network.Mainnet), new Money(send))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(FeeRate.MinimumRelay);
            var error = AssertBuilder(() => builder.BuildTransaction(false));
            Assert.Equal(KaspaTransactionBuilderReason.FeeEstimateDidNotConverge, error.Reason);
            Assert.Equal("Fee estimate did not converge.", error.Message);
            Assert.Null(error.Needed);
            Assert.Null(error.Mass);
            Assert.Equal(0UL, builder.ChangeFoldedIntoFee.Sompi);
        }

        [Fact]
        public void Unknown_built_input_reports_the_outpoint()
        {
            var outpoint = new OutPoint(new uint256(4), 1);
            var error = KaspaTransactionBuilderException.UnknownInput(outpoint);
            Assert.IsAssignableFrom<InvalidOperationException>(error);
            Assert.Equal(KaspaTransactionBuilderReason.UnknownInput, error.Reason);
            Assert.Equal("Built input is not a known coin.", error.Message);
            Assert.Equal(outpoint, error.Outpoint);
            Assert.Null(error.Needed);
            Assert.Throws<ArgumentNullException>(() => KaspaTransactionBuilderException.UnknownInput(null!));
        }

        [Fact]
        public void Missing_change_script_reports_the_change()
        {
            using var payer = Key.Parse(SecretA);
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(CoinOf(payer, 100_000_000, 11, 0))
                .Send(payer.GetAddress(Network.Mainnet), new Money(40_000_000))
                .SendFees(new Money(1_000_000));
            var error = AssertBuilder(() => builder.BuildTransaction(true));
            Assert.Equal(KaspaTransactionBuilderReason.ChangeScriptMissing, error.Reason);
            Assert.Equal("Change is due and no change script was set.", error.Message);
            Assert.Equal(59_000_000UL, error.Change);
            Assert.Null(error.MaxFoldedChange);
        }

        [Fact]
        public void Two_fee_modes_report_the_conflict()
        {
            using var payer = Key.Parse(SecretA);
            var builder = new TransactionBuilder()
                .Send(payer.GetAddress(Network.Mainnet), new Money(1000))
                .SendFees(new Money(1000))
                .SendEstimatedFees(FeeRate.MinimumRelay);
            var error = AssertBuilder(() => builder.BuildTransaction(false));
            Assert.Equal(KaspaTransactionBuilderReason.ConflictingFeeModes, error.Reason);
            Assert.Equal("Set only one of SendFees, SendEstimatedFees, or SendMinimumRelayFee.", error.Message);
            Assert.Null(error.Fee);
        }

        [Fact]
        public void Missing_fee_mode_reports_that_reason()
        {
            using var payer = Key.Parse(SecretA);
            var builder = new TransactionBuilder()
                .AddCoins(CoinOf(payer, 100_000_000, 12, 0))
                .Send(payer.GetAddress(Network.Mainnet), new Money(1000));
            var error = AssertBuilder(() => builder.BuildTransaction(false));
            Assert.Equal(KaspaTransactionBuilderReason.FeeModeMissing, error.Reason);
            Assert.Equal("Set SendFees, SendEstimatedFees, or SendMinimumRelayFee.", error.Message);
            Assert.Null(error.Fee);
            Assert.Null(error.Needed);
        }

        private static KaspaTransactionBuilderException AssertBuilder(Action build)
        {
            var error = Assert.Throws<KaspaTransactionBuilderException>(build);
            Assert.IsAssignableFrom<InvalidOperationException>(error);
            return error;
        }

        private static Coin CoinOf(Key key, ulong amount, ulong txId, uint index)
        {
            return new Coin(new OutPoint(new uint256(txId), index), new UtxoEntry(amount, key.PubKey.GetScriptPubKey(), 1, false));
        }
    }
}
