using System.Text.Json;
using System.Text.Json.Serialization;
using NKaspa.RPC;

namespace NKaspa.JsonConverters
{
    /// <summary>Script bytes as lowercase hex, the same form as <c>signatureScript</c>. The script version is not included.</summary>
    public sealed class ScriptJsonConverter : JsonConverter<Script>
    {
        /// <inheritdoc />
        public override Script Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new FormatException("Script must be a hex string.");
            }

            return Script.Parse(reader.GetString() ?? string.Empty);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Script value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStringValue(value.ToHex());
        }
    }

    /// <summary>
    /// A versioned script public key.
    /// Writing uses <c>{ "version", "script" }</c>, the same object as <see cref="KaspaRpcJson"/>.
    /// Reading also accepts <c>scriptPublicKey</c> on that object, and the rusty-kaspa hex string
    /// whose first two bytes are the big-endian version.
    /// </summary>
    public sealed class ScriptPubKeyJsonConverter : JsonConverter<ScriptPubKey>
    {
        /// <inheritdoc />
        public override ScriptPubKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, KaspaRpcJson.ReadScript);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, ScriptPubKey value, JsonSerializerOptions options)
        {
            KaspaRpcJson.WriteScriptPublicKey(writer, value);
        }
    }
}
