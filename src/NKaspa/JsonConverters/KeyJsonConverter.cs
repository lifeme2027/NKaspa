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
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.FormatException">Private key must be a hex string. Hex string contains a non-hex character. Hex string length must be even.</exception>
        /// <exception cref="System.InvalidOperationException">Refusing to convert Key. {NKaspaJsonSerializer.PrivateKeyWarning}.</exception>
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
        /// <exception cref="System.ArgumentNullException">The assigned value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.InvalidOperationException">Refusing to convert Key. {NKaspaJsonSerializer.PrivateKeyWarning}.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
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
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        public ExtKeyJsonConverter(Network network, bool allowPrivateKeys)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _allowPrivateKeys = allowPrivateKeys;
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">Private key is zero or not in the secp256k1 range. Private key must be 32 bytes. An HD fingerprint is 4 bytes.</exception>
        /// <exception cref="System.FormatException">
        /// Extended private key prefix does not match the {_network.Name} network. Extended private key must be
        /// a Base58Check string. Extended private key is not in the secp256k1 range. Extended private key
        /// prefix byte is not 0x00. A master extended key must have a zero parent fingerprint and a zero child
        /// index. Extended key version is not a private prefix, or it does not match the key type. An extended
        /// key payload is 78 bytes. Invalid Base58Check checksum. Base58Check payload is shorter than the
        /// checksum. Base58 value does not fit in the decode buffer. Invalid Base58 character.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">Refusing to convert ExtKey. {NKaspaJsonSerializer.PrivateKeyWarning}.</exception>
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
        /// <exception cref="System.ArgumentNullException">The assigned value is null.</exception>
        /// <exception cref="System.InvalidOperationException">Refusing to convert ExtKey. {NKaspaJsonSerializer.PrivateKeyWarning}.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
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
