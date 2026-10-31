using System.Text.Json;
using NKaspa.Policy;
using NKaspa.RPC;
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
                .SendFees(new Money(1_000_000));
            var unsigned = builder.BuildTransaction(false);
            Assert.Equal(0, unsigned.Inputs[0].SignatureScript.Length);
            Assert.False(builder.Verify(unsigned));
        }

        [Fact]
        public void Paying_0_99_kas_from_1_kas_folds_the_small_change_instead_of_throwing()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            var coin = new Coin(new OutPoint(new uint256(7), 0), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(99_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(FeeRate.MinimumRelay);

            var tx = builder.BuildTransaction(sign: true);
            Assert.Single(tx.Outputs);
            Assert.Equal(99_000_000UL, tx.Outputs[0].Value.Sompi);
            Assert.True(builder.Verify(tx));
            Assert.True(tx.StorageMass <= BlockMassLimits.Default.Storage);
        }

        [Fact]
        public void Change_that_exceeds_the_mass_limit_is_folded_even_when_minimum_change_is_zero()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            var coin = new Coin(new OutPoint(new uint256(8), 0), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder
            {
                MinimumChange = Money.Zero,
            };
            builder.AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(99_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(FeeRate.MinimumRelay);

            var tx = builder.BuildTransaction(sign: true);
            Assert.Single(tx.Outputs);
            Assert.Equal(99_000_000UL, tx.Outputs[0].Value.Sompi);
        }

        [Fact]
        public void Storage_mass_between_100001_and_500000_can_be_built()
        {
            using var payer = Key.Parse(SecretA);
            var coin = new Coin(new OutPoint(new uint256(9), 0), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Mainnet), new Money(4_761_904))
                .SendFees(new Money(95_238_096));

            var tx = builder.BuildTransaction(sign: true);
            Assert.InRange(tx.StorageMass, 100_001UL, 500_000UL);
            Assert.True(builder.Verify(tx));

            var legacy = new TransactionBuilder(Network.Mainnet, toccataActive: false)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Mainnet), new Money(4_761_904))
                .SendFees(new Money(95_238_096));
            var error = Assert.Throws<InvalidOperationException>(() => legacy.BuildTransaction(true));
            Assert.Contains("mass", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(100_000UL, MassCalculator.MaxStandardTransactionMass);
        }

        [Fact]
        public void Explicit_fee_below_the_relay_floor_is_rejected_unless_allowed()
        {
            using var payer = Key.Parse(SecretA);
            var coin = new Coin(new OutPoint(new uint256(10), 0), new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payer.GetAddress(Network.Mainnet), new Money(50_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1));

            var error = Assert.Throws<InvalidOperationException>(() => builder.BuildTransaction(true));
            Assert.Contains("relay", error.Message, StringComparison.OrdinalIgnoreCase);

            builder.AllowBelowMinimumRelayFee = true;
            var tx = builder.BuildTransaction(true);
            Assert.Equal(2, tx.Outputs.Count);
            Assert.True(builder.Verify(tx));
        }

        [Theory]
        [InlineData(2_500_000UL)]
        [InlineData(5_000_000UL)]
        [InlineData(10_000_000UL)]
        public void Small_post_toccata_leftover_is_folded_into_the_fee(ulong leftover)
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            const ulong input = 100_000_000UL;
            var coin = new Coin(new OutPoint(new uint256(20), 0), new UtxoEntry(input, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(input - leftover))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(FeeRate.MinimumRelay);

            var tx = builder.BuildTransaction(sign: true);
            Assert.True(builder.ChangeFolded);
            Assert.Equal(leftover, builder.FoldedChange.Sompi);
            Assert.Single(tx.Outputs);
            Assert.Equal(input - leftover, tx.Outputs[0].Value.Sompi);
            Assert.Equal(leftover, input - tx.Outputs[0].Value.Sompi);
            Assert.True(builder.Verify(tx));
        }

        [Theory]
        [InlineData(30_000_000UL)]
        [InlineData(50_000_000UL)]
        public void Larger_post_toccata_leftover_keeps_the_change_output(ulong leftover)
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            const ulong input = 100_000_000UL;
            var coin = new Coin(new OutPoint(new uint256(21), 0), new UtxoEntry(input, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(input - leftover))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(FeeRate.MinimumRelay);

            var tx = builder.BuildTransaction(sign: true);
            Assert.False(builder.ChangeFolded);
            Assert.Equal(0UL, builder.FoldedChange.Sompi);
            Assert.Equal(2, tx.Outputs.Count);
            Assert.Equal(input - leftover, tx.Outputs[0].Value.Sompi);
            var fee = input - tx.Outputs[0].Value.Sompi - tx.Outputs[1].Value.Sompi;
            Assert.True(fee < leftover);
            Assert.True(tx.Outputs[1].Value.Sompi > 0);
            Assert.True(builder.Verify(tx));
        }

        [Fact]
        public void Fold_cap_refuses_a_small_leftover_when_the_caller_tightens_it()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            const ulong input = 100_000_000UL;
            const ulong leftover = 2_500_000UL;
            var coin = new Coin(new OutPoint(new uint256(22), 0), new UtxoEntry(input, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder
            {
                MaxFoldedFeeFactor = 1,
            };
            builder.AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(input - leftover))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(FeeRate.MinimumRelay);

            var error = Assert.Throws<InvalidOperationException>(() => builder.BuildTransaction(true));
            Assert.Contains("mass", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Split the send or add inputs", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Not enough funds", error.Message, StringComparison.Ordinal);
            Assert.False(builder.ChangeFolded);
            Assert.Equal(0UL, builder.FoldedChange.Sompi);
        }

        [Fact]
        public void Post_toccata_fifty_kas_payment_keeps_change_at_rate_150()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            const ulong input = 5_000_000_000UL;
            const ulong send = 10_000_000UL;
            var coin = new Coin(new OutPoint(new uint256(23), 0), new UtxoEntry(input, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(send))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(new FeeRate(150));

            var tx = builder.BuildTransaction(sign: true);
            Assert.False(builder.ChangeFolded);
            Assert.Equal(2, tx.Outputs.Count);
            Assert.Equal(send, tx.Outputs[0].Value.Sompi);
            var fee = input - tx.Outputs[0].Value.Sompi - tx.Outputs[1].Value.Sompi;
            Assert.InRange(fee, 1UL, 20_000_000UL);
            Assert.True(tx.Outputs[1].Value.Sompi > 4_000_000_000UL);
            Assert.True(builder.Verify(tx));
        }

        [Fact]
        public void Pre_toccata_fifty_kas_payment_at_rate_150_refuses_to_burn_the_change()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            const ulong input = 5_000_000_000UL;
            const ulong send = 10_000_000UL;
            var coin = new Coin(new OutPoint(new uint256(24), 0), new UtxoEntry(input, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder(Network.Mainnet, toccataActive: false)
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(send))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendEstimatedFees(new FeeRate(150));

            var error = Assert.Throws<InvalidOperationException>(() => builder.BuildTransaction(true));
            Assert.Contains("mass", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Split the send or add inputs", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Not enough funds", error.Message, StringComparison.Ordinal);
            Assert.False(builder.ChangeFolded);
            Assert.Equal(0UL, builder.FoldedChange.Sompi);
        }

        [Fact]
        public void Duplicate_outpoints_are_spent_once()
        {
            using var payer = Key.Parse(SecretA);
            var outpoint = new OutPoint(new uint256(11), 4);
            var coin = new Coin(outpoint, new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var duplicate = new Coin(outpoint, new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin, duplicate)
                .Send(payer.GetAddress(Network.Mainnet), new Money(40_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1_000_000));
            var tx = builder.BuildTransaction(sign: true);
            Assert.Single(tx.Inputs);

            var small = new Coin(outpoint, new UtxoEntry(40_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var tooSmall = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(small, small)
                .Send(payer.GetAddress(Network.Mainnet), new Money(70_000_000))
                .SendFees(new Money(1_000));
            Assert.Throws<InvalidOperationException>(() => tooSmall.BuildTransaction(true));

            var other = new Coin(new OutPoint(new uint256(12), 0), new UtxoEntry(40_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));
            var both = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(small, other)
                .Send(payer.GetAddress(Network.Mainnet), new Money(70_000_000))
                .SendFees(new Money(10_000_000));
            Assert.Equal(2, both.BuildTransaction(true).Inputs.Count);
        }

        [Fact]
        public void Selector_uses_one_coin_when_it_covers_payment_fee_and_minimum_change()
        {
            using var payer = Key.Parse(SecretA);
            var huge = CoinOf(payer, 500_000_000, 30, 0);
            var fit = CoinOf(payer, 80_000_000, 31, 0);
            var builder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(
                    huge,
                    fit,
                    CoinOf(payer, 3_000_000, 32, 0),
                    CoinOf(payer, 3_000_000, 33, 0),
                    CoinOf(payer, 3_000_000, 34, 0),
                    CoinOf(payer, 3_000_000, 35, 0),
                    CoinOf(payer, 3_000_000, 36, 0))
                .Send(payer.GetAddress(Network.Mainnet), new Money(10_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1_000_000));

            var tx = builder.BuildTransaction(true);
            Assert.Single(tx.Inputs);
            Assert.Equal(fit.Outpoint, tx.Inputs[0].PreviousOutpoint);
            Assert.True(tx.VerifyInput(0, fit.Utxo));
            Assert.Equal(2, tx.Outputs.Count);
            Assert.True(builder.Verify(tx));
        }

        [Fact]
        public void Selector_combines_coins_only_when_no_single_coin_covers_and_skips_dust()
        {
            using var payer = Key.Parse(SecretA);
            var first = CoinOf(payer, 10_000_000, 40, 0);
            var second = CoinOf(payer, 10_000_000, 41, 0);
            var larger = CoinOf(payer, 12_000_000, 42, 0);
            var dust = CoinOf(payer, 1_000, 43, 0);
            var tx = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(dust, larger, first, second)
                .Send(payer.GetAddress(Network.Mainnet), new Money(15_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1_000_000))
                .BuildTransaction(true);

            Assert.Equal(2, tx.Inputs.Count);
            var spent = new List<OutPoint>();
            for (var i = 0; i < tx.Inputs.Count; i++)
            {
                spent.Add(tx.Inputs[i].PreviousOutpoint);
            }

            Assert.Contains(first.Outpoint, spent);
            Assert.Contains(second.Outpoint, spent);
            Assert.DoesNotContain(larger.Outpoint, spent);
            Assert.DoesNotContain(dust.Outpoint, spent);
            Assert.True(tx.VerifyInput(0, FindUtxo(tx, 0, first, second)));
            Assert.True(tx.VerifyInput(1, FindUtxo(tx, 1, first, second)));
            Assert.NotEqual(tx.Inputs[0].SignatureScript.ToBytes(), tx.Inputs[1].SignatureScript.ToBytes());
        }

        [Fact]
        public void Must_spend_signs_every_input_with_its_own_script()
        {
            using var payer = Key.Parse(SecretA);
            using var other = Key.Parse(SecretB);
            var large = CoinOf(payer, 100_000_000, 50, 0);
            var ecdsa = CoinOf(other, 8_000_000, 51, 1, ecdsa: true);
            var dust = CoinOf(payer, 1_000, 52, 2);
            var builder = new TransactionBuilder()
                .AddKeys(payer, other)
                .AddCoins(large)
                .MustSpend(ecdsa, dust)
                .MustSpend(large.Outpoint)
                .Send(payer.GetAddress(Network.Mainnet), new Money(10_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1_000_000));

            var tx = builder.BuildTransaction(true);
            Assert.Equal(new[] { ecdsa.Outpoint, dust.Outpoint, large.Outpoint }, tx.Inputs.Select(input => input.PreviousOutpoint).ToArray());
            Assert.True(tx.VerifyInput(0, ecdsa.Utxo));
            Assert.True(tx.VerifyInput(1, dust.Utxo));
            Assert.True(tx.VerifyInput(2, large.Utxo));
            Assert.False(tx.VerifyInput(0, large.Utxo));
            Assert.False(tx.VerifyInput(2, ecdsa.Utxo));
            Assert.Equal(66, tx.Inputs[0].SignatureScript.Length);
            Assert.Equal(66, tx.Inputs[1].SignatureScript.Length);
            Assert.Equal(0x41, tx.Inputs[0].SignatureScript.ToBytes()[0]);
            Assert.NotEqual(tx.Inputs[1].SignatureScript.ToBytes(), tx.Inputs[2].SignatureScript.ToBytes());
            Assert.True(builder.Verify(tx));

            var missing = new OutPoint(new uint256(99), 7);
            var unknown = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(large)
                .MustSpend(missing)
                .Send(payer.GetAddress(Network.Mainnet), new Money(1_000_000))
                .SendFees(new Money(1_000_000));
            Assert.Throws<InvalidOperationException>(() => unknown.BuildTransaction(true));
        }

        [Fact]
        public void Spend_all_coins_keeps_each_input_even_when_one_would_pay()
        {
            using var payer = Key.Parse(SecretA);
            var huge = CoinOf(payer, 500_000_000, 60, 0);
            var fit = CoinOf(payer, 80_000_000, 61, 0);
            var tx = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(fit, huge)
                .SpendAllCoins()
                .Send(payer.GetAddress(Network.Mainnet), new Money(10_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1_000_000))
                .BuildTransaction(true);

            Assert.Equal(huge.Outpoint, tx.Inputs[0].PreviousOutpoint);
            Assert.Equal(fit.Outpoint, tx.Inputs[1].PreviousOutpoint);
            Assert.True(tx.VerifyInput(0, huge.Utxo));
            Assert.True(tx.VerifyInput(1, fit.Utxo));
            Assert.NotEqual(tx.Inputs[0].SignatureScript.ToBytes(), tx.Inputs[1].SignatureScript.ToBytes());
        }

        [Fact]
        public void Accepted_mainnet_shapes_match_minimum_relay_fee()
        {
            // Accepted on mainnet by the integration tool. The records carry no keys.
            // 5fd8f8600950c36b1d9195d9afab0bbf5bde94594fd8246d2c2419c64f703868 version 0, 1 input, fee 203600, mass 2036.
            // 2febe6cafd5f78f8ce21636d48f060560b39a7529b00646a00cf5d62098aa3d4 version 0, 2 inputs, fee 315400, mass 3154.
            // 4d1c6a5e1b127278945673b3ed116786110b2b1ca44090c9f810a50e12350714 version 1, budget 10, fee 203800, mass 2038.
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            var singleCoin = CoinOf(payer, 100_000_000, 70, 0);
            var single = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(singleCoin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(20_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendMinimumRelayFee()
                .BuildTransaction(true);
            AssertShape(single, 0, 1, 20_000_000, 203_600, MassCalculator.StandardSchnorrPaymentMass, singleCoin);
            Assert.Equal(1, single.Inputs[0].SigOpCount);
            Assert.Null(single.Inputs[0].ComputeBudget);
            using (var json = JsonDocument.Parse(KaspaRpcJson.WriteTransaction(single)))
            {
                var input = json.RootElement.GetProperty("inputs")[0];
                Assert.Equal(1, input.GetProperty("sigOpCount").GetInt32());
                Assert.False(input.TryGetProperty("computeBudget", out _));
            }

            var left = CoinOf(payer, 50_000_000, 71, 0);
            var right = CoinOf(payer, 50_000_000, 72, 0);
            var multiBuilder = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(left, right)
                .MustSpend(left, right)
                .Send(payee.GetAddress(Network.Mainnet), new Money(20_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendMinimumRelayFee();
            var multi = multiBuilder.BuildTransaction(true);
            AssertShape(multi, 0, 2, 20_000_000, 315_400, MassCalculator.StandardTwoInputSchnorrPaymentMass, left, right);
            Assert.True(multi.VerifyInput(0, left.Utxo));
            Assert.True(multi.VerifyInput(1, right.Utxo));
            Assert.True(multiBuilder.Verify(multi));
            Assert.NotEqual(multi.Inputs[0].SignatureScript.ToBytes(), multi.Inputs[1].SignatureScript.ToBytes());

            var v1Coin = CoinOf(payer, 100_000_000, 73, 0);
            var v1Builder = new TransactionBuilder()
                .SetVersion(1)
                .AddKeys(payer)
                .AddCoins(v1Coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(10_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendMinimumRelayFee();
            var v1 = v1Builder.BuildTransaction(true);
            AssertShape(v1, 1, 1, 10_000_000, 203_800, MassCalculator.StandardSchnorrPaymentMassVersion1, v1Coin);
            Assert.Equal(TransactionBuilder.DefaultComputeBudget, v1.Inputs[0].ComputeBudget);
            Assert.Equal(0, v1.Inputs[0].SigOpCount);
            Assert.Equal(66, v1.Inputs[0].SignatureScript.Length);
            Assert.True(v1.VerifyInput(0, v1Coin.Utxo));
            Assert.True(v1Builder.Verify(v1));
            var version1Hash = v1.GetSignatureHash(0, v1Coin.Utxo);
            v1.Version = 0;
            Assert.NotEqual(version1Hash, v1.GetSignatureHash(0, v1Coin.Utxo));
            v1.Version = 1;
            Assert.True(v1.VerifyInput(0, v1Coin.Utxo));
            using (var json = JsonDocument.Parse(KaspaRpcJson.WriteTransaction(v1)))
            {
                var input = json.RootElement.GetProperty("inputs")[0];
                Assert.Equal(0, input.GetProperty("sigOpCount").GetInt32());
                Assert.Equal(10, input.GetProperty("computeBudget").GetInt32());
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => new TransactionBuilder().SetVersion(2));
            var conflict = new TransactionBuilder().SendMinimumRelayFee().SendEstimatedFees(FeeRate.MinimumRelay);
            Assert.Throws<InvalidOperationException>(() => conflict.AddKeys(payer).AddCoins(v1Coin).Send(payer.GetAddress(Network.Mainnet), new Money(1_000_000)).BuildTransaction(true));
        }

        [Fact]
        public void Version1_compute_budget_can_be_set_per_input()
        {
            using var payer = Key.Parse(SecretA);
            var first = CoinOf(payer, 50_000_000, 80, 0);
            var second = CoinOf(payer, 50_000_000, 81, 0);
            var tx = new TransactionBuilder()
                .SetVersion(1)
                .SetComputeBudget(10)
                .SetComputeBudget(second.Outpoint, 25)
                .AddKeys(payer)
                .MustSpend(first, second)
                .Send(payer.GetAddress(Network.Mainnet), new Money(10_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendMinimumRelayFee()
                .BuildTransaction(true);

            Assert.Equal((ushort)1, tx.Version);
            Assert.Equal((ushort)10, tx.Inputs[0].ComputeBudget);
            Assert.Equal((ushort)25, tx.Inputs[1].ComputeBudget);
            Assert.Equal(0, tx.Inputs[0].SigOpCount);
            Assert.Equal(0, tx.Inputs[1].SigOpCount);
            Assert.True(tx.VerifyInput(0, first.Utxo));
            Assert.True(tx.VerifyInput(1, second.Utxo));
            var mass = MassCalculator.Calculate(tx, new[] { first.Utxo, second.Utxo });
            Assert.Equal(4658UL, mass.ComputeMass);
            var paid = first.Amount.Sompi + second.Amount.Sompi - tx.Outputs[0].Value.Sompi - tx.Outputs[1].Value.Sompi;
            Assert.Equal(MassCalculator.MinimumRelayFee(mass), paid);
            Assert.Equal(TransactionBuilder.EstimateMinimumRelayFee(mass).Sompi, paid);
            using var json = JsonDocument.Parse(KaspaRpcJson.WriteTransaction(tx));
            var inputs = json.RootElement.GetProperty("inputs");
            Assert.Equal(0, inputs[0].GetProperty("sigOpCount").GetInt32());
            Assert.Equal(10, inputs[0].GetProperty("computeBudget").GetInt32());
            Assert.Equal(0, inputs[1].GetProperty("sigOpCount").GetInt32());
            Assert.Equal(25, inputs[1].GetProperty("computeBudget").GetInt32());
        }

        [Fact]
        public void Selector_prefers_an_exact_coin_and_the_greedy_path_covers_change()
        {
            using var payer = Key.Parse(SecretA);
            var exact = CoinOf(payer, 50_000_000, 90, 0);
            var bigger = CoinOf(payer, 80_000_000, 91, 0);
            var picked = new DefaultCoinSelector(new Money(2_000_000)).Select(new[] { bigger, exact }, new Money(50_000_000));
            Assert.Equal(exact.Outpoint, picked[0].Outpoint);

            var many = new List<Coin>();
            for (var i = 0; i < 17; i++)
            {
                many.Add(CoinOf(payer, 1_000_000, (ulong)(100 + i), 0));
            }

            var selected = new DefaultCoinSelector(new Money(2_000_000)).Select(many, new Money(5_000_000));
            Assert.Equal(7, selected.Count);
            ulong sum = 0;
            for (var i = 0; i < selected.Count; i++)
            {
                sum += selected[i].Amount.Sompi;
            }

            Assert.Equal(7_000_000UL, sum);
            Assert.Throws<InvalidOperationException>(() => new DefaultCoinSelector().Select(new[] { exact }, new Money(80_000_000)));
            Assert.Throws<ArgumentNullException>(() => new DefaultCoinSelector().Select(null!, new Money(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultCoinSelector().Select(new[] { exact }, Money.Zero));
        }

        private static void AssertShape(Transaction tx, ushort version, int inputs, ulong amount, ulong fee, ulong relayMass, params Coin[] coins)
        {
            Assert.Equal(version, tx.Version);
            Assert.Equal(inputs, tx.Inputs.Count);
            Assert.Equal(2, tx.Outputs.Count);
            Assert.Equal(amount, tx.Outputs[0].Value.Sompi);
            var entries = new UtxoEntry[tx.Inputs.Count];
            ulong inputSum = 0;
            for (var i = 0; i < tx.Inputs.Count; i++)
            {
                var match = FindCoin(tx.Inputs[i].PreviousOutpoint, coins);
                Assert.True(tx.VerifyInput(i, match.Utxo));
                entries[i] = match.Utxo;
                inputSum += match.Amount.Sompi;
            }

            var mass = MassCalculator.Calculate(tx, entries);
            Assert.Equal(relayMass, MassCalculator.RelayMass(mass));
            Assert.Equal(fee, MassCalculator.MinimumRelayFee(mass));
            ulong outputSum = 0;
            for (var i = 0; i < tx.Outputs.Count; i++)
            {
                outputSum += tx.Outputs[i].Value.Sompi;
            }

            Assert.Equal(fee, inputSum - outputSum);
        }

        private static UtxoEntry FindUtxo(Transaction tx, int index, params Coin[] coins)
        {
            return FindCoin(tx.Inputs[index].PreviousOutpoint, coins).Utxo;
        }

        private static Coin FindCoin(OutPoint outpoint, params Coin[] coins)
        {
            for (var i = 0; i < coins.Length; i++)
            {
                if (coins[i].Outpoint.Equals(outpoint))
                {
                    return coins[i];
                }
            }

            throw new InvalidOperationException("Input is not one of the expected coins.");
        }

        private static Coin CoinOf(Key key, ulong amount, ulong txId, uint index, bool ecdsa = false)
        {
            var script = ecdsa ? key.PubKeyECDSA.GetScriptPubKey() : key.PubKey.GetScriptPubKey();
            return new Coin(new OutPoint(new uint256(txId), index), new UtxoEntry(amount, script, 1, false));
        }
    }
}
