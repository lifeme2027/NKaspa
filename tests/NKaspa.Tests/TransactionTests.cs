using System.Security.Cryptography;
using System.Text;
using NKaspa.Crypto;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Transaction id tests 1-9 are rusty-kaspa <c>consensus/core/src/hashing/tx.rs</c>
    /// <c>test_transaction_hashing</c>, copied through kaswallet <c>TransactionIdVectorTests</c>.
    /// Sighash rows are rusty-kaspa <c>consensus/core/src/hashing/sighash.rs</c> <c>test_signature_hash</c>,
    /// copied through kaswallet <c>SigHashVectorTests</c> plus the other hash-type rows in that Rust test.
    /// Hasher rows are rusty-kaspa <c>crypto/hashes/src/hashers.rs</c> <c>test_vectors</c>.
    /// </summary>
    public class TransactionTests
    {
        private const string PrevTxId = "880eb9819a31821d9d2399e2f35e2433b72637e393d71ecc9b8d0250f49153c3";
        private const string Spk1 = "208325613d2eeaf7176ac6c670b13c0043156c427438ed72d74b7800862ad884e8ac";
        private const string Spk2 = "20fcef4c106cf11135bbd70f02a726a92162d2fb8b22f0469126f800862ad884e8ac";
        private const string NonZeroPrev = "59b3d6dc6cdc660c389c3fdb5704c48c598d279cdf1bab54182db586a4c95dd5";

        public static IEnumerable<object[]> IdVectors()
        {
            yield return Id(1, Array.Empty<TxIn>(), Array.Empty<TxOut>(), 0, 0, 0, Array.Empty<byte>(), 0,
                "2c18d5e59ca8fc4c23d9560da3bf738a8f40935c11c162017fbf2c907b7e665c",
                "c9e29784564c269ce2faaffd3487cb4684383018ace11133de082dce4bb88b0b");
            yield return Id(2, new[] { SampleInput(new string('0', 64)) }, Array.Empty<TxOut>(), 0, 0, 0, Array.Empty<byte>(), 0,
                "b2d65ae36e123eb73f253176d7234a57656b84d0d60b9fc746ab0d0f085c9cc7",
                "7d9f7cfdd77f236a41895ac5cdda2fa42f7122964ba995fdfacebce54efad7e8");
            yield return Id(3, new[] { SampleInput(new string('0', 64)) }, new[] { SampleOutput() }, 0, 0, 0, Array.Empty<byte>(), 0,
                "67289b12146d1b5ef384332137399791a5cfe89506ff31688b0d95ae821d0a0c",
                "492279c0ed5018aa00b0b2d42c1c42350285f2e689236a81829edaf818e30fdb");
            yield return Id(4, new[] { SampleInput(new string('0', 64)) }, new[] { SampleOutput() }, 54, 0, 3, Array.Empty<byte>(), 0,
                "7cd34b788d7d230970d4bfd955c34c5abc49e3bcdd5adb03a77bb71d05554401",
                "de319664ee9f4197e89be0d0e08b2b6cac110efc2cf107de1fbc6bd2ce29d545");
            yield return Id(5, new[] { SampleInput(NonZeroPrev) }, new[] { SampleOutput() }, 54, 0, 3, Array.Empty<byte>(), 0,
                "c9dd78e818445f617a28348d6db752142e2fab440effa58140ad2773e638b628",
                "1be9978bcab9424f15adac6fca0a64c3f56344a7cd0ec92a225496e19a0d122c");
            yield return Id(6, Array.Empty<TxIn>(), new[] { SampleOutput() }, 54, 1, 3, Array.Empty<byte>(), 0,
                "2578783ec93c3a02414a228e10b1b5af298623254775f972f97df08d4ec28c8f",
                "dffa96c75ef9d17520991fc6d88813531e230488e75b65f65ce958f2d54d2451");
            yield return Id(7, new[] { SampleInput(NonZeroPrev) }, new[] { SampleOutput() }, 54, 2, 3, Array.Empty<byte>(), 0,
                "3f6cea6d7ac8f6b2f86209fa748ea0ef5a1d5d380d43b79e77d52e770bb9a7b9",
                "9abf01c6c312dd984ff19c23bec85e8678e6ea34041fe3c5de52fd9344adac63");
            yield return Id(8, new[] { SampleInput(NonZeroPrev) }, new[] { SampleOutput() }, 54, 2, 3, new byte[] { 1, 2, 3 }, 0,
                "4acda997dfb31c6518224c9ac00d0777fc7cbecdab461be3c0816b1cba19a056",
                "f0bb137ed71a91445ddf9224c76f755153a296eeb4fdc29b8393ddd81bf34ce6");
            yield return Id(9, new[] { SampleInput(NonZeroPrev) }, new[] { SampleOutput() }, 54, 2, 3, new byte[] { 1, 2, 3 }, 5,
                "4acda997dfb31c6518224c9ac00d0777fc7cbecdab461be3c0816b1cba19a056",
                "ced89bbf642cda42d29d9518d16e35cbbf85d10e1ab106b7dc2e0a821308ac91");
        }

        [Theory]
        [MemberData(nameof(IdVectors))]
        public void Official_transaction_id_and_hash(int number, Transaction tx, ulong mass, string expectedId, string expectedHash)
        {
            Assert.InRange(number, 1, 9);
            tx.StorageMass = mass;
            Assert.Equal(expectedId, tx.GetId().ToString());
            Assert.Equal(expectedHash, tx.GetHash().ToString());
        }

        [Fact]
        public void Version0_bytes_roundtrip_and_ignore_signature_script_in_the_id()
        {
            var tx = (Transaction)IdVectors().ElementAt(7)[1];
            var id = tx.GetId();
            var restored = Transaction.FromBytes(tx.ToBytes());
            Assert.Equal(id, restored.GetId());
            Assert.Equal(tx.GetHash(), restored.GetHash());
            Assert.Equal(tx.ToHex(), restored.ToHex());

            tx.Inputs[0].SignatureScript = new Script(new byte[] { 1, 9 });
            tx.Inputs[0].SigOpCount = 1;
            Assert.Equal(id, tx.GetId());
            Assert.NotEqual(restored.GetHash(), tx.GetHash());
        }

        [Theory]
        [InlineData("native", "none", "03b7ac6927b2b67100734c3cc313ff8c2e8b3ce3e746d46dd660b706a916b1f5")]
        [InlineData("native", "outpoint-index", "a9f563d86c0ef19ec2e4f483901d202e90150580b6123c3d492e26e7965f488c")]
        [InlineData("native", "output-value", "aad2b61bd2405dfcf7294fc2be85f325694f02dda22d0af30381cb50d8295e0a")]
        [InlineData("native", "sequence", "0818bd0a3703638d4f01014c92cf866a8903cab36df2fa2506dc0d06b94295e8")]
        [InlineData("native", "payload", "72ea6c2871e0f44499f1c2b556f265d9424bfea67cca9cb343b4b040ead65525")]
        [InlineData("subnetwork", "none", "b2f421c933eb7e1a91f1d9e1efa3f120fe419326c0dbac487752189522550e0c")]
        [InlineData("subnetwork", "payload", "12ab63b9aea3d58db339245a9b6e9cb6075b2253615ce0fb18104d28de4435a1")]
        [InlineData("subnetwork", "gas", "2501edfc0068d591160c4bd98646c6e6892cdc051182a8be3ccd6d67f104fd17")]
        [InlineData("subnetwork", "subnetwork-id", "a5d1230ede0dfcfd522e04123a7bcd721462fed1d3a87352031a4f6e3c4389b6")]
        public void Official_sighash_all_input0(string kind, string mutation, string expected)
        {
            var tx = SighashTx(kind == "subnetwork", mutation);
            var hash = SigHash.CalcSchnorrSignatureHash(tx, 0, SighashUtxo(0), SigHashType.All);
            Assert.Equal(expected, hash.ToString());
        }

        [Theory]
        [InlineData(SigHashType.AllAnyOneCanPay, 0, "24821e466e53ff8e5fa93257cb17bb06131a48be4ef282e87f59d2bdc9afebc2")]
        [InlineData(SigHashType.None, 0, "38ce4bc93cf9116d2e377b33ff8449c665b7b5e2f2e65303c543b9afdaa4bbba")]
        [InlineData(SigHashType.Single, 0, "44a0b407ff7b239d447743dd503f7ad23db5b2ee4d25279bd3dffaf6b474e005")]
        [InlineData(SigHashType.Single, 2, "022ad967192f39d8d5895d243e025ec14cc7a79708c5e364894d4eff3cecb1b0")]
        [InlineData(SigHashType.SingleAnyOneCanPay, 0, "43b20aba775050cf9ba8d5e48fc7ed2dc6c071d23f30382aea58b7c59cfb8ed7")]
        [InlineData(SigHashType.NoneAnyOneCanPay, 0, "06aa9f4239491e07bb2b6bda6b0657b921aeae51e193d2c5bf9e81439cfeafa0")]
        public void Official_sighash_types(SigHashType hashType, int inputIndex, string expected)
        {
            var tx = SighashTx(false, "none");
            Assert.Equal(expected, SigHash.CalcSchnorrSignatureHash(tx, inputIndex, SighashUtxo(inputIndex), hashType).ToString());
        }

        [Fact]
        public void Version1_sighash_omits_sigops_and_id_is_not_blake2b()
        {
            var tx = SighashTx(false, "none");
            tx.Version = 1;
            const string expected = "5b2657524be672e019897646b56da3d192b453d78ae5e6e5c07f029a69f5f075";
            Assert.Equal(expected, SigHash.CalcSchnorrSignatureHash(tx, 0, SighashUtxo(0)).ToString());
            tx.Inputs[0].SigOpCount = 123;
            tx.Inputs[1].SigOpCount = 9;
            Assert.Equal(expected, SigHash.CalcSchnorrSignatureHash(tx, 0, SighashUtxo(0)).ToString());
            Assert.Throws<NotSupportedException>(() => tx.GetId());
            Assert.Throws<NotSupportedException>(() => tx.ToBytes());
        }

        [Fact]
        public void Ecdsa_hasher_matches_rusty_kaspa_and_wraps_the_known_schnorr_hash()
        {
            var chunks = new[]
            {
                Array.Empty<byte>(),
                new byte[] { 1 },
                Convert.FromHexString("05c77e2c4720528b7ad92b30347028d1b4538be748308830a8e285073c04a0cd"),
                Enumerable.Repeat((byte)0x2a, 64).ToArray(),
                new byte[8],
            };
            var expected = new[]
            {
                "b31ad1fbbe41b0e2a90e07c84708b38ba581f0c0e9185416913a04fb6d342027",
                "c43e1f75ea9df6379b56a95074c2b6289ed8c5a01fff2d49d9d44ad5575c164b",
                "49085f99fa0084b5436663f757a5916b1e4290c3321707fb76921ed4e47844ec",
                "3f887e866428de813c1d0463b14eef3ca1363c8187e917dda1eee0ec5996490b",
                "56de89a8c75f0fee2de61b11ab05d0d42e29ed50879467cf128dd80800a52ada",
            };
            var accumulated = new List<byte>();
            for (var i = 0; i < chunks.Length; i++)
            {
                accumulated.AddRange(chunks[i]);
                Assert.Equal(expected[i], SigHash.EcdsaDomainHash(accumulated.ToArray()).ToHex());
            }

            var tx = SighashTx(false, "none");
            var schnorr = SigHash.CalcSchnorrSignatureHash(tx, 0, SighashUtxo(0));
            Assert.Equal("03b7ac6927b2b67100734c3cc313ff8c2e8b3ce3e746d46dd660b706a916b1f5", schnorr.ToString());

            using var domain = SHA256.Create();
            var prefix = domain.ComputeHash(Encoding.ASCII.GetBytes("TransactionSigningHashECDSA"));
            using var outer = SHA256.Create();
            outer.TransformBlock(prefix, 0, prefix.Length, null, 0);
            var schnorrBytes = schnorr.ToBytes();
            outer.TransformBlock(schnorrBytes, 0, schnorrBytes.Length, null, 0);
            outer.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            Assert.Equal(outer.Hash!.ToHex(), SigHash.CalcEcdsaSignatureHash(tx, 0, SighashUtxo(0)).ToString());
        }

        [Fact]
        public void Empty_domain_hashes_match_rusty_kaspa_hashers()
        {
            Assert.Equal(
                "e5f65efda0894d2b0590c2e9e46e9acc03032f505a1522f5e8c78c5ec70b1d9c",
                Blake2b.ComputeHash(ReadOnlySpan<byte>.Empty, 32, Encoding.ASCII.GetBytes("TransactionID")).ToHex());
            Assert.Equal(
                "50272a9e37c728026f93d0eda6ab4467f627338b879076483c88d291193cb3bf",
                Blake2b.ComputeHash(ReadOnlySpan<byte>.Empty, 32, Encoding.ASCII.GetBytes("TransactionHash")).ToHex());
        }

        [Fact]
        public void End_to_end_schnorr_signs_and_verifies_one_input()
        {
            using var key = Key.Create();
            var address = key.GetAddress(Network.Mainnet);
            var locking = address.ScriptPubKey;
            Assert.True(locking.IsPayToPubKey);
            Assert.Equal(key.PubKey.ToString(), address.Payload.ToHex());

            var tx = new Transaction();
            tx.Inputs.Add(new TxIn(new OutPoint(uint256.One, 0))
            {
                Sequence = 1,
                SigOpCount = 1,
            });
            tx.Outputs.Add(new TxOut(Money.Coins(1), locking));
            var spent = new UtxoEntry(Money.Coins(2), locking, blockDaaScore: 7, isCoinbase: false);

            var idBefore = tx.GetId();
            var hashBefore = tx.GetHash();
            tx.SignInput(0, key, spent);
            Assert.Equal(idBefore, tx.GetId());
            Assert.NotEqual(hashBefore, tx.GetHash());
            Assert.Equal(66, tx.Inputs[0].SignatureScript.Length);
            Assert.True(tx.VerifyInput(0, spent));
            Assert.True(tx.VerifyInput(0, key.PubKey, spent));

            tx.Outputs[0].Value = Money.FromSompi(tx.Outputs[0].Value.Sompi + 1);
            Assert.False(tx.VerifyInput(0, spent));
        }

        [Fact]
        public void End_to_end_two_inputs_sign_and_verify()
        {
            using var first = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
            using var second = new Key(GeneratorSecret());
            var firstLock = first.GetAddress(Network.Testnet).ScriptPubKey;
            var secondLock = second.PubKey.GetScriptPubKey();
            var tx = new Transaction
            {
                LockTime = 9,
            };
            tx.Inputs.Add(new TxIn(new OutPoint(uint256.Parse(NonZeroPrev), 1), Script.Empty, 3, 1));
            tx.Inputs.Add(new TxIn(new OutPoint(uint256.One, 4), Script.Empty, 4, 1));
            tx.Outputs.Add(new TxOut(50_000_000UL, firstLock));
            tx.Outputs.Add(new TxOut(40_000_000UL, secondLock));
            var spent = new[]
            {
                new UtxoEntry(60_000_000UL, firstLock, 1, false),
                new UtxoEntry(40_000_000UL, secondLock, 2, false),
            };

            var idBefore = tx.GetId();
            tx.SignInput(0, first, spent[0]);
            tx.SignInput(1, second, spent[1]);
            Assert.Equal(idBefore, tx.GetId());
            Assert.True(tx.VerifyInput(0, spent[0]));
            Assert.True(tx.VerifyInput(1, spent[1]));
            Assert.False(tx.VerifyInput(0, second.PubKey, spent[0]));

            tx.Inputs[1].Sequence = 99;
            Assert.False(tx.VerifyInput(0, spent[0]));
        }

        [Fact]
        public void End_to_end_ecdsa_signs_and_verifies()
        {
            using var key = new Key(GeneratorSecret());
            var locking = key.PubKeyECDSA.GetScriptPubKey();
            Assert.True(locking.IsPayToPubKeyEcdsa);
            Assert.StartsWith("kaspa:q", key.GetEcdsaAddress(Network.Mainnet).ToString());

            var tx = new Transaction();
            tx.Inputs.Add(new TxIn(new OutPoint(uint256.One, 0), Script.Empty, 0, 1));
            tx.Outputs.Add(new TxOut(1_000UL, ScriptPubKey.PayToPubKey(key.PubKey)));
            var spent = new UtxoEntry(2_000UL, locking, 0, false);
            tx.SignInput(0, key, spent);
            Assert.True(tx.VerifyInput(0, key.PubKeyECDSA, spent));
            Assert.True(tx.VerifyInput(0, spent));
            Assert.False(tx.VerifyInput(0, key.PubKey, spent));
        }

        [Fact]
        public void Pay_to_script_hash_signature_script_carries_a_valid_inner_signature()
        {
            using var key = new Key(GeneratorSecret());
            var redeem = Script.PayToPubKey(key.PubKey);
            var locking = ScriptPubKey.PayToScriptHash(redeem);
            var address = new KaspaScriptHashAddress(redeem.GetScriptHash(), Network.Mainnet);
            Assert.Equal(redeem.GetScriptHash().ToString(), address.Payload.ToHex());
            Assert.Equal(locking.ToHex(), address.ScriptPubKey.ToHex());

            var tx = new Transaction();
            tx.Inputs.Add(new TxIn(new OutPoint(uint256.One, 0), Script.Empty, 0, 1));
            tx.Outputs.Add(new TxOut(Money.Coins(1), locking));
            var spent = new UtxoEntry(Money.Coins(1), locking, 0, false);
            var hash = tx.GetSignatureHash(0, spent, SigHashType.All);
            var push = Script.SignatureScript(key.SignSchnorr(hash.ToBytes(), new byte[32]), SigHashType.All);
            tx.Inputs[0].SignatureScript = Script.PayToScriptHashSignature(redeem, push);

            Assert.True(push.TryParseSignature(out var signature, out var hashType));
            Assert.Equal(SigHashType.All, hashType);
            Assert.True(key.PubKey.Verify(new SchnorrSignature(signature), hash.ToBytes()));
            Assert.EndsWith(redeem.ToHex(), tx.Inputs[0].SignatureScript.ToHex());
            Assert.False(tx.VerifyInput(0, spent));
        }

        [Fact]
        public void Money_uses_sompi()
        {
            Assert.Equal(100_000_000UL, Money.Coins(1).Sompi);
            Assert.Equal(150_000_000UL, Money.Coins(1.5m).Sompi);
            Assert.Equal("1 KAS", Money.Coins(1).ToString());
            Assert.Equal("0.00000001 KAS", Money.FromSompi(1).ToString());
            Assert.Equal("1.5 KAS", Money.Coins(1.5m).ToString());
            Assert.Throws<ArgumentOutOfRangeException>(() => Money.Coins(-1));
            Assert.Throws<ArgumentException>(() => Money.Coins(0.000000001m));
        }

        private static object[] Id(int number, TxIn[] inputs, TxOut[] outputs, ulong lockTime, byte subnet, ulong gas, byte[] payload, ulong mass, string id, string hash)
        {
            var tx = new Transaction(0, inputs, outputs, lockTime, SubnetworkId.FromByte(subnet), gas, payload);
            return new object[] { number, tx, mass, id, hash };
        }

        private static TxIn SampleInput(string previous)
        {
            return new TxIn(new OutPoint(uint256.Parse(previous), 2), new Script(new byte[] { 1, 2 }), 7, 5);
        }

        private static TxOut SampleOutput()
        {
            return new TxOut(1564UL, new ScriptPubKey(7, new byte[] { 1, 2, 3, 4, 5 }));
        }

        private static Transaction SighashTx(bool subnetwork, string mutation)
        {
            var previous = uint256.Parse(PrevTxId);
            var index1 = mutation == "outpoint-index" ? 2u : 1u;
            var sequence1 = mutation == "sequence" ? 12345UL : 1UL;
            var outputValue = mutation == "output-value" ? 100UL : 300UL;
            byte[] subnetworkId;
            ulong gas;
            byte[] payload;
            if (!subnetwork)
            {
                subnetworkId = new byte[20];
                gas = 0;
                payload = mutation == "payload" ? new byte[] { 6, 6, 6, 4, 2, 0, 1, 3, 3, 7 } : Array.Empty<byte>();
            }
            else
            {
                subnetworkId = new byte[20];
                if (mutation == "subnetwork-id")
                {
                    new byte[] { 6, 6, 6, 4, 2, 0, 1, 3, 3, 7 }.CopyTo(subnetworkId, 0);
                }
                else
                {
                    for (var i = 0; i < 10; i++)
                    {
                        subnetworkId[i] = (byte)(i + 1);
                    }
                }

                gas = mutation == "gas" ? 1234UL : 250UL;
                payload = mutation == "payload"
                    ? new byte[] { 6, 6, 6, 4, 2, 0, 1, 3, 3, 7 }
                    : Enumerable.Range(10, 11).Select(i => (byte)i).ToArray();
            }

            return new Transaction(
                0,
                new[]
                {
                    new TxIn(new OutPoint(previous, 0), Script.Empty, 0, 0),
                    new TxIn(new OutPoint(previous, index1), Script.Empty, sequence1, 0),
                    new TxIn(new OutPoint(previous, 2), Script.Empty, 2, 0),
                },
                new[]
                {
                    new TxOut(300UL, new ScriptPubKey(0, Convert.FromHexString(Spk2))),
                    new TxOut(outputValue, new ScriptPubKey(0, Convert.FromHexString(Spk1))),
                },
                1615462089000,
                new SubnetworkId(subnetworkId),
                gas,
                payload);
        }

        private static UtxoEntry SighashUtxo(int index)
        {
            var amount = index switch
            {
                0 => 100UL,
                1 => 200UL,
                _ => 300UL,
            };
            var script = index == 0 ? Spk1 : Spk2;
            return new UtxoEntry(amount, new ScriptPubKey(0, Convert.FromHexString(script)), 0, false);
        }

        private static byte[] GeneratorSecret()
        {
            var secret = new byte[32];
            secret[31] = 1;
            return secret;
        }
    }
}
