using System.Text.Json;
using System.Text.Json.Serialization;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// A fee rate as a whole number of sompi per gram.
    /// A fractional rate cannot be represented by <see cref="FeeRate"/> and is rejected.
    /// </summary>
    public sealed class FeeRateJsonConverter : JsonConverter<FeeRate>
    {
        /// <inheritdoc />
        /// <exception cref="System.FormatException">{name} must be a whole unsigned 64-bit integer.</exception>
        public override FeeRate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new FeeRate(MoneyJsonConverter.ReadSompi(ref reader, "Fee rate"));
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, FeeRate value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.SompiPerGram);
        }
    }
}
