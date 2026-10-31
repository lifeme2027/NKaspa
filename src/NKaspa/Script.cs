using NKaspa.Crypto;
using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// A Kaspa script. Kaspa counterpart of NBitcoin.Script.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parsing accepts the Kaspa push opcodes. Building uses the canonical push
    /// from rusty-kaspa <c>script_builder.rs</c>: an empty push is <see cref="OpcodeType.OP_0"/>,
    /// a single byte 1 through 16 is <see cref="OpcodeType.OP_1"/> through <see cref="OpcodeType.OP_16"/>,
    /// a single byte 0x81 is <see cref="OpcodeType.OP_1NEGATE"/>, and longer data uses
    /// <c>OP_DATA_n</c>, <see cref="OpcodeType.OP_PUSHDATA1"/>, <see cref="OpcodeType.OP_PUSHDATA2"/>,
    /// or <see cref="OpcodeType.OP_PUSHDATA4"/>.
    /// </para>
    /// <para>
    /// <see cref="VerifyScript(Script, ScriptPubKey, Transaction, int, IReadOnlyList{UtxoEntry})"/> executes scripts.
    /// Standard templates and signature scripts are recognized and built.
    /// Pay-to-pubkey Schnorr is <c>OP_DATA_32 || x-only || OP_CHECKSIG</c>.
    /// Pay-to-pubkey ECDSA is <c>OP_DATA_33 || compressed || OP_CHECKSIGECDSA</c>.
    /// Pay-to-script-hash is <c>OP_BLAKE2B || OP_DATA_32 || hash || OP_EQUAL</c>.
    /// A signature script for either pubkey template is <c>OP_DATA_65 || 64-byte signature || sighash byte</c>.
    /// </para>
    /// </remarks>
    public sealed class Script : IEquatable<Script>
    {
        /// <summary>The empty script.</summary>
        public static Script Empty { get; } = new Script(ReadOnlySpan<byte>.Empty);

        private readonly byte[] _bytes;
        private readonly Op[] _ops;

        /// <summary>Copies <paramref name="script"/> and parses it. A truncated push throws <see cref="FormatException"/>.</summary>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public Script(ReadOnlySpan<byte> script)
        {
            _bytes = script.ToArray();
            _ops = ParseOps(_bytes);
        }

        /// <summary>Copies and parses <paramref name="script"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="script"/> is null.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public Script(byte[] script)
            : this(script == null ? throw new ArgumentNullException(nameof(script)) : script.AsSpan())
        {
        }

        /// <summary>Parses lowercase or uppercase hex.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="hex"/> is null.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated. Hex string contains a non-hex character. Hex string length must be even.</exception>
        public static Script Parse(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            return new Script(Encoders.Hex.DecodeData(hex));
        }

        /// <summary>Number of script bytes.</summary>
        public int Length
        {
            get { return _bytes.Length; }
        }

        /// <summary>Parsed instructions.</summary>
        public IReadOnlyList<Op> Ops
        {
            get { return _ops; }
        }

        /// <summary>A copy of the script bytes.</summary>
        public byte[] ToBytes()
        {
            return (byte[])_bytes.Clone();
        }

        /// <summary>Lowercase hex of the script bytes.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public string ToHex()
        {
            return Encoders.Hex.EncodeData(_bytes);
        }

        /// <summary>Space-separated opcodes. Data pushes append lowercase hex.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public override string ToString()
        {
            if (_ops.Length == 0)
            {
                return string.Empty;
            }

            var parts = new string[_ops.Length];
            for (var i = 0; i < _ops.Length; i++)
            {
                parts[i] = _ops[i].ToString();
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Rusty-kaspa <c>script_to_str</c> form. Opcode names match the txscript type names,
        /// and pushed bytes are <c>0x</c> hex so <c>ScriptShortForm</c> can parse the text back.
        /// <see cref="ToString"/> stays the NKaspa form without the <c>0x</c> prefix.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public string ToRustString()
        {
            if (_ops.Length == 0)
            {
                return string.Empty;
            }

            var parts = new string[_ops.Length];
            for (var i = 0; i < _ops.Length; i++)
            {
                var op = _ops[i];
                var name = OpcodeRust.Name((byte)op.Code);
                if (op.Data == null || op.Data.Length == 0)
                {
                    parts[i] = name;
                }
                else
                {
                    parts[i] = name + " 0x" + Encoders.Hex.EncodeData(op.Data);
                }
            }

            return string.Join(" ", parts);
        }

        /// <summary>Unkeyed BLAKE2b-256 of these script bytes. This is the Kaspa script hash.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">BLAKE2b key must be at most 64 bytes.</exception>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">BLAKE2b hasher is already finished.</exception>
        public uint256 GetScriptHash()
        {
            return Hashes.Blake2b256(_bytes);
        }

        /// <summary>True for <c>OP_DATA_32 || 32 bytes || OP_CHECKSIG</c>.</summary>
        public bool IsPayToPubKey
        {
            get
            {
                return _bytes.Length == 34
                    && _bytes[0] == (byte)OpcodeType.OP_DATA_32
                    && _bytes[33] == (byte)OpcodeType.OP_CHECKSIG;
            }
        }

        /// <summary>True for <c>OP_DATA_33 || 33 bytes || OP_CHECKSIGECDSA</c>.</summary>
        public bool IsPayToPubKeyEcdsa
        {
            get
            {
                return _bytes.Length == 35
                    && _bytes[0] == (byte)OpcodeType.OP_DATA_33
                    && _bytes[34] == (byte)OpcodeType.OP_CHECKSIGECDSA;
            }
        }

        /// <summary>True for <c>OP_BLAKE2B || OP_DATA_32 || 32 bytes || OP_EQUAL</c>.</summary>
        public bool IsPayToScriptHash
        {
            get
            {
                return _bytes.Length == 35
                    && _bytes[0] == (byte)OpcodeType.OP_BLAKE2B
                    && _bytes[1] == (byte)OpcodeType.OP_DATA_32
                    && _bytes[34] == (byte)OpcodeType.OP_EQUAL;
            }
        }

        /// <summary>The 32-byte x-only key inside a Schnorr pay-to-pubkey script.</summary>
        public bool TryGetPayToPubKey(out byte[] xOnly)
        {
            if (!IsPayToPubKey)
            {
                xOnly = Array.Empty<byte>();
                return false;
            }

            xOnly = new byte[32];
            _bytes.AsSpan(1, 32).CopyTo(xOnly);
            return true;
        }

        /// <summary>The 33-byte compressed key inside an ECDSA pay-to-pubkey script.</summary>
        public bool TryGetPayToPubKeyEcdsa(out byte[] compressed)
        {
            if (!IsPayToPubKeyEcdsa)
            {
                compressed = Array.Empty<byte>();
                return false;
            }

            compressed = new byte[33];
            _bytes.AsSpan(1, 33).CopyTo(compressed);
            return true;
        }

        /// <summary>The 32-byte script hash inside a pay-to-script-hash script.</summary>
        /// <exception cref="System.FormatException">uint256 requires 32 bytes.</exception>
        public bool TryGetPayToScriptHash(out uint256 scriptHash)
        {
            if (!IsPayToScriptHash)
            {
                scriptHash = uint256.Zero;
                return false;
            }

            scriptHash = new uint256(_bytes.AsSpan(2, 32));
            return true;
        }

        /// <summary>
        /// True when this script is one canonical 65-byte signature push:
        /// 64 signature bytes followed by a defined <see cref="SigHashType"/>.
        /// </summary>
        public bool TryParseSignature(out byte[] signature, out SigHashType hashType)
        {
            signature = Array.Empty<byte>();
            hashType = 0;
            var pushed = _ops.Length == 1 ? _ops[0].Data : null;
            if (_ops.Length != 1 || _ops[0].Code != OpcodeType.OP_DATA_65 || pushed == null || pushed.Length != 65)
            {
                return false;
            }
            var type = (SigHashType)pushed[64];
            if (!type.IsDefined())
            {
                return false;
            }

            signature = new byte[64];
            pushed.AsSpan(0, 64).CopyTo(signature);
            hashType = type;
            return true;
        }

        /// <summary>Schnorr pay-to-pubkey script. The 32 bytes are not required to be a curve point.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">Schnorr x-only public key must be 32 bytes.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script PayToPubKey(ReadOnlySpan<byte> xOnlyPublicKey)
        {
            if (xOnlyPublicKey.Length != 32)
            {
                throw new ArgumentException("Schnorr x-only public key must be 32 bytes.", nameof(xOnlyPublicKey));
            }

            var script = new byte[34];
            script[0] = (byte)OpcodeType.OP_DATA_32;
            xOnlyPublicKey.CopyTo(script.AsSpan(1));
            script[33] = (byte)OpcodeType.OP_CHECKSIG;
            return new Script(script);
        }

        /// <summary>Schnorr pay-to-pubkey script.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="publicKey"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Schnorr x-only public key must be 32 bytes.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script PayToPubKey(PubKey publicKey)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            return PayToPubKey(publicKey.ToBytes());
        }

        /// <summary>ECDSA pay-to-pubkey script.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="publicKey"/> is null.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script PayToPubKey(PubKeyECDSA publicKey)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            var compressed = publicKey.ToBytes();
            var script = new byte[35];
            script[0] = (byte)OpcodeType.OP_DATA_33;
            compressed.CopyTo(script.AsSpan(1));
            script[34] = (byte)OpcodeType.OP_CHECKSIGECDSA;
            return new Script(script);
        }

        /// <summary>Pay-to-script-hash script for a 32-byte BLAKE2b script hash.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">Script hash must be 32 bytes.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script PayToScriptHash(ReadOnlySpan<byte> scriptHash)
        {
            if (scriptHash.Length != 32)
            {
                throw new ArgumentException("Script hash must be 32 bytes.", nameof(scriptHash));
            }

            var script = new byte[35];
            script[0] = (byte)OpcodeType.OP_BLAKE2B;
            script[1] = (byte)OpcodeType.OP_DATA_32;
            scriptHash.CopyTo(script.AsSpan(2));
            script[34] = (byte)OpcodeType.OP_EQUAL;
            return new Script(script);
        }

        /// <summary>Pay-to-script-hash script for a script hash.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptHash"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Script hash must be 32 bytes.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script PayToScriptHash(uint256 scriptHash)
        {
            if (scriptHash == null)
            {
                throw new ArgumentNullException(nameof(scriptHash));
            }

            return PayToScriptHash(scriptHash.ToBytes());
        }

        /// <summary>Pay-to-script-hash script for the BLAKE2b-256 of <paramref name="redeemScript"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="redeemScript"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">BLAKE2b digest size must be from 1 to 64 bytes.</exception>
        /// <exception cref="System.ArgumentException">Script hash must be 32 bytes. BLAKE2b key must be at most 64 bytes.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated. uint256 requires 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">BLAKE2b hasher is already finished.</exception>
        public static Script PayToScriptHash(Script redeemScript)
        {
            if (redeemScript == null)
            {
                throw new ArgumentNullException(nameof(redeemScript));
            }

            return PayToScriptHash(redeemScript.GetScriptHash());
        }

        /// <summary>Signature script: <c>OP_DATA_65 || signature || sighash type</c>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="signature"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Sighash type is not one of the six Kaspa values. Signature must be 64 bytes.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script SignatureScript(SchnorrSignature signature, SigHashType hashType)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            return SignatureScript(signature.ToBytes(), hashType);
        }

        /// <summary>ECDSA signature script. The signature is the 64-byte compact form, not DER.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="signature"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Sighash type is not one of the six Kaspa values. Signature must be 64 bytes.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script SignatureScript(ECDSASignature signature, SigHashType hashType)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            return SignatureScript(signature.ToBytes(), hashType);
        }

        /// <summary>
        /// Pay-to-script-hash signature script.
        /// <paramref name="signaturePushes"/> is written first, then a canonical push of the redeem script.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="redeemScript"/> is null.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static Script PayToScriptHashSignature(Script redeemScript, Script? signaturePushes = null)
        {
            if (redeemScript == null)
            {
                throw new ArgumentNullException(nameof(redeemScript));
            }

            var builder = new ScriptBuilder();
            if (signaturePushes != null && signaturePushes.Length > 0)
            {
                builder.AddRaw(signaturePushes.ToBytes());
            }

            builder.AddData(redeemScript.ToBytes());
            return builder.ToScript();
        }

        /// <inheritdoc />
        public bool Equals(Script? other)
        {
            return other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as Script);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = 17;
            for (var i = 0; i < _bytes.Length; i++)
            {
                hash = (hash * 31) + _bytes[i];
            }

            return hash;
        }

        private static Script SignatureScript(byte[] signature, SigHashType hashType)
        {
            if (signature.Length != 64)
            {
                throw new ArgumentException("Signature must be 64 bytes.", nameof(signature));
            }

            if (!hashType.IsDefined())
            {
                throw new ArgumentException("Sighash type is not one of the six Kaspa values.", nameof(hashType));
            }

            var raw = new byte[66];
            raw[0] = (byte)OpcodeType.OP_DATA_65;
            signature.CopyTo(raw, 1);
            raw[65] = (byte)hashType;
            return new Script(raw);
        }

        private static Op[] ParseOps(byte[] script)
        {
            var ops = new List<Op>();
            var i = 0;
            while (i < script.Length)
            {
                var code = script[i++];
                if (code >= 1 && code <= 75)
                {
                    if (script.Length - i < code)
                    {
                        throw new FormatException("Script push is truncated.");
                    }

                    var data = new byte[code];
                    Buffer.BlockCopy(script, i, data, 0, code);
                    i += code;
                    ops.Add(new Op((OpcodeType)code, data));
                }
                else if (code == (byte)OpcodeType.OP_PUSHDATA1)
                {
                    if (i >= script.Length)
                    {
                        throw new FormatException("Script push is truncated.");
                    }

                    var length = script[i++];
                    ops.Add(new Op(OpcodeType.OP_PUSHDATA1, Take(script, ref i, length)));
                }
                else if (code == (byte)OpcodeType.OP_PUSHDATA2)
                {
                    if (script.Length - i < 2)
                    {
                        throw new FormatException("Script push is truncated.");
                    }

                    var length = script[i] | (script[i + 1] << 8);
                    i += 2;
                    ops.Add(new Op(OpcodeType.OP_PUSHDATA2, Take(script, ref i, length)));
                }
                else if (code == (byte)OpcodeType.OP_PUSHDATA4)
                {
                    if (script.Length - i < 4)
                    {
                        throw new FormatException("Script push is truncated.");
                    }

                    var length = (uint)script[i]
                        | ((uint)script[i + 1] << 8)
                        | ((uint)script[i + 2] << 16)
                        | ((uint)script[i + 3] << 24);
                    i += 4;
                    if (length > int.MaxValue)
                    {
                        throw new FormatException("Script push is too long.");
                    }

                    ops.Add(new Op(OpcodeType.OP_PUSHDATA4, Take(script, ref i, (int)length)));
                }
                else
                {
                    ops.Add(new Op((OpcodeType)code, null));
                }
            }

            return ops.ToArray();
        }

        /// <summary>
        /// Runs <paramref name="scriptSig"/> and <paramref name="scriptPubKey"/> for one input.
        /// The input may spend its committed sigop count or compute budget, plus 9999 script units.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utxos"/> is null. <paramref name="scriptPubKey"/> is null. <paramref
        /// name="scriptSig"/> is null. <paramref name="tx"/> is null.
        /// </exception>
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
        public static bool VerifyScript(
            Script scriptSig,
            ScriptPubKey scriptPubKey,
            Transaction tx,
            int inputIndex,
            IReadOnlyList<UtxoEntry> utxos)
        {
            return VerifyScript(scriptSig, scriptPubKey, tx, inputIndex, utxos, out _);
        }

        /// <summary>
        /// Runs <paramref name="scriptSig"/> and <paramref name="scriptPubKey"/> for one input.
        /// <paramref name="error"/> is null when the input is valid.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utxos"/> is null. <paramref name="scriptPubKey"/> is null. <paramref
        /// name="scriptSig"/> is null. <paramref name="tx"/> is null.
        /// </exception>
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
        public static bool VerifyScript(
            Script scriptSig,
            ScriptPubKey scriptPubKey,
            Transaction tx,
            int inputIndex,
            IReadOnlyList<UtxoEntry> utxos,
            out ScriptError? error)
        {
            var context = ScriptEvaluationContext.Verify(scriptSig, scriptPubKey, tx, inputIndex, utxos);
            error = context.Error;
            return context.Result;
        }

        /// <summary>Runs <paramref name="scriptSig"/> against a version-0 <paramref name="scriptPubKey"/>.</summary>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="scriptPubKey"/> is null. <paramref name="utxos"/> is null. <paramref
        /// name="scriptSig"/> is null. <paramref name="tx"/> is null.
        /// </exception>
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
        public static bool VerifyScript(
            Script scriptSig,
            Script scriptPubKey,
            Transaction tx,
            int inputIndex,
            IReadOnlyList<UtxoEntry> utxos)
        {
            return VerifyScript(scriptSig, scriptPubKey, tx, inputIndex, utxos, out _);
        }

        /// <summary>Runs <paramref name="scriptSig"/> against a version-0 <paramref name="scriptPubKey"/>.</summary>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="scriptPubKey"/> is null. <paramref name="utxos"/> is null. <paramref
        /// name="scriptSig"/> is null. <paramref name="tx"/> is null.
        /// </exception>
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
        public static bool VerifyScript(
            Script scriptSig,
            Script scriptPubKey,
            Transaction tx,
            int inputIndex,
            IReadOnlyList<UtxoEntry> utxos,
            out ScriptError? error)
        {
            if (scriptPubKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPubKey));
            }

            return VerifyScript(scriptSig, new ScriptPubKey(0, scriptPubKey), tx, inputIndex, utxos, out error);
        }

        private static byte[] Take(byte[] script, ref int offset, int length)
        {
            if (length < 0 || script.Length - offset < length)
            {
                throw new FormatException("Script push is truncated.");
            }

            var data = new byte[length];
            Buffer.BlockCopy(script, offset, data, 0, length);
            offset += length;
            return data;
        }
    }

    /// <summary>
    /// Builds a script with canonical pushes. Kaspa counterpart of NBitcoin.Script.Builder / bitcoin script builder.
    /// </summary>
    public sealed partial class ScriptBuilder
    {
        private readonly List<byte> _bytes = new List<byte>();

        /// <summary>Bytes written so far.</summary>
        public int Length
        {
            get { return _bytes.Count; }
        }

        /// <summary>Appends an opcode that is not a data push.</summary>
        /// <exception cref="System.ArgumentException">Use AddData for a push.</exception>
        public ScriptBuilder AddOp(OpcodeType opcode)
        {
            var code = (byte)opcode;
            if ((code >= 1 && code <= 75)
                || opcode == OpcodeType.OP_PUSHDATA1
                || opcode == OpcodeType.OP_PUSHDATA2
                || opcode == OpcodeType.OP_PUSHDATA4)
            {
                throw new ArgumentException("Use AddData for a push.", nameof(opcode));
            }

            _bytes.Add(code);
            return this;
        }

        /// <summary>Appends a canonical data push.</summary>
        public ScriptBuilder AddData(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                _bytes.Add((byte)OpcodeType.OP_0);
                return this;
            }

            if (data.Length == 1)
            {
                var value = data[0];
                if (value >= 1 && value <= 16)
                {
                    _bytes.Add((byte)((byte)OpcodeType.OP_1 + value - 1));
                    return this;
                }

                if (value == 0x81)
                {
                    _bytes.Add((byte)OpcodeType.OP_1NEGATE);
                    return this;
                }
            }

            if (data.Length <= 75)
            {
                _bytes.Add((byte)data.Length);
                AddSpan(data);
                return this;
            }

            if (data.Length <= 255)
            {
                _bytes.Add((byte)OpcodeType.OP_PUSHDATA1);
                _bytes.Add((byte)data.Length);
                AddSpan(data);
                return this;
            }

            if (data.Length <= 65535)
            {
                _bytes.Add((byte)OpcodeType.OP_PUSHDATA2);
                _bytes.Add((byte)data.Length);
                _bytes.Add((byte)(data.Length >> 8));
                AddSpan(data);
                return this;
            }

            _bytes.Add((byte)OpcodeType.OP_PUSHDATA4);
            var length = data.Length;
            _bytes.Add((byte)length);
            _bytes.Add((byte)(length >> 8));
            _bytes.Add((byte)(length >> 16));
            _bytes.Add((byte)(length >> 24));
            AddSpan(data);
            return this;
        }

        /// <summary>
        /// Appends the canonical encoding of a script number.
        /// Zero, -1, and 1 through 16 use the small-integer opcodes.
        /// </summary>
        /// <exception cref="System.ArgumentException">Use AddData for a push.</exception>
        public ScriptBuilder AddInt64(long value)
        {
            if (value == 0)
            {
                return AddOp(OpcodeType.OP_0);
            }

            if (value == -1)
            {
                return AddOp(OpcodeType.OP_1NEGATE);
            }

            if (value >= 1 && value <= 16)
            {
                return AddOp((OpcodeType)((byte)OpcodeType.OP_1 + (byte)(value - 1)));
            }

            bool negative = value < 0;
            ulong magnitude = value == long.MinValue
                ? 1UL << 63
                : (ulong)(negative ? -value : value);

            Span<byte> encoded = stackalloc byte[9];
            var count = 0;
            while (magnitude != 0)
            {
                encoded[count++] = (byte)magnitude;
                magnitude >>= 8;
            }

            if ((encoded[count - 1] & 0x80) != 0)
            {
                encoded[count++] = negative ? (byte)0x80 : (byte)0x00;
            }
            else if (negative)
            {
                encoded[count - 1] |= 0x80;
            }

            return AddData(encoded.Slice(0, count));
        }

        /// <summary>Appends bytes without encoding them as a push.</summary>
        public ScriptBuilder AddRaw(ReadOnlySpan<byte> raw)
        {
            AddSpan(raw);
            return this;
        }

        /// <summary>Raw bytes written so far, including a truncated push.</summary>
        public byte[] ToBytes()
        {
            return _bytes.ToArray();
        }

        /// <summary>Parses the bytes written so far. A truncated push throws <see cref="FormatException"/>.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public Script ToScript()
        {
            return new Script(_bytes.ToArray());
        }

        private void AddSpan(ReadOnlySpan<byte> data)
        {
            for (var i = 0; i < data.Length; i++)
            {
                _bytes.Add(data[i]);
            }
        }
    }
}
