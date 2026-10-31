using System.Text.Json;
using System.Text.Json.Serialization;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// An extended public key as Base58Check, using the Kaspa public prefix of the converter network
    /// (<c>kpub</c> on mainnet, <c>ktub</c> on testnet, simnet, and devnet).
    /// A key whose version is not that prefix is rejected.
    /// </summary>
    public sealed class ExtPubKeyJsonConverter : JsonConverter<ExtPubKey>
    {
        private readonly Network _network;

        /// <summary>Creates a converter that writes and requires <paramref name="network"/>'s public prefix.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        public ExtPubKeyJsonConverter(Network network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// ECDSA public key is not on the curve. ECDSA public key must be compressed (0x02 or 0x03). ECDSA
        /// public key must be 33 bytes. An HD fingerprint is 4 bytes.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Extended public key prefix does not match the {_network.Name} network. Extended public key must be a
        /// Base58Check string. Extended public key is not on the curve. Extended public key must start with
        /// 0x02 or 0x03. A master extended key must have a zero parent fingerprint and a zero child index.
        /// Extended key version is not a public prefix, or it does not match the key type. An extended key
        /// payload is 78 bytes. Invalid Base58Check checksum. Base58Check payload is shorter than the checksum.
        /// Base58 value does not fit in the decode buffer. Invalid Base58 character.
        /// </exception>
        public override ExtPubKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new FormatException("Extended public key must be a Base58Check string.");
            }

            var key = ExtPubKey.Parse(reader.GetString() ?? string.Empty);
            if (key.Version != _network.ExtPubKeyVersion)
            {
                throw new FormatException("Extended public key prefix does not match the " + _network.Name + " network.");
            }

            return key;
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">The assigned value is null.</exception>
        public override void Write(Utf8JsonWriter writer, ExtPubKey value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStringValue(value.ToString(_network));
        }
    }
}
