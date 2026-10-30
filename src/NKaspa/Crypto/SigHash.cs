using System.Security.Cryptography;
using System.Text;

namespace NKaspa.Crypto
{
    /// <summary>
    /// Kaspa signature hash, transaction id, and transaction hash.
    /// Kaspa counterpart of the sighash half of NBitcoin.Transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Schnorr sighash is BLAKE2b-256 keyed with the ASCII domain <c>TransactionSigningHash</c>,
    /// matching rusty-kaspa <c>calc_schnorr_signature_hash</c>.
    /// ECDSA sighash is <c>SHA256(SHA256("TransactionSigningHashECDSA") || schnorrHash)</c>,
    /// matching <c>TransactionSigningHashECDSA</c> in <c>crypto/hashes/src/hashers.rs</c>.
    /// It is not a second BLAKE2b, and it is not a raw SHA256 of the Schnorr hash.
    /// </para>
    /// <para>
    /// A native subnetwork with an empty payload commits to 32 zero bytes.
    /// A native subnetwork with a non-empty payload hashes that payload.
    /// Anyone-can-pay zeros the previous-output hash and the sigop-count hash.
    /// None, Single, and anyone-can-pay zero the sequence hash.
    /// None zeros the output hash. Single hashes the output at the input index, or 32 zeros when that output is missing.
    /// Version 0 writes sigop counts. Version 1 and later omit them and write a false covenant byte on each hashed output.
    /// </para>
    /// </remarks>
    public static class SigHash
    {
        private static readonly byte[] SigningKey = Encoding.ASCII.GetBytes("TransactionSigningHash");
        private static readonly byte[] TransactionIdKey = Encoding.ASCII.GetBytes("TransactionID");
        private static readonly byte[] TransactionHashKey = Encoding.ASCII.GetBytes("TransactionHash");
        private static readonly byte[] EcdsaDomain = Sha256(Encoding.ASCII.GetBytes("TransactionSigningHashECDSA"));

        /// <summary>BLAKE2b-256 keyed with <c>TransactionSigningHash</c> over the Schnorr preimage.</summary>
        public static uint256 CalcSchnorrSignatureHash(Transaction tx, int inputIndex, UtxoEntry utxo, SigHashType hashType = SigHashType.All)
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (utxo == null)
            {
                throw new ArgumentNullException(nameof(utxo));
            }

            if (!hashType.IsDefined())
            {
                throw new ArgumentException("Sighash type is not one of the six Kaspa values.", nameof(hashType));
            }

            if ((uint)inputIndex >= (uint)tx.Inputs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(inputIndex));
            }

            var input = tx.Inputs[inputIndex];
            if (input == null || input.PreviousOutpoint == null)
            {
                throw new InvalidOperationException("Transaction input is missing an outpoint.");
            }

            var preimage = new KaspaWriter();
            preimage.U16(tx.Version);
            preimage.Raw(PreviousOutputsHash(tx, hashType));
            preimage.Raw(SequencesHash(tx, hashType));
            if (tx.Version < 1)
            {
                preimage.Raw(SigOpCountsHash(tx, hashType));
            }

            WriteOutpoint(preimage, input.PreviousOutpoint);
            WriteScript(preimage, utxo.ScriptPublicKey);
            preimage.U64(utxo.Amount.Sompi);
            preimage.U64(input.Sequence);
            if (tx.Version < 1)
            {
                preimage.U8(input.SigOpCount);
            }

            preimage.Raw(OutputsHash(tx, hashType, inputIndex));
            preimage.U64(tx.LockTime);
            preimage.Raw(tx.SubnetworkId.ToBytes());
            preimage.U64(tx.Gas);
            preimage.Raw(PayloadHash(tx));
            preimage.U8((byte)hashType);
            return new uint256(Keyed(preimage.ToArray(), SigningKey));
        }

        /// <summary>ECDSA signing hash: domain-separated SHA256 of the Schnorr signature hash.</summary>
        public static uint256 CalcEcdsaSignatureHash(Transaction tx, int inputIndex, UtxoEntry utxo, SigHashType hashType = SigHashType.All)
        {
            var schnorr = CalcSchnorrSignatureHash(tx, inputIndex, utxo, hashType).ToBytes();
            return new uint256(EcdsaDomainHash(schnorr));
        }

        /// <summary>Version-0 transaction id. Keyed BLAKE2b <c>TransactionID</c>.</summary>
        public static uint256 TransactionId(Transaction tx)
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            return new uint256(Keyed(tx.Serialize(forId: true, includeMass: false), TransactionIdKey));
        }

        /// <summary>Version-0 transaction hash. Keyed BLAKE2b <c>TransactionHash</c>.</summary>
        public static uint256 TransactionHash(Transaction tx)
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            return new uint256(Keyed(tx.Serialize(forId: false, includeMass: true), TransactionHashKey));
        }

        /// <summary>
        /// One-shot <c>TransactionSigningHashECDSA</c> digest.
        /// The prefix is SHA256 of the domain string, then the message is absorbed, then one SHA256 is finalized.
        /// </summary>
        public static byte[] EcdsaDomainHash(ReadOnlySpan<byte> data)
        {
            using var sha = SHA256.Create();
            sha.TransformBlock(EcdsaDomain, 0, EcdsaDomain.Length, null, 0);
            var message = data.ToArray();
            if (message.Length > 0)
            {
                sha.TransformBlock(message, 0, message.Length, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return sha.Hash ?? throw new InvalidOperationException("SHA256 did not produce a digest.");
        }

        private static byte[] PreviousOutputsHash(Transaction tx, SigHashType hashType)
        {
            if (hashType.IsAnyOneCanPay())
            {
                return new byte[32];
            }

            var writer = new KaspaWriter();
            foreach (var input in tx.Inputs)
            {
                if (input == null || input.PreviousOutpoint == null)
                {
                    throw new InvalidOperationException("Transaction input is missing an outpoint.");
                }

                WriteOutpoint(writer, input.PreviousOutpoint);
            }

            return Keyed(writer.ToArray(), SigningKey);
        }

        private static byte[] SequencesHash(Transaction tx, SigHashType hashType)
        {
            if (hashType.IsSingle() || hashType.IsAnyOneCanPay() || hashType.IsNone())
            {
                return new byte[32];
            }

            var writer = new KaspaWriter();
            foreach (var input in tx.Inputs)
            {
                writer.U64(input.Sequence);
            }

            return Keyed(writer.ToArray(), SigningKey);
        }

        private static byte[] SigOpCountsHash(Transaction tx, SigHashType hashType)
        {
            if (hashType.IsAnyOneCanPay())
            {
                return new byte[32];
            }

            var writer = new KaspaWriter();
            foreach (var input in tx.Inputs)
            {
                writer.U8(input.SigOpCount);
            }

            return Keyed(writer.ToArray(), SigningKey);
        }

        private static byte[] OutputsHash(Transaction tx, SigHashType hashType, int inputIndex)
        {
            if (hashType.IsNone())
            {
                return new byte[32];
            }

            if (hashType.IsSingle())
            {
                if (inputIndex >= tx.Outputs.Count)
                {
                    return new byte[32];
                }

                var single = new KaspaWriter();
                WriteOutput(single, tx.Outputs[inputIndex], tx.Version);
                return Keyed(single.ToArray(), SigningKey);
            }

            var writer = new KaspaWriter();
            foreach (var output in tx.Outputs)
            {
                WriteOutput(writer, output, tx.Version);
            }

            return Keyed(writer.ToArray(), SigningKey);
        }

        private static byte[] PayloadHash(Transaction tx)
        {
            var payload = tx.Payload;
            if (tx.SubnetworkId.IsNative && payload.Length == 0)
            {
                return new byte[32];
            }

            var writer = new KaspaWriter();
            writer.VarBytes(payload);
            return Keyed(writer.ToArray(), SigningKey);
        }

        private static void WriteOutpoint(KaspaWriter writer, OutPoint outpoint)
        {
            writer.Raw(outpoint.TransactionId.ToBytes());
            writer.U32(outpoint.Index);
        }

        private static void WriteScript(KaspaWriter writer, ScriptPubKey script)
        {
            if (script == null)
            {
                throw new ArgumentNullException(nameof(script));
            }

            writer.U16(script.Version);
            writer.VarBytes(script.Script);
        }

        private static void WriteOutput(KaspaWriter writer, TxOut output, ushort txVersion)
        {
            if (output == null || output.ScriptPublicKey == null)
            {
                throw new InvalidOperationException("Transaction output is missing a script.");
            }

            writer.U64(output.Value.Sompi);
            WriteScript(writer, output.ScriptPublicKey);
            if (txVersion >= 1)
            {
                writer.U8(0);
            }
        }

        private static byte[] Keyed(byte[] data, byte[] key)
        {
            return Blake2b.ComputeHash(data, 32, key);
        }

        private static byte[] Sha256(byte[] data)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(data);
        }
    }
}
