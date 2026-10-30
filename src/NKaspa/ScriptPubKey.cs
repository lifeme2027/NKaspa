using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// Versioned script public key. Kaspa counterpart of the <c>Script</c> value
    /// exposed by NBitcoin.BitcoinAddress.ScriptPubKey, stored with the u16 version
    /// Kaspa puts on an output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Version"/> is the u16 stored next to the script in a Kaspa output.
    /// Standard payment scripts use version 0. It is not the address version byte.
    /// </para>
    /// <para>
    /// Layout matches rusty-kaspa <c>pay_to_address_script</c> and kaswallet
    /// <c>SchnorrAddress.ScriptForAddress</c>:
    /// Schnorr P2PK is <c>OP_DATA_32 || x-only || OP_CHECKSIG</c> (34 bytes),
    /// ECDSA P2PK is <c>OP_DATA_33 || compressed key || OP_CHECKSIGECDSA</c> (35 bytes),
    /// P2SH is <c>OP_BLAKE2B || OP_DATA_32 || hash || OP_EQUAL</c> (35 bytes).
    /// Parsing and building live on <see cref="Script"/>. This type does not execute scripts.
    /// </para>
    /// </remarks>
    public sealed class ScriptPubKey : IEquatable<ScriptPubKey>
    {
        /// <summary>Pushes the next 32 bytes. The opcode value is the length, 0x20.</summary>
        public const byte OpData32 = 0x20;

        /// <summary>Pushes the next 33 bytes. The opcode value is the length, 0x21.</summary>
        public const byte OpData33 = 0x21;

        /// <summary>Pops two items and pushes whether they are equal. Opcode 0x87.</summary>
        public const byte OpEqual = 0x87;

        /// <summary>Replaces the top stack item with its BLAKE2b-256 digest. Opcode 0xaa.</summary>
        public const byte OpBlake2b = 0xaa;

        /// <summary>Checks an ECDSA signature against the 33-byte public key. Opcode 0xab.</summary>
        public const byte OpCheckSigECDSA = 0xab;

        /// <summary>Checks a Schnorr signature against the 32-byte x-only key. Opcode 0xac.</summary>
        public const byte OpCheckSig = 0xac;

        private readonly byte[] _script;

        /// <summary>Wraps an existing script. The bytes are copied.</summary>
        public ScriptPubKey(ushort version, byte[] script)
        {
            if (script == null)
            {
                throw new ArgumentNullException(nameof(script));
            }

            Version = version;
            _script = (byte[])script.Clone();
        }

        /// <summary>Wraps a parsed script. The bytes are copied.</summary>
        public ScriptPubKey(ushort version, Script script)
        {
            if (script == null)
            {
                throw new ArgumentNullException(nameof(script));
            }

            Version = version;
            _script = script.ToBytes();
        }

        /// <summary>Script version stored with the output. Standard payments use 0.</summary>
        public ushort Version { get; }

        /// <summary>A copy of the script bytes.</summary>
        public byte[] Script
        {
            get { return (byte[])_script.Clone(); }
        }

        /// <summary>Number of script bytes.</summary>
        public int Length
        {
            get { return _script.Length; }
        }

        /// <summary>Schnorr pay-to-pubkey script, version 0.</summary>
        public static ScriptPubKey PayToPubKey(ReadOnlySpan<byte> xOnlyPublicKey)
        {
            if (xOnlyPublicKey.Length != 32)
            {
                throw new ArgumentException("Schnorr x-only public key must be 32 bytes.", nameof(xOnlyPublicKey));
            }

            var script = new byte[34];
            script[0] = OpData32;
            xOnlyPublicKey.CopyTo(script.AsSpan(1));
            script[33] = OpCheckSig;
            return new ScriptPubKey(0, script);
        }

        /// <summary>ECDSA pay-to-pubkey script, version 0. The key is the 33-byte compressed form.</summary>
        public static ScriptPubKey PayToPubKeyEcdsa(ReadOnlySpan<byte> compressedPublicKey)
        {
            if (compressedPublicKey.Length != 33)
            {
                throw new ArgumentException("ECDSA public key must be 33 bytes.", nameof(compressedPublicKey));
            }

            var script = new byte[35];
            script[0] = OpData33;
            compressedPublicKey.CopyTo(script.AsSpan(1));
            script[34] = OpCheckSigECDSA;
            return new ScriptPubKey(0, script);
        }

        /// <summary>Pay-to-script-hash script, version 0. <paramref name="scriptHash"/> is BLAKE2b-256 of the redeem script.</summary>
        public static ScriptPubKey PayToScriptHash(ReadOnlySpan<byte> scriptHash)
        {
            if (scriptHash.Length != 32)
            {
                throw new ArgumentException("Script hash must be 32 bytes.", nameof(scriptHash));
            }

            var script = new byte[35];
            script[0] = OpBlake2b;
            script[1] = OpData32;
            scriptHash.CopyTo(script.AsSpan(2));
            script[34] = OpEqual;
            return new ScriptPubKey(0, script);
        }

        /// <summary>Schnorr pay-to-pubkey script, version 0.</summary>
        public static ScriptPubKey PayToPubKey(PubKey publicKey)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            return new ScriptPubKey(0, global::NKaspa.Script.PayToPubKey(publicKey));
        }

        /// <summary>ECDSA pay-to-pubkey script, version 0.</summary>
        public static ScriptPubKey PayToPubKeyEcdsa(PubKeyECDSA publicKey)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            return new ScriptPubKey(0, global::NKaspa.Script.PayToPubKey(publicKey));
        }

        /// <summary>Pay-to-script-hash script, version 0.</summary>
        public static ScriptPubKey PayToScriptHash(uint256 scriptHash)
        {
            return new ScriptPubKey(0, global::NKaspa.Script.PayToScriptHash(scriptHash));
        }

        /// <summary>Pay-to-script-hash script for <paramref name="redeemScript"/>, version 0.</summary>
        public static ScriptPubKey PayToScriptHash(Script redeemScript)
        {
            return new ScriptPubKey(0, global::NKaspa.Script.PayToScriptHash(redeemScript));
        }

        /// <summary>The script bytes parsed as a <see cref="Script"/>.</summary>
        public Script ToScript()
        {
            return new Script(_script);
        }

        /// <summary>True for a version-0 Schnorr pay-to-pubkey script.</summary>
        public bool IsPayToPubKey
        {
            get { return Version == 0 && ToScript().IsPayToPubKey; }
        }

        /// <summary>True for a version-0 ECDSA pay-to-pubkey script.</summary>
        public bool IsPayToPubKeyEcdsa
        {
            get { return Version == 0 && ToScript().IsPayToPubKeyEcdsa; }
        }

        /// <summary>True for a version-0 pay-to-script-hash script.</summary>
        public bool IsPayToScriptHash
        {
            get { return Version == 0 && ToScript().IsPayToScriptHash; }
        }

        /// <summary>Lowercase hex of the script bytes. The version is not included.</summary>
        public string ToHex()
        {
            return Encoders.Hex.EncodeData(_script);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToHex();
        }

        /// <inheritdoc />
        public bool Equals(ScriptPubKey? other)
        {
            if (other is null || other.Version != Version || other._script.Length != _script.Length)
            {
                return false;
            }

            for (var i = 0; i < _script.Length; i++)
            {
                if (_script[i] != other._script[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as ScriptPubKey);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Version.GetHashCode();
                for (var i = 0; i < _script.Length; i++)
                {
                    hash = (hash * 31) + _script[i];
                }

                return hash;
            }
        }
    }
}
