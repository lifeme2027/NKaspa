using System.Text.Json;

namespace NKaspa.JsonConverters
{
    /// <summary>Reads one JSON value into a <see cref="JsonElement"/> for the wRPC helpers.</summary>
    internal static class JsonValueReader
    {
        internal static T Read<T>(ref Utf8JsonReader reader, Func<JsonElement, T> convert)
        {
            if (convert == null)
            {
                throw new ArgumentNullException(nameof(convert));
            }

            using var document = JsonDocument.ParseValue(ref reader);
            return convert(document.RootElement);
        }
    }
}
