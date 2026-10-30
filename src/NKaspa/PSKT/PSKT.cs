using NKaspa.Crypto;
using NKaspa.Policy;

namespace NKaspa.PSKT
{
    /// <summary>
    /// Partially signed Kaspa transaction. Kaspa counterpart of NBitcoin.PartiallySignedTransaction,
    /// with the field layout and hex encoding of rusty-kaspa <c>wallet/pskt</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hex is <c>PSKT</c> plus lowercase hex of compact JSON. A bundle uses <c>PSKB</c> and a JSON array.
    /// Roles follow Creator, Constructor, Updater, Signer, Combiner, Finalizer, and Extractor.
    /// The role is not written into the JSON.
    /// </para>
    /// <para>
    /// Finalize covers Schnorr and ECDSA pay-to-pubkey, and multisig pay-to-script-hash.
    /// Extract builds the transaction and sets KIP-9 storage mass. It does not run scripts and does not broadcast.
    /// Version 0 sighash matches rusty-kaspa. Version 1 sighash matches NKaspa: the budget is not in the preimage.
    /// rusty-kaspa still stores a sigop count on a version-1 PSKT input and hashes a zero compute budget.
    /// </para>
    /// </remarks>
    public sealed class PSKT
    {
        private readonly List<PSKTInput> _inputs = new List<PSKTInput>();
        private readonly List<PSKTOutput> _outputs = new List<PSKTOutput>();
        private PSKTRole _role;

        private PSKT(PSKTRole role)
        {
            Global = new PSKTGlobal();
            _role = role;
        }

        private PSKT(PSKTGlobal global, List<PSKTInput> inputs, List<PSKTOutput> outputs, PSKTRole role)
        {
            Global = global;
            _inputs = inputs;
            _outputs = outputs;
            _role = role;
        }

        /// <summary>Global map.</summary>
        public PSKTGlobal Global { get; }

        /// <summary>Inputs, in construction order.</summary>
        public IReadOnlyList<PSKTInput> Inputs
        {
            get { return _inputs; }
        }

        /// <summary>Outputs, in construction order.</summary>
        public IReadOnlyList<PSKTOutput> Outputs
        {
            get { return _outputs; }
        }

        /// <summary>Current role. Not serialized.</summary>
        public PSKTRole Role
        {
            get { return _role; }
        }

        /// <summary>Creator with format version 1 and transaction version 0.</summary>
        public static PSKT Create()
        {
            return new PSKT(PSKTRole.Creator);
        }

        /// <summary>Parses <c>PSKT</c> plus hex of UTF-8 JSON. The role is Combiner.</summary>
        public static PSKT FromHex(string text)
        {
            return FromJson(PSKTJson.FromTransport(text, PSKTJson.PsktPrefix, "PSKT serialization requires 'PSKT' prefix."));
        }

        /// <summary>Parses the compact JSON object. The role is Combiner.</summary>
        public static PSKT FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var pskt = new PSKT(PSKTRole.Combiner);
            PSKTJson.ReadInto(json, pskt.Global, pskt._inputs, pskt._outputs);
            return pskt;
        }

        /// <summary>Compact JSON. Field order matches rusty-kaspa serde.</summary>
        public string ToJson()
        {
            return PSKTJson.Write(Global, _inputs, _outputs);
        }

        /// <summary><c>PSKT</c> plus lowercase hex of <see cref="ToJson"/>.</summary>
        public string ToHex()
        {
            return PSKTJson.ToTransport(PSKTJson.PsktPrefix, ToJson());
        }

        /// <summary>Deep copy. The copy keeps the role.</summary>
        public PSKT Clone()
        {
            var inputs = new List<PSKTInput>(_inputs.Count);
            for (var i = 0; i < _inputs.Count; i++)
            {
                inputs.Add(_inputs[i].Copy());
            }

            var outputs = new List<PSKTOutput>(_outputs.Count);
            for (var i = 0; i < _outputs.Count; i++)
            {
                outputs.Add(_outputs[i].Copy());
            }

            return new PSKT(Global.Copy(), inputs, outputs, _role);
        }

        /// <summary>Sets the fallback lock time.</summary>
        public PSKT FallbackLockTime(ulong fallback)
        {
            Require(PSKTRole.Creator);
            Global.FallbackLockTime = fallback;
            return this;
        }

        /// <summary>Sets the PSKT format version.</summary>
        public PSKT SetVersion(PSKTVersion version)
        {
            Require(PSKTRole.Creator);
            Global.Version = version;
            return this;
        }

        /// <summary>Marks inputs modifiable.</summary>
        public PSKT InputsModifiable()
        {
            Require(PSKTRole.Creator);
            Global.InputsModifiable = true;
            return this;
        }

        /// <summary>Marks outputs modifiable.</summary>
        public PSKT OutputsModifiable()
        {
            Require(PSKTRole.Creator);
            Global.OutputsModifiable = true;
            return this;
        }

        /// <summary>Moves a creator to the constructor role.</summary>
        public PSKT Constructor()
        {
            Require(PSKTRole.Creator);
            _role = PSKTRole.Constructor;
            return this;
        }

        /// <summary>Sets the transaction version.</summary>
        public PSKT SetTransactionVersion(ushort txVersion)
        {
            Require(PSKTRole.Constructor);
            Global.TxVersion = txVersion;
            return this;
        }

        /// <summary>Appends an input and increments <see cref="PSKTGlobal.InputCount"/>.</summary>
        public PSKT AddInput(PSKTInput input)
        {
            Require(PSKTRole.Constructor);
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            _inputs.Add(input);
            Global.InputCount++;
            return this;
        }

        /// <summary>Appends an output and increments <see cref="PSKTGlobal.OutputCount"/>.</summary>
        public PSKT AddOutput(PSKTOutput output)
        {
            Require(PSKTRole.Constructor);
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (output.Covenant != null && (Global.Version < PSKTVersion.Two || Global.TxVersion < 1))
            {
                throw new PSKTException("Outputs not allowed to contain covenant due to pskt or tx versions mismatch.");
            }

            _outputs.Add(output);
            Global.OutputCount++;
            return this;
        }

        /// <summary>Sets the payload. A non-null payload requires format version 1 or higher.</summary>
        public PSKT SetPayload(byte[]? payload)
        {
            Require(PSKTRole.Constructor);
            if (payload != null && Global.Version < PSKTVersion.One)
            {
                throw new PSKTException("Cannot set payload on PSKT version " + (byte)Global.Version + ", payload requires version 1 or higher.");
            }

            Global.Payload = payload == null ? null : (byte[])payload.Clone();
            return this;
        }

        /// <summary>Clears both modifiable flags and moves to the updater role.</summary>
        public PSKT Updater()
        {
            Require(PSKTRole.Constructor);
            Global.InputsModifiable = false;
            Global.OutputsModifiable = false;
            _role = PSKTRole.Updater;
            return this;
        }

        /// <summary>Sets one input sequence.</summary>
        public PSKT SetSequence(int inputIndex, ulong sequence)
        {
            Require(PSKTRole.Updater);
            if ((uint)inputIndex >= (uint)_inputs.Count)
            {
                throw new PSKTException("OutputNotModifiable");
            }

            _inputs[inputIndex].Sequence = sequence;
            return this;
        }

        /// <summary>Moves to the signer role. A constructor is finished first.</summary>
        public PSKT Signer()
        {
            if (_role == PSKTRole.Constructor)
            {
                return Updater().Signer();
            }

            if (_role != PSKTRole.Updater && _role != PSKTRole.Combiner && _role != PSKTRole.Signer)
            {
                throw new PSKTException("Signer is reached from the constructor, updater, or combiner.");
            }

            _role = PSKTRole.Signer;
            return this;
        }

        /// <summary>
        /// Adds this key's partial signature on every input it can spend.
        /// Schnorr uses BIP-340. ECDSA uses RFC 6979 compact signatures, stored in JSON as DER.
        /// Pass 32 <paramref name="auxRand32"/> bytes, including zeros, for a deterministic Schnorr signature.
        /// </summary>
        public PSKT Sign(Key key, byte[]? auxRand32 = null, PSKTKeySource? keySource = null)
        {
            Require(PSKTRole.Signer);
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var unsigned = ToUnsignedTransaction();
            for (var i = 0; i < _inputs.Count; i++)
            {
                if (!TryMatch(_inputs[i], key, out var ecdsa))
                {
                    continue;
                }

                var utxo = _inputs[i].Utxo;
                if (utxo == null)
                {
                    throw new PSKTException("Missing UTXO entry.");
                }

                PSKTSignature signature;
                if (ecdsa)
                {
                    var hash = unsigned.GetEcdsaSignatureHash(i, utxo, _inputs[i].SighashType);
                    signature = PSKTSignature.Ecdsa(key.PubKeyECDSA, key.SignECDSA(hash.ToBytes()));
                }
                else
                {
                    var hash = unsigned.GetSignatureHash(i, utxo, _inputs[i].SighashType);
                    var schnorr = auxRand32 == null
                        ? key.SignSchnorr(hash.ToBytes())
                        : key.SignSchnorr(hash.ToBytes(), auxRand32);
                    signature = PSKTSignature.Schnorr(key.PubKeyECDSA, schnorr);
                }

                ReplaceSignature(_inputs[i], signature);
                if (keySource != null)
                {
                    ReplaceDerivation(_inputs[i].Derivations, new PSKTDerivation(key.PubKeyECDSA, keySource));
                }
            }

            return this;
        }

        /// <summary>Moves to the combiner role.</summary>
        public PSKT Combiner()
        {
            if (_role != PSKTRole.Constructor && _role != PSKTRole.Updater && _role != PSKTRole.Signer && _role != PSKTRole.Combiner)
            {
                throw new PSKTException("Combiner is reached from the constructor, updater, or signer.");
            }

            _role = PSKTRole.Combiner;
            return this;
        }

        /// <summary>
        /// Merges <paramref name="other"/> into a new combiner PSKT.
        /// Neither argument is modified. Rules match rusty-kaspa <c>Add</c>.
        /// </summary>
        public PSKT Combine(PSKT other)
        {
            Require(PSKTRole.Combiner);
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            var global = CombineGlobal(Global, other.Global);
            var inputs = CombineList(_inputs, other._inputs, AddInputs);
            var outputs = CombineList(_outputs, other._outputs, AddOutputs);
            for (var i = 0; i < outputs.Count; i++)
            {
                if (outputs[i].Covenant != null && (global.Version < PSKTVersion.Two || global.TxVersion < 1))
                {
                    throw new PSKTException("Outputs not allowed to contain covenant due to pskt or tx versions mismatch.");
                }
            }

            return new PSKT(global, inputs, outputs, PSKTRole.Combiner);
        }

        /// <summary>Moves to the finalizer role.</summary>
        public PSKT Finalizer()
        {
            if (_role != PSKTRole.Signer && _role != PSKTRole.Combiner && _role != PSKTRole.Finalizer)
            {
                throw new PSKTException("Finalizer is reached from the signer or combiner.");
            }

            _role = PSKTRole.Finalizer;
            return this;
        }

        /// <summary>Builds final scripts for pay-to-pubkey and multisig pay-to-script-hash, then sets the unsigned id.</summary>
        public PSKT Finalize()
        {
            return Finalize(BuildFinalScripts);
        }

        /// <summary>
        /// Writes one non-empty script per input, then sets <see cref="PSKTGlobal.Id"/> to the unsigned transaction id.
        /// A null sequence becomes <see cref="ulong.MaxValue"/>.
        /// </summary>
        public PSKT Finalize(Func<PSKT, IReadOnlyList<Script>> finalScripts)
        {
            Require(PSKTRole.Finalizer);
            if (finalScripts == null)
            {
                throw new ArgumentNullException(nameof(finalScripts));
            }

            var scripts = finalScripts(this);
            if (scripts == null || scripts.Count != _inputs.Count)
            {
                throw new PSKTException("Signatures count mismatch.");
            }

            for (var i = 0; i < scripts.Count; i++)
            {
                if (scripts[i] == null || scripts[i].Length == 0)
                {
                    throw new PSKTException("Signature at index " + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is empty.");
                }

                _inputs[i].Sequence = _inputs[i].Sequence ?? ulong.MaxValue;
                _inputs[i].FinalScriptSig = scripts[i];
            }

            Global.Id = ToUnsignedTransaction().GetId();
            return this;
        }

        /// <summary>Moves to the extractor. Fails when finalize has not set the id.</summary>
        public PSKT Extractor()
        {
            if (_role != PSKTRole.Finalizer && _role != PSKTRole.Extractor)
            {
                throw new PSKTException("Extractor is reached from the finalizer.");
            }

            if (Global.Id == null)
            {
                throw new PSKTException("Transaction is not finalized.");
            }

            _role = PSKTRole.Extractor;
            return this;
        }

        /// <summary>
        /// Copies final scripts onto the unsigned transaction and sets KIP-9 storage mass.
        /// The script engine is not run. The transaction is not broadcast.
        /// </summary>
        public Transaction ExtractTransaction()
        {
            Require(PSKTRole.Extractor);
            var transaction = ToUnsignedTransaction();
            for (var i = 0; i < _inputs.Count; i++)
            {
                if (_inputs[i].FinalScriptSig == null)
                {
                    throw new PSKTException("Transaction is not finalized.");
                }

                transaction.Inputs[i].SignatureScript = _inputs[i].FinalScriptSig!;
            }

            if (_inputs.Count > 0 && _inputs.TrueForAll(input => input.Utxo != null))
            {
                var entries = new UtxoEntry[_inputs.Count];
                for (var i = 0; i < _inputs.Count; i++)
                {
                    entries[i] = _inputs[i].Utxo!;
                }

                transaction.StorageMass = MassCalculator.StorageMass(transaction, entries);
            }

            if (Global.Id == null || !transaction.GetId().Equals(Global.Id))
            {
                throw new PSKTException("Transaction is not finalized.");
            }

            return transaction;
        }

        /// <summary>
        /// Unsigned transaction used for sighash and the PSKT id.
        /// Signature scripts are empty. A missing sequence is <see cref="ulong.MaxValue"/>.
        /// Payload is included only when the PSKT version is 1 or higher.
        /// Version 1 carries <see cref="PSKTInput.ComputeBudget"/> when it was set in memory, otherwise 0.
        /// </summary>
        public Transaction ToUnsignedTransaction()
        {
            var inputs = new TxIn[_inputs.Count];
            for (var i = 0; i < _inputs.Count; i++)
            {
                var input = _inputs[i];
                var sequence = input.Sequence ?? ulong.MaxValue;
                var sigop = input.SigOpCount ?? (byte)0;
                inputs[i] = new TxIn(input.PreviousOutpoint, Script.Empty, sequence, sigop);
                if (Global.TxVersion >= 1)
                {
                    inputs[i].ComputeBudget = input.ComputeBudget ?? (ushort)0;
                }
            }

            var outputs = new TxOut[_outputs.Count];
            for (var i = 0; i < _outputs.Count; i++)
            {
                outputs[i] = new TxOut(_outputs[i].Amount, _outputs[i].ScriptPublicKey)
                {
                    Covenant = _outputs[i].Covenant,
                };
            }

            var payload = Global.Version >= PSKTVersion.One && Global.Payload != null
                ? Global.Payload
                : Array.Empty<byte>();
            return new Transaction(Global.TxVersion, inputs, outputs, DetermineLockTime(), SubnetworkId.Native, 0, payload);
        }

        /// <summary>
        /// Schnorr multisig redeem script: required, x-only keys in the given order, key count, <c>OP_CHECKMULTISIG</c>.
        /// Keys are not sorted.
        /// </summary>
        public static Script MultisigRedeemScript(int required, IReadOnlyList<PubKey> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            var encoded = new byte[keys.Count][];
            for (var i = 0; i < keys.Count; i++)
            {
                if (keys[i] == null)
                {
                    throw new ArgumentNullException(nameof(keys));
                }

                encoded[i] = keys[i].ToBytes();
            }

            return MultisigRedeemScript(required, encoded, OpcodeType.OP_CHECKMULTISIG);
        }

        /// <summary>ECDSA multisig redeem script. Keys stay in the given order and use <c>OP_CHECKMULTISIGECDSA</c>.</summary>
        public static Script MultisigRedeemScript(int required, IReadOnlyList<PubKeyECDSA> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            var encoded = new byte[keys.Count][];
            for (var i = 0; i < keys.Count; i++)
            {
                if (keys[i] == null)
                {
                    throw new ArgumentNullException(nameof(keys));
                }

                encoded[i] = keys[i].ToBytes();
            }

            return MultisigRedeemScript(required, encoded, OpcodeType.OP_CHECKMULTISIGECDSA);
        }

        private static Script MultisigRedeemScript(int required, byte[][] keys, OpcodeType opcode)
        {
            if (keys.Length == 0 || keys.Length > 16 || required < 1 || required > keys.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(required), "Multisig requires 1 to 16 keys and a threshold inside that range.");
            }

            var builder = new ScriptBuilder().AddInt64(required);
            for (var i = 0; i < keys.Length; i++)
            {
                builder.AddData(keys[i]);
            }

            return builder.AddInt64(keys.Length).AddOp(opcode).ToScript();
        }

        private ulong DetermineLockTime()
        {
            ulong? max = null;
            for (var i = 0; i < _inputs.Count; i++)
            {
                if (!_inputs[i].MinTime.HasValue)
                {
                    continue;
                }

                if (!max.HasValue || _inputs[i].MinTime!.Value > max.Value)
                {
                    max = _inputs[i].MinTime;
                }
            }

            return max ?? Global.FallbackLockTime ?? 0;
        }

        private IReadOnlyList<Script> BuildFinalScripts(PSKT pskt)
        {
            var scripts = new Script[pskt._inputs.Count];
            for (var i = 0; i < scripts.Length; i++)
            {
                scripts[i] = BuildFinalScript(pskt._inputs[i]);
            }

            return scripts;
        }

        private static Script BuildFinalScript(PSKTInput input)
        {
            if (input.Utxo == null)
            {
                throw new PSKTException("Missing UTXO entry.");
            }

            var script = input.Utxo.ScriptPublicKey.ToScript();
            if (script.TryGetPayToPubKey(out var xOnly))
            {
                var partial = FindPartial(input, xOnly, schnorr: true);
                if (partial == null)
                {
                    throw new PSKTException("Missing signature.");
                }

                return Script.SignatureScript(new SchnorrSignature(partial.ToCompact()), input.SighashType);
            }

            if (script.TryGetPayToPubKeyEcdsa(out var compressed))
            {
                var partial = FindPartial(input, compressed, schnorr: false);
                if (partial == null)
                {
                    throw new PSKTException("Missing signature.");
                }

                return Script.SignatureScript(new ECDSASignature(partial.ToCompact()), input.SighashType);
            }

            if (!script.IsPayToScriptHash)
            {
                throw new PSKTException("Cannot finalize input.");
            }

            if (input.RedeemScript == null)
            {
                throw new PSKTException("Missing redeem script.");
            }

            if (!TryParseMultisig(input.RedeemScript, out var required, out var keys, out var schnorr))
            {
                throw new PSKTException("Redeem script is not a standard multisig.");
            }

            var pushes = new ScriptBuilder();
            var found = 0;
            for (var i = 0; i < keys.Count && found < required; i++)
            {
                var partial = FindPartial(input, keys[i], schnorr);
                if (partial == null)
                {
                    continue;
                }

                var body = new byte[65];
                partial.ToCompact().CopyTo(body, 0);
                body[64] = (byte)input.SighashType;
                pushes.AddData(body);
                found++;
            }

            if (found != required)
            {
                throw new PSKTException("Missing signature.");
            }

            return Script.PayToScriptHashSignature(input.RedeemScript, pushes.ToScript());
        }

        private static bool TryParseMultisig(Script redeem, out int required, out List<byte[]> keys, out bool schnorr)
        {
            required = 0;
            keys = new List<byte[]>();
            schnorr = false;
            var ops = redeem.Ops;
            if (ops.Count < 4 || !ops[0].TryGetSmallInt(out var requiredValue) || requiredValue < 1)
            {
                return false;
            }

            var index = 1;
            while (index < ops.Count && ops[index].Data != null && (ops[index].Data!.Length == 32 || ops[index].Data!.Length == 33))
            {
                keys.Add(ops[index].Data!);
                index++;
            }

            if (keys.Count == 0 || index + 1 >= ops.Count || !ops[index].TryGetSmallInt(out var keyCount) || keyCount != keys.Count)
            {
                return false;
            }

            var opcode = ops[index + 1].Code;
            if (opcode == OpcodeType.OP_CHECKMULTISIG)
            {
                schnorr = true;
            }
            else if (opcode == OpcodeType.OP_CHECKMULTISIGECDSA)
            {
                schnorr = false;
            }
            else
            {
                return false;
            }

            if (keys[0].Length != (schnorr ? 32 : 33))
            {
                return false;
            }

            for (var i = 1; i < keys.Count; i++)
            {
                if (keys[i].Length != keys[0].Length)
                {
                    return false;
                }
            }

            required = (int)requiredValue;
            return required <= keys.Count && index + 2 == ops.Count;
        }

        private static PSKTSignature? FindPartial(PSKTInput input, byte[] key, bool schnorr)
        {
            for (var i = 0; i < input.PartialSignatures.Count; i++)
            {
                var partial = input.PartialSignatures[i];
                if (partial.IsSchnorr != schnorr)
                {
                    continue;
                }

                var candidate = schnorr ? partial.PublicKey.XOnly : partial.PublicKey.ToBytes();
                if (candidate.AsSpan().SequenceEqual(key))
                {
                    return partial;
                }
            }

            return null;
        }

        private static bool TryMatch(PSKTInput input, Key key, out bool ecdsa)
        {
            ecdsa = false;
            if (input.Utxo == null)
            {
                return false;
            }

            var script = input.Utxo.ScriptPublicKey.ToScript();
            if (script.TryGetPayToPubKey(out var xOnly) && key.PubKey.ToBytes().AsSpan().SequenceEqual(xOnly))
            {
                return true;
            }

            if (script.TryGetPayToPubKeyEcdsa(out var compressed) && key.PubKeyECDSA.ToBytes().AsSpan().SequenceEqual(compressed))
            {
                ecdsa = true;
                return true;
            }

            if (!script.IsPayToScriptHash || input.RedeemScript == null || !TryParseMultisig(input.RedeemScript, out _, out var keys, out var schnorr))
            {
                return false;
            }

            var mine = schnorr ? key.PubKey.ToBytes() : key.PubKeyECDSA.ToBytes();
            for (var i = 0; i < keys.Count; i++)
            {
                if (keys[i].AsSpan().SequenceEqual(mine))
                {
                    ecdsa = !schnorr;
                    return true;
                }
            }

            return false;
        }

        private static void ReplaceSignature(PSKTInput input, PSKTSignature signature)
        {
            for (var i = 0; i < input.PartialSignatures.Count; i++)
            {
                if (input.PartialSignatures[i].PublicKey.ToBytes().AsSpan().SequenceEqual(signature.PublicKey.ToBytes()))
                {
                    input.PartialSignatures[i] = signature;
                    return;
                }
            }

            input.PartialSignatures.Add(signature);
        }

        private static void ReplaceDerivation(List<PSKTDerivation> derivations, PSKTDerivation derivation)
        {
            for (var i = 0; i < derivations.Count; i++)
            {
                if (derivations[i].PublicKey.ToBytes().AsSpan().SequenceEqual(derivation.PublicKey.ToBytes()))
                {
                    derivations[i] = derivation;
                    return;
                }
            }

            derivations.Add(derivation);
        }

        private static List<T> CombineList<T>(List<T> left, List<T> right, Func<T, T, T> add)
        {
            if (left.Count > right.Count)
            {
                var result = new List<T>(left.Count);
                for (var i = 0; i < right.Count; i++)
                {
                    result.Add(add(left[i], right[i]));
                }

                for (var i = right.Count; i < left.Count; i++)
                {
                    result.Add(add(left[i], default!));
                }

                return result;
            }

            var merged = new List<T>(right.Count);
            for (var i = 0; i < left.Count; i++)
            {
                merged.Add(add(right[i], left[i]));
            }

            for (var i = left.Count; i < right.Count; i++)
            {
                merged.Add(add(right[i], default!));
            }

            return merged;
        }

        private static PSKTInput AddInputs(PSKTInput self, PSKTInput rhs)
        {
            if (rhs == null)
            {
                return self.Copy();
            }

            if (!self.PreviousOutpoint.TransactionId.Equals(rhs.PreviousOutpoint.TransactionId))
            {
                throw new PSKTException("The previous txids are not the same.");
            }

            if (self.PreviousOutpoint.Index != rhs.PreviousOutpoint.Index)
            {
                throw new PSKTException("The spent output indexes are not the same.");
            }

            var copy = self.Copy();
            copy.Utxo = MergeUtxo(self.Utxo, rhs.Utxo);
            copy.Sequence = MaxOptional(self.Sequence, rhs.Sequence);
            copy.MinTime = MaxOptional(self.MinTime, rhs.MinTime);
            for (var i = 0; i < rhs.PartialSignatures.Count; i++)
            {
                ReplaceSignature(copy, rhs.PartialSignatures[i]);
            }

            copy.RedeemScript = MergeScript(self.RedeemScript, rhs.RedeemScript);
            copy.FinalScriptSig = MergeScript(self.FinalScriptSig, rhs.FinalScriptSig);
            MergeDerivations(copy.Derivations, rhs.Derivations);
            MergeRaw(copy.Proprietaries, rhs.Proprietaries, "Two different proprietary values.");
            MergeRaw(copy.Unknowns, rhs.Unknowns, "Two different unknown field values.");
            return copy;
        }

        private static PSKTOutput AddOutputs(PSKTOutput self, PSKTOutput rhs)
        {
            if (rhs == null)
            {
                return self.Copy();
            }

            if (self.Amount != rhs.Amount)
            {
                throw new PSKTException("The amounts are not the same.");
            }

            if (!self.ScriptPublicKey.Equals(rhs.ScriptPublicKey))
            {
                throw new PSKTException("The script_pubkeys are not the same.");
            }

            var copy = self.Copy();
            copy.RedeemScript = MergeScript(self.RedeemScript, rhs.RedeemScript);
            copy.Covenant = MergeCovenant(self.Covenant, rhs.Covenant);
            MergeDerivations(copy.Derivations, rhs.Derivations);
            MergeRaw(copy.Proprietaries, rhs.Proprietaries, "Two different proprietary values.");
            MergeRaw(copy.Unknowns, rhs.Unknowns, "Two different unknown field values.");
            return copy;
        }

        private static PSKTGlobal CombineGlobal(PSKTGlobal left, PSKTGlobal right)
        {
            if (left.Version != right.Version)
            {
                throw new PSKTException("The version numbers are not the same.");
            }

            if (left.TxVersion != right.TxVersion)
            {
                throw new PSKTException("The transaction version numbers are not the same.");
            }

            var copy = left.Copy();
            copy.FallbackLockTime = MergeLock(left.FallbackLockTime, right.FallbackLockTime, "The transaction lock times are not the same.");
            copy.InputsModifiable = left.InputsModifiable && right.InputsModifiable;
            copy.OutputsModifiable = left.OutputsModifiable && right.OutputsModifiable;
            copy.InputCount = Math.Max(left.InputCount, right.InputCount);
            copy.OutputCount = Math.Max(left.OutputCount, right.OutputCount);
            copy.Xpubs.Clear();
            copy.Xpubs.AddRange(left.Xpubs);
            MergeXpubs(copy.Xpubs, right.Xpubs);
            if (left.Id != null && right.Id != null && !left.Id.Equals(right.Id))
            {
                throw new PSKTException("The transaction ids are not the same.");
            }

            copy.Id = left.Id ?? right.Id;
            MergeRaw(copy.Proprietaries, right.Proprietaries, "Two different proprietary values.");
            MergeRaw(copy.Unknowns, right.Unknowns, "Two different unknown field values.");
            if ((left.Payload != null || right.Payload != null) && left.Version < PSKTVersion.One)
            {
                throw new PSKTException("Payload requires PSKT version 1 or higher.");
            }

            if (left.Payload != null && right.Payload != null && !left.Payload.AsSpan().SequenceEqual(right.Payload))
            {
                throw new PSKTException("The transaction payloads are not compatible.");
            }

            copy.Payload = left.Payload ?? (right.Payload == null ? null : (byte[])right.Payload.Clone());
            return copy;
        }

        private static void MergeXpubs(List<PSKTXpub> self, List<PSKTXpub> rhs)
        {
            for (var i = 0; i < rhs.Count; i++)
            {
                var index = IndexOfXpub(self, rhs[i].ToXpub());
                if (index < 0)
                {
                    self.Add(rhs[i]);
                    continue;
                }

                var current = self[index].Source;
                var incoming = rhs[i].Source;
                var incomingShorterSuffix = incoming.Path.Indexes.Count < current.Path.Indexes.Count && IsSuffix(incoming.Path, current.Path);
                if (SamePath(incoming.Path, current.Path) && incoming.Fingerprint.Equals(current.Fingerprint) || incomingShorterSuffix)
                {
                    continue;
                }

                if (current.Path.Indexes.Count <= incoming.Path.Indexes.Count && IsSuffix(current.Path, incoming.Path))
                {
                    self[index] = rhs[i];
                    continue;
                }

                throw new PSKTException("combining PSKT, key-source conflict for xpub " + rhs[i].ToXpub());
            }
        }

        private static int IndexOfXpub(List<PSKTXpub> xpubs, string text)
        {
            for (var i = 0; i < xpubs.Count; i++)
            {
                if (xpubs[i].ToXpub() == text)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool SamePath(KeyPath left, KeyPath right)
        {
            if (left.Indexes.Count != right.Indexes.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Indexes.Count; i++)
            {
                if (left.Indexes[i] != right.Indexes[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSuffix(KeyPath shorter, KeyPath longer)
        {
            if (shorter.Indexes.Count > longer.Indexes.Count)
            {
                return false;
            }

            var offset = longer.Indexes.Count - shorter.Indexes.Count;
            for (var i = 0; i < shorter.Indexes.Count; i++)
            {
                if (shorter.Indexes[i] != longer.Indexes[offset + i])
                {
                    return false;
                }
            }

            return true;
        }

        private static UtxoEntry? MergeUtxo(UtxoEntry? left, UtxoEntry? right)
        {
            if (left == null)
            {
                return PSKTInput.CopyUtxo(right);
            }

            if (right == null || SameUtxo(left, right))
            {
                return PSKTInput.CopyUtxo(left);
            }

            throw new PSKTException("Two different utxos detected.");
        }

        private static bool SameUtxo(UtxoEntry left, UtxoEntry right)
        {
            return left.Amount.Sompi == right.Amount.Sompi
                && left.BlockDaaScore == right.BlockDaaScore
                && left.IsCoinbase == right.IsCoinbase
                && left.ScriptPublicKey.Equals(right.ScriptPublicKey)
                && (left.CovenantId == null ? right.CovenantId == null : right.CovenantId != null && left.CovenantId.Equals(right.CovenantId));
        }

        private static Script? MergeScript(Script? left, Script? right)
        {
            if (left == null)
            {
                return PSKTInput.CopyScript(right);
            }

            if (right == null || left.Equals(right))
            {
                return PSKTInput.CopyScript(left);
            }

            throw new PSKTException("Two different redeem scripts detected.");
        }

        private static CovenantBinding? MergeCovenant(CovenantBinding? left, CovenantBinding? right)
        {
            if (left == null)
            {
                return right;
            }

            if (right == null || (left.AuthorizingInput == right.AuthorizingInput && left.CovenantId.Equals(right.CovenantId)))
            {
                return left;
            }

            throw new PSKTException("Two different covenants detected.");
        }

        private static ulong? MergeLock(ulong? left, ulong? right, string message)
        {
            if (left.HasValue && right.HasValue && left.Value != right.Value)
            {
                throw new PSKTException(message);
            }

            return left ?? right;
        }

        private static ulong? MaxOptional(ulong? left, ulong? right)
        {
            if (!left.HasValue)
            {
                return right;
            }

            if (!right.HasValue || left.Value >= right.Value)
            {
                return left;
            }

            return right;
        }

        private static void MergeDerivations(List<PSKTDerivation> self, List<PSKTDerivation> rhs)
        {
            for (var i = 0; i < rhs.Count; i++)
            {
                var found = false;
                for (var j = 0; j < self.Count; j++)
                {
                    if (!self[j].PublicKey.ToBytes().AsSpan().SequenceEqual(rhs[i].PublicKey.ToBytes()))
                    {
                        continue;
                    }

                    found = true;
                    if (!SameSource(self[j].Source, rhs[i].Source))
                    {
                        throw new PSKTException("Two different derivations for the same key.");
                    }

                    break;
                }

                if (!found)
                {
                    self.Add(rhs[i]);
                }
            }
        }

        private static bool SameSource(PSKTKeySource? left, PSKTKeySource? right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            return left.SameAs(right);
        }

        private static void MergeRaw(Dictionary<string, string> self, Dictionary<string, string> rhs, string message)
        {
            foreach (var pair in rhs)
            {
                if (self.TryGetValue(pair.Key, out var existing) && existing != pair.Value)
                {
                    throw new PSKTException(message);
                }

                self[pair.Key] = pair.Value;
            }
        }

        private void Require(PSKTRole role)
        {
            if (_role != role)
            {
                throw new PSKTException("This operation belongs to the " + role.ToString() + " role.");
            }
        }
    }
}
