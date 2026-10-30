using System.Text.Json;
using System.Text.Json.Serialization;
using NKaspa.RPC;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// A UTXO entry: <c>amount</c> (sompi), <c>scriptPublicKey</c>, <c>blockDaaScore</c>, <c>isCoinbase</c>.
    /// <c>covenantId</c> is written only when set. Null and a missing field both mean none.
    /// <c>scriptPublicKey</c> accepts an object or version-prefixed hex.
    /// </summary>
    public sealed class UtxoEntryJsonConverter : JsonConverter<UtxoEntry>
    {
        /// <inheritdoc />
        public override UtxoEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, KaspaRpcJson.ReadUtxoEntry);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, UtxoEntry value, JsonSerializerOptions options)
        {
            KaspaRpcJson.WriteUtxoEntry(writer, value);
        }
    }

    /// <summary>
    /// A coin as <c>{ "outpoint", "utxoEntry" }</c>, matching one getUtxosByAddresses entry without the address.
    /// An <c>address</c> property is ignored. <c>entry</c> is accepted in place of <c>utxoEntry</c>.
    /// </summary>
    public sealed class CoinJsonConverter : JsonConverter<Coin>
    {
        /// <inheritdoc />
        public override Coin Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, KaspaRpcJson.ReadCoin);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Coin value, JsonSerializerOptions options)
        {
            KaspaRpcJson.WriteCoin(writer, value);
        }
    }
}
