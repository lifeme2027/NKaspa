using System.Text.Json;
using System.Text.Json.Serialization;
using KaspaPskt = NKaspa.PSKT.PSKT;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// A PSKT as the compact JSON object from <see cref="KaspaPskt.ToJson"/>.
    /// That object follows rusty-kaspa serde field order. The <c>PSKT</c> hex transport is <see cref="KaspaPskt.ToHex"/>.
    /// </summary>
    public sealed class PSKTJsonConverter : JsonConverter<KaspaPskt>
    {
        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Hardened index must be below 2^31.</exception>
        /// <exception cref="System.ArgumentException">
        /// An HD fingerprint is 4 bytes. ECDSA public key is not on the curve. ECDSA public key must be
        /// compressed (0x02 or 0x03). ECDSA public key must be 33 bytes. ECDSA signature must be 64 bytes.
        /// Schnorr signature must be 64 bytes.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// PSKT JSON must be an object. Missing JSON property {name}. uint256 hex must be 32 bytes. uint256
        /// requires 32 bytes. Hex string contains a non-hex character. Hex string length must be even. Expected
        /// an unsigned integer. Key path is empty. Key path index is not a number. Key path contains an empty
        /// index. Script push is too long. Script push is truncated. Script public key hex must start with a
        /// 2-byte version. Sighash type is not one of the six Kaspa values. Expected a byte. Partial signature
        /// is empty. Partial signature must be schnorr or eCDSA. ECDSA signature is not strict DER. Further
        /// failures of this type use the same exception.
        /// </exception>
        public override KaspaPskt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, element =>
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException("PSKT JSON must be an object.");
                }

                return KaspaPskt.FromJson(element.GetRawText());
            });
        }

        /// <inheritdoc />
        /// <exception cref="System.ArgumentNullException">The assigned value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">ECDSA signature must be 64 bytes.</exception>
        public override void Write(Utf8JsonWriter writer, KaspaPskt value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteRawValue(value.ToJson());
        }
    }
}
