namespace NKaspa
{
    /// <summary>
    /// Fee rate in sompi per gram. Kaspa counterpart of NBitcoin.FeeRate.
    /// A gram is one unit of normalized transaction mass, not a kilobyte.
    /// The minimum relay fee is 100 sompi times the larger of compute mass and normalized transient mass.
    /// Storage mass is not part of that floor. <see cref="Policy.MassCalculator.MinimumRelayFee(Policy.TransactionMass)"/> applies it.
    /// A standard one-input Schnorr payment with change is 2036 grams, so the floor is 203600 sompi, about 0.002 KAS.
    /// <see cref="GetFee"/> multiplies this rate by a gram count. It does not itself reject a rate below the relay floor.
    /// </summary>
    public readonly struct FeeRate : IEquatable<FeeRate>
    {
        /// <summary>Minimum relay rate, 100 sompi per gram.</summary>
        public const ulong MinimumRelaySompiPerGram = 100;

        /// <summary>Creates a rate. Values below <see cref="MinimumRelaySompiPerGram"/> are representable.</summary>
        public FeeRate(ulong sompiPerGram)
        {
            SompiPerGram = sompiPerGram;
        }

        /// <summary>Sompi charged for one gram of mass.</summary>
        public ulong SompiPerGram { get; }

        /// <summary>100 sompi per gram.</summary>
        public static FeeRate MinimumRelay
        {
            get { return new FeeRate(MinimumRelaySompiPerGram); }
        }

        /// <summary>
        /// Fee for <paramref name="mass"/> grams at this rate. The product must fit in 64 bits.
        /// Pass compute mass, normalized transient mass, or <see cref="Policy.TransactionMass.FeeMass"/>, whichever the quote is for.
        /// <see cref="Policy.MassCalculator.EstimateFee(FeeRate, ulong)"/> is the same product and rejects a rate below 100 sompi per gram.
        /// </summary>
        public Money GetFee(ulong mass)
        {
            return new Money(checked(SompiPerGram * mass));
        }

        /// <inheritdoc />
        public bool Equals(FeeRate other)
        {
            return SompiPerGram == other.SompiPerGram;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is FeeRate other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return SompiPerGram.GetHashCode();
        }

        /// <summary>The rate as a decimal number of sompi per gram.</summary>
        public override string ToString()
        {
            return SompiPerGram.ToString(System.Globalization.CultureInfo.InvariantCulture) + " sompi/g";
        }
    }
}
