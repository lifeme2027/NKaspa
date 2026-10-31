using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// Sompi as an unsigned JSON integer. A numeric string is accepted.
    /// KAS decimals such as <c>1.5 KAS</c> are rejected. This is not NBitcoin's money unit string.
    /// </summary>
    public sealed class MoneyJsonConverter : JsonConverter<Money>
    {
        /// <inheritdoc />
        /// <exception cref="System.FormatException">{name} must be a whole unsigned 64-bit integer.</exception>
        public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new Money(ReadSompi(ref reader, "Money"));
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.Sompi);
        }

        internal static ulong ReadSompi(ref Utf8JsonReader reader, string name)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetUInt64(out var number))
                {
                    return number;
                }

                throw new FormatException(name + " must be a whole unsigned 64-bit integer.");
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                if (text != null && ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            throw new FormatException(name + " must be a whole unsigned 64-bit integer.");
        }
    }
}
