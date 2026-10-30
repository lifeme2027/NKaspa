using System.Text.Json;
using System.Text.Json.Serialization;
using KaspaPskt = NKaspa.PSKT.PSKT;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// A PSKT as the compact JSON object from <see cref="KaspaPskt.ToJson"/>.
    /// That object follows rusty-kaspa serde field order. The <c>PSKT</c> hex transport is <see cref="KaspaPskt.ToHex"/>.
    /// </summary>
    public sealed class PSKTJsonConverter : JsonConverter<KaspaPskt>
    {
        /// <inheritdoc />
        public override KaspaPskt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, element =>
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException("PSKT JSON must be an object.");
                }

                return KaspaPskt.FromJson(element.GetRawText());
            });
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, KaspaPskt value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteRawValue(value.ToJson());
        }
    }
}
