namespace NKaspa
{
    /// <summary>
    /// Selects coins for one Kaspa payment.
    /// Kaspa counterpart of NBitcoin.DefaultCoinSelector.
    /// </summary>
    /// <remarks>
    /// Coins that share a script are not grouped. Each coin is one input.
    /// Grouping would spend sibling UTXOs that the payment does not need, and those extra inputs
    /// would have to be signed as separate signature scripts.
    /// When one coin covers the target plus <see cref="MinimumChange"/>, only the smallest such coin is used.
    /// A coin that covers the target but not the minimum change is still used alone.
    /// The short change is left for <see cref="TransactionBuilder"/> to fold into the fee.
    /// Coins are combined only when no single coin covers the target.
    /// The combination uses as few coins as possible. A combination that also covers
    /// <see cref="MinimumChange"/> wins over one that does not. Ties keep the smaller sum.
    /// </remarks>
    public sealed class DefaultCoinSelector
    {
        /// <summary>Creates a selector that does not reserve change.</summary>
        public DefaultCoinSelector()
            : this(Money.Zero)
        {
        }

        /// <summary>Creates a selector that prefers change of at least <paramref name="minimumChange"/>.</summary>
        public DefaultCoinSelector(Money minimumChange)
        {
            MinimumChange = minimumChange;
        }

        /// <summary>
        /// Change the selector tries to leave when a single coin, or a combination, can do so.
        /// The builder passes <see cref="TransactionBuilder.MinimumChange"/>.
        /// </summary>
        public Money MinimumChange { get; set; }

        /// <summary>
        /// Selects coins that sum to at least <paramref name="target"/>.
        /// <paramref name="target"/> is the payment plus the fee and does not include change.
        /// Duplicate outpoints keep the first coin. The result is ordered by amount descending, then outpoint.
        /// </summary>
        public List<Coin> Select(IReadOnlyList<Coin> coins, Money target)
        {
            if (target.Sompi == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(target), "The selection target must be greater than zero.");
            }

            var pool = Dedupe(coins);
            if (pool.Count == 0)
            {
                throw new InvalidOperationException("Not enough funds.");
            }

            var targetValue = target.Sompi;
            var prefer = targetValue > ulong.MaxValue - MinimumChange.Sompi
                ? ulong.MaxValue
                : targetValue + MinimumChange.Sompi;

            Coin? exact = null;
            Coin? coveringChange = null;
            Coin? coveringTarget = null;
            for (var i = 0; i < pool.Count; i++)
            {
                var coin = pool[i];
                var amount = coin.Amount.Sompi;
                if (amount == targetValue && (exact == null || PreferSmaller(coin, exact)))
                {
                    exact = coin;
                }

                if (amount >= prefer && (coveringChange == null || PreferSmaller(coin, coveringChange)))
                {
                    coveringChange = coin;
                }
                else if (amount >= targetValue && amount < prefer && (coveringTarget == null || PreferSmaller(coin, coveringTarget)))
                {
                    coveringTarget = coin;
                }
            }

            if (exact != null)
            {
                return new List<Coin> { exact };
            }

            if (coveringChange != null)
            {
                return new List<Coin> { coveringChange };
            }

            if (coveringTarget != null)
            {
                return new List<Coin> { coveringTarget };
            }

            List<Coin> combined;
            if (pool.Count > 16)
            {
                combined = Greedy(pool, targetValue, prefer);
            }
            else
            {
                combined = Combine(pool, targetValue, prefer);
            }

            combined.Sort(CompareLargestFirst);
            return combined;
        }

        private static List<Coin> Combine(List<Coin> pool, ulong target, ulong prefer)
        {
            pool.Sort(CompareOutpoint);
            var best = new BestPick();
            var chosen = new int[pool.Count];
            Search(pool, 0, 0, 0, target, prefer, chosen, best);
            if (!best.Found)
            {
                throw new InvalidOperationException("Not enough funds.");
            }

            var result = new List<Coin>(best.Count);
            for (var i = 0; i < best.Count; i++)
            {
                result.Add(pool[best.Indexes[i]]);
            }

            return result;
        }

        private static void Search(List<Coin> pool, int index, ulong sum, int count, ulong target, ulong prefer, int[] chosen, BestPick best)
        {
            if (sum >= target)
            {
                var meets = sum >= prefer;
                if (IsBetter(best, meets, count, sum))
                {
                    best.Found = true;
                    best.Meets = meets;
                    best.Count = count;
                    best.Sum = sum;
                    best.Indexes = new int[count];
                    Array.Copy(chosen, best.Indexes, count);
                }

                return;
            }

            if (index >= pool.Count || !CanReach(pool, index, sum, target))
            {
                return;
            }

            if (best.Found && best.Meets && count >= best.Count)
            {
                return;
            }

            var amount = pool[index].Amount.Sompi;
            if (sum <= ulong.MaxValue - amount)
            {
                chosen[count] = index;
                Search(pool, index + 1, sum + amount, count + 1, target, prefer, chosen, best);
            }

            Search(pool, index + 1, sum, count, target, prefer, chosen, best);
        }

        private static bool CanReach(List<Coin> pool, int index, ulong sum, ulong target)
        {
            for (var i = index; i < pool.Count; i++)
            {
                var amount = pool[i].Amount.Sompi;
                if (sum >= target || amount >= target - sum)
                {
                    return true;
                }

                if (sum > ulong.MaxValue - amount)
                {
                    return true;
                }

                sum += amount;
            }

            return sum >= target;
        }

        private static bool IsBetter(BestPick best, bool meets, int count, ulong sum)
        {
            if (!best.Found)
            {
                return true;
            }

            if (meets != best.Meets)
            {
                return meets;
            }

            if (count != best.Count)
            {
                return count < best.Count;
            }

            return sum < best.Sum;
        }

        private static List<Coin> Greedy(List<Coin> pool, ulong target, ulong prefer)
        {
            var ordered = new List<Coin>(pool);
            ordered.Sort(CompareLargestFirst);
            ulong total = 0;
            for (var i = 0; i < ordered.Count; i++)
            {
                var amount = ordered[i].Amount.Sompi;
                if (total > ulong.MaxValue - amount)
                {
                    throw new InvalidOperationException("Not enough funds.");
                }

                total += amount;
            }

            if (total < target)
            {
                throw new InvalidOperationException("Not enough funds.");
            }

            var goal = total >= prefer ? prefer : target;
            var selected = new List<Coin>();
            ulong sum = 0;
            for (var i = 0; i < ordered.Count; i++)
            {
                selected.Add(ordered[i]);
                sum += ordered[i].Amount.Sompi;
                if (sum >= goal)
                {
                    return selected;
                }
            }

            throw new InvalidOperationException("Not enough funds.");
        }

        private static List<Coin> Dedupe(IReadOnlyList<Coin> coins)
        {
            if (coins == null)
            {
                throw new ArgumentNullException(nameof(coins));
            }

            var result = new List<Coin>(coins.Count);
            for (var i = 0; i < coins.Count; i++)
            {
                var coin = coins[i];
                if (coin == null)
                {
                    throw new ArgumentNullException(nameof(coins));
                }

                var seen = false;
                for (var j = 0; j < result.Count; j++)
                {
                    if (result[j].Outpoint.Equals(coin.Outpoint))
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                {
                    result.Add(coin);
                }
            }

            return result;
        }

        private static bool PreferSmaller(Coin candidate, Coin current)
        {
            var byAmount = candidate.Amount.Sompi.CompareTo(current.Amount.Sompi);
            if (byAmount != 0)
            {
                return byAmount < 0;
            }

            return string.CompareOrdinal(candidate.Outpoint.ToString(), current.Outpoint.ToString()) < 0;
        }

        private static int CompareLargestFirst(Coin left, Coin right)
        {
            var byAmount = right.Amount.Sompi.CompareTo(left.Amount.Sompi);
            if (byAmount != 0)
            {
                return byAmount;
            }

            return string.CompareOrdinal(left.Outpoint.ToString(), right.Outpoint.ToString());
        }

        private static int CompareOutpoint(Coin left, Coin right)
        {
            return string.CompareOrdinal(left.Outpoint.ToString(), right.Outpoint.ToString());
        }

        private sealed class BestPick
        {
            public bool Found;

            public bool Meets;

            public int Count;

            public ulong Sum;

            public int[] Indexes = Array.Empty<int>();
        }
    }
}
