using System.Text.Json;
using System.Text.Json.Serialization;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// A Kaspa address string, including its network prefix.
    /// The prefix must be the network passed to <see cref="JsonSerializerOptionsExtensions.AddNKaspaConverters(JsonSerializerOptions, Network)"/>.
    /// </summary>
    public sealed class KaspaAddressJsonConverter : JsonConverterFactory
    {
        private readonly Network _network;

        /// <summary>Creates a converter that requires <paramref name="network"/>.</summary>
        public KaspaAddressJsonConverter(Network network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert != null && typeof(KaspaAddress).IsAssignableFrom(typeToConvert);
        }

        /// <inheritdoc />
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeof(KaspaAddressConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType, _network)!;
        }
    }

    /// <summary>Reads and writes one address type. The prefix must match the converter network.</summary>
    public sealed class KaspaAddressConverter<TAddress> : JsonConverter<TAddress>
        where TAddress : KaspaAddress
    {
        private readonly Network _network;

        /// <summary>Creates a converter that requires <paramref name="network"/>.</summary>
        public KaspaAddressConverter(Network network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        /// <inheritdoc />
        public override TAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new FormatException("Kaspa address must be a JSON string.");
            }

            var address = KaspaAddress.Parse(reader.GetString() ?? string.Empty, _network);
            if (address is not TAddress typed)
            {
                throw new FormatException("JSON address is " + address.GetType().Name + ", not " + typeof(TAddress).Name + ".");
            }

            return typed;
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, TAddress value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Network != _network)
            {
                throw new FormatException("Address network is " + value.Network.Name + ", not " + _network.Name + ".");
            }

            writer.WriteStringValue(value.ToString());
        }
    }
}
