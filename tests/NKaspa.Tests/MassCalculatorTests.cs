using NKaspa.Policy;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Storage and normalization rows are the rusty-kaspa tests in
    /// consensus/core/src/mass/mod.rs (test_storage_mass, test_storage_mass_pluralities,
    /// verify_utxo_plurality_limits, test_normalized_max_ranking), via the kaswallet port
    /// KasWallet.Core.Tests.MassCalculatorTests.
    /// The three amount triples were measured at api.kaspa.org /transactions/mass and are not upstream unit tests.
    /// </summary>
    public class MassCalculatorTests
    {
        private const ulong C12 = 1_000_000_000_000UL;
        private const ulong Kas = 100_000_000UL;

        [Fact]
        public void Constants_match_rusty_kaspa()
        {
            Assert.Equal(1_000_000_000_000UL, MassCalculator.StorageMassParameter);
            Assert.Equal(4UL, MassCalculator.TransientByteToMassFactor);
            Assert.Equal(1UL, MassCalculator.MassPerTxByte);
            Assert.Equal(10UL, MassCalculator.MassPerScriptPubKeyByte);
            Assert.Equal(1000UL, MassCalculator.MassPerSigOp);
            Assert.Equal(500_000UL, BlockMassLimits.Default.Storage);
            Assert.Equal(500_000UL, BlockMassLimits.Default.Compute);
            Assert.Equal(1_000_000UL, BlockMassLimits.Default.Transient);
        }

        [Fact]
        public void Storage_cases_from_rusty_kaspa()
        {
            Assert.Equal(0UL, Storage(new ulong[] { 100, 200, 300 }, new ulong[] { 300, 300 }, C12));
            Assert.Equal(C12 / 50 + C12 / 550 - 3 * (C12 / 200), Storage(new ulong[] { 100, 200, 300 }, new ulong[] { 50, 550 }, C12));
            var b = 10_000 * Kas;
            Assert.Equal(4UL, Storage(new[] { b, b, b * 2 }, new[] { b, b, b, b }));
            Assert.Equal(1003UL, Storage(new[] { b, b, b * 2 }, new[] { 10 * Kas, b, b, b }));
            Assert.Equal(0UL, Storage(new[] { b + 4, b, b * 2 }, new[] { b + 1, b + 1, b + 1, b + 1 }));
            Assert.Equal(9_000_000_000UL, Storage(new ulong[] { 100, 200 }, new ulong[] { 50, 250 }, C12));
            Assert.Equal(0UL, Storage(new ulong[] { 100, 200 }, new ulong[] { 100, 200 }, C12));
            Assert.Equal(5_000_000_000UL, Storage(new ulong[] { 100, 200 }, new ulong[] { 50 }, C12));
        }

        [Theory]
        [MemberData(nameof(PluralityCases))]
        public void Super_entry_matches_split_entries(ulong[] ins1, ulong[] outs1, ulong[] ins2, ulong[] outs2, int index, int plurality, bool output)
        {
            var leftIn = Cells(ins1);
            var leftOut = Cells(outs1);
            var rightIn = Cells(ins2);
            var rightOut = Cells(outs2);
            var p = MassCalculator.UtxoPlurality((plurality - 1) * 100);
            Assert.Equal((ulong)plurality, p);
            if (output)
            {
                rightOut[index] = new UtxoCell(p, rightOut[index].Amount);
            }
            else
            {
                rightIn[index] = new UtxoCell(p, rightIn[index].Amount);
            }

            var mass1 = MassCalculator.StorageMass(leftIn, leftOut, C12);
            var mass2 = MassCalculator.StorageMass(rightIn, rightOut, C12);
            Assert.NotEqual(0UL, mass1);
            Assert.Equal(mass1, mass2);
        }

        public static IEnumerable<object[]> PluralityCases()
        {
            yield return new object[] { new ulong[] { 300, 200, 200 }, new ulong[] { 200, 200, 200, 100 }, new ulong[] { 300, 400 }, new ulong[] { 200, 200, 200, 100 }, 1, 2, false };
            yield return new object[] { new ulong[] { 350, 400 }, new ulong[] { 300, 200, 200 }, new ulong[] { 350, 400 }, new ulong[] { 300, 400 }, 1, 2, true };
            yield return new object[] { new ulong[] { 500 }, new ulong[] { 200, 200 }, new ulong[] { 500 }, new ulong[] { 400 }, 0, 2, true };
            yield return new object[] { new ulong[] { 1000 }, new ulong[] { 200, 200, 200 }, new ulong[] { 1000 }, new ulong[] { 200, 400 }, 1, 2, true };
            yield return new object[] { new[] { 1000 * Kas }, new[] { 200 * Kas, 200 * Kas, 200 * Kas }, new[] { 1000 * Kas }, new[] { 200 * Kas, 400 * Kas }, 1, 2, true };
            yield return new object[] { new[] { 1000 * Kas }, new[] { 200 * Kas, 200 * Kas }, new[] { 1000 * Kas }, new[] { 400 * Kas }, 0, 2, true };
            yield return new object[] { new[] { 350 * Kas, 500 * Kas }, new[] { 200 * Kas, 200 * Kas }, new[] { 350 * Kas, 500 * Kas }, new[] { 400 * Kas }, 0, 2, true };
            yield return new object[]
            {
                new[] { 350 * Kas, 500 * Kas, 350 * Kas, 500 * Kas },
                new[] { 200 * Kas, 200 * Kas, 400 * Kas, 250 * Kas, 250 * Kas, 250 * Kas },
                new[] { 350 * Kas, 500 * Kas, 350 * Kas, 500 * Kas },
                new[] { 200 * Kas, 200 * Kas, 400 * Kas, 750 * Kas },
                3, 3, true,
            };
        }

        [Fact]
        public void Plurality_boundaries_and_normalization_match_rusty_kaspa()
        {
            Assert.Equal(1UL, MassCalculator.UtxoPlurality(0));
            Assert.Equal(1UL, MassCalculator.UtxoPlurality(100 - 63));
            Assert.Equal(2UL, MassCalculator.UtxoPlurality(100 - 63 + 1));
            Assert.Equal(2UL, MassCalculator.UtxoPlurality(100 - 63, hasCovenantId: true));
            Assert.Equal(2UL, MassCalculator.UtxoPlurality(2 * 100 - 63 - 32, hasCovenantId: true));
            Assert.Equal(3UL, MassCalculator.UtxoPlurality(2 * 100 - 63 - 32 + 1, hasCovenantId: true));
            Assert.Equal(1UL, MassCalculator.UtxoPlurality(34));

            var limits = new BlockMassLimits(1_000_000, 500_000, 250_000);
            Assert.Equal(250_000UL, MassCalculator.NormalizedMax(0, 0, 500_000, limits));
            Assert.Equal(300_000UL, MassCalculator.NormalizedMax(300_000, 0, 0, limits));
            Assert.Equal(400_000UL, MassCalculator.NormalizedMax(0, 200_000, 0, limits));
            Assert.Equal(123_456UL, MassCalculator.NormalizedMax(123_456, 45_678, 78_901, new BlockMassLimits(78_901, 123_456, 45_678)));
        }

        [Fact]
        public void Api_kaspa_org_storage_samples()
        {
            Assert.Equal(83_333UL, Storage(new ulong[] { 5_675_155_239 }, new ulong[] { 12_000_000, 5_654_655_239 }));
            Assert.Equal(10_003UL, Storage(new ulong[] { 5_675_155_239 }, new ulong[] { 100_000_000, 5_573_155_239 }));
            Assert.Equal(2_017UL, Storage(new ulong[] { 5_675_155_239 }, new ulong[] { 500_000_000, 5_171_555_239 }));
        }

        [Fact]
        public void Blank_and_signed_p2pk_mass_use_the_consensus_size_not_ToBytes()
        {
            var blank = new Transaction();
            Assert.Equal(94UL, MassCalculator.EstimatedSerializedSize(blank));
            Assert.Equal(94UL, MassCalculator.ComputeMass(blank));
            Assert.Equal(94UL * 4, MassCalculator.TransientMass(blank));
            Assert.Equal(62, blank.ToBytes().Length);

            var signed = Tx(new[] { 66 }, new ulong[] { 500_000_000, 5_000_000_000 });
            Assert.Equal(316UL, MassCalculator.EstimatedSerializedSize(signed));
            Assert.Equal(2036UL, MassCalculator.ComputeMass(signed));
            Assert.Equal(316UL * 4, MassCalculator.TransientMass(signed));
            Assert.Equal(285, signed.ToBytes().Length);

            var mass = MassCalculator.Calculate(signed, new[] { Entry(5_675_155_239) });
            Assert.Equal(316UL * 2, mass.NormalizedTransientMass);
            Assert.Equal(2017UL, MassCalculator.Calculate(Tx(new[] { 66 }, new ulong[] { 500_000_000, 5_171_555_239 }), new[] { Entry(5_675_155_239) }).StorageMass);
            Assert.Equal(2036UL, mass.FeeMass);

            var small = MassCalculator.Calculate(Tx(new[] { 66 }, new ulong[] { 12_000_000, 5_654_655_239 }), new[] { Entry(5_675_155_239) });
            Assert.Equal(83_333UL, small.FeeMass);
        }

        [Fact]
        public void Compute_mass_grows_by_input_and_payload_and_coinbase_is_free()
        {
            var one = MassCalculator.ComputeMass(Tx(new[] { 66 }, new ulong[] { 100 }));
            var three = MassCalculator.ComputeMass(Tx(new[] { 66, 66, 66 }, new ulong[] { 100 }));
            Assert.Equal(2 * (118 + 1000), (long)(three - one));
            var withPayload = new Transaction(0, new[] { Input(66, 0) }, new[] { Output(100) }, 0, SubnetworkId.Native, 0, new byte[10]);
            Assert.Equal(one + 10, MassCalculator.ComputeMass(withPayload));

            var coinbase = new Transaction(0, Array.Empty<TxIn>(), new[] { Output(100) }, 0, SubnetworkId.Coinbase, 0, Array.Empty<byte>());
            var free = MassCalculator.Calculate(coinbase, Array.Empty<UtxoEntry>());
            Assert.Equal(0UL, free.FeeMass);
            Assert.Equal(0UL, free.ComputeMass);
            Assert.Equal(0UL, free.StorageMass);
        }

        [Fact]
        public void Errors_and_fee_product()
        {
            Assert.Throws<ArgumentException>(() => Storage(Array.Empty<ulong>(), new ulong[] { 100 }));
            Assert.Throws<ArgumentException>(() => Storage(new ulong[] { 100 }, Array.Empty<ulong>()));
            Assert.Throws<ArgumentException>(() => Storage(new ulong[] { 0 }, new ulong[] { 100 }));
            Assert.Throws<ArgumentException>(() => MassCalculator.Calculate(Tx(new[] { 66, 66 }, new ulong[] { 100 }), new[] { Entry(500) }));
            var version1 = new Transaction(1, Array.Empty<TxIn>(), Array.Empty<TxOut>(), 0, SubnetworkId.Native, 0, Array.Empty<byte>());
            Assert.Equal(94UL, MassCalculator.ComputeMass(version1));
            Assert.Equal(100UL, MassCalculator.MassPerComputeBudget);
            Assert.Equal(203_600UL, MassCalculator.EstimateFee(2036, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => MassCalculator.EstimateFee(2036, 99));
            Assert.Throws<OverflowException>(() => MassCalculator.EstimateFee(ulong.MaxValue, 100));
            Assert.Equal(new Money(203600), new FeeRate(100).GetFee(2036));
        }

        [Fact]
        public void Minimum_relay_fee_uses_compute_or_normalized_transient_only()
        {
            Assert.Equal(2036UL, MassCalculator.StandardSchnorrPaymentMass);
            Assert.Equal(2038UL, MassCalculator.StandardSchnorrPaymentMassVersion1);
            Assert.Equal(3154UL, MassCalculator.StandardTwoInputSchnorrPaymentMass);

            var standard = new TransactionMass(2036, 1264, 50_000, 632, 50_000, 50_000);
            Assert.Equal(2036UL, MassCalculator.RelayMass(standard));
            Assert.Equal(203_600UL, MassCalculator.MinimumRelayFee(standard));
            Assert.Equal(new Money(203_600), MassCalculator.EstimateMinimumRelayFee(standard));
            Assert.Equal(new Money(203_600), TransactionBuilder.EstimateMinimumRelayFee(standard));
            Assert.Equal(new Money(203_600), TransactionBuilder.EstimateFee(FeeRate.MinimumRelay, MassCalculator.StandardSchnorrPaymentMass));
            Assert.Equal(new Money(203_600), MassCalculator.EstimateFee(FeeRate.MinimumRelay, 2036UL));
            Assert.Equal(new Money(5_000_000), MassCalculator.EstimateFee(FeeRate.MinimumRelay, standard));
            Assert.Equal(Money.Coins(0.002036m), new Money(203_600));

            var transientWins = new TransactionMass(100, 1_000, 10, 500, 10, 500);
            Assert.Equal(500UL, MassCalculator.RelayMass(transientWins));
            Assert.Equal(50_000UL, MassCalculator.MinimumRelayFee(transientWins));
            Assert.Equal(new Money(50_000), FeeRate.MinimumRelay.GetFee(MassCalculator.RelayMass(transientWins)));
            Assert.Throws<ArgumentOutOfRangeException>(() => MassCalculator.EstimateFee(new FeeRate(99), 2036UL));
            Assert.Equal(new Money(99UL * 2036UL), new FeeRate(99).GetFee(2036));
        }

        private static ulong Storage(ulong[] inputs, ulong[] outputs, ulong parameter = MassCalculator.StorageMassParameter)
        {
            return MassCalculator.StorageMass(Cells(inputs), Cells(outputs), parameter);
        }

        private static UtxoCell[] Cells(ulong[] amounts)
        {
            var cells = new UtxoCell[amounts.Length];
            for (var i = 0; i < amounts.Length; i++)
            {
                cells[i] = new UtxoCell(1, amounts[i]);
            }

            return cells;
        }

        private static ScriptPubKey P2pk()
        {
            return ScriptPubKey.PayToPubKey(new byte[32]);
        }

        private static TxIn Input(int sigScriptLength, int marker)
        {
            var id = new byte[32];
            id[0] = (byte)marker;
            return new TxIn(new OutPoint(new uint256(id), (uint)marker), new Script(new byte[sigScriptLength]), 0, 1);
        }

        private static TxOut Output(ulong value)
        {
            return new TxOut(value, P2pk());
        }

        private static UtxoEntry Entry(ulong amount)
        {
            return new UtxoEntry(amount, P2pk(), 0, false);
        }

        private static Transaction Tx(int[] sigScriptLengths, ulong[] outputValues)
        {
            var inputs = new TxIn[sigScriptLengths.Length];
            for (var i = 0; i < sigScriptLengths.Length; i++)
            {
                inputs[i] = Input(sigScriptLengths[i], i);
            }

            var outputs = new TxOut[outputValues.Length];
            for (var i = 0; i < outputValues.Length; i++)
            {
                outputs[i] = Output(outputValues[i]);
            }

            return new Transaction(0, inputs, outputs, 0, SubnetworkId.Native, 0, Array.Empty<byte>());
        }
    }
}
