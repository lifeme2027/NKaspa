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
    /// Version 0 <see cref="GetId"/> hashes that body with signature scripts replaced by empty and with sigop counts omitted,
    /// under the BLAKE2b key <c>TransactionID</c>.
    /// <see cref="GetHash"/> hashes the full body, and appends <see cref="StorageMass"/> when it is non-zero,
    /// under the key <c>TransactionHash</c>.
    /// </para>
    /// <para>
    /// Version 1 (Toccata) adds a compute-budget u16 on each input and an optional covenant on each output.
    /// <see cref="GetId"/> is BLAKE3: <c>TransactionV1Id</c> over the payload digest and the rest digest.
    /// The rest digest excludes the payload, the signature scripts, and the mass commitment.
    /// <see cref="GetHash"/> stays keyed BLAKE2b <c>TransactionHash</c> over the full encoding, which always includes storage mass.
    /// <see cref="ToBytes"/> for version 1 is that full encoding, so a round trip keeps the hash.
    /// Version 0 <see cref="ToBytes"/> still omits storage mass.
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
        /// Storage mass committed by <see cref="GetHash"/>.
        /// Version 0 appends it only when it is greater than zero, and omits it from <see cref="ToBytes"/> and <see cref="GetId"/>.
        /// Version 1 always appends it to the hash encoding and to <see cref="ToBytes"/>. It is still excluded from <see cref="GetId"/>.
        /// </summary>
        public ulong StorageMass { get; set; }

        /// <summary>Transaction id. Version 0 is keyed BLAKE2b. Version 1 and later are keyed BLAKE3.</summary>
        public uint256 GetId()
        {
            return SigHash.TransactionId(this);
        }

        /// <summary>Transaction hash. Keyed BLAKE2b over the full encoding, for every supported version.</summary>
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

        /// <summary>
        /// Consensus bytes. Version 0 omits storage mass.
        /// Version 1 includes compute budgets, covenant bytes, and storage mass.
        /// </summary>
        public byte[] ToBytes()
        {
            return Serialize(Version == 0 ? TxEncoding.ExcludeMassCommit : TxEncoding.None);
        }

        /// <summary>Lowercase hex of <see cref="ToBytes"/>.</summary>
        public string ToHex()
        {
            return Encoders.Hex.EncodeData(ToBytes());
        }

        /// <summary>
        /// Reads a version-0 or version-1 transaction.
        /// Version 0 leaves storage mass at zero. Version 1 reads the storage mass that follows the payload.
        /// </summary>
        public static Transaction FromBytes(ReadOnlySpan<byte> bytes)
        {
            var reader = new KaspaReader(bytes);
            var version = reader.U16();
            if (version > 1)
            {
                throw new NotSupportedException("Only version 0 and version 1 transactions can be deserialized.");
            }

            var inputs = ReadInputs(bytes, ref reader, version);
            var outputs = ReadOutputs(bytes, ref reader, version);
            var lockTime = reader.U64();
            var subnetwork = new SubnetworkId(reader.Take(SubnetworkId.Length));
            var gas = reader.U64();
            var payload = reader.VarBytes();
            var transaction = new Transaction(version, inputs, outputs, lockTime, subnetwork, gas, payload);
            if (version >= 1)
            {
                transaction.StorageMass = reader.U64();
            }

            if (!reader.IsEnd)
            {
                throw new FormatException("Transaction has trailing bytes.");
            }

            return transaction;
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
        /// Hash encoding from rusty-kaspa <c>write_transaction</c>.
        /// <see cref="TxEncoding.ExcludeSignatureScript"/> writes an empty signature script and omits the version-0 sigop byte.
        /// <see cref="TxEncoding.ExcludeMassCommit"/> omits storage mass and the version-1 compute budget.
        /// <see cref="TxEncoding.ExcludePayload"/> writes an empty payload.
        /// </summary>
        internal byte[] Serialize(TxEncoding encoding)
        {
            var excludeSignature = (encoding & TxEncoding.ExcludeSignatureScript) != 0;
            var excludeMass = (encoding & TxEncoding.ExcludeMassCommit) != 0;
            var excludePayload = (encoding & TxEncoding.ExcludePayload) != 0;
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
                if (excludeSignature)
                {
                    writer.U64(0);
                }
                else
                {
                    writer.VarBytes(input.SignatureScript.ToBytes());
                    if (Version < 1)
                    {
                        writer.U8(input.SigOpCount);
                    }
                }

                writer.U64(input.Sequence);
                if (!excludeMass && Version >= 1)
                {
                    writer.U16(input.ComputeBudget ?? 0);
                }
            }

            writer.U64((ulong)Outputs.Count);
            foreach (var output in Outputs)
            {
                WriteOutput(writer, output, Version);
            }

            writer.U64(LockTime);
            writer.Raw(SubnetworkId.ToBytes());
            writer.U64(Gas);
            writer.VarBytes(excludePayload ? ReadOnlySpan<byte>.Empty : _payload);
            if (!excludeMass)
            {
                if (Version < 1)
                {
                    if (StorageMass > 0)
                    {
                        writer.U64(StorageMass);
                    }
                }
                else
                {
                    writer.U64(StorageMass);
                }
            }

            return writer.ToArray();
        }

        internal static void WriteOutput(KaspaWriter writer, TxOut output, ushort version)
        {
            if (output == null || output.ScriptPublicKey == null)
            {
                throw new InvalidOperationException("Transaction output is missing a script.");
            }

            writer.U64(output.Value.Sompi);
            writer.U16(output.ScriptPublicKey.Version);
            writer.VarBytes(output.ScriptPublicKey.Script);
            if (version < 1)
            {
                return;
            }

            var covenant = output.Covenant;
            writer.U8(covenant == null ? (byte)0 : (byte)1);
            if (covenant != null)
            {
                writer.U16(covenant.AuthorizingInput);
                writer.Raw(covenant.CovenantId.ToBytes());
            }
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

        private static List<TxIn> ReadInputs(ReadOnlySpan<byte> bytes, ref KaspaReader reader, ushort version)
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
                byte sigOpCount = 0;
                if (version < 1)
                {
                    sigOpCount = reader.U8();
                }

                var sequence = reader.U64();
                var input = new TxIn(new OutPoint(transactionId, index), signatureScript, sequence, sigOpCount);
                if (version >= 1)
                {
                    input.ComputeBudget = reader.U16();
                }

                inputs.Add(input);
            }

            return inputs;
        }

        private static List<TxOut> ReadOutputs(ReadOnlySpan<byte> bytes, ref KaspaReader reader, ushort version)
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
                var scriptVersion = reader.U16();
                var script = reader.VarBytes();
                var output = new TxOut(value, new ScriptPubKey(scriptVersion, script));
                if (version >= 1)
                {
                    var present = reader.U8();
                    if (present == 1)
                    {
                        var authorizing = reader.U16();
                        var covenantId = new uint256(reader.Take(uint256.Length));
                        output.Covenant = new CovenantBinding(authorizing, covenantId);
                    }
                    else if (present != 0)
                    {
                        throw new FormatException("Covenant flag must be 0 or 1.");
                    }
                }

                outputs.Add(output);
            }

            return outputs;
        }
    }

    /// <summary>Flags for <see cref="Transaction"/> hash encoding. Values match rusty-kaspa <c>TxEncodingFlags</c>.</summary>
    [Flags]
    internal enum TxEncoding
    {
        /// <summary>Full encoding.</summary>
        None = 0,

        /// <summary>Replace each signature script with an empty script and omit the version-0 sigop byte.</summary>
        ExcludeSignatureScript = 1 << 0,

        /// <summary>Omit storage mass and the version-1 compute budget.</summary>
        ExcludeMassCommit = 1 << 1,

        /// <summary>Write an empty payload.</summary>
        ExcludePayload = 1 << 2,
    }
}
