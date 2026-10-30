using NKaspa.Crypto;
using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// A Kaspa transaction. Kaspa counterpart of NBitcoin.Transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Version 0 serialization uses little-endian integers and u64 lengths, not Bitcoin compact size.
    /// Each input is outpoint, signature script, sigop count, sequence.
    /// Each output is value, script version, script.
    /// The tail is lock time, 20-byte subnetwork id, gas, and payload.
    /// </para>
    /// <para>
    /// <see cref="GetId"/> hashes that body with signature scripts replaced by empty and with sigop counts omitted,
    /// under the BLAKE2b key <c>TransactionID</c>.
    /// <see cref="GetHash"/> hashes the full body, and appends <see cref="StorageMass"/> when it is non-zero,
    /// under the key <c>TransactionHash</c>.
    /// Version 1 transaction ids use BLAKE3 and are not implemented. Signature hashes for version 1 are implemented.
    /// </para>
    /// </remarks>
    public sealed class Transaction
    {
        private byte[] _payload;

        /// <summary>Creates an empty native version-0 transaction.</summary>
        public Transaction()
            : this(0, Array.Empty<TxIn>(), Array.Empty<TxOut>(), 0, SubnetworkId.Native, 0, Array.Empty<byte>())
        {
        }

        /// <summary>Creates a transaction. The input and output lists are copied.</summary>
        public Transaction(
            ushort version,
            IEnumerable<TxIn> inputs,
            IEnumerable<TxOut> outputs,
            ulong lockTime,
            SubnetworkId subnetworkId,
            ulong gas,
            byte[] payload)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (outputs == null)
            {
                throw new ArgumentNullException(nameof(outputs));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            Version = version;
            Inputs = new List<TxIn>(inputs);
            Outputs = new List<TxOut>(outputs);
            LockTime = lockTime;
            SubnetworkId = subnetworkId;
            Gas = gas;
            _payload = (byte[])payload.Clone();
        }

        /// <summary>Transaction version. Standard payments use 0.</summary>
        public ushort Version { get; set; }

        /// <summary>Inputs. The list can be changed in place.</summary>
        public List<TxIn> Inputs { get; }

        /// <summary>Outputs. The list can be changed in place.</summary>
        public List<TxOut> Outputs { get; }

        /// <summary>Lock time, as a u64.</summary>
        public ulong LockTime { get; set; }

        /// <summary>Subnetwork id. Native is 20 zero bytes.</summary>
        public SubnetworkId SubnetworkId { get; set; }

        /// <summary>Gas. Native transactions use 0.</summary>
        public ulong Gas { get; set; }

        /// <summary>A copy of the payload.</summary>
        public byte[] Payload
        {
            get { return (byte[])_payload.Clone(); }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _payload = (byte[])value.Clone();
            }
        }

        /// <summary>
        /// Storage mass. Version-0 <see cref="GetHash"/> appends it only when it is greater than zero.
        /// It is not part of <see cref="ToBytes"/> or <see cref="GetId"/>.
        /// </summary>
        public ulong StorageMass { get; set; }

        /// <summary>Version-0 transaction id. Signature scripts do not change it.</summary>
        public uint256 GetId()
        {
            return SigHash.TransactionId(this);
        }

        /// <summary>Version-0 transaction hash. Signature scripts and a non-zero storage mass change it.</summary>
        public uint256 GetHash()
        {
            return SigHash.TransactionHash(this);
        }

        /// <summary>Schnorr signature hash for one input.</summary>
        public uint256 GetSignatureHash(int inputIndex, UtxoEntry utxo, SigHashType hashType = SigHashType.All)
        {
            return SigHash.CalcSchnorrSignatureHash(this, inputIndex, utxo, hashType);
        }

        /// <summary>ECDSA signature hash for one input.</summary>
        public uint256 GetEcdsaSignatureHash(int inputIndex, UtxoEntry utxo, SigHashType hashType = SigHashType.All)
        {
            return SigHash.CalcEcdsaSignatureHash(this, inputIndex, utxo, hashType);
        }

        /// <summary>
        /// Signs one pay-to-pubkey input.
        /// A Schnorr template uses BIP-340. An ECDSA template uses RFC 6979 compact signatures.
        /// The call does not change <see cref="TxIn.SigOpCount"/>.
        /// </summary>
        public void SignInput(int inputIndex, Key key, UtxoEntry utxo, SigHashType hashType = SigHashType.All)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (utxo == null)
            {
                throw new ArgumentNullException(nameof(utxo));
            }

            if (!hashType.IsDefined())
            {
                throw new ArgumentException("Sighash type is not one of the six Kaspa values.", nameof(hashType));
            }

            EnsureInput(inputIndex);
            var scriptPublicKey = utxo.ScriptPublicKey;
            Script signatureScript;
            if (scriptPublicKey.IsPayToPubKeyEcdsa)
            {
                var hash = GetEcdsaSignatureHash(inputIndex, utxo, hashType);
                signatureScript = Script.SignatureScript(key.SignECDSA(hash.ToBytes()), hashType);
            }
            else if (scriptPublicKey.IsPayToPubKey)
            {
                var hash = GetSignatureHash(inputIndex, utxo, hashType);
                signatureScript = Script.SignatureScript(key.SignSchnorr(hash.ToBytes()), hashType);
            }
            else
            {
                throw new InvalidOperationException(
                    "SignInput signs pay-to-pubkey scripts. Build a pay-to-script-hash signature with Script.PayToScriptHashSignature.");
            }

            Inputs[inputIndex].SignatureScript = signatureScript;
        }

        /// <summary>Verifies the Schnorr signature script on one input against <paramref name="publicKey"/>.</summary>
        public bool VerifyInput(int inputIndex, PubKey publicKey, UtxoEntry utxo)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            if (!TryReadSignature(inputIndex, utxo, out var signature, out var hashType))
            {
                return false;
            }

            var hash = GetSignatureHash(inputIndex, utxo, hashType);
            return publicKey.Verify(new SchnorrSignature(signature), hash.ToBytes());
        }

        /// <summary>Verifies the ECDSA signature script on one input against <paramref name="publicKey"/>.</summary>
        public bool VerifyInput(int inputIndex, PubKeyECDSA publicKey, UtxoEntry utxo)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            if (!TryReadSignature(inputIndex, utxo, out var signature, out var hashType))
            {
                return false;
            }

            var hash = GetEcdsaSignatureHash(inputIndex, utxo, hashType);
            return publicKey.Verify(new ECDSASignature(signature), hash.ToBytes());
        }

        /// <summary>
        /// Verifies one input using the public key inside its pay-to-pubkey script.
        /// Other script templates return false. The script engine is not executed.
        /// </summary>
        public bool VerifyInput(int inputIndex, UtxoEntry utxo)
        {
            if (utxo == null)
            {
                throw new ArgumentNullException(nameof(utxo));
            }

            var script = utxo.ScriptPublicKey.ToScript();
            if (utxo.ScriptPublicKey.IsPayToPubKey && script.TryGetPayToPubKey(out var xOnly))
            {
                try
                {
                    return VerifyInput(inputIndex, new PubKey(xOnly), utxo);
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            if (utxo.ScriptPublicKey.IsPayToPubKeyEcdsa && script.TryGetPayToPubKeyEcdsa(out var compressed))
            {
                try
                {
                    return VerifyInput(inputIndex, new PubKeyECDSA(compressed), utxo);
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>Version-0 consensus bytes. Storage mass is not included.</summary>
        public byte[] ToBytes()
        {
            return Serialize(forId: false, includeMass: false);
        }

        /// <summary>Lowercase hex of <see cref="ToBytes"/>.</summary>
        public string ToHex()
        {
            return Encoders.Hex.EncodeData(ToBytes());
        }

        /// <summary>Reads a version-0 transaction. Storage mass is left at zero.</summary>
        public static Transaction FromBytes(ReadOnlySpan<byte> bytes)
        {
            var reader = new KaspaReader(bytes);
            var version = reader.U16();
            if (version != 0)
            {
                throw new NotSupportedException("Only version 0 transactions can be deserialized.");
            }

            var inputs = ReadInputs(bytes, ref reader);
            var outputs = ReadOutputs(bytes, ref reader);
            var lockTime = reader.U64();
            var subnetwork = new SubnetworkId(reader.Take(SubnetworkId.Length));
            var gas = reader.U64();
            var payload = reader.VarBytes();
            if (!reader.IsEnd)
            {
                throw new FormatException("Transaction has trailing bytes.");
            }

            return new Transaction(version, inputs, outputs, lockTime, subnetwork, gas, payload);
        }

        /// <summary>Reads a version-0 transaction from hex.</summary>
        public static Transaction FromHex(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            return FromBytes(Encoders.Hex.DecodeData(hex));
        }

        /// <summary>
        /// Version-0 body. <paramref name="forId"/> writes an empty signature script and omits the sigop byte.
        /// <paramref name="includeMass"/> appends storage mass when it is non-zero.
        /// </summary>
        internal byte[] Serialize(bool forId, bool includeMass)
        {
            if (Version != 0)
            {
                throw new NotSupportedException(
                    "Only version 0 transactions use keyed BLAKE2b transaction ids. Version 1 ids use BLAKE3.");
            }

            var writer = new KaspaWriter();
            writer.U16(Version);
            writer.U64((ulong)Inputs.Count);
            foreach (var input in Inputs)
            {
                if (input == null || input.PreviousOutpoint == null || input.PreviousOutpoint.TransactionId == null)
                {
                    throw new InvalidOperationException("Transaction input is missing an outpoint.");
                }

                writer.Raw(input.PreviousOutpoint.TransactionId.ToBytes());
                writer.U32(input.PreviousOutpoint.Index);
                if (forId)
                {
                    writer.U64(0);
                }
                else
                {
                    writer.VarBytes(input.SignatureScript.ToBytes());
                    writer.U8(input.SigOpCount);
                }

                writer.U64(input.Sequence);
            }

            writer.U64((ulong)Outputs.Count);
            foreach (var output in Outputs)
            {
                if (output == null || output.ScriptPublicKey == null)
                {
                    throw new InvalidOperationException("Transaction output is missing a script.");
                }

                writer.U64(output.Value.Sompi);
                writer.U16(output.ScriptPublicKey.Version);
                writer.VarBytes(output.ScriptPublicKey.Script);
            }

            writer.U64(LockTime);
            writer.Raw(SubnetworkId.ToBytes());
            writer.U64(Gas);
            writer.VarBytes(_payload);
            if (includeMass && StorageMass > 0)
            {
                writer.U64(StorageMass);
            }

            return writer.ToArray();
        }

        private bool TryReadSignature(int inputIndex, UtxoEntry utxo, out byte[] signature, out SigHashType hashType)
        {
            if (utxo == null)
            {
                throw new ArgumentNullException(nameof(utxo));
            }

            EnsureInput(inputIndex);
            return Inputs[inputIndex].SignatureScript.TryParseSignature(out signature, out hashType);
        }

        private void EnsureInput(int inputIndex)
        {
            if ((uint)inputIndex >= (uint)Inputs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(inputIndex));
            }
        }

        private static List<TxIn> ReadInputs(ReadOnlySpan<byte> bytes, ref KaspaReader reader)
        {
            var count = reader.U64();
            if (count > (ulong)bytes.Length)
            {
                throw new FormatException("Transaction input count is invalid.");
            }

            var inputs = new List<TxIn>((int)count);
            for (ulong i = 0; i < count; i++)
            {
                var transactionId = new uint256(reader.Take(uint256.Length));
                var index = reader.U32();
                var signatureScript = new Script(reader.VarBytes());
                var sigOpCount = reader.U8();
                var sequence = reader.U64();
                inputs.Add(new TxIn(new OutPoint(transactionId, index), signatureScript, sequence, sigOpCount));
            }

            return inputs;
        }

        private static List<TxOut> ReadOutputs(ReadOnlySpan<byte> bytes, ref KaspaReader reader)
        {
            var count = reader.U64();
            if (count > (ulong)bytes.Length)
            {
                throw new FormatException("Transaction output count is invalid.");
            }

            var outputs = new List<TxOut>((int)count);
            for (ulong i = 0; i < count; i++)
            {
                var value = reader.U64();
                var version = reader.U16();
                var script = reader.VarBytes();
                outputs.Add(new TxOut(value, new ScriptPubKey(version, script)));
            }

            return outputs;
        }
    }
}
