using System.Text;
using NKaspa.Crypto;

namespace NKaspa
{
    /// <summary>
    /// Per-input and per-covenant indexes used by the covenant opcodes.
    /// Built the way rusty-kaspa <c>CovenantsContext::from_tx</c> builds them.
    /// A continuation output shares its covenant id with the authorizing input.
    /// A genesis output is checked by recomputing the id and is not added to the opcode indexes.
    /// </summary>
    public sealed class CovenantsContext
    {
        private readonly Dictionary<int, List<int>> _authOutputs = new Dictionary<int, List<int>>();
        private readonly Dictionary<uint256, Shared> _shared = new Dictionary<uint256, Shared>();

        private CovenantsContext()
        {
        }

        /// <summary>An empty context. Covenant opcodes then report zero matches.</summary>
        public static CovenantsContext Empty { get; } = new CovenantsContext();

        /// <summary>
        /// Indexes <paramref name="tx"/> against <paramref name="utxos"/>.
        /// A wrong genesis id or an authorizing input that does not exist returns an error and no context.
        /// </summary>
        public static ScriptError? TryFromTransaction(Transaction tx, IReadOnlyList<UtxoEntry?> utxos, out CovenantsContext context)
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (utxos == null)
            {
                throw new ArgumentNullException(nameof(utxos));
            }

            context = new CovenantsContext();
            for (var i = 0; i < tx.Inputs.Count; i++)
            {
                var entry = UtxoAt(utxos, i);
                if (entry?.CovenantId != null)
                {
                    context.SharedFor(entry.CovenantId).InputIndices.Add(i);
                }
            }

            var genesis = new Dictionary<GenesisKey, List<int>>();
            for (var i = 0; i < tx.Outputs.Count; i++)
            {
                var covenant = tx.Outputs[i].Covenant;
                if (covenant == null)
                {
                    continue;
                }

                var auth = covenant.AuthorizingInput;
                var utxo = UtxoAt(utxos, auth);
                if (utxo == null)
                {
                    context = Empty;
                    return ScriptError.Covenants("authorizing input " + auth.ToString() + " of output " + i.ToString() + " is out of bounds");
                }

                if (utxo.CovenantId != null && utxo.CovenantId.Equals(covenant.CovenantId))
                {
                    if (!context._authOutputs.TryGetValue(auth, out var children))
                    {
                        children = new List<int>();
                        context._authOutputs.Add(auth, children);
                    }

                    children.Add(i);
                    context.SharedFor(covenant.CovenantId).OutputIndices.Add(i);
                }
                else
                {
                    var key = new GenesisKey(auth, covenant.CovenantId);
                    if (!genesis.TryGetValue(key, out var outputs))
                    {
                        outputs = new List<int>();
                        genesis.Add(key, outputs);
                    }

                    outputs.Add(i);
                }
            }

            foreach (var pair in genesis)
            {
                var input = tx.Inputs[pair.Key.Input];
                var authorized = new List<KeyValuePair<uint, TxOut>>(pair.Value.Count);
                for (var n = 0; n < pair.Value.Count; n++)
                {
                    var outputIndex = pair.Value[n];
                    authorized.Add(new KeyValuePair<uint, TxOut>((uint)outputIndex, tx.Outputs[outputIndex]));
                }

                var expected = CovenantId(input.PreviousOutpoint, authorized);
                if (!expected.Equals(pair.Key.Id))
                {
                    context = Empty;
                    return ScriptError.Covenants("wrong genesis covenant id for input " + pair.Key.Input.ToString());
                }
            }

            return null;
        }

        internal int AuthOutputCount(int inputIndex)
        {
            return _authOutputs.TryGetValue(inputIndex, out var outputs) ? outputs.Count : 0;
        }

        internal ScriptError? AuthOutputIndex(int inputIndex, int k, out int outputIndex)
        {
            if (!_authOutputs.TryGetValue(inputIndex, out var outputs) || (uint)k >= (uint)outputs.Count)
            {
                outputIndex = 0;
                var count = outputs == null ? 0 : outputs.Count;
                return ScriptError.Covenants("authorized output " + k.ToString() + " of input " + inputIndex.ToString() + " count " + count.ToString());
            }

            outputIndex = outputs[k];
            return null;
        }

        internal int CovenantInputCount(uint256 covenantId)
        {
            return _shared.TryGetValue(covenantId, out var shared) ? shared.InputIndices.Count : 0;
        }

        internal ScriptError? CovenantInputIndex(uint256 covenantId, int k, out int inputIndex)
        {
            if (!_shared.TryGetValue(covenantId, out var shared) || (uint)k >= (uint)shared.InputIndices.Count)
            {
                inputIndex = 0;
                return ScriptError.Covenants("covenant input index " + k.ToString());
            }

            inputIndex = shared.InputIndices[k];
            return null;
        }

        internal int CovenantOutputCount(uint256 covenantId)
        {
            return _shared.TryGetValue(covenantId, out var shared) ? shared.OutputIndices.Count : 0;
        }

        internal ScriptError? CovenantOutputIndex(uint256 covenantId, int k, out int outputIndex)
        {
            if (!_shared.TryGetValue(covenantId, out var shared) || (uint)k >= (uint)shared.OutputIndices.Count)
            {
                outputIndex = 0;
                return ScriptError.Covenants("covenant output index " + k.ToString());
            }

            outputIndex = shared.OutputIndices[k];
            return null;
        }

        /// <summary>
        /// Genesis covenant id. Keyed BLAKE2b over the outpoint and the authorized outputs.
        /// The covenant binding itself is not hashed. Lengths are little-endian u64.
        /// </summary>
        public static uint256 CovenantId(OutPoint outpoint, IReadOnlyList<KeyValuePair<uint, TxOut>> authorizedOutputs)
        {
            if (outpoint == null)
            {
                throw new ArgumentNullException(nameof(outpoint));
            }

            if (authorizedOutputs == null)
            {
                throw new ArgumentNullException(nameof(authorizedOutputs));
            }

            var writer = new KaspaWriter();
            writer.Raw(outpoint.TransactionId.ToBytes());
            writer.U32(outpoint.Index);
            writer.U64((ulong)authorizedOutputs.Count);
            for (var i = 0; i < authorizedOutputs.Count; i++)
            {
                var output = authorizedOutputs[i].Value;
                writer.U32(authorizedOutputs[i].Key);
                writer.U64(output.Value.Sompi);
                writer.U16(output.ScriptPublicKey.Version);
                writer.VarBytes(output.ScriptPublicKey.Script);
            }

            return new uint256(Blake2b.ComputeHash(writer.ToArray(), 32, Encoding.ASCII.GetBytes("CovenantID")));
        }

        private Shared SharedFor(uint256 id)
        {
            if (!_shared.TryGetValue(id, out var shared))
            {
                shared = new Shared();
                _shared.Add(id, shared);
            }

            return shared;
        }

        private static UtxoEntry? UtxoAt(IReadOnlyList<UtxoEntry?> utxos, int index)
        {
            if ((uint)index >= (uint)utxos.Count)
            {
                return null;
            }

            return utxos[index];
        }

        private sealed class Shared
        {
            internal List<int> InputIndices { get; } = new List<int>();

            internal List<int> OutputIndices { get; } = new List<int>();
        }

        private readonly struct GenesisKey : IEquatable<GenesisKey>
        {
            internal GenesisKey(int input, uint256 id)
            {
                Input = input;
                Id = id;
            }

            internal int Input { get; }

            internal uint256 Id { get; }

            public bool Equals(GenesisKey other)
            {
                return Input == other.Input && Id.Equals(other.Id);
            }

            public override bool Equals(object? obj)
            {
                return obj is GenesisKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (Input * 31) + Id.GetHashCode();
            }
        }
    }
}
