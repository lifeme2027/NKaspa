using System.Text.Json;
using System.Text.Json.Serialization;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// Kaspa counterpart of <c>NBitcoin.JsonConverters.Serializer</c>.
    /// Serialization uses <see cref="System.Text.Json.JsonSerializer"/>. Newtonsoft.Json is not a dependency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Field names inside Kaspa values are camelCase. Amounts are sompi integers, not KAS decimals.
    /// A <see cref="Transaction"/> is the same compact string as <c>KaspaRpcJson.WriteTransaction</c>.
    /// Pass the network that addresses and extended keys must belong to.
    /// </para>
    /// <para>
    /// <see cref="Key"/> and <see cref="ExtKey"/> are refused unless the caller passes true for the private-key argument of
    /// <see cref="Serialize{T}(T, Network, bool, bool)"/> or
    /// <see cref="JsonSerializerOptionsExtensions.AddNKaspaConverters(JsonSerializerOptions, Network, bool)"/>.
    /// That argument writes secret bytes. <see cref="PrivateKeyWarning"/> is the warning those methods document.
    /// </para>
    /// </remarks>
    public static class NKaspaJsonSerializer
    {
        /// <summary>
        /// WARNING: serializing a private key writes secret material into JSON.
        /// Leave private-key serialization off unless that output is protected.
        /// </summary>
        public const string PrivateKeyWarning = "WARNING: serializing a private key writes secret material into JSON. Leave allowPrivateKeys false unless that output is protected.";

        /// <summary>Compact JSON for <paramref name="value"/> on <paramref name="network"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.Exception">The operation failed.</exception>
        /// <exception cref="System.Text.Json.JsonException">The operation failed.</exception>
        public static string Serialize<T>(T value, Network network)
        {
            return Serialize(value, network, writeIndented: false, allowPrivateKeys: false);
        }

        /// <summary>JSON for <paramref name="value"/>. Indentation does not change the tokens.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.Exception">The operation failed.</exception>
        /// <exception cref="System.Text.Json.JsonException">The operation failed.</exception>
        public static string Serialize<T>(T value, Network network, bool writeIndented)
        {
            return Serialize(value, network, writeIndented, allowPrivateKeys: false);
        }

        /// <summary>
        /// JSON for <paramref name="value"/>.
        /// <paramref name="allowPrivateKeys"/> must stay false unless the caller accepts <see cref="PrivateKeyWarning"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.Exception">The operation failed.</exception>
        /// <exception cref="System.Text.Json.JsonException">The operation failed.</exception>
        public static string Serialize<T>(T value, Network network, bool writeIndented, bool allowPrivateKeys)
        {
            return Serialize(value, CreateOptions(network, writeIndented, allowPrivateKeys));
        }

        /// <summary>
        /// JSON using options that already contain the Kaspa converters.
        /// A converter <see cref="FormatException"/>, <see cref="InvalidOperationException"/>, or <see cref="ArgumentException"/>
        /// is thrown as itself.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="System.Exception">The operation failed.</exception>
        /// <exception cref="System.Text.Json.JsonException">The operation failed.</exception>
        public static string Serialize<T>(T value, JsonSerializerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            try
            {
                return JsonSerializer.Serialize(value, options);
            }
            catch (JsonException exception)
            {
                if (exception.InnerException is Exception inner && inner is FormatException or InvalidOperationException or ArgumentException)
                {
                    throw inner;
                }

                throw;
            }
        }

        /// <summary>Reads <paramref name="json"/> with the Kaspa converters for <paramref name="network"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="json"/> is null. <paramref name="network"/> is null.</exception>
        /// <exception cref="System.FormatException">JSON value is null.</exception>
        /// <exception cref="System.Exception">The operation failed.</exception>
        /// <exception cref="System.Text.Json.JsonException">The operation failed.</exception>
        public static T Deserialize<T>(string json, Network network)
        {
            return Deserialize<T>(json, network, allowPrivateKeys: false);
        }

        /// <summary>
        /// Reads <paramref name="json"/>.
        /// <paramref name="allowPrivateKeys"/> must stay false unless the caller accepts <see cref="PrivateKeyWarning"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="json"/> is null. <paramref name="network"/> is null.</exception>
        /// <exception cref="System.FormatException">JSON value is null.</exception>
        /// <exception cref="System.Exception">The operation failed.</exception>
        /// <exception cref="System.Text.Json.JsonException">The operation failed.</exception>
        public static T Deserialize<T>(string json, Network network, bool allowPrivateKeys)
        {
            return Deserialize<T>(json, CreateOptions(network, writeIndented: false, allowPrivateKeys));
        }

        /// <summary>
        /// Reads <paramref name="json"/> with options from <see cref="JsonSerializerOptionsExtensions.AddNKaspaConverters(JsonSerializerOptions, Network)"/>.
        /// A converter <see cref="FormatException"/>, <see cref="InvalidOperationException"/>, or <see cref="ArgumentException"/>
        /// is thrown as itself. Other JSON syntax errors remain <see cref="JsonException"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="options"/> is null. <paramref name="json"/> is null.</exception>
        /// <exception cref="System.FormatException">JSON value is null.</exception>
        /// <exception cref="System.Exception">The operation failed.</exception>
        /// <exception cref="System.Text.Json.JsonException">The operation failed.</exception>
        public static T Deserialize<T>(string json, JsonSerializerOptions options)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            try
            {
                var value = JsonSerializer.Deserialize<T>(json, options);
                if (value is null)
                {
                    throw new FormatException("JSON value is null.");
                }

                return value;
            }
            catch (JsonException exception)
            {
                if (exception.InnerException is Exception inner && inner is FormatException or InvalidOperationException or ArgumentException)
                {
                    throw inner;
                }

                throw;
            }
        }

        /// <summary>Options with the Kaspa converters, camelCase names, and private keys refused.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        public static JsonSerializerOptions CreateOptions(Network network)
        {
            return CreateOptions(network, writeIndented: false, allowPrivateKeys: false);
        }

        /// <summary>Options with the Kaspa converters. See <see cref="PrivateKeyWarning"/> before passing true.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        public static JsonSerializerOptions CreateOptions(Network network, bool writeIndented, bool allowPrivateKeys)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = writeIndented,
            };
            options.AddNKaspaConverters(network, allowPrivateKeys);
            return options;
        }
    }

    /// <summary>Registers Kaspa converters on <see cref="JsonSerializerOptions"/>.</summary>
    public static class JsonSerializerOptionsExtensions
    {
        /// <summary>
        /// Adds converters for Kaspa amounts, hashes, addresses, scripts, transactions, UTXOs, public keys, fee rates, and PSKT.
        /// <paramref name="network"/> is required when an address or extended key is read.
        /// Private keys are refused. Call this before the options are used. A second call registers a second set.
        /// When <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> is null, it becomes camelCase.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null. <paramref name="options"/> is null.</exception>
        public static JsonSerializerOptions AddNKaspaConverters(this JsonSerializerOptions options, Network network)
        {
            return AddNKaspaConverters(options, network, allowPrivateKeys: false);
        }

        /// <summary>
        /// Adds the Kaspa converters.
        /// <paramref name="allowPrivateKeys"/> writes <see cref="Key"/> and <see cref="ExtKey"/> secrets.
        /// <see cref="NKaspaJsonSerializer.PrivateKeyWarning"/> applies when it is true.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null. <paramref name="options"/> is null.</exception>
        public static JsonSerializerOptions AddNKaspaConverters(this JsonSerializerOptions options, Network network, bool allowPrivateKeys)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            if (options.PropertyNamingPolicy == null)
            {
                options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            }

            options.Converters.Add(new MoneyJsonConverter());
            options.Converters.Add(new UInt256JsonConverter());
            options.Converters.Add(new KaspaAddressJsonConverter(network));
            options.Converters.Add(new NetworkJsonConverter());
            options.Converters.Add(new ScriptJsonConverter());
            options.Converters.Add(new ScriptPubKeyJsonConverter());
            options.Converters.Add(new OutPointJsonConverter());
            options.Converters.Add(new TxInJsonConverter());
            options.Converters.Add(new TxOutJsonConverter());
            options.Converters.Add(new TransactionJsonConverter());
            options.Converters.Add(new CovenantBindingJsonConverter());
            options.Converters.Add(new UtxoEntryJsonConverter());
            options.Converters.Add(new CoinJsonConverter());
            options.Converters.Add(new PubKeyJsonConverter());
            options.Converters.Add(new PubKeyEcdsaJsonConverter());
            options.Converters.Add(new ExtPubKeyJsonConverter(network));
            options.Converters.Add(new FeeRateJsonConverter());
            options.Converters.Add(new PSKTJsonConverter());
            options.Converters.Add(new KeyJsonConverter(allowPrivateKeys));
            options.Converters.Add(new ExtKeyJsonConverter(network, allowPrivateKeys));
            return options;
        }
    }
}
