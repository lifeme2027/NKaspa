using System.Text;
using NKaspa.Crypto;
using NKaspa.Policy;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Version-1 vectors are tests 10 through 14 of rusty-kaspa
    /// consensus/core/src/hashing/tx.rs test_transaction_hashing (1-based).
    /// The covenant sighash layout is hashing/sighash.rs hash_output.
    /// </summary>
    public class ToccataTransactionTests
    {
        [Fact]
        public void Rusty_kaspa_tests_10_through_14()
        {
            var sharedId = uint256.Parse("59b3d6dc6cdc660c389c3fdb5704c48c598d279cdf1bab54182db586a4c95dd5");
            var input = new TxIn(new OutPoint(sharedId, 2), new Script(new byte[] { 1, 2 }), 7, 5);
            var output = new TxOut(1564UL, new ScriptPubKey(7, new byte[] { 1, 2, 3, 4, 5 }));
            var test10 = new Transaction(1, new[] { input }, new[] { output }, 54, SubnetworkId.Registry, 3, new byte[] { 1, 2, 3 });
            AssertIdHash(test10, "a08a500b21be3e692c080b14e399fcfa2cfa01b25c08f2f8e7414d1c116e8d18", "773f5582d847a1c48947eb4e6e6ac569f90f0f9d979b4c939b72ef008f025e02");

            var budget111 = Transaction11(111);
            var budget222 = Transaction11(222);
            AssertIdHash(budget111, "5978e7aa1a9ba8fdf12dae6aa39aa198a91985e91192b291e207d4d6246349e6", "c41c18964aab2abe309a79de3dcf0353eee216e29ab83448cbec0c4c5792056c");
            AssertIdHash(budget222, "5978e7aa1a9ba8fdf12dae6aa39aa198a91985e91192b291e207d4d6246349e6", "415dfbc5b38e5805e20831d43a49bc770f4f591b00964ac922d108f6a224c590");
            Assert.Equal(budget111.GetId(), budget222.GetId());
            Assert.NotEqual(budget111.GetHash(), budget222.GetHash());

            var sigop111 = TransactionSigop(111);
            var sigop222 = TransactionSigop(222);
            AssertIdHash(sigop111, "5978e7aa1a9ba8fdf12dae6aa39aa198a91985e91192b291e207d4d6246349e6", "55724643b090b9a1c1b9b93b03ffac9cb1bd913a1cf0605a36509322af825864");
            AssertIdHash(sigop222, "5978e7aa1a9ba8fdf12dae6aa39aa198a91985e91192b291e207d4d6246349e6", "55724643b090b9a1c1b9b93b03ffac9cb1bd913a1cf0605a36509322af825864");
            Assert.Equal(sigop111.GetHash(), sigop222.GetHash());

            var roundTrip = Transaction.FromBytes(budget111.ToBytes());
            Assert.Equal(budget111.GetId(), roundTrip.GetId());
            Assert.Equal(budget111.GetHash(), roundTrip.GetHash());
            Assert.Equal((ushort)111, roundTrip.Inputs[0].ComputeBudget);

            var withCovenant = Transaction11(111);
            withCovenant.Outputs.Add(new TxOut(50UL, new ScriptPubKey(0, new byte[] { 9 }))
            {
                Covenant = new CovenantBinding(0, new uint256(4)),
            });
            var restored = Transaction.FromBytes(withCovenant.ToBytes());
            Assert.NotNull(restored.Outputs[0].Covenant);
            Assert.Equal((ushort)0, restored.Outputs[0].Covenant!.AuthorizingInput);
            Assert.Equal(new uint256(4), restored.Outputs[0].Covenant!.CovenantId);
            Assert.Equal(withCovenant.GetId(), restored.GetId());
            Assert.Equal(withCovenant.GetHash(), restored.GetHash());
        }

        [Fact]
        public void Version1_mass_counts_compute_budget_and_covenant_bytes()
        {
            var bare = Transaction11(0);
            var budget = Transaction11(111);
            Assert.Equal(148UL, MassCalculator.EstimatedSerializedSize(bare));
            Assert.Equal(148UL + (111UL * MassCalculator.MassPerComputeBudget), MassCalculator.ComputeMass(budget));
            Assert.Equal(148UL * MassCalculator.TransientByteToMassFactor, MassCalculator.TransientMass(budget));

            var output = new TxOut(1_000_000UL, new ScriptPubKey(0, new byte[37]));
            Assert.Equal(1UL, MassCalculator.CellOf(output).Plurality);
            output.Covenant = new CovenantBinding(1, uint256.Zero);
            Assert.Equal(2UL, MassCalculator.CellOf(output).Plurality);
            var spent = new UtxoEntry(1_000_000UL, output.ScriptPublicKey, 1, false, uint256.Zero);
            Assert.Equal(2UL, MassCalculator.CellOf(spent).Plurality);

            var sized = Transaction11(0);
            sized.Outputs.Add(output);
            var without = MassCalculator.EstimatedSerializedSize(Transaction11(0));
            without += 8 + 2 + 8 + 37;
            Assert.Equal(without + 34, MassCalculator.EstimatedSerializedSize(sized));
        }

        [Fact]
        public void Version1_sighash_commits_the_covenant()
        {
            var script = new ScriptPubKey(1, new byte[] { 1, 2, 3 });
            var tx = new Transaction(
                1,
                new[] { new TxIn(new OutPoint(uint256.Zero, 3), Script.Empty, 9, 0) },
                new[] { new TxOut(1000UL, script) },
                4,
                SubnetworkId.Native,
                0,
                Array.Empty<byte>());
            var utxo = new UtxoEntry(1000UL, script, 1, false);
            var without = tx.GetSignatureHash(0, utxo);
            tx.Outputs[0].Covenant = new CovenantBinding(2, new uint256(6));
            var withCovenant = tx.GetSignatureHash(0, utxo);
            Assert.NotEqual(without, withCovenant);
            Assert.Equal(ExpectedSchnorr(tx), withCovenant.ToString());
        }

        private static Transaction Transaction11(ushort budget)
        {
            var input = TxIn.WithComputeBudget(new OutPoint(uint256.Zero, 0), Script.Empty, 0, budget);
            var tx = new Transaction(1, new[] { input }, Array.Empty<TxOut>(), 0, SubnetworkId.Native, 0, Array.Empty<byte>());
            tx.StorageMass = 0;
            return tx;
        }

        private static Transaction TransactionSigop(byte sigops)
        {
            var input = new TxIn(new OutPoint(uint256.Zero, 0), Script.Empty, 0, sigops);
            return new Transaction(1, new[] { input }, Array.Empty<TxOut>(), 0, SubnetworkId.Native, 0, Array.Empty<byte>());
        }

        private static void AssertIdHash(Transaction tx, string id, string hash)
        {
            Assert.Equal(id, tx.GetId().ToString());
            Assert.Equal(hash, tx.GetHash().ToString());
        }

        private static string ExpectedSchnorr(Transaction tx)
        {
            var key = Encoding.ASCII.GetBytes("TransactionSigningHash");
            var previous = new KaspaWriter();
            previous.Raw(tx.Inputs[0].PreviousOutpoint.TransactionId.ToBytes());
            previous.U32(tx.Inputs[0].PreviousOutpoint.Index);
            var sequences = new KaspaWriter();
            sequences.U64(tx.Inputs[0].Sequence);
            var outputs = new KaspaWriter();
            outputs.U64(tx.Outputs[0].Value.Sompi);
            outputs.U16(tx.Outputs[0].ScriptPublicKey.Version);
            outputs.VarBytes(tx.Outputs[0].ScriptPublicKey.Script);
            outputs.U8(1);
            outputs.U16(tx.Outputs[0].Covenant!.AuthorizingInput);
            outputs.Raw(tx.Outputs[0].Covenant!.CovenantId.ToBytes());

            var preimage = new KaspaWriter();
            preimage.U16(tx.Version);
            preimage.Raw(Blake2b.ComputeHash(previous.ToArray(), 32, key));
            preimage.Raw(Blake2b.ComputeHash(sequences.ToArray(), 32, key));
            preimage.Raw(tx.Inputs[0].PreviousOutpoint.TransactionId.ToBytes());
            preimage.U32(tx.Inputs[0].PreviousOutpoint.Index);
            preimage.U16(tx.Outputs[0].ScriptPublicKey.Version);
            preimage.VarBytes(tx.Outputs[0].ScriptPublicKey.Script);
            preimage.U64(1000);
            preimage.U64(tx.Inputs[0].Sequence);
            preimage.Raw(Blake2b.ComputeHash(outputs.ToArray(), 32, key));
            preimage.U64(tx.LockTime);
            preimage.Raw(tx.SubnetworkId.ToBytes());
            preimage.U64(tx.Gas);
            preimage.Raw(new byte[32]);
            preimage.U8((byte)SigHashType.All);
            return new uint256(Blake2b.ComputeHash(preimage.ToArray(), 32, key)).ToString();
        }
    }
}
