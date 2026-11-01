using System.Text.Json;
using System.Text.Json.Serialization;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// Refuses to read or write a <see cref="KeystoreSecret"/>.
    /// <c>allowPrivateKeys</c> does not unlock this type. The encrypted keystore file is <see cref="WalletKeystore"/>, not this plaintext.
    /// Newtonsoft.Json is not a package reference. <see cref="KeystoreSecret.ShouldSerializeIsPhrase"/> is the hook its default contract resolver calls.
    /// </summary>
    public sealed class KeystoreSecretJsonConverter : JsonConverter<KeystoreSecret>
    {
        /// <summary>The message thrown for every read and write. It contains no secret.</summary>
        public const string Refusal = "Refusing to convert KeystoreSecret.";

        /// <inheritdoc />
        /// <exception cref="System.InvalidOperationException">Refusing to convert KeystoreSecret.</exception>
        public override KeystoreSecret Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new InvalidOperationException(Refusal);
        }

        /// <inheritdoc />
        /// <exception cref="System.InvalidOperationException">Refusing to convert KeystoreSecret.</exception>
        public override void Write(Utf8JsonWriter writer, KeystoreSecret value, JsonSerializerOptions options)
        {
            throw new InvalidOperationException(Refusal);
        }
    }
}
