namespace NKaspa
{
    /// <summary>
    /// Kaspa amount in sompi. Kaspa counterpart of NBitcoin.Money.
    /// 1 KAS = 100,000,000 sompi. The protocol field is an unsigned 64-bit integer.
    /// </summary>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        /// <summary>Sompi in one KAS.</summary>
        public const ulong SompiPerKas = 100_000_000;

        /// <summary>Creates an amount from a sompi count.</summary>
        public Money(ulong sompi)
        {
            Sompi = sompi;
        }

        /// <summary>Zero sompi.</summary>
        public static Money Zero
        {
            get { return default; }
        }

        /// <summary>Amount in sompi.</summary>
        public ulong Sompi { get; }

        /// <summary>Creates an amount from a sompi count.</summary>
        public static Money FromSompi(ulong sompi)
        {
            return new Money(sompi);
        }

        /// <summary>
        /// Creates an amount from a KAS quantity.
        /// The value must be a whole number of sompi and must fit in 64 bits.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">KAS amount does not fit in 64-bit sompi. A Kaspa amount cannot be negative.</exception>
        /// <exception cref="System.ArgumentException">KAS amount is finer than one sompi.</exception>
        public static Money Coins(decimal kas)
        {
            if (kas < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kas), "A Kaspa amount cannot be negative.");
            }

            var maxKas = ulong.MaxValue / (decimal)SompiPerKas;
            if (kas > maxKas)
            {
                throw new ArgumentOutOfRangeException(nameof(kas), "KAS amount does not fit in 64-bit sompi.");
            }

            var scaled = kas * SompiPerKas;
            if (scaled != decimal.Truncate(scaled))
            {
                throw new ArgumentException("KAS amount is finer than one sompi.", nameof(kas));
            }

            return new Money((ulong)scaled);
        }

        /// <summary>Whole KAS plus a fractional part, up to 8 decimal places, without trailing zeros.</summary>
        public override string ToString()
        {
            var whole = Sompi / SompiPerKas;
            var fraction = Sompi % SompiPerKas;
            if (fraction == 0)
            {
                return whole.ToString(System.Globalization.CultureInfo.InvariantCulture) + " KAS";
            }

            var digits = fraction.ToString("D8", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0');
            return whole.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + digits + " KAS";
        }

        /// <inheritdoc />
        public bool Equals(Money other)
        {
            return Sompi == other.Sompi;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is Money other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Sompi.GetHashCode();
        }

        /// <inheritdoc />
        public int CompareTo(Money other)
        {
            return Sompi.CompareTo(other.Sompi);
        }

        /// <summary>Sompi equality.</summary>
        public static bool operator ==(Money left, Money right)
        {
            return left.Sompi == right.Sompi;
        }

        /// <summary>Sompi inequality.</summary>
        public static bool operator !=(Money left, Money right)
        {
            return left.Sompi != right.Sompi;
        }

        /// <summary>Treats an unsigned sompi count as <see cref="Money"/>.</summary>
        public static implicit operator Money(ulong sompi)
        {
            return new Money(sompi);
        }

        /// <summary>The sompi count.</summary>
        public static implicit operator ulong(Money money)
        {
            return money.Sompi;
        }
    }
}
