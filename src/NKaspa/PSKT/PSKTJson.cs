using System.Text;
using System.Text.Json;
using NBitcoin.Secp256k1;
using NKaspa.Crypto;
using NKaspa.DataEncoders;

namespace NKaspa.PSKT
{
    /// <summary>
    /// Compact JSON matching serde_json on the rusty-kaspa PSKT structs.
    /// Hex transport is <c>PSKT</c> plus lowercase hex of the UTF-8 JSON.
    /// A bundle is <c>PSKB</c> plus hex of a JSON array.
    /// </summary>
    internal static class PSKTJson
    {
        internal const string PsktPrefix = "PSKT";
        internal const string PskbPrefix = "PSKB";

        private static readonly string[] GlobalFields =
        {
            "version", "txVersion", "fallbackLockTime", "inputsModifiable", "outputsModifiable",
            "inputCount", "outputCount", "xpubs", "id", "proprietaries", "payload",
        };

        private static readonly string[] InputFields =
        {
            "utxoEntry", "previousOutpoint", "sequence", "minTime", "partialSigs", "sighashType",
            "redeemScript", "sigOpCount", "bip32Derivations", "finalScriptSig", "proprietaries",
        };

        private static readonly string[] OutputFields =
        {
            "amount", "scriptPublicKey", "redeemScript", "bip32Derivations", "covenant", "proprietaries",
        };

        internal static string Write(PSKTGlobal global, IReadOnlyList<PSKTInput> inputs, IReadOnlyList<PSKTOutput> outputs)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("global");
                WriteGlobal(writer, global);
                writer.WritePropertyName("inputs");
                writer.WriteStartArray();
                for (var i = 0; i < inputs.Count; i++)
                {
                    WriteInput(writer, inputs[i]);
                }

                writer.WriteEndArray();
                writer.WritePropertyName("outputs");
                writer.WriteStartArray();
                for (var i = 0; i < outputs.Count; i++)
                {
                    WriteOutput(writer, outputs[i]);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        internal static string WriteBundle(IReadOnlyList<PSKT> items)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();
                for (var i = 0; i < items.Count; i++)
                {
                    WriteInner(writer, items[i]);
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        internal static void ReadInto(string json, PSKTGlobal global, List<PSKTInput> inputs, List<PSKTOutput> outputs)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("PSKT JSON must be an object.");
            }

            var globalElement = Require(root, "global");
            ReadGlobal(globalElement, global);
            inputs.Clear();
            outputs.Clear();
            foreach (var item in Require(root, "inputs").EnumerateArray())
            {
                inputs.Add(ReadInput(item));
            }

            foreach (var item in Require(root, "outputs").EnumerateArray())
            {
                outputs.Add(ReadOutput(item));
            }
        }

        internal static List<string> ReadBundleArray(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("PSKB JSON must be an array.");
            }

            var items = new List<string>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                items.Add(item.GetRawText());
            }

            return items;
        }

        internal static string ToTransport(string prefix, string json)
        {
            return prefix + Encoders.Hex.EncodeData(Encoding.UTF8.GetBytes(json));
        }

        internal static string FromTransport(string text, string prefix, string error)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (!text.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new FormatException(error);
            }

            var bytes = Encoders.Hex.DecodeData(text.Substring(prefix.Length));
            return Encoding.UTF8.GetString(bytes);
        }

        private static void WriteInner(Utf8JsonWriter writer, PSKT pskt)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("global");
            WriteGlobal(writer, pskt.Global);
            writer.WritePropertyName("inputs");
            writer.WriteStartArray();
            for (var i = 0; i < pskt.Inputs.Count; i++)
            {
                WriteInput(writer, pskt.Inputs[i]);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("outputs");
            writer.WriteStartArray();
            for (var i = 0; i < pskt.Outputs.Count; i++)
            {
                WriteOutput(writer, pskt.Outputs[i]);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteGlobal(Utf8JsonWriter writer, PSKTGlobal global)
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", (byte)global.Version);
            writer.WriteNumber("txVersion", global.TxVersion);
            WriteU64(writer, "fallbackLockTime", global.FallbackLockTime);
            writer.WriteBoolean("inputsModifiable", global.InputsModifiable);
            writer.WriteBoolean("outputsModifiable", global.OutputsModifiable);
            writer.WriteNumber("inputCount", global.InputCount);
            writer.WriteNumber("outputCount", global.OutputCount);
            writer.WritePropertyName("xpubs");
            writer.WriteStartObject();
            var xpubs = global.Xpubs.ToList();
            xpubs.Sort((a, b) => string.CompareOrdinal(a.ToXpub(), b.ToXpub()));
            for (var i = 0; i < xpubs.Count; i++)
            {
                writer.WritePropertyName(xpubs[i].ToXpub());
                WriteKeySource(writer, xpubs[i].Source);
            }

            writer.WriteEndObject();
            if (global.Id == null)
            {
                writer.WriteNull("id");
            }
            else
            {
                writer.WriteString("id", global.Id.ToString());
            }

            WriteRawMap(writer, "proprietaries", global.Proprietaries);
            WriteRawMap(writer, null, global.Unknowns);
            WriteHex(writer, "payload", global.Payload);
            writer.WriteEndObject();
        }

        private static void WriteInput(Utf8JsonWriter writer, PSKTInput input)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("utxoEntry");
            if (input.Utxo == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                WriteUtxo(writer, input.Utxo);
            }

            writer.WritePropertyName("previousOutpoint");
            writer.WriteStartObject();
            writer.WriteString("transactionId", input.PreviousOutpoint.TransactionId.ToString());
            writer.WriteNumber("index", input.PreviousOutpoint.Index);
            writer.WriteEndObject();
            WriteU64(writer, "sequence", input.Sequence);
            WriteU64(writer, "minTime", input.MinTime);
            writer.WritePropertyName("partialSigs");
            WriteSignatures(writer, input.PartialSignatures);
            writer.WriteNumber("sighashType", (byte)input.SighashType);
            WriteHex(writer, "redeemScript", input.RedeemScript?.ToBytes());
            if (input.SigOpCount.HasValue)
            {
                writer.WriteNumber("sigOpCount", input.SigOpCount.Value);
            }
            else
            {
                writer.WriteNull("sigOpCount");
            }

            writer.WritePropertyName("bip32Derivations");
            WriteDerivations(writer, input.Derivations);
            WriteHex(writer, "finalScriptSig", input.FinalScriptSig?.ToBytes());
            WriteRawMap(writer, "proprietaries", input.Proprietaries);
            WriteRawMap(writer, null, input.Unknowns);
            writer.WriteEndObject();
        }

        private static void WriteOutput(Utf8JsonWriter writer, PSKTOutput output)
        {
            writer.WriteStartObject();
            writer.WriteNumber("amount", output.Amount);
            writer.WriteString("scriptPublicKey", FormatScript(output.ScriptPublicKey));
            WriteHex(writer, "redeemScript", output.RedeemScript?.ToBytes());
            writer.WritePropertyName("bip32Derivations");
            WriteDerivations(writer, output.Derivations);
            writer.WritePropertyName("covenant");
            if (output.Covenant == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteNumber("authorizingInput", output.Covenant.AuthorizingInput);
                writer.WriteString("covenantId", output.Covenant.CovenantId.ToString());
                writer.WriteEndObject();
            }

            WriteRawMap(writer, "proprietaries", output.Proprietaries);
            WriteRawMap(writer, null, output.Unknowns);
            writer.WriteEndObject();
        }

        private static void WriteUtxo(Utf8JsonWriter writer, UtxoEntry utxo)
        {
            writer.WriteStartObject();
            writer.WriteNumber("amount", utxo.Amount.Sompi);
            writer.WriteString("scriptPublicKey", FormatScript(utxo.ScriptPublicKey));
            writer.WriteNumber("blockDaaScore", utxo.BlockDaaScore);
            writer.WriteBoolean("isCoinbase", utxo.IsCoinbase);
            if (utxo.CovenantId == null)
            {
                writer.WriteNull("covenantId");
            }
            else
            {
                writer.WriteString("covenantId", utxo.CovenantId.ToString());
            }

            writer.WriteEndObject();
        }

        private static void WriteSignatures(Utf8JsonWriter writer, List<PSKTSignature> signatures)
        {
            writer.WriteStartObject();
            var ordered = signatures.ToList();
            ordered.Sort((a, b) => CompareKeys(a.PublicKey, b.PublicKey));
            for (var i = 0; i < ordered.Count; i++)
            {
                var signature = ordered[i];
                writer.WritePropertyName(signature.PublicKey.ToString());
                writer.WriteStartObject();
                if (signature.IsSchnorr)
                {
                    writer.WriteString("schnorr", Encoders.Hex.EncodeData(signature.ToCompact()));
                }
                else
                {
                    writer.WriteString("eCDSA", new ECDSASignature(signature.ToCompact()).ToDerHex());
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void WriteDerivations(Utf8JsonWriter writer, List<PSKTDerivation> derivations)
        {
            writer.WriteStartObject();
            var ordered = derivations.ToList();
            ordered.Sort((a, b) => CompareKeys(a.PublicKey, b.PublicKey));
            for (var i = 0; i < ordered.Count; i++)
            {
                writer.WritePropertyName(ordered[i].PublicKey.ToString());
                var source = ordered[i].Source;
                if (source == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    WriteKeySource(writer, source);
                }
            }

            writer.WriteEndObject();
        }

        private static void WriteKeySource(Utf8JsonWriter writer, PSKTKeySource source)
        {
            writer.WriteStartObject();
            writer.WriteString("keyFingerprint", source.Fingerprint.ToString());
            writer.WriteString("derivationPath", source.Path.ToString());
            writer.WriteEndObject();
        }

        private static void WriteRawMap(Utf8JsonWriter writer, string? name, Dictionary<string, string> map)
        {
            if (name != null)
            {
                writer.WritePropertyName(name);
                writer.WriteStartObject();
            }

            var keys = map.Keys.ToList();
            keys.Sort(StringComparer.Ordinal);
            for (var i = 0; i < keys.Count; i++)
            {
                if (name == null)
                {
                    writer.WritePropertyName(keys[i]);
                    writer.WriteRawValue(map[keys[i]]);
                }
                else
                {
                    writer.WritePropertyName(keys[i]);
                    writer.WriteRawValue(map[keys[i]]);
                }
            }

            if (name != null)
            {
                writer.WriteEndObject();
            }
        }

        private static void WriteU64(Utf8JsonWriter writer, string name, ulong? value)
        {
            if (value.HasValue)
            {
                writer.WriteNumber(name, value.Value);
            }
            else
            {
                writer.WriteNull(name);
            }
        }

        private static void WriteHex(Utf8JsonWriter writer, string name, byte[]? value)
        {
            if (value == null)
            {
                writer.WriteNull(name);
            }
            else
            {
                writer.WriteString(name, Encoders.Hex.EncodeData(value));
            }
        }

        private static void ReadGlobal(JsonElement element, PSKTGlobal global)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("PSKT global must be an object.");
            }

            global.Version = (PSKTVersion)ReadByte(Require(element, "version"));
            global.TxVersion = (ushort)ReadU64(Require(element, "txVersion"));
            global.FallbackLockTime = ReadU64Optional(Require(element, "fallbackLockTime"));
            global.InputsModifiable = Require(element, "inputsModifiable").GetBoolean();
            global.OutputsModifiable = Require(element, "outputsModifiable").GetBoolean();
            global.InputCount = checked((int)ReadU64(Require(element, "inputCount")));
            global.OutputCount = checked((int)ReadU64(Require(element, "outputCount")));
            global.Xpubs.Clear();
            foreach (var pair in Require(element, "xpubs").EnumerateObject())
            {
                global.Xpubs.Add(new PSKTXpub(ExtPubKey.Parse(pair.Name), ReadKeySource(pair.Value)));
            }

            var id = Require(element, "id");
            global.Id = id.ValueKind == JsonValueKind.Null ? null : uint256.Parse(id.GetString() ?? string.Empty);
            ReadRawMap(Require(element, "proprietaries"), global.Proprietaries);
            global.Payload = ReadHex(Require(element, "payload"));
            global.Unknowns.Clear();
            ReadUnknowns(element, GlobalFields, global.Unknowns);
        }

        private static PSKTInput ReadInput(JsonElement element)
        {
            var outpoint = Require(element, "previousOutpoint");
            var input = new PSKTInput(new OutPoint(
                uint256.Parse(Require(outpoint, "transactionId").GetString() ?? string.Empty),
                (uint)ReadU64(Require(outpoint, "index"))));
            var utxo = Require(element, "utxoEntry");
            input.Utxo = utxo.ValueKind == JsonValueKind.Null ? null : ReadUtxo(utxo);
            input.Sequence = ReadU64Optional(Require(element, "sequence"));
            input.MinTime = ReadU64Optional(Require(element, "minTime"));
            ReadSignatures(Require(element, "partialSigs"), input.PartialSignatures);
            var sighash = ReadByte(Require(element, "sighashType"));
            var hashType = (SigHashType)sighash;
            if (!hashType.IsDefined())
            {
                throw new FormatException("Sighash type is not one of the six Kaspa values.");
            }

            input.SighashType = hashType;
            input.RedeemScript = ReadScript(Require(element, "redeemScript"));
            var sigops = Require(element, "sigOpCount");
            input.SigOpCount = sigops.ValueKind == JsonValueKind.Null ? null : ReadByte(sigops);
            ReadDerivations(Require(element, "bip32Derivations"), input.Derivations);
            input.FinalScriptSig = ReadScript(Require(element, "finalScriptSig"));
            ReadRawMap(Require(element, "proprietaries"), input.Proprietaries);
            ReadUnknowns(element, InputFields, input.Unknowns);
            return input;
        }

        private static PSKTOutput ReadOutput(JsonElement element)
        {
            var output = new PSKTOutput(ReadU64(Require(element, "amount")), ReadScriptPublicKey(Require(element, "scriptPublicKey")));
            output.RedeemScript = ReadScript(Require(element, "redeemScript"));
            ReadDerivations(Require(element, "bip32Derivations"), output.Derivations);
            var covenant = Require(element, "covenant");
            if (covenant.ValueKind != JsonValueKind.Null)
            {
                output.Covenant = new CovenantBinding(
                    (ushort)ReadU64(Require(covenant, "authorizingInput")),
                    uint256.Parse(Require(covenant, "covenantId").GetString() ?? string.Empty));
            }

            ReadRawMap(Require(element, "proprietaries"), output.Proprietaries);
            ReadUnknowns(element, OutputFields, output.Unknowns);
            return output;
        }

        private static UtxoEntry ReadUtxo(JsonElement element)
        {
            var covenant = element.TryGetProperty("covenantId", out var covenantElement) && covenantElement.ValueKind != JsonValueKind.Null
                ? uint256.Parse(covenantElement.GetString() ?? string.Empty)
                : null;
            return new UtxoEntry(
                ReadU64(Require(element, "amount")),
                ReadScriptPublicKey(Require(element, "scriptPublicKey")),
                ReadU64(Require(element, "blockDaaScore")),
                Require(element, "isCoinbase").GetBoolean(),
                covenant);
        }

        private static void ReadSignatures(JsonElement element, List<PSKTSignature> signatures)
        {
            foreach (var pair in element.EnumerateObject())
            {
                var key = new PubKeyECDSA(DecodeHex(pair.Name));
                string? schnorr = null;
                string? ecdsa = null;
                foreach (var field in pair.Value.EnumerateObject())
                {
                    if (field.Name == "schnorr")
                    {
                        schnorr = field.Value.GetString();
                    }
                    else if (field.Name == "eCDSA")
                    {
                        ecdsa = field.Value.GetString();
                    }
                    else
                    {
                        throw new FormatException("Partial signature must be schnorr or eCDSA.");
                    }
                }

                if (schnorr != null)
                {
                    signatures.Add(PSKTSignature.Schnorr(key, SchnorrSignature.Parse(schnorr)));
                }
                else if (ecdsa != null)
                {
                    signatures.Add(PSKTSignature.Ecdsa(key, ECDSASignature.FromDer(DecodeHex(ecdsa))));
                }
                else
                {
                    throw new FormatException("Partial signature is empty.");
                }
            }
        }

        private static void ReadDerivations(JsonElement element, List<PSKTDerivation> derivations)
        {
            foreach (var pair in element.EnumerateObject())
            {
                var key = new PubKeyECDSA(DecodeHex(pair.Name));
                PSKTKeySource? source = pair.Value.ValueKind == JsonValueKind.Null ? null : ReadKeySource(pair.Value);
                derivations.Add(new PSKTDerivation(key, source));
            }
        }

        private static PSKTKeySource ReadKeySource(JsonElement element)
        {
            var fingerprint = new HDFingerprint(DecodeHex(Require(element, "keyFingerprint").GetString() ?? string.Empty));
            var path = new KeyPath(Require(element, "derivationPath").GetString() ?? string.Empty);
            return new PSKTKeySource(fingerprint, path);
        }

        private static ScriptPubKey ReadScriptPublicKey(JsonElement element)
        {
            var text = element.GetString() ?? string.Empty;
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(2);
            }

            var bytes = DecodeHex(text);
            if (bytes.Length < 2)
            {
                throw new FormatException("Script public key hex must start with a 2-byte version.");
            }

            var version = (ushort)((bytes[0] << 8) | bytes[1]);
            var script = new byte[bytes.Length - 2];
            Buffer.BlockCopy(bytes, 2, script, 0, script.Length);
            return new ScriptPubKey(version, script);
        }

        private static string FormatScript(ScriptPubKey script)
        {
            var body = script.Script;
            var bytes = new byte[2 + body.Length];
            bytes[0] = (byte)(script.Version >> 8);
            bytes[1] = (byte)script.Version;
            Buffer.BlockCopy(body, 0, bytes, 2, body.Length);
            return Encoders.Hex.EncodeData(bytes);
        }

        private static Script? ReadScript(JsonElement element)
        {
            var bytes = ReadHex(element);
            return bytes == null ? null : new Script(bytes);
        }

        private static byte[]? ReadHex(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return DecodeHex(element.GetString() ?? string.Empty);
        }

        private static byte[] DecodeHex(string text)
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(2);
            }

            return Encoders.Hex.DecodeData(text);
        }

        private static void ReadRawMap(JsonElement element, Dictionary<string, string> map)
        {
            map.Clear();
            foreach (var pair in element.EnumerateObject())
            {
                map[pair.Name] = pair.Value.GetRawText();
            }
        }

        private static void ReadUnknowns(JsonElement element, string[] known, Dictionary<string, string> map)
        {
            foreach (var pair in element.EnumerateObject())
            {
                var found = false;
                for (var i = 0; i < known.Length; i++)
                {
                    if (known[i] == pair.Name)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    map[pair.Name] = pair.Value.GetRawText();
                }
            }
        }

        private static JsonElement Require(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                throw new FormatException("Missing JSON property " + name + ".");
            }

            return value;
        }

        private static ulong ReadU64(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetUInt64(out var value))
            {
                return value;
            }

            throw new FormatException("Expected an unsigned integer.");
        }

        private static ulong? ReadU64Optional(JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Null ? null : ReadU64(element);
        }

        private static byte ReadByte(JsonElement element)
        {
            var value = ReadU64(element);
            if (value > byte.MaxValue)
            {
                throw new FormatException("Expected a byte.");
            }

            return (byte)value;
        }

        internal static int CompareKeys(PubKeyECDSA left, PubKeyECDSA right)
        {
            var leftKey = OrderKey(left);
            var rightKey = OrderKey(right);
            var length = Math.Min(leftKey.Length, rightKey.Length);
            for (var i = 0; i < length; i++)
            {
                var compare = leftKey[i].CompareTo(rightKey[i]);
                if (compare != 0)
                {
                    return compare;
                }
            }

            return leftKey.Length.CompareTo(rightKey.Length);
        }

        private static byte[] OrderKey(PubKeyECDSA key)
        {
            if (!ECPubKey.TryCreate(key.ToBytes(), null, out _, out var parsed) || parsed == null)
            {
                return key.ToBytes();
            }

            var uncompressed = parsed.ToBytes(false);
            if (uncompressed.Length == 65 && uncompressed[0] == 0x04)
            {
                var body = new byte[64];
                Buffer.BlockCopy(uncompressed, 1, body, 0, 64);
                return body;
            }

            return uncompressed;
        }
    }
}
