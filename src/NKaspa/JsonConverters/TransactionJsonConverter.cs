using System.Text.Json;
using System.Text.Json.Serialization;
using NKaspa.RPC;

namespace NKaspa.JsonConverters
{
    /// <summary>
    /// One transaction input.
    /// <c>computeBudget</c> is written only when the input has one.
    /// A version-1 <see cref="Transaction"/> always writes it, including 0, through <see cref="TransactionJsonConverter"/>.
    /// </summary>
    public sealed class TxInJsonConverter : JsonConverter<TxIn>
    {
        /// <inheritdoc />
        public override TxIn Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, element => KaspaRpcJson.ReadInput(element, readComputeBudget: true));
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, TxIn value, JsonSerializerOptions options)
        {
            KaspaRpcJson.WriteInput(writer, value, value != null && value.ComputeBudget.HasValue);
        }
    }

    /// <summary>
    /// One transaction output. <c>value</c> is sompi.
    /// <c>amount</c> is accepted when reading. <c>covenant</c> is omitted when null.
    /// </summary>
    public sealed class TxOutJsonConverter : JsonConverter<TxOut>
    {
        /// <inheritdoc />
        public override TxOut Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, KaspaRpcJson.ReadOutput);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, TxOut value, JsonSerializerOptions options)
        {
            KaspaRpcJson.WriteOutput(writer, value);
        }
    }

    /// <summary>
    /// A transaction in wRPC shape.
    /// The compact string matches <c>KaspaRpcJson.WriteTransaction</c>.
    /// Version 1 writes <c>sigOpCount</c> and <c>computeBudget</c> on every input.
    /// </summary>
    public sealed class TransactionJsonConverter : JsonConverter<Transaction>
    {
        /// <inheritdoc />
        public override Transaction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, KaspaRpcJson.ReadTransaction);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Transaction value, JsonSerializerOptions options)
        {
            KaspaRpcJson.WriteTransaction(writer, value);
        }
    }

    /// <summary>A covenant binding as <c>{ "authorizingInput", "covenantId" }</c>.</summary>
    public sealed class CovenantBindingJsonConverter : JsonConverter<CovenantBinding>
    {
        /// <inheritdoc />
        public override CovenantBinding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonValueReader.Read(ref reader, KaspaRpcJson.ReadCovenant);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, CovenantBinding value, JsonSerializerOptions options)
        {
            KaspaRpcJson.WriteCovenant(writer, value);
        }
    }
}
