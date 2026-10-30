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
        public ExtPubKeyJsonConverter(Network network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        /// <inheritdoc />
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
