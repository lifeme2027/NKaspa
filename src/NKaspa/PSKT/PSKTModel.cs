using NKaspa.Crypto;

namespace NKaspa.PSKT
{
    /// <summary>
    /// PSKT format version. rusty-kaspa <c>pskt::Version</c>. JSON is the byte.
    /// A new <see cref="PSKT"/> uses <see cref="One"/>. The transaction version is separate and defaults to 0.
    /// </summary>
    public enum PSKTVersion : byte
    {
        /// <summary>Format version 0. Payload is omitted from the unsigned transaction.</summary>
        Zero = 0,

        /// <summary>Format version 1. Payload is allowed. This is the creator default.</summary>
        One = 1,

        /// <summary>Format version 2. Required before an output may carry a covenant.</summary>
        Two = 2,
    }

    /// <summary>
    /// BIP-174 style role. The role is not serialized.
    /// rusty-kaspa encodes it as a type parameter. Here it is a field that the role methods update.
    /// </summary>
    public enum PSKTRole
    {
        /// <summary>Sets format flags. No inputs or outputs yet.</summary>
        Creator,

        /// <summary>Adds inputs, outputs, payload, and the transaction version.</summary>
        Constructor,

        /// <summary>Sets sequences. Inputs and outputs are no longer modifiable.</summary>
        Updater,

        /// <summary>Adds partial signatures.</summary>
        Signer,

        /// <summary>Merges two PSKTs.</summary>
        Combiner,

        /// <summary>Writes final signature scripts and the unsigned transaction id.</summary>
        Finalizer,

        /// <summary>Copies the final scripts onto a transaction. Does not broadcast.</summary>
        Extractor,
    }

    /// <summary>A PSKT rule failed. The message names the conflict. Nothing is broadcast.</summary>
    public sealed class PSKTException : Exception
    {
        /// <summary>Creates an exception with <paramref name="message"/>.</summary>
        public PSKTException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Master-key fingerprint and derivation path. JSON is <c>keyFingerprint</c> (4-byte hex)
    /// and <c>derivationPath</c> (<c>m/...</c>).
    /// </summary>
    public sealed class PSKTKeySource
    {
        /// <summary>Creates a key source.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="path"/> is null.</exception>
        public PSKTKeySource(HDFingerprint fingerprint, KeyPath path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Fingerprint = fingerprint;
        }

        /// <summary>First four bytes of HASH160 of the master compressed public key.</summary>
        public HDFingerprint Fingerprint { get; }

        /// <summary>Path from that master key. Display form is the BIP32 string rusty-kaspa writes.</summary>
        public KeyPath Path { get; }

        /// <summary>True when the fingerprint and every child index match.</summary>
        public bool SameAs(PSKTKeySource? other)
        {
            if (other == null || !Fingerprint.Equals(other.Fingerprint) || Path.Indexes.Count != other.Path.Indexes.Count)
            {
                return false;
            }

            for (var i = 0; i < Path.Indexes.Count; i++)
            {
                if (Path.Indexes[i] != other.Path.Indexes[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// One partial signature. The map key is the 33-byte compressed public key.
    /// Schnorr JSON is 64-byte hex. ECDSA JSON is DER hex. Scripts use the compact 64 bytes either way.
    /// </summary>
    public sealed class PSKTSignature
    {
        private readonly byte[] _compact;

        private PSKTSignature(PubKeyECDSA publicKey, bool schnorr, byte[] compact)
        {
            PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
            IsSchnorr = schnorr;
            _compact = compact;
        }

        /// <summary>Schnorr partial signature under <paramref name="publicKey"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="signature"/> is null. <paramref name="publicKey"/> is null.</exception>
        public static PSKTSignature Schnorr(PubKeyECDSA publicKey, SchnorrSignature signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            return new PSKTSignature(publicKey, true, signature.ToBytes());
        }

        /// <summary>ECDSA partial signature under <paramref name="publicKey"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="signature"/> is null. <paramref name="publicKey"/> is null.</exception>
        public static PSKTSignature Ecdsa(PubKeyECDSA publicKey, ECDSASignature signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            return new PSKTSignature(publicKey, false, signature.ToBytes());
        }

        /// <summary>Compressed public key. This is the JSON object key.</summary>
        public PubKeyECDSA PublicKey { get; }

        /// <summary>True for a BIP-340 signature.</summary>
        public bool IsSchnorr { get; }

        /// <summary>64-byte compact signature. ECDSA script pushes use this, not DER.</summary>
        public byte[] ToCompact()
        {
            return (byte[])_compact.Clone();
        }
    }

    /// <summary>A public key and an optional BIP32 source. The JSON value is null when <see cref="Source"/> is null.</summary>
    public sealed class PSKTDerivation
    {
        /// <summary>Creates a derivation entry.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="publicKey"/> is null.</exception>
        public PSKTDerivation(PubKeyECDSA publicKey, PSKTKeySource? source)
        {
            PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
            Source = source;
        }

        /// <summary>Compressed public key.</summary>
        public PubKeyECDSA PublicKey { get; }

        /// <summary>Fingerprint and path. Null is a JSON null, which rusty-kaspa allows.</summary>
        public PSKTKeySource? Source { get; }
    }

    /// <summary>
    /// Extended public key entry. JSON uses the Bitcoin <c>xpub</c> prefix, which is what
    /// rusty-kaspa <c>ExtendedPublicKey::to_string(None)</c> writes, not the Kaspa <c>kpub</c> prefix.
    /// </summary>
    public sealed class PSKTXpub
    {
        /// <summary>Creates an xpub entry.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="source"/> is null. <paramref name="extendedPublicKey"/> is null.</exception>
        public PSKTXpub(ExtPubKey extendedPublicKey, PSKTKeySource source)
        {
            ExtendedPublicKey = extendedPublicKey ?? throw new ArgumentNullException(nameof(extendedPublicKey));
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>Extended public key. Serialization re-encodes it with <see cref="HdKeyVersions.Xpub"/>.</summary>
        public ExtPubKey ExtendedPublicKey { get; }

        /// <summary>Fingerprint and path paired with this xpub.</summary>
        public PSKTKeySource Source { get; }

        /// <summary>Bitcoin <c>xpub</c> text used as the JSON key.</summary>
        public string ToXpub()
        {
            return ExtendedPublicKey.ToBase58(HdKeyVersions.Xpub);
        }
    }

    /// <summary>
    /// Global PSKT map. Field order matches rusty-kaspa <c>Global</c>.
    /// </summary>
    public sealed class PSKTGlobal
    {
        /// <summary>Creates the creator default: format version 1, transaction version 0, counts 0.</summary>
        public PSKTGlobal()
        {
            Version = PSKTVersion.One;
        }

        /// <summary>PSKT format version.</summary>
        public PSKTVersion Version { get; set; }

        /// <summary>Transaction version. 0 is pre-Toccata. 1 is Toccata.</summary>
        public ushort TxVersion { get; set; }

        /// <summary>Lock time used when no input sets <see cref="PSKTInput.MinTime"/>.</summary>
        public ulong? FallbackLockTime { get; set; }

        /// <summary>True while the constructor may add inputs. Combine ANDs this flag.</summary>
        public bool InputsModifiable { get; set; }

        /// <summary>True while the constructor may add outputs. Combine ANDs this flag.</summary>
        public bool OutputsModifiable { get; set; }

        /// <summary>Declared input count. Adding an input increments it. Combine takes the max.</summary>
        public int InputCount { get; set; }

        /// <summary>Declared output count. Adding an output increments it. Combine takes the max.</summary>
        public int OutputCount { get; set; }

        /// <summary>Unsigned transaction id. Finalize sets it. Extract requires it.</summary>
        public uint256? Id { get; set; }

        /// <summary>Payload. Allowed when <see cref="Version"/> is 1 or higher. JSON is hex or null.</summary>
        public byte[]? Payload { get; set; }

        /// <summary>xpub map. Empty in the common case.</summary>
        public List<PSKTXpub> Xpubs { get; } = new List<PSKTXpub>();

        /// <summary>Proprietary JSON values, keyed by string. Each value is raw JSON.</summary>
        public Dictionary<string, string> Proprietaries { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Unknown flattened JSON values. Each value is raw JSON.</summary>
        public Dictionary<string, string> Unknowns { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        internal PSKTGlobal Copy()
        {
            var copy = new PSKTGlobal
            {
                Version = Version,
                TxVersion = TxVersion,
                FallbackLockTime = FallbackLockTime,
                InputsModifiable = InputsModifiable,
                OutputsModifiable = OutputsModifiable,
                InputCount = InputCount,
                OutputCount = OutputCount,
                Id = Id,
                Payload = Payload == null ? null : (byte[])Payload.Clone(),
            };
            copy.Xpubs.AddRange(Xpubs);
            CopyMap(Proprietaries, copy.Proprietaries);
            CopyMap(Unknowns, copy.Unknowns);
            return copy;
        }

        internal static void CopyMap(Dictionary<string, string> source, Dictionary<string, string> destination)
        {
            foreach (var pair in source)
            {
                destination[pair.Key] = pair.Value;
            }
        }
    }

    /// <summary>One PSKT input. Field order matches rusty-kaspa <c>Input</c>.</summary>
    public sealed class PSKTInput
    {
        /// <summary>Creates an input that spends <paramref name="previousOutpoint"/>. Sighash defaults to <see cref="SigHashType.All"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="previousOutpoint"/> is null.</exception>
        public PSKTInput(OutPoint previousOutpoint)
        {
            PreviousOutpoint = previousOutpoint ?? throw new ArgumentNullException(nameof(previousOutpoint));
            SighashType = SigHashType.All;
        }

        /// <summary>Spent outpoint.</summary>
        public OutPoint PreviousOutpoint { get; }

        /// <summary>UTXO being spent. Sighash needs it. Null until an updater fills it.</summary>
        public UtxoEntry? Utxo { get; set; }

        /// <summary>Sequence. Null means the unsigned transaction uses <see cref="ulong.MaxValue"/>.</summary>
        public ulong? Sequence { get; set; }

        /// <summary>Minimum lock time this input requires.</summary>
        public ulong? MinTime { get; set; }

        /// <summary>Sighash type every signature on this input must use. Combine keeps the surviving side.</summary>
        public SigHashType SighashType { get; set; }

        /// <summary>Redeem script for a pay-to-script-hash input.</summary>
        public Script? RedeemScript { get; set; }

        /// <summary>Version-0 sigop count committed by sighash. Null signs as 0.</summary>
        public byte? SigOpCount { get; set; }

        /// <summary>Final signature script. Finalize writes it.</summary>
        public Script? FinalScriptSig { get; set; }

        /// <summary>
        /// In-memory Toccata compute budget. It is not a PSKT JSON field.
        /// rusty-kaspa's unsigned transaction always stores a sigop count and hashes a zero budget.
        /// NKaspa keeps this so a version-1 builder payment can still carry its budget after extract.
        /// </summary>
        public ushort? ComputeBudget { get; set; }

        /// <summary>Partial signatures, keyed by compressed public key.</summary>
        public List<PSKTSignature> PartialSignatures { get; } = new List<PSKTSignature>();

        /// <summary>BIP32 derivations for the keys that can sign this input.</summary>
        public List<PSKTDerivation> Derivations { get; } = new List<PSKTDerivation>();

        /// <summary>Proprietary JSON values.</summary>
        public Dictionary<string, string> Proprietaries { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Unknown flattened JSON values.</summary>
        public Dictionary<string, string> Unknowns { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        internal PSKTInput Copy()
        {
            var copy = new PSKTInput(PreviousOutpoint)
            {
                Utxo = CopyUtxo(Utxo),
                Sequence = Sequence,
                MinTime = MinTime,
                SighashType = SighashType,
                RedeemScript = CopyScript(RedeemScript),
                SigOpCount = SigOpCount,
                FinalScriptSig = CopyScript(FinalScriptSig),
                ComputeBudget = ComputeBudget,
            };
            copy.PartialSignatures.AddRange(PartialSignatures);
            copy.Derivations.AddRange(Derivations);
            PSKTGlobal.CopyMap(Proprietaries, copy.Proprietaries);
            PSKTGlobal.CopyMap(Unknowns, copy.Unknowns);
            return copy;
        }

        internal static UtxoEntry? CopyUtxo(UtxoEntry? utxo)
        {
            if (utxo == null)
            {
                return null;
            }

            return new UtxoEntry(
                utxo.Amount,
                new ScriptPubKey(utxo.ScriptPublicKey.Version, utxo.ScriptPublicKey.Script),
                utxo.BlockDaaScore,
                utxo.IsCoinbase,
                utxo.CovenantId);
        }

        internal static Script? CopyScript(Script? script)
        {
            return script == null ? null : new Script(script.ToBytes());
        }
    }

    /// <summary>One PSKT output. Field order matches rusty-kaspa <c>Output</c>.</summary>
    public sealed class PSKTOutput
    {
        /// <summary>Creates an output. <paramref name="amount"/> is sompi.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPublicKey"/> is null.</exception>
        public PSKTOutput(ulong amount, ScriptPubKey scriptPublicKey)
        {
            Amount = amount;
            ScriptPublicKey = scriptPublicKey ?? throw new ArgumentNullException(nameof(scriptPublicKey));
        }

        /// <summary>Amount in sompi.</summary>
        public ulong Amount { get; set; }

        /// <summary>Locking script.</summary>
        public ScriptPubKey ScriptPublicKey { get; set; }

        /// <summary>Redeem script carried for the receiver. Not required to spend this output.</summary>
        public Script? RedeemScript { get; set; }

        /// <summary>Covenant. Constructor rejects it unless the PSKT version is 2 or higher and the transaction version is 1 or higher.</summary>
        public CovenantBinding? Covenant { get; set; }

        /// <summary>BIP32 derivations for this output.</summary>
        public List<PSKTDerivation> Derivations { get; } = new List<PSKTDerivation>();

        /// <summary>Proprietary JSON values.</summary>
        public Dictionary<string, string> Proprietaries { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Unknown flattened JSON values.</summary>
        public Dictionary<string, string> Unknowns { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        internal PSKTOutput Copy()
        {
            var copy = new PSKTOutput(Amount, new ScriptPubKey(ScriptPublicKey.Version, ScriptPublicKey.Script))
            {
                RedeemScript = PSKTInput.CopyScript(RedeemScript),
                Covenant = Covenant,
            };
            copy.Derivations.AddRange(Derivations);
            PSKTGlobal.CopyMap(Proprietaries, copy.Proprietaries);
            PSKTGlobal.CopyMap(Unknowns, copy.Unknowns);
            return copy;
        }
    }
}
