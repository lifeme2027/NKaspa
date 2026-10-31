using System.Text.Json;
using System.Text.Json.Serialization;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// A BIP39 mnemonic as its canonical phrase.
    /// Refused unless the options were built with <c>allowPrivateKeys: true</c>.
    /// The phrase is secret. See <see cref="NKaspaJsonSerializer.PrivateKeyWarning"/>.
    /// <c>JsonSerializer.Serialize</c> uses this converter with private keys refused,
    /// because <see cref="Mnemonic"/> carries <see cref="JsonConverterAttribute"/>.
    /// </summary>
    public sealed class MnemonicJsonConverter : JsonConverter<Mnemonic>
    {
        private readonly bool _allowPrivateKeys;

        /// <summary>Creates a converter that refuses to read or write a mnemonic.</summary>
        public MnemonicJsonConverter()
            : this(false)
        {
        }

        /// <summary>Creates a converter. The default registration passes false.</summary>
        public MnemonicJsonConverter(bool allowPrivateKeys)
        {
            _allowPrivateKeys = allowPrivateKeys;
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.FormatException">
        /// BIP39 checksum does not match. Word count should be 12, 15, 18, 21, or 24. Unknown BIP39 word.
        /// Mnemonic must be a string.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">Refusing to convert Mnemonic. {NKaspaJsonSerializer.PrivateKeyWarning}.</exception>
        public override Mnemonic Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Refuse();
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new FormatException("Mnemonic must be a string.");
            }

            return Mnemonic.ParseValidated(reader.GetString() ?? string.Empty);
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">The assigned value is null.</exception>
        /// <exception cref="System.InvalidOperationException">Refusing to convert Mnemonic. {NKaspaJsonSerializer.PrivateKeyWarning}.</exception>
        public override void Write(Utf8JsonWriter writer, Mnemonic value, JsonSerializerOptions options)
        {
            Refuse();
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStringValue(value.ToPhrase());
        }

        private void Refuse()
        {
            if (!_allowPrivateKeys)
            {
                throw new InvalidOperationException("Refusing to convert Mnemonic. " + NKaspaJsonSerializer.PrivateKeyWarning);
            }
        }
    }
}
