using System.Text.Json;
using System.Text.Json.Serialization;
using NKaspa.RPC;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// An outpoint as <c>{ "transactionId", "index" }</c>.
    /// A <c>transactionId:index</c> string is accepted when reading.
    /// </summary>
    public sealed class OutPointJsonConverter : JsonConverter<OutPoint>
    {
        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        public override OutPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, KaspaRpcJson.ReadOutPoint);
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">The assigned value is null. <paramref name="writer"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public override void Write(Utf8JsonWriter writer, OutPoint value, JsonSerializerOptions options)
        {
            KaspaRpcJson.WriteOutPoint(writer, value);
        }
    }
}
