using NKaspa.Crypto;
using NKaspa.RPC;

namespace NKaspa.Policy
{
    /// <summary>
    /// One input of a transaction that has not been signed.
    /// The signature script is a placeholder of the length a real signature of this script kind occupies.
    /// Mass uses that length. It does not use the signature bytes or a private key.
    /// </summary>
    public readonly struct FeeInput
    {
        /// <summary>
        /// Creates an input shape.
        /// <paramref name="computeBudget"/> is used when the transaction version is 1 or higher.
        /// Null selects <see cref="TransactionBuilder.DefaultComputeBudget"/> for that version.
        /// Version 0 ignores it and charges <paramref name="sigOpCount"/> instead.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPublicKey"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="signatureScriptLength"/> is negative.</exception>
        public FeeInput(ulong amountSompi, ScriptPubKey scriptPublicKey, int signatureScriptLength, byte sigOpCount, ushort? computeBudget, bool hasCovenant)
        {
            if (scriptPublicKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPublicKey));
            }

            if (signatureScriptLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(signatureScriptLength), "Signature script length must be zero or positive.");
            }

            Amount = amountSompi;
            ScriptPublicKey = scriptPublicKey;
            SignatureScriptLength = signatureScriptLength;
            SigOpCount = sigOpCount;
            ComputeBudget = computeBudget;
            HasCovenant = hasCovenant;
        }

        /// <summary>
        /// Schnorr pay-to-pubkey input. The locking script is 34 bytes. The placeholder signature script is 66 bytes.
        /// <paramref name="computeBudget"/> null means version 0 charges one sigop, and version 1 uses
        /// <see cref="TransactionBuilder.DefaultComputeBudget"/>.
        /// </summary>
        public static FeeInput Schnorr(ulong amountSompi, ushort? computeBudget = null)
        {
            return new FeeInput(amountSompi, FeeScripts.SchnorrLock, FeeScripts.SignatureScriptLength, 1, computeBudget, false);
        }

        /// <summary>
        /// ECDSA pay-to-pubkey input. The locking script is 35 bytes. The placeholder signature script is 66 bytes.
        /// <paramref name="computeBudget"/> null means version 0 charges one sigop, and version 1 uses
        /// <see cref="TransactionBuilder.DefaultComputeBudget"/>.
        /// </summary>
        public static FeeInput Ecdsa(ulong amountSompi, ushort? computeBudget = null)
        {
            return new FeeInput(amountSompi, FeeScripts.EcdsaLock, FeeScripts.SignatureScriptLength, 1, computeBudget, false);
        }

        /// <summary>
        /// Pay-to-script-hash input. The locking script is the 35-byte script hash of <paramref name="redeemScript"/>.
        /// The placeholder signature script is one 66-byte pubkey signature followed by a canonical push of the redeem script.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="redeemScript"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Signature must be 64 bytes. Sighash type is not one of the six Kaspa values. Script hash must be 32 bytes.</exception>
        /// <exception cref="System.FormatException">Script push is too long. Script push is truncated.</exception>
        public static FeeInput ScriptHash(ulong amountSompi, Script redeemScript, byte sigOpCount = 1, ushort? computeBudget = null)
        {
            if (redeemScript == null)
            {
                throw new ArgumentNullException(nameof(redeemScript));
            }

            var signature = Script.PayToScriptHashSignature(redeemScript, FeeScripts.PlaceholderSignature);
            var locking = new ScriptPubKey(0, Script.PayToScriptHash(redeemScript));
            return new FeeInput(amountSompi, locking, signature.Length, sigOpCount, computeBudget, false);
        }

        /// <summary>Spent amount in sompi. Storage mass uses this value. Zero is rejected when the mass is calculated.</summary>
        public ulong Amount { get; }

        /// <summary>Locking script of the spent output. Storage plurality uses its length.</summary>
        public ScriptPubKey ScriptPublicKey { get; }

        /// <summary>Byte length of the signature script a signed input of this shape would carry.</summary>
        public int SignatureScriptLength { get; }

        /// <summary>Signature operations charged on a version-0 input. A standard pubkey input is 1.</summary>
        public byte SigOpCount { get; }

        /// <summary>
        /// Version-1 compute budget. Null selects <see cref="TransactionBuilder.DefaultComputeBudget"/> when the quote version is 1 or higher.
        /// </summary>
        public ushort? ComputeBudget { get; }

        /// <summary>True when the spent UTXO carries a covenant id. Storage plurality then counts an extra 32 bytes.</summary>
        public bool HasCovenant { get; }
    }

    /// <summary>
    /// One output of a transaction that has not been signed.
    /// Pass the amount and the locking script. A private key is not required.
    /// </summary>
    public readonly struct FeeOutput
    {
        /// <summary>Creates an output shape.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPublicKey"/> is null.</exception>
        public FeeOutput(ulong amountSompi, ScriptPubKey scriptPublicKey, bool hasCovenant)
        {
            if (scriptPublicKey == null)
            {
                throw new ArgumentNullException(nameof(scriptPublicKey));
            }

            Amount = amountSompi;
            ScriptPublicKey = scriptPublicKey;
            HasCovenant = hasCovenant;
        }

        /// <summary>Schnorr pay-to-pubkey output. The locking script is 34 bytes, the same length as <see cref="Script.PayToPubKey(System.ReadOnlySpan{byte})"/>.</summary>
        public static FeeOutput Schnorr(ulong amountSompi)
        {
            return new FeeOutput(amountSompi, FeeScripts.SchnorrLock, false);
        }

        /// <summary>ECDSA pay-to-pubkey output. The locking script is 35 bytes.</summary>
        public static FeeOutput Ecdsa(ulong amountSompi)
        {
            return new FeeOutput(amountSompi, FeeScripts.EcdsaLock, false);
        }

        /// <summary>Output locked by <paramref name="scriptPublicKey"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="scriptPublicKey"/> is null.</exception>
        public static FeeOutput Of(ulong amountSompi, ScriptPubKey scriptPublicKey, bool hasCovenant = false)
        {
            return new FeeOutput(amountSompi, scriptPublicKey, hasCovenant);
        }

        /// <summary>Amount in sompi. Storage mass uses this value. Zero is rejected when the mass is calculated.</summary>
        public ulong Amount { get; }

        /// <summary>Locking script. Compute mass charges 10 per script byte, plus the 2-byte version.</summary>
        public ScriptPubKey ScriptPublicKey { get; }

        /// <summary>True when the output carries a covenant. Serialized size and storage plurality then count an extra 34 or 32 bytes.</summary>
        public bool HasCovenant { get; }
    }

    /// <summary>
    /// Mass of one hypothetical transaction and the fee at one sompi-per-gram rate.
    /// <see cref="RelayFee"/> multiplies relay mass (compute and normalized transient, not storage).
    /// <see cref="Fee"/> multiplies <see cref="TransactionMass.FeeMass"/>, which includes normalized storage mass.
    /// </summary>
    public readonly struct FeeQuote
    {
        /// <summary>Creates a quote.</summary>
        public FeeQuote(TransactionMass mass, FeeRate feerate, Money relayFee, Money fee)
        {
            Mass = mass;
            Feerate = feerate;
            RelayFee = relayFee;
            Fee = fee;
        }

        /// <summary>Compute, transient, and storage mass of the shape.</summary>
        public TransactionMass Mass { get; }

        /// <summary>Sompi per gram used for <see cref="RelayFee"/> and <see cref="Fee"/>.</summary>
        public FeeRate Feerate { get; }

        /// <summary>Fee on relay mass at <see cref="Feerate"/>. This is the minimum relay fee when the rate is the network floor.</summary>
        public Money RelayFee { get; }

        /// <summary>Fee on <see cref="TransactionMass.FeeMass"/> at <see cref="Feerate"/>. Storage mass can make this larger than <see cref="RelayFee"/>.</summary>
        public Money Fee { get; }
    }

    /// <summary>
    /// Estimates Kaspa compute mass, KIP-9 storage mass, and the fee for a transaction shape without private keys.
    /// A Schnorr pay-to-pubkey input contributes a 66-byte signature script and one version-0 sigop, or a version-1 compute budget.
    /// The numbers match <see cref="MassCalculator"/> on a signed transaction of the same shape.
    /// A version-0 payment with one Schnorr input and two 34-byte outputs has compute mass <see cref="MassCalculator.StandardSchnorrPaymentMass"/> (2036).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The protocol minimum relay rate after Toccata is <see cref="DefaultMinimumFeerateSompiPerGram"/> (100 sompi per gram).
    /// <see cref="MinimumFeerate(RpcFeeEstimate)"/> reads the lowest positive bucket from <c>getFeeEstimate</c>.
    /// When every bucket is missing or not a positive finite number, that method returns the constant.
    /// <see cref="PayableFeerate(RpcFeeEstimate, ulong?)"/> raises a lower node bucket up to the constant, because a current node rejects a fee below it.
    /// </para>
    /// <para>
    /// DAA-aware relay rates stay on <see cref="MassPolicy"/>. This type uses 100 sompi per gram unless the caller passes another floor.
    /// </para>
    /// </remarks>
    public static class FeeEstimator
    {
        /// <summary>
        /// Protocol minimum relay rate after Toccata, in sompi per gram.
        /// Same value as <see cref="MassCalculator.MinimumFeeRate"/> and <see cref="FeeRate.MinimumRelaySompiPerGram"/>.
        /// </summary>
        public const ulong DefaultMinimumFeerateSompiPerGram = MassCalculator.MinimumFeeRate;

        /// <summary>100 sompi per gram.</summary>
        public static FeeRate DefaultMinimumFeerate
        {
            get { return new FeeRate(DefaultMinimumFeerateSompiPerGram); }
        }

        /// <summary>
        /// Compute, transient, and KIP-9 storage mass of <paramref name="inputs"/> and <paramref name="outputs"/>.
        /// No private key is used. The signature script is a placeholder of <see cref="FeeInput.SignatureScriptLength"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="inputs"/> is null. <paramref name="outputs"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ArgumentException">
        /// Output amount and plurality must be non-zero. Input amount and plurality must be non-zero. A
        /// transaction needs at least one output. A non-coinbase transaction needs at least one input.
        /// Subnetwork id must be 20 bytes.
        /// </exception>
        /// <exception cref="System.OverflowException">Normalized mass does not fit in 64 bits. Input amount mean does not fit in 64 bits.</exception>
        public static TransactionMass Estimate(
            IReadOnlyList<FeeInput> inputs,
            IReadOnlyList<FeeOutput> outputs,
            ushort version = 0,
            BlockMassLimits? limits = null)
        {
            var built = Build(inputs, outputs, version);
            return MassCalculator.Calculate(built.Transaction, built.Entries, limits);
        }

        /// <summary>
        /// Mass and fee for a shape. A null <paramref name="feerate"/> uses <paramref name="minimumFeerateSompiPerGram"/>,
        /// which defaults to <see cref="DefaultMinimumFeerateSompiPerGram"/>.
        /// A rate below that floor throws <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="inputs"/> is null. <paramref name="outputs"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// A value is outside the allowed range. Fee rate {rate} is below the network minimum of {floor} sompi/gram.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Output amount and plurality must be non-zero. Input amount and plurality must be non-zero. A
        /// transaction needs at least one output. A non-coinbase transaction needs at least one input.
        /// Subnetwork id must be 20 bytes.
        /// </exception>
        /// <exception cref="System.OverflowException">
        /// Normalized mass does not fit in 64 bits. Input amount mean does not fit in 64 bits. The fee does not fit in 64 bits.
        /// </exception>
        public static FeeQuote Quote(
            IReadOnlyList<FeeInput> inputs,
            IReadOnlyList<FeeOutput> outputs,
            FeeRate? feerate = null,
            ushort version = 0,
            BlockMassLimits? limits = null,
            ulong? minimumFeerateSompiPerGram = null)
        {
            var floor = FloorOf(minimumFeerateSompiPerGram, nameof(minimumFeerateSompiPerGram));
            var rate = feerate ?? new FeeRate(floor);
            if (rate.SompiPerGram < floor)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(feerate),
                    "Fee rate " + rate.SompiPerGram.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " is below the network minimum of " + floor.ToString(System.Globalization.CultureInfo.InvariantCulture) + " sompi/gram.");
            }

            var mass = Estimate(inputs, outputs, version, limits);
            var relay = checked(MassCalculator.RelayMass(mass) * rate.SompiPerGram);
            var fee = checked(mass.FeeMass * rate.SompiPerGram);
            return new FeeQuote(mass, rate, new Money(relay), new Money(fee));
        }

        /// <summary>
        /// Mass and fee using <see cref="PayableFeerate(RpcFeeEstimate, ulong?)"/> as the rate.
        /// The node bucket is raised to <paramref name="minimumFeerateSompiPerGram"/> when it is lower.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="inputs"/> is null. <paramref name="outputs"/> is null. <paramref name="nodeEstimate"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range. The protocol minimum feerate must be non-zero.</exception>
        /// <exception cref="System.ArgumentException">
        /// Output amount and plurality must be non-zero. Input amount and plurality must be non-zero. A
        /// transaction needs at least one output. A non-coinbase transaction needs at least one input.
        /// Subnetwork id must be 20 bytes.
        /// </exception>
        /// <exception cref="System.OverflowException">
        /// Fee rate does not fit in 64 bits. Normalized mass does not fit in 64 bits. Input amount mean does not
        /// fit in 64 bits. The fee does not fit in 64 bits.
        /// </exception>
        public static FeeQuote Quote(
            IReadOnlyList<FeeInput> inputs,
            IReadOnlyList<FeeOutput> outputs,
            RpcFeeEstimate nodeEstimate,
            ushort version = 0,
            BlockMassLimits? limits = null,
            ulong? minimumFeerateSompiPerGram = null)
        {
            var floor = FloorOf(minimumFeerateSompiPerGram, nameof(minimumFeerateSompiPerGram));
            var rate = PayableFeerate(nodeEstimate, floor);
            return Quote(inputs, outputs, rate, version, limits, floor);
        }

        /// <summary>
        /// Lowest positive finite feerate in <paramref name="estimate"/>, rounded up to a whole sompi per gram.
        /// Buckets that are missing, NaN, infinite, or not positive are skipped.
        /// When none remain, the result is <see cref="DefaultMinimumFeerate"/>.
        /// A node may advertise a bucket below 100. This method returns that bucket. <see cref="PayableFeerate(RpcFeeEstimate, ulong?)"/> applies the protocol floor.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="estimate"/> is null.</exception>
        /// <exception cref="System.OverflowException">Fee rate does not fit in 64 bits.</exception>
        public static FeeRate MinimumFeerate(RpcFeeEstimate estimate)
        {
            if (estimate == null)
            {
                throw new ArgumentNullException(nameof(estimate));
            }

            if (!TryLowest(estimate, out var lowest))
            {
                return DefaultMinimumFeerate;
            }

            return new FeeRate(CeilSompi(lowest));
        }

        /// <summary>
        /// <see cref="MinimumFeerate(RpcFeeEstimate)"/> raised to <paramref name="protocolMinimumSompiPerGram"/>.
        /// The default floor is <see cref="DefaultMinimumFeerateSompiPerGram"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="estimate"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">The protocol minimum feerate must be non-zero.</exception>
        /// <exception cref="System.OverflowException">Fee rate does not fit in 64 bits.</exception>
        public static FeeRate PayableFeerate(RpcFeeEstimate estimate, ulong? protocolMinimumSompiPerGram = null)
        {
            var floor = FloorOf(protocolMinimumSompiPerGram, nameof(protocolMinimumSompiPerGram));
            var node = MinimumFeerate(estimate);
            if (node.SompiPerGram < floor)
            {
                return new FeeRate(floor);
            }

            return node;
        }

        /// <summary>
        /// Calls <see cref="IKaspaRpcClient.GetFeeEstimateAsync"/> and returns <see cref="MinimumFeerate(RpcFeeEstimate)"/>.
        /// A node that omits every bucket yields <see cref="DefaultMinimumFeerate"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="client"/> is null. A required value is null.</exception>
        /// <exception cref="System.ArgumentException">wRPC request is missing a numeric id.</exception>
        /// <exception cref="System.InvalidOperationException">WebSocket is not connected. Call ConnectAsync first.</exception>
        /// <exception cref="System.OverflowException">Fee rate does not fit in 64 bits.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        public static async Task<FeeRate> ReadMinimumFeerateAsync(IKaspaRpcClient client, CancellationToken cancellationToken = default)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var estimate = await client.GetFeeEstimateAsync(cancellationToken).ConfigureAwait(false);
            return MinimumFeerate(estimate);
        }

        private static ulong FloorOf(ulong? minimumFeerateSompiPerGram, string parameterName)
        {
            var floor = minimumFeerateSompiPerGram ?? DefaultMinimumFeerateSompiPerGram;
            if (floor == 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "The protocol minimum feerate must be non-zero.");
            }

            return floor;
        }

        private static bool TryLowest(RpcFeeEstimate estimate, out double lowest)
        {
            var best = double.MaxValue;
            var found = false;
            Consider(estimate.PriorityBucket);
            ConsiderAll(estimate.NormalBuckets);
            ConsiderAll(estimate.LowBuckets);
            lowest = best;
            return found;

            void ConsiderAll(IReadOnlyList<RpcFeeBucket> buckets)
            {
                if (buckets == null)
                {
                    return;
                }

                for (var i = 0; i < buckets.Count; i++)
                {
                    Consider(buckets[i]);
                }
            }

            void Consider(RpcFeeBucket? bucket)
            {
                if (bucket == null)
                {
                    return;
                }

                var value = bucket.Feerate;
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                {
                    return;
                }

                if (value < best)
                {
                    best = value;
                }

                found = true;
            }
        }

        private static ulong CeilSompi(double feerate)
        {
            var ceil = Math.Ceiling(feerate);
            if (ceil > ulong.MaxValue)
            {
                throw new OverflowException("Fee rate does not fit in 64 bits.");
            }

            var whole = (ulong)ceil;
            return whole == 0 ? 1UL : whole;
        }

        private static BuiltShape Build(IReadOnlyList<FeeInput> inputs, IReadOnlyList<FeeOutput> outputs, ushort version)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (outputs == null)
            {
                throw new ArgumentNullException(nameof(outputs));
            }

            var txInputs = new TxIn[inputs.Count];
            var entries = new UtxoEntry[inputs.Count];
            for (var i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                if (input.ScriptPublicKey == null)
                {
                    throw new ArgumentException("Input script public key is required.", nameof(inputs));
                }

                var signature = FeeScripts.PlaceholderOfLength(input.SignatureScriptLength);
                var outpoint = new OutPoint(uint256.Zero, (uint)i);
                if (version >= 1)
                {
                    var budget = input.ComputeBudget ?? TransactionBuilder.DefaultComputeBudget;
                    txInputs[i] = TxIn.WithComputeBudget(outpoint, signature, 0, budget);
                }
                else
                {
                    txInputs[i] = new TxIn(outpoint, signature, 0, input.SigOpCount);
                }

                uint256? covenant = input.HasCovenant ? uint256.Zero : null;
                entries[i] = new UtxoEntry(input.Amount, input.ScriptPublicKey, 0, false, covenant);
            }

            var txOutputs = new TxOut[outputs.Count];
            for (var i = 0; i < outputs.Count; i++)
            {
                var output = outputs[i];
                if (output.ScriptPublicKey == null)
                {
                    throw new ArgumentException("Output script public key is required.", nameof(outputs));
                }

                var txOut = new TxOut(output.Amount, output.ScriptPublicKey);
                if (output.HasCovenant)
                {
                    txOut.Covenant = new CovenantBinding(0, uint256.Zero);
                }

                txOutputs[i] = txOut;
            }

            var transaction = new Transaction(version, txInputs, txOutputs, 0, SubnetworkId.Native, 0, Array.Empty<byte>());
            return new BuiltShape(transaction, entries);
        }

        private readonly struct BuiltShape
        {
            public BuiltShape(Transaction transaction, UtxoEntry[] entries)
            {
                Transaction = transaction;
                Entries = entries;
            }

            public Transaction Transaction { get; }

            public UtxoEntry[] Entries { get; }
        }
    }

    /// <summary>Locking scripts and the 66-byte pubkey signature placeholder used by <see cref="FeeEstimator"/>.</summary>
    internal static class FeeScripts
    {
        /// <summary>Byte length of <c>OP_DATA_65 || 64-byte signature || sighash</c>.</summary>
        internal const int SignatureScriptLength = 66;

        internal static ScriptPubKey SchnorrLock { get; } = new ScriptPubKey(0, Script.PayToPubKey(SchnorrKey()));

        internal static ScriptPubKey EcdsaLock { get; } = new ScriptPubKey(0, EcdsaScript());

        internal static Script PlaceholderSignature { get; } = Script.SignatureScript(new SchnorrSignature(new byte[SchnorrSignature.Length]), SigHashType.All);

        internal static Script PlaceholderOfLength(int length)
        {
            if (length == SignatureScriptLength)
            {
                return PlaceholderSignature;
            }

            if (length == 0)
            {
                return Script.Empty;
            }

            return new Script(new byte[length]);
        }

        private static byte[] SchnorrKey()
        {
            var key = new byte[32];
            key[31] = 1;
            return key;
        }

        private static Script EcdsaScript()
        {
            var script = new byte[35];
            script[0] = ScriptPubKey.OpData33;
            script[1] = 0x02;
            script[34] = ScriptPubKey.OpCheckSigECDSA;
            return new Script(script);
        }
    }
}
