using System.Text.Json;
using System.Text.Json.Serialization;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// A <see cref="uint256"/> as 64 hex characters in Kaspa wire order.
    /// The bytes are not reversed the way <c>NBitcoin.uint256</c> reverses them for display.
    /// An optional <c>0x</c> prefix is accepted.
    /// </summary>
    public sealed class UInt256JsonConverter : JsonConverter<uint256>
    {
        /// <inheritdoc />
        public override uint256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new FormatException("uint256 must be a hex string.");
            }

            return uint256.Parse(reader.GetString() ?? string.Empty);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, uint256 value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStringValue(value.ToString());
        }
    }
}
