using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// Address version byte stored in front of the payload before cashaddr encoding.
    /// </summary>
    public enum KaspaAddressVersion : byte
    {
        /// <summary>Schnorr x-only public key, 32 bytes. Opcode-level version 0x00.</summary>
        PubKey = 0x00,

        /// <summary>Compressed ECDSA public key, 33 bytes. Version 0x01.</summary>
        PubKeyECDSA = 0x01,

        /// <summary>BLAKE2b-256 script hash, 32 bytes. Version 0x08.</summary>
        ScriptHash = 0x08,
    }

    /// <summary>
    /// Kaspa counterpart of NBitcoin.BitcoinAddress.
    /// A cashaddr string <c>{prefix}:{payload}</c> with an 8-character checksum.
    /// </summary>
    public abstract class KaspaAddress : IEquatable<KaspaAddress>
    {
        private readonly string _encoded;
        private readonly byte[] _payload;
        private ScriptPubKey? _scriptPubKey;

        /// <summary>Called by the concrete address types after the payload length has been checked.</summary>
        protected KaspaAddress(Network network, KaspaAddressVersion version, byte[] payload, string encoded)
        {
            Network = network ?? throw new ArgumentNullException(nameof(network));
            Version = version;
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            _encoded = encoded ?? throw new ArgumentNullException(nameof(encoded));
        }

        /// <summary>Network named by the address prefix.</summary>
        public Network Network { get; }

        /// <summary>Version byte.</summary>
        public KaspaAddressVersion Version { get; }

        /// <summary>A copy of the payload. Schnorr and script hash are 32 bytes. ECDSA is 33 bytes.</summary>
        public byte[] Payload
        {
            get { return (byte[])_payload.Clone(); }
        }

        /// <summary>
        /// Script that locks an output to this address.
        /// Kaspa counterpart of NBitcoin.BitcoinAddress.ScriptPubKey.
        /// </summary>
        public ScriptPubKey ScriptPubKey
        {
            get { return _scriptPubKey ??= GeneratePaymentScript(); }
        }

        /// <summary>Builds the script public key for this address version.</summary>
        protected abstract ScriptPubKey GeneratePaymentScript();

        /// <summary>The canonical lowercase address string.</summary>
        public override string ToString()
        {
            return _encoded;
        }

        /// <summary>
        /// Parses an address and selects the network from its prefix.
        /// Kaspa counterpart of NBitcoin.BitcoinAddress.Create.
        /// </summary>
        public static KaspaAddress Parse(string address)
        {
            if (!TryParse(address, null, out var parsed, out var error) || parsed == null)
            {
                throw new FormatException(error);
            }

            return parsed;
        }

        /// <summary>Parses an address and requires <paramref name="expectedNetwork"/>.</summary>
        public static KaspaAddress Parse(string address, Network expectedNetwork)
        {
            if (expectedNetwork == null)
            {
                throw new ArgumentNullException(nameof(expectedNetwork));
            }

            if (!TryParse(address, expectedNetwork, out var parsed, out var error) || parsed == null)
            {
                throw new FormatException(error);
            }

            return parsed;
        }

        /// <summary>Parses without throwing. A null or invalid string returns false.</summary>
        public static bool TryParse(string? address, out KaspaAddress? parsed)
        {
            return TryParse(address, null, out parsed, out _);
        }

        /// <summary>Parses without throwing and requires <paramref name="expectedNetwork"/>.</summary>
        public static bool TryParse(string? address, Network expectedNetwork, out KaspaAddress? parsed)
        {
            if (expectedNetwork == null)
            {
                throw new ArgumentNullException(nameof(expectedNetwork));
            }

            return TryParse(address, expectedNetwork, out parsed, out _);
        }

        /// <summary>True when <see cref="TryParse(string?, out KaspaAddress?)"/> succeeds.</summary>
        public static bool IsValid(string? address)
        {
            return TryParse(address, out _);
        }

        /// <inheritdoc />
        public bool Equals(KaspaAddress? other)
        {
            return other is not null && _encoded == other._encoded;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as KaspaAddress);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _encoded.GetHashCode();
        }

        public static bool operator ==(KaspaAddress? left, KaspaAddress? right)
        {
            if (left is null)
            {
                return right is null;
            }

            return left.Equals(right);
        }

        public static bool operator !=(KaspaAddress? left, KaspaAddress? right)
        {
            return !(left == right);
        }

        /// <summary>Payload bytes stored by a derived address. The array must not be published.</summary>
        protected byte[] PayloadCore
        {
            get { return _payload; }
        }

        private static bool TryParse(
            string? address,
            Network? expectedNetwork,
            out KaspaAddress? parsed,
            out string error)
        {
            parsed = null;
            error = string.Empty;
            if (address == null)
            {
                error = "Kaspa address is null.";
                return false;
            }

            if (!KaspaBech32Encoder.TryDecode(address, out var prefix, out var versionByte, out var payload, out error))
            {
                return false;
            }

            if (!Network.TryFromAddressPrefix(prefix, out var network) || network == null)
            {
                error = "Unknown Kaspa address prefix '" + prefix + "'.";
                return false;
            }

            if (expectedNetwork != null && !expectedNetwork.Equals(network))
            {
                error = "Kaspa address is for " + network.Name + ", not " + expectedNetwork.Name + ".";
                return false;
            }

            if (!IsStandard(versionByte, payload.Length))
            {
                error = "Kaspa address version or payload length is not Schnorr (0, 32 bytes), ECDSA (1, 33 bytes), or P2SH (8, 32 bytes).";
                return false;
            }

            var canonical = network.AddressPrefix + ":" + address.Trim().Substring(address.Trim().IndexOf(':') + 1);
            var version = (KaspaAddressVersion)versionByte;
            parsed = version switch
            {
                KaspaAddressVersion.PubKey => new KaspaPubKeyAddress(network, payload, canonical),
                KaspaAddressVersion.PubKeyECDSA => new KaspaPubKeyEcdsaAddress(network, payload, canonical),
                KaspaAddressVersion.ScriptHash => new KaspaScriptHashAddress(network, payload, canonical),
                _ => null,
            };
            return parsed != null;
        }

        private static bool IsStandard(byte version, int payloadLength)
        {
            return version switch
            {
                (byte)KaspaAddressVersion.PubKey => payloadLength == 32,
                (byte)KaspaAddressVersion.PubKeyECDSA => payloadLength == 33,
                (byte)KaspaAddressVersion.ScriptHash => payloadLength == 32,
                _ => false,
            };
        }

        /// <summary>Encodes a checked payload with the network prefix.</summary>
        protected static string Encode(Network network, KaspaAddressVersion version, byte[] payload)
        {
            return new KaspaBech32Encoder(network.AddressPrefix).Encode((byte)version, payload);
        }
    }

    /// <summary>
    /// Schnorr pay-to-pubkey address, version 0x00 and a 32-byte x-only key.
    /// Kaspa counterpart of NBitcoin.BitcoinWitPubKeyAddress for the role of the
    /// default public-key address. Kaspa has no SegWit witness program. The payload
    /// is the key itself, not a HASH160.
    /// </summary>
    public sealed class KaspaPubKeyAddress : KaspaAddress
    {
        /// <summary>Builds an address from a 32-byte x-only key. The key is not checked against the curve in this phase.</summary>
        public KaspaPubKeyAddress(byte[] xOnlyPublicKey, Network network)
            : this(
                network ?? throw new ArgumentNullException(nameof(network)),
                CopyKey(xOnlyPublicKey, 32, "Schnorr x-only public key"),
                null)
        {
        }

        internal KaspaPubKeyAddress(Network network, byte[] payload, string? encoded)
            : base(
                network,
                KaspaAddressVersion.PubKey,
                payload,
                encoded ?? Encode(network, KaspaAddressVersion.PubKey, payload))
        {
        }

        /// <summary>The 32-byte x-only public key.</summary>
        public byte[] PubKey
        {
            get { return Payload; }
        }

        /// <inheritdoc />
        protected override ScriptPubKey GeneratePaymentScript()
        {
            return ScriptPubKey.PayToPubKey(PayloadCore);
        }

        private static byte[] CopyKey(byte[] key, int size, string name)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length != size)
            {
                throw new ArgumentException(name + " must be " + size + " bytes.", nameof(key));
            }

            return (byte[])key.Clone();
        }
    }

    /// <summary>
    /// ECDSA pay-to-pubkey address, version 0x01 and a 33-byte compressed key.
    /// Kaspa counterpart of NBitcoin.BitcoinPubKeyAddress for the ECDSA public-key role.
    /// The payload is the compressed key, not a HASH160.
    /// </summary>
    public sealed class KaspaPubKeyEcdsaAddress : KaspaAddress
    {
        /// <summary>Builds an address from a 33-byte compressed ECDSA key.</summary>
        public KaspaPubKeyEcdsaAddress(byte[] compressedPublicKey, Network network)
            : this(
                network ?? throw new ArgumentNullException(nameof(network)),
                CopyKey(compressedPublicKey),
                null)
        {
        }

        internal KaspaPubKeyEcdsaAddress(Network network, byte[] payload, string? encoded)
            : base(
                network,
                KaspaAddressVersion.PubKeyECDSA,
                payload,
                encoded ?? Encode(network, KaspaAddressVersion.PubKeyECDSA, payload))
        {
        }

        /// <summary>The 33-byte compressed public key.</summary>
        public byte[] PubKey
        {
            get { return Payload; }
        }

        /// <inheritdoc />
        protected override ScriptPubKey GeneratePaymentScript()
        {
            return ScriptPubKey.PayToPubKeyEcdsa(PayloadCore);
        }

        private static byte[] CopyKey(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length != 33)
            {
                throw new ArgumentException("ECDSA public key must be 33 bytes.", nameof(key));
            }

            return (byte[])key.Clone();
        }
    }

    /// <summary>
    /// Pay-to-script-hash address, version 0x08 and a 32-byte BLAKE2b-256 script hash.
    /// Kaspa counterpart of NBitcoin.BitcoinScriptAddress. The hash is BLAKE2b-256, not HASH160.
    /// </summary>
    public sealed class KaspaScriptHashAddress : KaspaAddress
    {
        /// <summary>Builds an address from a 32-byte script hash in Kaspa wire order.</summary>
        public KaspaScriptHashAddress(uint256 scriptHash, Network network)
            : this(
                network ?? throw new ArgumentNullException(nameof(network)),
                ScriptHashBytes(scriptHash),
                null)
        {
        }

        /// <summary>Builds an address from 32 script-hash bytes.</summary>
        public KaspaScriptHashAddress(byte[] scriptHash, Network network)
            : this(
                network ?? throw new ArgumentNullException(nameof(network)),
                CopyHash(scriptHash),
                null)
        {
        }

        internal KaspaScriptHashAddress(Network network, byte[] payload, string? encoded)
            : base(
                network,
                KaspaAddressVersion.ScriptHash,
                payload,
                encoded ?? Encode(network, KaspaAddressVersion.ScriptHash, payload))
        {
        }

        /// <summary>The 32-byte script hash.</summary>
        public uint256 Hash
        {
            get { return new uint256(PayloadCore); }
        }

        /// <inheritdoc />
        protected override ScriptPubKey GeneratePaymentScript()
        {
            return ScriptPubKey.PayToScriptHash(PayloadCore);
        }

        private static byte[] ScriptHashBytes(uint256 scriptHash)
        {
            if (scriptHash == null)
            {
                throw new ArgumentNullException(nameof(scriptHash));
            }

            return scriptHash.ToBytes();
        }

        private static byte[] CopyHash(byte[] hash)
        {
            if (hash == null)
            {
                throw new ArgumentNullException(nameof(hash));
            }

            if (hash.Length != 32)
            {
                throw new ArgumentException("Script hash must be 32 bytes.", nameof(hash));
            }

            return (byte[])hash.Clone();
        }
    }
}
