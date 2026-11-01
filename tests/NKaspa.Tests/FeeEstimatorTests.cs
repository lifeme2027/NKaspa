using NKaspa.Crypto;
using NKaspa.Policy;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Fee estimates without keys must match MassCalculator on the signed transaction of the same shape.
    /// The 1-in/2-out Schnorr vector is the payment accepted on mainnet at mass 2036.
    /// </summary>
    public class FeeEstimatorTests
    {
        private const string SecretA = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string SecretB = "B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF";

        [Fact]
        public void Schnorr_shape_matches_the_signed_payment()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            var coin = CoinOf(payer, 100_000_000, 70, 0);
            var tx = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(20_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendMinimumRelayFee()
                .BuildTransaction(true);

            Assert.Equal(66, tx.Inputs[0].SignatureScript.Length);
            var signed = MassCalculator.Calculate(tx, new[] { coin.Utxo });
            var inputs = new[] { FeeInput.Schnorr(coin.Amount.Sompi) };
            var outputs = Outputs(tx, ecdsa: false);
            var fromScripts = ActualOutputs(tx);

            AssertSame(signed, FeeEstimator.Estimate(inputs, outputs));
            AssertSame(signed, FeeEstimator.Estimate(inputs, fromScripts));
            Assert.Equal(MassCalculator.StandardSchnorrPaymentMass, signed.ComputeMass);
            Assert.Equal(2036UL, signed.ComputeMass);
            Assert.Equal(34, inputs[0].ScriptPublicKey.Script.Length);

            var quote = FeeEstimator.Quote(inputs, outputs);
            Assert.Equal(FeeEstimator.DefaultMinimumFeerateSompiPerGram, quote.Feerate.SompiPerGram);
            Assert.Equal(100UL, quote.Feerate.SompiPerGram);
            Assert.Equal(203_600UL, quote.RelayFee.Sompi);
            Assert.Equal(MassCalculator.MinimumRelayFee(signed), quote.RelayFee.Sompi);
            Assert.Equal(Paid(tx, coin), quote.RelayFee.Sompi);
            Assert.Equal(52_531UL, signed.StorageMass);
            Assert.Equal(52_531UL, signed.FeeMass);
            Assert.Equal(5_253_100UL, quote.Fee.Sompi);
            Assert.True(quote.Fee.Sompi > quote.RelayFee.Sompi);

            var balanced = FeeEstimator.Quote(
                new[] { FeeInput.Schnorr(5_675_155_239) },
                new[] { FeeOutput.Schnorr(500_000_000), FeeOutput.Schnorr(5_171_555_239) });
            Assert.Equal(2036UL, balanced.Mass.ComputeMass);
            Assert.Equal(2017UL, balanced.Mass.StorageMass);
            Assert.Equal(2036UL, balanced.Mass.FeeMass);
            Assert.Equal(203_600UL, balanced.RelayFee.Sompi);
            Assert.Equal(203_600UL, balanced.Fee.Sompi);
        }

        [Fact]
        public void Two_input_and_version1_shapes_match_the_signed_payment()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            var left = CoinOf(payer, 50_000_000, 71, 0);
            var right = CoinOf(payer, 50_000_000, 72, 0);
            var multi = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(left, right)
                .MustSpend(left, right)
                .Send(payee.GetAddress(Network.Mainnet), new Money(20_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendMinimumRelayFee()
                .BuildTransaction(true);
            var multiMass = MassCalculator.Calculate(multi, new[] { Find(multi, 0, left, right).Utxo, Find(multi, 1, left, right).Utxo });
            var multiInputs = new[]
            {
                FeeInput.Schnorr(Find(multi, 0, left, right).Amount.Sompi),
                FeeInput.Schnorr(Find(multi, 1, left, right).Amount.Sompi)
            };
            AssertSame(multiMass, FeeEstimator.Estimate(multiInputs, Outputs(multi, false)));
            Assert.Equal(3154UL, multiMass.ComputeMass);
            var multiQuote = FeeEstimator.Quote(multiInputs, Outputs(multi, false));
            Assert.Equal(315_400UL, multiQuote.RelayFee.Sompi);
            Assert.Equal(Paid(multi, left, right), multiQuote.RelayFee.Sompi);
            Assert.Equal(multiMass.FeeMass * 100, multiQuote.Fee.Sompi);

            var v1Coin = CoinOf(payer, 100_000_000, 73, 0);
            var v1 = new TransactionBuilder()
                .SetVersion(1)
                .AddKeys(payer)
                .AddCoins(v1Coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(10_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendMinimumRelayFee()
                .BuildTransaction(true);
            var v1Mass = MassCalculator.Calculate(v1, new[] { v1Coin.Utxo });
            var v1Inputs = new[] { FeeInput.Schnorr(v1Coin.Amount.Sompi) };
            AssertSame(v1Mass, FeeEstimator.Estimate(v1Inputs, Outputs(v1, false), 1));
            Assert.Equal(MassCalculator.StandardSchnorrPaymentMassVersion1, v1Mass.ComputeMass);
            Assert.Equal(2038UL, v1Mass.ComputeMass);
            Assert.Equal(203_800UL, FeeEstimator.Quote(v1Inputs, Outputs(v1, false), version: 1).RelayFee.Sompi);
            Assert.Equal((ushort)10, v1.Inputs[0].ComputeBudget);

            var custom = new TransactionBuilder()
                .SetVersion(1)
                .SetComputeBudget(25)
                .AddKeys(payer)
                .AddCoins(v1Coin)
                .Send(payee.GetAddress(Network.Mainnet), new Money(10_000_000))
                .SetChange(payer.GetAddress(Network.Mainnet))
                .SendFees(new Money(1_000_000))
                .BuildTransaction(true);
            var customMass = MassCalculator.Calculate(custom, new[] { v1Coin.Utxo });
            AssertSame(customMass, FeeEstimator.Estimate(new[] { FeeInput.Schnorr(v1Coin.Amount.Sompi, 25) }, Outputs(custom, false), 1));
            Assert.True(customMass.ComputeMass > v1Mass.ComputeMass);
        }

        [Fact]
        public void Ecdsa_shape_matches_the_signed_payment()
        {
            using var payer = Key.Parse(SecretA);
            using var payee = Key.Parse(SecretB);
            var coin = CoinOf(payer, 80_000_000, 80, 0, ecdsa: true);
            var tx = new TransactionBuilder()
                .AddKeys(payer)
                .AddCoins(coin)
                .Send(payee.GetEcdsaAddress(Network.Mainnet), new Money(20_000_000))
                .SetChange(payer.GetEcdsaAddress(Network.Mainnet))
                .SendMinimumRelayFee()
                .BuildTransaction(true);

            Assert.Equal(35, tx.Outputs[0].ScriptPublicKey.Script.Length);
            Assert.Equal(66, tx.Inputs[0].SignatureScript.Length);
            var signed = MassCalculator.Calculate(tx, new[] { coin.Utxo });
            var inputs = new[] { FeeInput.Ecdsa(coin.Amount.Sompi) };
            Assert.Equal(35, inputs[0].ScriptPublicKey.Script.Length);
            AssertSame(signed, FeeEstimator.Estimate(inputs, Outputs(tx, ecdsa: true)));
            AssertSame(signed, FeeEstimator.Estimate(inputs, ActualOutputs(tx)));
            Assert.Equal(Paid(tx, coin), FeeEstimator.Quote(inputs, Outputs(tx, true)).RelayFee.Sompi);
        }

        [Fact]
        public void Script_hash_shape_matches_a_placeholder_signature_script()
        {
            var key = new byte[32];
            key[31] = 1;
            var redeem = Script.PayToPubKey(key);
            var locking = new ScriptPubKey(0, Script.PayToScriptHash(redeem));
            var signature = Script.PayToScriptHashSignature(redeem, Script.SignatureScript(new SchnorrSignature(new byte[SchnorrSignature.Length]), SigHashType.All));
            const ulong amount = 50_000_000;
            const ulong send = 10_000_000;
            const ulong change = 39_000_000;
            var pay = FeeOutput.Schnorr(send);
            var back = FeeOutput.Schnorr(change);
            var tx = new Transaction(
                0,
                new[] { new TxIn(new OutPoint(uint256.Zero, 0), signature, 0, 1) },
                new[] { new TxOut(send, pay.ScriptPublicKey), new TxOut(change, back.ScriptPublicKey) },
                0,
                SubnetworkId.Native,
                0,
                System.Array.Empty<byte>());
            var signed = MassCalculator.Calculate(tx, new[] { new UtxoEntry(amount, locking, 0, false) });
            var input = FeeInput.ScriptHash(amount, redeem);
            Assert.Equal(signature.Length, input.SignatureScriptLength);
            Assert.Equal(35, input.ScriptPublicKey.Script.Length);
            AssertSame(signed, FeeEstimator.Estimate(new[] { input }, new[] { pay, back }));
        }

        [Fact]
        public void Storage_mass_can_exceed_relay_mass()
        {
            var inputs = new[] { FeeInput.Schnorr(5_675_155_239) };
            var outputs = new[] { FeeOutput.Schnorr(12_000_000), FeeOutput.Schnorr(5_654_655_239) };
            var mass = FeeEstimator.Estimate(inputs, outputs);
            Assert.Equal(2036UL, mass.ComputeMass);
            Assert.Equal(83_333UL, mass.StorageMass);
            Assert.Equal(83_333UL, mass.FeeMass);
            Assert.Equal(2036UL, MassCalculator.RelayMass(mass));

            var quote = FeeEstimator.Quote(inputs, outputs);
            Assert.Equal(203_600UL, quote.RelayFee.Sompi);
            Assert.Equal(8_333_300UL, quote.Fee.Sompi);
            Assert.True(quote.Fee.Sompi > quote.RelayFee.Sompi);
        }

        [Fact]
        public void Minimum_feerate_reads_the_lowest_positive_bucket()
        {
            Assert.Equal(100UL, FeeEstimator.DefaultMinimumFeerate.SompiPerGram);
            Assert.Equal(MassCalculator.MinimumFeeRate, FeeEstimator.DefaultMinimumFeerateSompiPerGram);

            var low = Estimate(new RpcFeeBucket(5.5, 1), new[] { new RpcFeeBucket(1, 2) }, new[] { new RpcFeeBucket(0.5, 3) });
            Assert.Equal(1UL, FeeEstimator.MinimumFeerate(low).SompiPerGram);
            Assert.Equal(100UL, FeeEstimator.PayableFeerate(low).SompiPerGram);

            var high = Estimate(new RpcFeeBucket(400, 1), new[] { new RpcFeeBucket(250.5, 2) }, new[] { new RpcFeeBucket(300, 3) });
            Assert.Equal(251UL, FeeEstimator.MinimumFeerate(high).SompiPerGram);
            Assert.Equal(251UL, FeeEstimator.PayableFeerate(high).SompiPerGram);

            var skipped = Estimate(
                new RpcFeeBucket(double.NaN, 1),
                new[] { new RpcFeeBucket(double.PositiveInfinity, 1), new RpcFeeBucket(0, 1), new RpcFeeBucket(-3, 1) },
                new[] { new RpcFeeBucket(3.2, 1) });
            Assert.Equal(4UL, FeeEstimator.MinimumFeerate(skipped).SompiPerGram);

            var empty = new RpcFeeEstimate(null, System.Array.Empty<RpcFeeBucket>(), System.Array.Empty<RpcFeeBucket>());
            Assert.Equal(100UL, FeeEstimator.MinimumFeerate(empty).SompiPerGram);
            Assert.Throws<ArgumentNullException>(() => FeeEstimator.MinimumFeerate(null!));
            Assert.Throws<OverflowException>(() => FeeEstimator.MinimumFeerate(Estimate(new RpcFeeBucket(1e20, 1), System.Array.Empty<RpcFeeBucket>(), System.Array.Empty<RpcFeeBucket>())));

            var inputs = new[] { FeeInput.Schnorr(100_000_000) };
            var outputs = new[] { FeeOutput.Schnorr(20_000_000), FeeOutput.Schnorr(79_796_400) };
            var shaped = FeeEstimator.Estimate(inputs, outputs);
            Assert.Equal(2036UL, shaped.ComputeMass);
            var fromNode = FeeEstimator.Quote(inputs, outputs, low);
            Assert.Equal(100UL, fromNode.Feerate.SompiPerGram);
            Assert.Equal(203_600UL, fromNode.RelayFee.Sompi);
            Assert.Equal(shaped.FeeMass * 100, fromNode.Fee.Sompi);
            var raised = FeeEstimator.Quote(inputs, outputs, high);
            Assert.Equal(251UL, raised.Feerate.SompiPerGram);
            Assert.Equal(2036UL * 251UL, raised.RelayFee.Sompi);
            Assert.Equal(shaped.FeeMass * 251, raised.Fee.Sompi);

            var below = Assert.Throws<ArgumentOutOfRangeException>(() => FeeEstimator.Quote(inputs, outputs, new FeeRate(99)));
            Assert.Contains("below the network minimum of 100 sompi/gram", below.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => FeeEstimator.Quote(inputs, outputs, new FeeRate(1), minimumFeerateSompiPerGram: 0));
            var legacy = FeeEstimator.Quote(inputs, outputs, new FeeRate(1), minimumFeerateSompiPerGram: 1);
            Assert.Equal(1UL, legacy.Feerate.SompiPerGram);
            Assert.Equal(2036UL, legacy.RelayFee.Sompi);
            Assert.Throws<ArgumentNullException>(() => FeeEstimator.Quote(inputs, outputs, (RpcFeeEstimate)null!));
        }

        [Fact]
        public async Task ReadMinimumFeerateAsync_returns_the_node_bucket()
        {
            var client = new FeeClient(Estimate(new RpcFeeBucket(5.5, 1), new[] { new RpcFeeBucket(1, 2) }, new[] { new RpcFeeBucket(0.5, 3) }));
            var rate = await FeeEstimator.ReadMinimumFeerateAsync(client);
            Assert.Equal(1UL, rate.SompiPerGram);
            await Assert.ThrowsAsync<ArgumentNullException>(() => FeeEstimator.ReadMinimumFeerateAsync(null!));
        }

        [Fact]
        public void Missing_scripts_and_zero_amounts_throw()
        {
            Assert.Throws<ArgumentNullException>(() => FeeEstimator.Estimate(null!, new[] { FeeOutput.Schnorr(1) }));
            Assert.Throws<ArgumentNullException>(() => FeeEstimator.Estimate(new[] { FeeInput.Schnorr(1) }, null!));
            Assert.Throws<ArgumentException>(() => FeeEstimator.Estimate(new[] { default(FeeInput) }, new[] { FeeOutput.Schnorr(1) }));
            Assert.Throws<ArgumentException>(() => FeeEstimator.Estimate(new[] { FeeInput.Schnorr(1) }, new[] { default(FeeOutput) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FeeInput(1, ScriptPubKey.PayToPubKey(new byte[32]), -1, 1, null, false));
            Assert.Throws<ArgumentNullException>(() => FeeInput.ScriptHash(1, null!));
            Assert.Throws<ArgumentException>(() => FeeEstimator.Estimate(new[] { FeeInput.Schnorr(0) }, new[] { FeeOutput.Schnorr(1) }));
            Assert.Throws<ArgumentException>(() => FeeEstimator.Estimate(System.Array.Empty<FeeInput>(), new[] { FeeOutput.Schnorr(1) }));
        }

        private static void AssertSame(TransactionMass left, TransactionMass right)
        {
            Assert.Equal(left.ComputeMass, right.ComputeMass);
            Assert.Equal(left.TransientMass, right.TransientMass);
            Assert.Equal(left.StorageMass, right.StorageMass);
            Assert.Equal(left.NormalizedTransientMass, right.NormalizedTransientMass);
            Assert.Equal(left.NormalizedStorageMass, right.NormalizedStorageMass);
            Assert.Equal(left.FeeMass, right.FeeMass);
        }

        private static FeeOutput[] Outputs(Transaction tx, bool ecdsa)
        {
            var outputs = new FeeOutput[tx.Outputs.Count];
            for (var i = 0; i < tx.Outputs.Count; i++)
            {
                var amount = tx.Outputs[i].Value.Sompi;
                outputs[i] = ecdsa ? FeeOutput.Ecdsa(amount) : FeeOutput.Schnorr(amount);
            }

            return outputs;
        }

        private static FeeOutput[] ActualOutputs(Transaction tx)
        {
            var outputs = new FeeOutput[tx.Outputs.Count];
            for (var i = 0; i < tx.Outputs.Count; i++)
            {
                outputs[i] = FeeOutput.Of(tx.Outputs[i].Value.Sompi, tx.Outputs[i].ScriptPublicKey, tx.Outputs[i].Covenant != null);
            }

            return outputs;
        }

        private static ulong Paid(Transaction tx, params Coin[] coins)
        {
            ulong input = 0;
            ulong output = 0;
            for (var i = 0; i < tx.Inputs.Count; i++)
            {
                input += Find(tx, i, coins).Amount.Sompi;
            }

            for (var i = 0; i < tx.Outputs.Count; i++)
            {
                output += tx.Outputs[i].Value.Sompi;
            }

            return input - output;
        }

        private static Coin Find(Transaction tx, int index, params Coin[] coins)
        {
            var outpoint = tx.Inputs[index].PreviousOutpoint;
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

        private static RpcFeeEstimate Estimate(RpcFeeBucket? priority, RpcFeeBucket[] normal, RpcFeeBucket[] low)
        {
            return new RpcFeeEstimate(priority, normal, low);
        }

        private sealed class FeeClient : IKaspaRpcClient
        {
            private readonly RpcFeeEstimate _estimate;

            public FeeClient(RpcFeeEstimate estimate)
            {
                _estimate = estimate;
            }

            public Task<RpcFeeEstimate> GetFeeEstimateAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_estimate);
            }

            public Task<RpcServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<RpcBlockDagInfo> GetBlockDagInfoAsync(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<ulong> GetSinkBlueScoreAsync(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<RpcVirtualChain> GetVirtualChainFromBlockAsync(uint256 startHash, bool includeAcceptedTransactionIds = false, ulong? minConfirmationCount = null, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyList<RpcUtxo>> GetUtxosByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<Money> GetBalanceByAddressAsync(string address, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<uint256> SubmitTransactionAsync(Transaction transaction, bool allowOrphan = false, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<RpcChainBlock?> GetBlockAsync(uint256 hash, bool includeTransactions = false, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<IReadOnlyList<RpcAddressBalance>> GetBalancesByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<KaspaSubscription> NotifyUtxosChangedAsync(IReadOnlyList<string> addresses, Action<RpcUtxosChanged> onChanged, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<KaspaSubscription> NotifyVirtualDaaScoreChangedAsync(Action<RpcVirtualDaaScoreChanged> onChanged, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<KaspaSubscription> NotifyBlockAddedAsync(Action<RpcBlockAdded> onAdded, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
    }
}
