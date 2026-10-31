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
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        public Transaction()
            : this(0, Array.Empty<TxIn>(), Array.Empty<TxOut>(), 0, SubnetworkId.Native, 0, Array.Empty<byte>())
        {
        }

        /// <summary>Creates a transaction. The input and output lists are copied.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="payload"/> is null. <paramref name="outputs"/> is null. <paramref name="inputs"/> is null.</exception>
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
        /// <exception cref="System.ArgumentNullException">The assigned value is null.</exception>
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
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">BLAKE3 domain must fit in 32 bytes. BLAKE3 key must be 32 bytes. BLAKE2b key must be at most 64 bytes.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">Transaction input is missing an outpoint. Transaction output is missing a script. BLAKE2b hasher is already finished.</exception>
        public uint256 GetId()
        {
            return SigHash.TransactionId(this);
        }

        /// <summary>Transaction hash. Keyed BLAKE2b over the full encoding, for every supported version.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">BLAKE2b key must be at most 64 bytes.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">BLAKE2b hasher is already finished. Transaction input is missing an outpoint. Transaction output is missing a script.</exception>
        public uint256 GetHash()
        {
            return SigHash.TransactionHash(this);
        }

        /// <summary>Schnorr signature hash for one input.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="utxo"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="inputIndex"/> is outside the allowed range. BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">Sighash type is not one of the six Kaspa values. BLAKE2b key must be at most 64 bytes.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">Transaction input is missing an outpoint. BLAKE2b hasher is already finished. Transaction output is missing a script.</exception>
        public uint256 GetSignatureHash(int inputIndex, UtxoEntry utxo, SigHashType hashType = SigHashType.All)
        {
            return SigHash.CalcSchnorrSignatureHash(this, inputIndex, utxo, hashType);
        }

        /// <summary>ECDSA signature hash for one input.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="utxo"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="inputIndex"/> is outside the allowed range. BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">Sighash type is not one of the six Kaspa values. BLAKE2b key must be at most 64 bytes.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">
        /// SHA256 did not produce a digest. Transaction input is missing an outpoint. BLAKE2b hasher is already
        /// finished. Transaction output is missing a script.
        /// </exception>
        public uint256 GetEcdsaSignatureHash(int inputIndex, UtxoEntry utxo, SigHashType hashType = SigHashType.All)
        {
            return SigHash.CalcEcdsaSignatureHash(this, inputIndex, utxo, hashType);
        }

        /// <summary>
        /// Signs one pay-to-pubkey input.
        /// A Schnorr template uses BIP-340. An ECDSA template uses RFC 6979 compact signatures.
        /// The call does not change <see cref="TxIn.SigOpCount"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="utxo"/> is null. <paramref name="key"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="inputIndex"/> is outside the allowed range. BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">
        /// Sighash type is not one of the six Kaspa values. Signature must be 64 bytes. BIP-340 auxiliary
        /// randomness must be empty or 32 bytes. Schnorr message must be 32 bytes. Schnorr signature must be 64
        /// bytes. BLAKE2b key must be at most 64 bytes. ECDSA message must be 32 bytes. ECDSA signature must be
        /// 64 bytes.
        /// </exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated. uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">
        /// SignInput signs pay-to-pubkey scripts. Build a pay-to-script-hash signature with
        /// Script.PayToScriptHashSignature. Stored private key is no longer valid. Transaction input is missing
        /// an outpoint. BLAKE2b hasher is already finished. Transaction output is missing a script. SHA256 did
        /// not produce a digest.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
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
        /// <exception cref="System.ArgumentNullException"><paramref name="publicKey"/> is null. <paramref name="utxo"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="inputIndex"/> is outside the allowed range. BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">Schnorr signature must be 64 bytes. Sighash type is not one of the six Kaspa values. BLAKE2b key must be at most 64 bytes.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">Transaction input is missing an outpoint. BLAKE2b hasher is already finished. Transaction output is missing a script.</exception>
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
        /// <exception cref="System.ArgumentNullException"><paramref name="publicKey"/> is null. <paramref name="utxo"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="inputIndex"/> is outside the allowed range. BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">ECDSA signature must be 64 bytes. Sighash type is not one of the six Kaspa values. BLAKE2b key must be at most 64 bytes.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">
        /// SHA256 did not produce a digest. Transaction input is missing an outpoint. BLAKE2b hasher is already
        /// finished. Transaction output is missing a script.
        /// </exception>
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
        /// Runs the script engine on one input against <paramref name="utxo"/>.
        /// Spent outputs of the other inputs are omitted, so introspection of those inputs fails.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="utxo"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="inputIndex"/> is outside the allowed range. BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">
        /// The spent output for this input is missing. BLAKE3 key must be 32 bytes. wRPC request is missing a
        /// numeric id. Set PointOfView or ChainBlock, not both. PointOfView is the selected parent P.
        /// ChainBlock is the chain block B whose selected parent becomes P. Sighash type is not one of the six
        /// Kaspa values. BLAKE2b key must be at most 64 bytes.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isChainBlock. Expected a hex hash for {name}. uint256 hex must be 32 bytes.
        /// uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a parent hash. Expected parent hashes. Expected an array for parents. Missing JSON string
        /// {name}. Missing JSON property {name}. Hash is missing. networkId is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// WebSocket is not connected. Call ConnectAsync first. Transaction input is missing an outpoint.
        /// BLAKE2b hasher is already finished. Transaction output is missing a script. SHA256 did not produce a
        /// digest.
        /// </exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        /// <exception cref="NKaspa.RPC.ConsensusSeqCommitException">
        /// acceptedIdMerkleRoot is missing for {target.Hash}. seq commitment was requested for {target.Hash},
        /// which is not a selected-parent ancestor of {pov.Hash}. header blueScore does not match verboseData
        /// blueScore for {block.Hash}. blueScore is missing for {block.Hash}. getBlockDagInfo.sink changed from
        /// {previous} to {dag.Sink} and the new selected chain does not contain {anchor}.
        /// verboseData.isChainBlock is missing for {current.Hash}. selected parent chain cycled at
        /// {current.Hash}. isChainBlock is false for {block.Hash} on the selected chain.
        /// verboseData.isChainBlock is missing for {block.Hash}. getBlock did not return {hash}. The node data
        /// is incomplete. verboseData.hash {block.VerboseHash} does not match the requested block {block.Hash}.
        /// header.hash {block.HeaderHash} does not match the requested block {block.Hash}. Request limit of
        /// {_maxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)} was reached before the
        /// selected parent chain could be checked. blue score does not decrease from {child.Hash}
        /// ({childScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}) to selected parent
        /// {parent.Hash} ({parentScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}). selected
        /// parent walk reached {block.Hash} but isChainBlock is false. isChainBlock is false for {block.Hash}
        /// and getBlockDagInfo.sink changed from {previous} to {again.Sink}. getBlockDagInfo failed:
        /// {ex.Message}. selectedParentHash {block.SelectedParentHash} does not match the first direct parent
        /// {block.DirectParent} of {block.Hash}. Further failures of this type use the same exception.
        /// </exception>
        public bool VerifyInput(int inputIndex, UtxoEntry utxo)
        {
            if (utxo == null)
            {
                throw new ArgumentNullException(nameof(utxo));
            }

            EnsureInput(inputIndex);
            var utxos = new UtxoEntry?[Inputs.Count];
            utxos[inputIndex] = utxo;
            var engine = TxScriptEngine.Run(
                Inputs[inputIndex].SignatureScript.ToBytes(),
                utxo.ScriptPublicKey,
                this,
                inputIndex,
                utxos);
            return engine.Error == null;
        }

        /// <summary>
        /// Runs the script engine on every input. <paramref name="utxos"/> is parallel to <see cref="Inputs"/>.
        /// A script failure returns false. Null entries and a different count throw <see cref="ArgumentException"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="utxos"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">
        /// Spent output {i.ToString()} is missing. The spent output list must have one entry per input. The
        /// spent output for this input is missing. BLAKE3 key must be 32 bytes. wRPC request is missing a
        /// numeric id. Set PointOfView or ChainBlock, not both. PointOfView is the selected parent P.
        /// ChainBlock is the chain block B whose selected parent becomes P. Sighash type is not one of the six
        /// Kaspa values. BLAKE2b key must be at most 64 bytes.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Expected a boolean for isChainBlock. Expected a hex hash for {name}. uint256 hex must be 32 bytes.
        /// uint256 requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even.
        /// Expected an unsigned 64-bit integer. JSON number does not fit in an unsigned 64-bit integer.
        /// Expected a parent hash. Expected parent hashes. Expected an array for parents. Missing JSON string
        /// {name}. Missing JSON property {name}. Hash is missing. networkId is not a string or an object.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// WebSocket is not connected. Call ConnectAsync first. Transaction input is missing an outpoint.
        /// BLAKE2b hasher is already finished. Transaction output is missing a script. SHA256 did not produce a
        /// digest.
        /// </exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        /// <exception cref="NKaspa.RPC.ConsensusSeqCommitException">
        /// acceptedIdMerkleRoot is missing for {target.Hash}. seq commitment was requested for {target.Hash},
        /// which is not a selected-parent ancestor of {pov.Hash}. header blueScore does not match verboseData
        /// blueScore for {block.Hash}. blueScore is missing for {block.Hash}. getBlockDagInfo.sink changed from
        /// {previous} to {dag.Sink} and the new selected chain does not contain {anchor}.
        /// verboseData.isChainBlock is missing for {current.Hash}. selected parent chain cycled at
        /// {current.Hash}. isChainBlock is false for {block.Hash} on the selected chain.
        /// verboseData.isChainBlock is missing for {block.Hash}. getBlock did not return {hash}. The node data
        /// is incomplete. verboseData.hash {block.VerboseHash} does not match the requested block {block.Hash}.
        /// header.hash {block.HeaderHash} does not match the requested block {block.Hash}. Request limit of
        /// {_maxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)} was reached before the
        /// selected parent chain could be checked. blue score does not decrease from {child.Hash}
        /// ({childScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}) to selected parent
        /// {parent.Hash} ({parentScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}). selected
        /// parent walk reached {block.Hash} but isChainBlock is false. isChainBlock is false for {block.Hash}
        /// and getBlockDagInfo.sink changed from {previous} to {again.Sink}. getBlockDagInfo failed:
        /// {ex.Message}. selectedParentHash {block.SelectedParentHash} does not match the first direct parent
        /// {block.DirectParent} of {block.Hash}. Further failures of this type use the same exception.
        /// </exception>
        public bool Verify(IReadOnlyList<UtxoEntry> utxos)
        {
            if (utxos == null)
            {
                throw new ArgumentNullException(nameof(utxos));
            }

            if (utxos.Count != Inputs.Count)
            {
                throw new ArgumentException("The spent output list must have one entry per input.", nameof(utxos));
            }

            var copy = new UtxoEntry?[utxos.Count];
            for (var i = 0; i < utxos.Count; i++)
            {
                if (utxos[i] == null)
                {
                    throw new ArgumentException("Spent output " + i.ToString() + " is missing.", nameof(utxos));
                }

                copy[i] = utxos[i];
            }

            for (var i = 0; i < Inputs.Count; i++)
            {
                var engine = TxScriptEngine.Run(
                    Inputs[i].SignatureScript.ToBytes(),
                    copy[i]!.ScriptPublicKey,
                    this,
                    i,
                    copy);
                if (engine.Error != null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Consensus bytes. Version 0 omits storage mass.
        /// Version 1 includes compute budgets, covenant bytes, and storage mass.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Transaction input is missing an outpoint. Transaction output is missing a script.</exception>
        public byte[] ToBytes()
        {
            return Serialize(Version == 0 ? TxEncoding.ExcludeMassCommit : TxEncoding.None);
        }

        /// <summary>Lowercase hex of <see cref="ToBytes"/>.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.InvalidOperationException">Transaction input is missing an outpoint. Transaction output is missing a script.</exception>
        public string ToHex()
        {
            return Encoders.Hex.EncodeData(ToBytes());
        }

        /// <summary>
        /// Reads a version-0 or version-1 transaction.
        /// Version 0 leaves storage mass at zero. Version 1 reads the storage mass that follows the payload.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">Subnetwork id must be 20 bytes.</exception>
        /// <exception cref="System.FormatException">
        /// Transaction has trailing bytes. Transaction is truncated. Transaction field is too long. Covenant
        /// flag must be 0 or 1. Transaction output count is invalid. uint256 requires 32 bytes. Transaction
        /// input count is invalid. Script push is too long. Script push is truncated.
        /// </exception>
        /// <exception cref="System.NotSupportedException">Only version 0 and version 1 transactions can be deserialized.</exception>
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
        /// <exception cref="System.ArgumentNullException"><paramref name="hex"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Subnetwork id must be 20 bytes.</exception>
        /// <exception cref="System.FormatException">
        /// Transaction has trailing bytes. Transaction is truncated. Transaction field is too long. Covenant
        /// flag must be 0 or 1. Transaction output count is invalid. uint256 requires 32 bytes. Transaction
        /// input count is invalid. Script push is too long. Script push is truncated. Hex string contains a
        /// non-hex character. Hex string length must be even.
        /// </exception>
        /// <exception cref="System.NotSupportedException">Only version 0 and version 1 transactions can be deserialized.</exception>
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
