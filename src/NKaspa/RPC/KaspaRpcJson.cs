using System.Globalization;
using System.Text.Json;
using NKaspa.DataEncoders;

namespace NKaspa.RPC
{
    /// <summary>
    /// Converts Kaspa values to and from wRPC JSON.
    /// <see cref="NKaspa.JsonConverters.NKaspaJsonSerializer"/> writes transactions through these methods,
    /// so the compact JSON is the same string.
    /// Every input writes <c>sigOpCount</c>. Version 1 also writes <c>computeBudget</c>.
    /// rusty-kaspa 2.1.0 requires <c>sig_op_count</c> even when the budget field is present.
    /// Output amounts are written as <c>value</c>. <c>amount</c> is accepted when reading.
    /// Script bytes are written as an object field <c>script</c>. Reading also accepts
    /// <c>scriptPublicKey</c> on that object, and the human-readable rusty-kaspa string:
    /// four hex digits of the big-endian version followed by the script hex.
    /// <c>storageMass</c> and the deprecated <c>mass</c> field are both written, and they are equal.
    /// <c>verboseData</c> is written as null on the transaction, each input, and each output.
    /// An output without a covenant writes <c>covenant</c> as null. rusty-kaspa's serde types
    /// require those fields; omitting them makes <c>submitTransaction</c> fail.
    /// </summary>
    public static class KaspaRpcJson
    {
        /// <summary>Serializes <paramref name="transaction"/> as one JSON object.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public static string WriteTransaction(Transaction transaction)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteTransaction(writer, transaction);
            }

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>Parses one transaction object.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="json"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Subnetwork id must be 20 bytes.</exception>
        /// <exception cref="System.FormatException">
        /// mass and storageMass do not match. Expected an unsigned 64-bit integer. JSON number does not fit in
        /// an unsigned 64-bit integer. Expected hex string. Hex string contains a non-hex character. Hex string
        /// length must be even. Transaction output is missing value. authorizingInput does not fit in an
        /// unsigned 16-bit integer. uint256 hex must be 32 bytes. uint256 requires 32 bytes. Missing JSON
        /// string {name}. Missing JSON property {name}. Script public key is missing script bytes. Script
        /// public key is not an object or a hex string. Script public key hex must start with a 2-byte version.
        /// computeBudget does not fit in an unsigned 16-bit integer. sigOpCount does not fit in a byte. Script
        /// push is too long. Further failures of this type use the same exception.
        /// </exception>
        public static Transaction ReadTransaction(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            using var document = JsonDocument.Parse(json);
            return ReadTransaction(document.RootElement);
        }

        /// <summary>Writes one transaction object.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="transaction"/> is null. <paramref name="writer"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public static void WriteTransaction(Utf8JsonWriter writer, Transaction transaction)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            writer.WriteStartObject();
            writer.WriteNumber("version", transaction.Version);
            writer.WritePropertyName("inputs");
            writer.WriteStartArray();
            for (var i = 0; i < transaction.Inputs.Count; i++)
            {
                WriteInput(writer, transaction.Inputs[i], transaction.Version >= 1);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("outputs");
            writer.WriteStartArray();
            for (var i = 0; i < transaction.Outputs.Count; i++)
            {
                WriteOutput(writer, transaction.Outputs[i]);
            }

            writer.WriteEndArray();
            writer.WriteNumber("lockTime", transaction.LockTime);
            writer.WriteString("subnetworkId", Encoders.Hex.EncodeData(transaction.SubnetworkId.ToBytes()));
            writer.WriteNumber("gas", transaction.Gas);
            writer.WriteString("payload", Encoders.Hex.EncodeData(transaction.Payload));
            writer.WriteNumber("mass", transaction.StorageMass);
            writer.WriteNumber("storageMass", transaction.StorageMass);
            writer.WriteNull("verboseData");
            writer.WriteEndObject();
        }

        /// <summary>Reads one transaction object.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">Subnetwork id must be 20 bytes.</exception>
        /// <exception cref="System.FormatException">
        /// mass and storageMass do not match. Expected an unsigned 64-bit integer. JSON number does not fit in
        /// an unsigned 64-bit integer. Expected hex string. Hex string contains a non-hex character. Hex string
        /// length must be even. Transaction output is missing value. authorizingInput does not fit in an
        /// unsigned 16-bit integer. uint256 hex must be 32 bytes. uint256 requires 32 bytes. Missing JSON
        /// string {name}. Missing JSON property {name}. Script public key is missing script bytes. Script
        /// public key is not an object or a hex string. Script public key hex must start with a 2-byte version.
        /// computeBudget does not fit in an unsigned 16-bit integer. sigOpCount does not fit in a byte. Script
        /// push is too long. Further failures of this type use the same exception.
        /// </exception>
        public static Transaction ReadTransaction(JsonElement element)
        {
            var version = (ushort)ReadU64(Require(element, "version"));
            var inputs = new List<TxIn>();
            foreach (var input in Require(element, "inputs").EnumerateArray())
            {
                inputs.Add(ReadInput(input, version >= 1));
            }

            var outputs = new List<TxOut>();
            foreach (var output in Require(element, "outputs").EnumerateArray())
            {
                outputs.Add(ReadOutput(output));
            }

            var lockTime = element.TryGetProperty("lockTime", out var lockElement) ? ReadU64(lockElement) : 0UL;
            var subnetwork = element.TryGetProperty("subnetworkId", out var subElement)
                ? new SubnetworkId(ReadHex(subElement))
                : SubnetworkId.Native;
            var gas = element.TryGetProperty("gas", out var gasElement) ? ReadU64(gasElement) : 0UL;
            var payload = element.TryGetProperty("payload", out var payloadElement) ? ReadHex(payloadElement) : Array.Empty<byte>();
            var transaction = new Transaction(version, inputs, outputs, lockTime, subnetwork, gas, payload);
            var hasMass = element.TryGetProperty("mass", out var massElement);
            var hasStorage = element.TryGetProperty("storageMass", out var storageElement);
            if (hasMass && hasStorage && ReadU64(massElement) != ReadU64(storageElement))
            {
                throw new FormatException("mass and storageMass do not match.");
            }

            if (hasStorage)
            {
                transaction.StorageMass = ReadU64(storageElement);
            }
            else if (hasMass)
            {
                transaction.StorageMass = ReadU64(massElement);
            }

            return transaction;
        }

        /// <summary>Writes <c>{ transactionId, index }</c>.</summary>
        internal static void WriteOutPoint(Utf8JsonWriter writer, OutPoint outpoint)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (outpoint == null)
            {
                throw new ArgumentNullException(nameof(outpoint));
            }

            writer.WriteStartObject();
            writer.WriteString("transactionId", outpoint.TransactionId.ToString());
            writer.WriteNumber("index", outpoint.Index);
            writer.WriteEndObject();
        }

        /// <summary>Reads an outpoint object, or <c>transactionId:index</c>.</summary>
        internal static OutPoint ReadOutPoint(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString() ?? string.Empty;
                var colon = text.LastIndexOf(':');
                if (colon <= 0 || colon == text.Length - 1)
                {
                    throw new FormatException("Outpoint string must be transactionId:index.");
                }

                if (!uint.TryParse(text.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    throw new FormatException("Outpoint index is not an unsigned 32-bit integer.");
                }

                return new OutPoint(uint256.Parse(text.Substring(0, colon)), index);
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Outpoint is not an object.");
            }

            return new OutPoint(uint256.Parse(ReadString(element, "transactionId")), ReadU32(Require(element, "index")));
        }

        /// <summary>
        /// Writes one input. <paramref name="writeComputeBudget"/> adds <c>computeBudget</c>
        /// (0 when the input has no budget). Version-0 transactions pass false.
        /// </summary>
        internal static void WriteInput(Utf8JsonWriter writer, TxIn input, bool writeComputeBudget)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            writer.WriteStartObject();
            writer.WritePropertyName("previousOutpoint");
            WriteOutPoint(writer, input.PreviousOutpoint);
            writer.WriteString("signatureScript", Encoders.Hex.EncodeData(input.SignatureScript.ToBytes()));
            writer.WriteNumber("sequence", input.Sequence);
            writer.WriteNumber("sigOpCount", input.SigOpCount);
            if (writeComputeBudget)
            {
                writer.WriteNumber("computeBudget", input.ComputeBudget ?? 0);
            }

            writer.WriteNull("verboseData");
            writer.WriteEndObject();
        }

        /// <summary>Reads one input. A null <c>sigOpCount</c> or <c>computeBudget</c> is absent.</summary>
        internal static TxIn ReadInput(JsonElement input, bool readComputeBudget)
        {
            var txIn = new TxIn(ReadOutPoint(Require(input, "previousOutpoint")), new Script(ReadHex(input, "signatureScript")), OptionalU64(input, "sequence"), ReadSigOpCount(input));
            if (readComputeBudget && input.TryGetProperty("computeBudget", out var budgetElement) && budgetElement.ValueKind != JsonValueKind.Null)
            {
                var budget = ReadU64(budgetElement);
                if (budget > ushort.MaxValue)
                {
                    throw new FormatException("computeBudget does not fit in an unsigned 16-bit integer.");
                }

                txIn.ComputeBudget = (ushort)budget;
            }

            return txIn;
        }

        /// <summary>Writes one output. <c>value</c> is sompi. A covenant is omitted when null.</summary>
        internal static void WriteOutput(Utf8JsonWriter writer, TxOut output)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            writer.WriteStartObject();
            writer.WriteNumber("value", output.Value.Sompi);
            WriteScript(writer, "scriptPublicKey", output.ScriptPublicKey);
            if (output.Covenant != null)
            {
                writer.WritePropertyName("covenant");
                WriteCovenant(writer, output.Covenant);
            }
            else
            {
                writer.WriteNull("covenant");
            }

            writer.WriteNull("verboseData");
            writer.WriteEndObject();
        }

        /// <summary>Reads one output. <c>amount</c> is accepted when <c>value</c> is absent.</summary>
        internal static TxOut ReadOutput(JsonElement output)
        {
            ulong value;
            if (output.TryGetProperty("value", out var valueElement))
            {
                value = ReadU64(valueElement);
            }
            else if (output.TryGetProperty("amount", out var amountElement))
            {
                value = ReadU64(amountElement);
            }
            else
            {
                throw new FormatException("Transaction output is missing value.");
            }

            var txOut = new TxOut(new Money(value), ReadScript(Require(output, "scriptPublicKey")));
            if (output.TryGetProperty("covenant", out var covenantElement) && covenantElement.ValueKind == JsonValueKind.Object)
            {
                txOut.Covenant = ReadCovenant(covenantElement);
            }

            return txOut;
        }

        /// <summary>Writes <c>{ authorizingInput, covenantId }</c>.</summary>
        internal static void WriteCovenant(Utf8JsonWriter writer, CovenantBinding covenant)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (covenant == null)
            {
                throw new ArgumentNullException(nameof(covenant));
            }

            writer.WriteStartObject();
            writer.WriteNumber("authorizingInput", covenant.AuthorizingInput);
            writer.WriteString("covenantId", covenant.CovenantId.ToString());
            writer.WriteEndObject();
        }

        /// <summary>Reads a covenant object.</summary>
        internal static CovenantBinding ReadCovenant(JsonElement element)
        {
            var authorizing = ReadU64(Require(element, "authorizingInput"));
            if (authorizing > ushort.MaxValue)
            {
                throw new FormatException("authorizingInput does not fit in an unsigned 16-bit integer.");
            }

            return new CovenantBinding((ushort)authorizing, uint256.Parse(ReadString(element, "covenantId")));
        }

        /// <summary>
        /// Writes a UTXO entry. <c>amount</c> is sompi.
        /// <c>covenantId</c> is omitted when the entry has none. A null in JSON is accepted on read.
        /// </summary>
        internal static void WriteUtxoEntry(Utf8JsonWriter writer, UtxoEntry entry)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            writer.WriteStartObject();
            writer.WriteNumber("amount", entry.Amount.Sompi);
            WriteScript(writer, "scriptPublicKey", entry.ScriptPublicKey);
            writer.WriteNumber("blockDaaScore", entry.BlockDaaScore);
            writer.WriteBoolean("isCoinbase", entry.IsCoinbase);
            if (entry.CovenantId != null)
            {
                writer.WriteString("covenantId", entry.CovenantId.ToString());
            }

            writer.WriteEndObject();
        }

        /// <summary>Reads a UTXO entry. <c>scriptPublicKey</c> may be an object or version-prefixed hex.</summary>
        internal static UtxoEntry ReadUtxoEntry(JsonElement element)
        {
            var amount = ReadU64(Require(element, "amount"));
            var script = ReadScript(Require(element, "scriptPublicKey"));
            var daa = OptionalU64(element, "blockDaaScore");
            var coinbase = false;
            if (element.TryGetProperty("isCoinbase", out var coinbaseElement) && coinbaseElement.ValueKind != JsonValueKind.Null)
            {
                if (coinbaseElement.ValueKind == JsonValueKind.True)
                {
                    coinbase = true;
                }
                else if (coinbaseElement.ValueKind == JsonValueKind.False)
                {
                    coinbase = false;
                }
                else
                {
                    throw new FormatException("Expected a boolean for isCoinbase.");
                }
            }

            uint256? covenantId = null;
            if (element.TryGetProperty("covenantId", out var covenantElement) && covenantElement.ValueKind == JsonValueKind.String)
            {
                var text = covenantElement.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    covenantId = uint256.Parse(text);
                }
            }

            return new UtxoEntry(amount, script, daa, coinbase, covenantId);
        }

        /// <summary>Writes <c>{ outpoint, utxoEntry }</c>, the getUtxosByAddresses entry without the address.</summary>
        internal static void WriteCoin(Utf8JsonWriter writer, Coin coin)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (coin == null)
            {
                throw new ArgumentNullException(nameof(coin));
            }

            writer.WriteStartObject();
            writer.WritePropertyName("outpoint");
            WriteOutPoint(writer, coin.Outpoint);
            writer.WritePropertyName("utxoEntry");
            WriteUtxoEntry(writer, coin.Utxo);
            writer.WriteEndObject();
        }

        /// <summary>Reads a coin. <c>entry</c> is accepted when <c>utxoEntry</c> is absent. An address property is ignored.</summary>
        internal static Coin ReadCoin(JsonElement element)
        {
            if (!element.TryGetProperty("utxoEntry", out var entryElement) && !element.TryGetProperty("entry", out entryElement))
            {
                throw new FormatException("Coin JSON is missing utxoEntry.");
            }

            return new Coin(ReadOutPoint(Require(element, "outpoint")), ReadUtxoEntry(entryElement));
        }

        /// <summary>Writes <c>{ version, script }</c> with no property name.</summary>
        internal static void WriteScriptPublicKey(Utf8JsonWriter writer, ScriptPubKey script)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (script == null)
            {
                throw new ArgumentNullException(nameof(script));
            }

            writer.WriteStartObject();
            writer.WriteNumber("version", script.Version);
            writer.WriteString("script", Encoders.Hex.EncodeData(script.Script));
            writer.WriteEndObject();
        }

        internal static ulong ReadU64(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetUInt64(out var number))
                {
                    return number;
                }

                throw new FormatException("JSON number does not fit in an unsigned 64-bit integer.");
            }

            if (element.ValueKind == JsonValueKind.String
                && ulong.TryParse(element.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new FormatException("Expected an unsigned 64-bit integer.");
        }

        internal static string ReadNetworkId(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? string.Empty;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                var type = element.TryGetProperty("networkType", out var typeElement) ? typeElement.GetString() ?? string.Empty : string.Empty;
                if (element.TryGetProperty("suffix", out var suffix) && suffix.ValueKind == JsonValueKind.Number)
                {
                    return type + "-" + suffix.GetUInt32().ToString(CultureInfo.InvariantCulture);
                }

                return type;
            }

            throw new FormatException("networkId is not a string or an object.");
        }

        internal static List<uint256> ReadHashes(JsonElement element)
        {
            var hashes = new List<uint256>();
            foreach (var item in element.EnumerateArray())
            {
                hashes.Add(uint256.Parse(item.GetString() ?? throw new FormatException("Hash is missing.")));
            }

            return hashes;
        }

        internal static ScriptPubKey ReadScript(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return ReadVersionPrefixedScript(element.GetString() ?? string.Empty);
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Script public key is not an object or a hex string.");
            }

            var version = element.TryGetProperty("version", out var versionElement) ? (ushort)ReadU64(versionElement) : (ushort)0;
            byte[] script;
            if (element.TryGetProperty("script", out var scriptElement))
            {
                script = ReadHex(scriptElement);
            }
            else if (element.TryGetProperty("scriptPublicKey", out var alias))
            {
                script = ReadHex(alias);
            }
            else
            {
                throw new FormatException("Script public key is missing script bytes.");
            }

            return new ScriptPubKey(version, script);
        }

        /// <summary>
        /// Human-readable form from rusty-kaspa <c>ScriptPublicKey</c>:
        /// big-endian u16 version, then the script bytes, as one hex string.
        /// </summary>
        private static ScriptPubKey ReadVersionPrefixedScript(string text)
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(2);
            }

            if (text.Length < 4 || (text.Length & 1) != 0)
            {
                throw new FormatException("Script public key hex must start with a 2-byte version.");
            }

            var bytes = Encoders.Hex.DecodeData(text);
            var version = (ushort)((bytes[0] << 8) | bytes[1]);
            var script = new byte[bytes.Length - 2];
            Buffer.BlockCopy(bytes, 2, script, 0, script.Length);
            return new ScriptPubKey(version, script);
        }

        internal static RpcFeeBucket? ReadBucket(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                return element.GetArrayLength() == 0 ? null : ReadBucket(element[0]);
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!element.TryGetProperty("feerate", out var rate) && !element.TryGetProperty("feeRate", out rate))
            {
                return null;
            }

            var seconds = 0d;
            if (element.TryGetProperty("estimatedSeconds", out var secondsElement) || element.TryGetProperty("estimated_seconds", out secondsElement))
            {
                seconds = secondsElement.GetDouble();
            }

            return new RpcFeeBucket(rate.GetDouble(), seconds);
        }

        internal static List<RpcFeeBucket> ReadBuckets(JsonElement element)
        {
            var buckets = new List<RpcFeeBucket>();
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var bucket = ReadBucket(item);
                    if (bucket != null)
                    {
                        buckets.Add(bucket);
                    }
                }
            }
            else
            {
                var bucket = ReadBucket(element);
                if (bucket != null)
                {
                    buckets.Add(bucket);
                }
            }

            return buckets;
        }

        private static void WriteScript(Utf8JsonWriter writer, string name, ScriptPubKey script)
        {
            writer.WritePropertyName(name);
            WriteScriptPublicKey(writer, script);
        }

        private static ulong OptionalU64(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return 0UL;
            }

            return ReadU64(value);
        }

        private static byte ReadSigOpCount(JsonElement input)
        {
            if (!input.TryGetProperty("sigOpCount", out var sigOpElement) || sigOpElement.ValueKind == JsonValueKind.Null)
            {
                return 0;
            }

            var value = ReadU64(sigOpElement);
            if (value > byte.MaxValue)
            {
                throw new FormatException("sigOpCount does not fit in a byte.");
            }

            return (byte)value;
        }

        private static JsonElement Require(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                throw new FormatException("Missing JSON property " + name + ".");
            }

            return value;
        }

        private static string ReadString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException("Missing JSON string " + name + ".");
            }

            return value.GetString() ?? string.Empty;
        }

        private static uint ReadU32(JsonElement element)
        {
            var value = ReadU64(element);
            if (value > uint.MaxValue)
            {
                throw new FormatException("JSON number does not fit in an unsigned 32-bit integer.");
            }

            return (uint)value;
        }

        private static byte[] ReadHex(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var element))
            {
                return Array.Empty<byte>();
            }

            return ReadHex(element);
        }

        private static byte[] ReadHex(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw new FormatException("Expected hex string.");
            }

            var text = element.GetString() ?? string.Empty;
            if (text.Length == 0)
            {
                return Array.Empty<byte>();
            }

            return Encoders.Hex.DecodeData(text);
        }
    }
}
