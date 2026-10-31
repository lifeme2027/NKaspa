using System.Text.Json;
using System.Text.Json.Serialization;
using NKaspa.RPC;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// A network name: <c>mainnet</c>, <c>testnet</c>, <c>simnet</c>, or <c>devnet</c>.
    /// Reading also accepts <c>testnet-10</c> and the wRPC object <c>{ networkType, suffix }</c>.
    /// </summary>
    public sealed class NetworkJsonConverter : JsonConverter<Network>
    {
        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.FormatException">Network must be a string or an object. Unknown Kaspa network '{name ?? string.Empty}'. networkId is not a string or an object.</exception>
        public override Network Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return Parse(reader.GetString());
            }

            return JsonValueReader.Read(ref reader, element =>
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException("Network must be a string or an object.");
                }

                return Parse(KaspaRpcJson.ReadNetworkId(element));
            });
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">The assigned value is null.</exception>
        public override void Write(Utf8JsonWriter writer, Network value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStringValue(value.Name);
        }

        private static Network Parse(string? name)
        {
            if (!Network.TryGetNetwork(name, out var network) || network == null)
            {
                throw new FormatException("Unknown Kaspa network '" + (name ?? string.Empty) + "'.");
            }

            return network;
        }
    }
}
