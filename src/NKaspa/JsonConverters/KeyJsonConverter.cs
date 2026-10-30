using System.Text.Json;
using System.Text.Json.Serialization;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// A 32-byte private key as hex.
    /// Refused unless the options were built with <c>allowPrivateKeys: true</c>.
    /// See <see cref="NKaspaJsonSerializer.PrivateKeyWarning"/>.
    /// </summary>
    public sealed class KeyJsonConverter : JsonConverter<Key>
    {
        private readonly bool _allowPrivateKeys;

        /// <summary>Creates a converter. The default registration passes false.</summary>
        public KeyJsonConverter(bool allowPrivateKeys)
        {
            _allowPrivateKeys = allowPrivateKeys;
        }

        /// <inheritdoc />
        public override Key Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Refuse();
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new FormatException("Private key must be a hex string.");
            }

            try
            {
                return Key.Parse(reader.GetString() ?? string.Empty);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Key value, JsonSerializerOptions options)
        {
            Refuse();
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStringValue(value.ToHex());
        }

        private void Refuse()
        {
            if (!_allowPrivateKeys)
            {
                throw new InvalidOperationException("Refusing to convert Key. " + NKaspaJsonSerializer.PrivateKeyWarning);
            }
        }
    }

    /// <summary>
    /// An extended private key as Base58Check with the network's private prefix (<c>kprv</c> or <c>ktrv</c>).
    /// Refused unless <c>allowPrivateKeys</c> is true.
    /// See <see cref="NKaspaJsonSerializer.PrivateKeyWarning"/>.
    /// </summary>
    public sealed class ExtKeyJsonConverter : JsonConverter<ExtKey>
    {
        private readonly Network _network;
        private readonly bool _allowPrivateKeys;

        /// <summary>Creates a converter. The default registration passes false.</summary>
        public ExtKeyJsonConverter(Network network, bool allowPrivateKeys)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _allowPrivateKeys = allowPrivateKeys;
        }

        /// <inheritdoc />
        public override ExtKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Refuse();
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new FormatException("Extended private key must be a Base58Check string.");
            }

            var key = ExtKey.Parse(reader.GetString() ?? string.Empty);
            if (key.Version != _network.ExtPrivKeyVersion)
            {
                key.Dispose();
                throw new FormatException("Extended private key prefix does not match the " + _network.Name + " network.");
            }

            return key;
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, ExtKey value, JsonSerializerOptions options)
        {
            Refuse();
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStringValue(value.ToString(_network));
        }

        private void Refuse()
        {
            if (!_allowPrivateKeys)
            {
                throw new InvalidOperationException("Refusing to convert ExtKey. " + NKaspaJsonSerializer.PrivateKeyWarning);
            }
        }
    }
}
