using System.Text.Json;
using System.Text.Json.Serialization;
using NKaspa.DataEncoders;

namespace NKaspa.JsonConverters
{
    /// <summary>A 32-byte Schnorr x-only public key as lowercase hex.</summary>
    public sealed class PubKeyJsonConverter : JsonConverter<PubKey>
    {
        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">Schnorr x-only public key is not on the curve. Schnorr x-only public key must be 32 bytes.</exception>
        /// <exception cref="System.FormatException">Hex string contains a non-hex character. Hex string length must be even. {name} must be a hex string.</exception>
        public override PubKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                return PubKey.Parse(PublicKeyHex.RequireHex(ref reader, "Public key"));
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">The assigned value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public override void Write(Utf8JsonWriter writer, PubKey value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStringValue(value.ToString());
        }
    }

    /// <summary>A 33-byte compressed ECDSA public key as lowercase hex.</summary>
    public sealed class PubKeyEcdsaJsonConverter : JsonConverter<PubKeyECDSA>
    {
        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">ECDSA public key is not on the curve. ECDSA public key must be compressed (0x02 or 0x03). ECDSA public key must be 33 bytes.</exception>
        /// <exception cref="System.FormatException">Hex string contains a non-hex character. Hex string length must be even. {name} must be a hex string.</exception>
        public override PubKeyECDSA Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                return PubKeyECDSA.Parse(PublicKeyHex.RequireHex(ref reader, "ECDSA public key"));
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">The assigned value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public override void Write(Utf8JsonWriter writer, PubKeyECDSA value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStringValue(Encoders.Hex.EncodeData(value.ToBytes()));
        }
    }

    internal static class PublicKeyHex
    {
        internal static string RequireHex(ref Utf8JsonReader reader, string name)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new FormatException(name + " must be a hex string.");
            }

            return reader.GetString() ?? string.Empty;
        }
    }
}
