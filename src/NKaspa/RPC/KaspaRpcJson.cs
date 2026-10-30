using System.Globalization;
using System.Text.Json;
using NKaspa.DataEncoders;

namespace NKaspa.RPC
{
    /// <summary>
    /// Converts a version-0 <see cref="Transaction"/> to and from wRPC JSON.
    /// Output amounts are written as <c>value</c>. <c>amount</c> is accepted when reading.
    /// Script bytes are written as <c>script</c>. <c>scriptPublicKey</c> is accepted when reading.
    /// <c>storageMass</c> and the deprecated <c>mass</c> field are both written, and they are equal.
    /// </summary>
    public static class KaspaRpcJson
    {
        /// <summary>Serializes <paramref name="transaction"/> as one JSON object.</summary>
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
                var input = transaction.Inputs[i];
                writer.WriteStartObject();
                writer.WritePropertyName("previousOutpoint");
                writer.WriteStartObject();
                writer.WriteString("transactionId", input.PreviousOutpoint.TransactionId.ToString());
                writer.WriteNumber("index", input.PreviousOutpoint.Index);
                writer.WriteEndObject();
                writer.WriteString("signatureScript", Encoders.Hex.EncodeData(input.SignatureScript.ToBytes()));
                writer.WriteNumber("sequence", input.Sequence);
                writer.WriteNumber("sigOpCount", input.SigOpCount);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("outputs");
            writer.WriteStartArray();
            for (var i = 0; i < transaction.Outputs.Count; i++)
            {
                var output = transaction.Outputs[i];
                writer.WriteStartObject();
                writer.WriteNumber("value", output.Value.Sompi);
                WriteScript(writer, "scriptPublicKey", output.ScriptPublicKey);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteNumber("lockTime", transaction.LockTime);
            writer.WriteString("subnetworkId", Encoders.Hex.EncodeData(transaction.SubnetworkId.ToBytes()));
            writer.WriteNumber("gas", transaction.Gas);
            writer.WriteString("payload", Encoders.Hex.EncodeData(transaction.Payload));
            writer.WriteNumber("mass", transaction.StorageMass);
            writer.WriteNumber("storageMass", transaction.StorageMass);
            writer.WriteEndObject();
        }

        /// <summary>Reads one transaction object.</summary>
        public static Transaction ReadTransaction(JsonElement element)
        {
            var version = (ushort)ReadU64(Require(element, "version"));
            var inputs = new List<TxIn>();
            foreach (var input in Require(element, "inputs").EnumerateArray())
            {
                var outpoint = Require(input, "previousOutpoint");
                var id = uint256.Parse(ReadString(outpoint, "transactionId"));
                var index = ReadU32(Require(outpoint, "index"));
                var signature = ReadHex(input, "signatureScript");
                var sequence = input.TryGetProperty("sequence", out var sequenceElement) ? ReadU64(sequenceElement) : 0UL;
                var sigOp = input.TryGetProperty("sigOpCount", out var sigOpElement) ? (byte)ReadU64(sigOpElement) : (byte)0;
                inputs.Add(new TxIn(new OutPoint(id, index), new Script(signature), sequence, sigOp));
            }

            var outputs = new List<TxOut>();
            foreach (var output in Require(element, "outputs").EnumerateArray())
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

                outputs.Add(new TxOut(new Money(value), ReadScript(Require(output, "scriptPublicKey"))));
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
            writer.WriteStartObject();
            writer.WriteNumber("version", script.Version);
            writer.WriteString("script", Encoders.Hex.EncodeData(script.Script));
            writer.WriteEndObject();
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
