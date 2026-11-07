using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The goods held by one holder, a ship's hold, a warehouse or a city's market: the amount of each good, counted
    /// in its own unit, a barrel or a bushel, and the room they take, counted in barrels, a bushel taking several.
    /// Goods are indices into the catalogue. Whole units are added and removed through <see cref="TryAdd"/> and
    /// <see cref="TryRemove"/>; only a holder that consumes fractions of a unit, the market, moves fractions.
    /// </summary>
    public abstract class GoodInventory
    {
        // Days of fractional consumption leave a whole stock a hair short of itself.
        private const double WholeUnitMargin = 1e-9;

        // Amount of each good, by index; grown when a good is first added, unless the goods are fixed.
        private readonly List<double> amounts = new List<double>();
        private readonly GoodUnits units;

        // The goods held, when they are fixed; int.MaxValue for any good.
        private readonly int goodCount;

        /// <param name="units">The room a unit of each good takes. Without it, every unit takes one barrel.</param>
        /// <param name="goodCount">The goods the inventory holds, from index 0; null for any good, a good beyond them throwing.</param>
        protected GoodInventory(GoodUnits units, int? goodCount = null)
        {
            if (goodCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goodCount));
            }

            this.units = units ?? GoodUnits.AllBarrels;
            this.goodCount = goodCount ?? int.MaxValue;

            for (int i = 0; i < (goodCount ?? 0); i++)
            {
                amounts.Add(0d);
            }
        }

        /// <summary>Raised when goods were added or removed, not by a refusal.</summary>
        public event Action Changed;

        /// <summary>Barrels of room taken, all goods together; fractional only where fractions were moved.</summary>
        protected double RoomTaken { get; private set; }

        /// <summary>Units of a good held, fractions included.</summary>
        public double AmountOf(int good)
        {
            RequireGood(good);

            return good < amounts.Count ? amounts[good] : 0d;
        }

        /// <summary>Whole units of a good held, at most <see cref="int.MaxValue"/>: what is shown and can be taken.</summary>
        public int UnitsOf(int good)
        {
            return (int)Math.Min(int.MaxValue, Math.Floor(AmountOf(good) + WholeUnitMargin));
        }

        /// <summary>The whole units of a good the inventory still takes; everything, unless the holder limits it.</summary>
        public virtual int RoomFor(int good)
        {
            RequireGood(good);

            return int.MaxValue;
        }

        /// <returns>False when there is no room for all of them; nothing was added then.</returns>
        public bool TryAdd(int good, int count)
        {
            RequirePositive(count);

            if (count > RoomFor(good))
            {
                return false;
            }

            AddAmount(good, count);
            RaiseChanged();
            return true;
        }

        /// <returns>False when fewer whole units are held; nothing was removed then.</returns>
        public bool TryRemove(int good, int count)
        {
            RequirePositive(count);

            if (count > UnitsOf(good))
            {
                return false;
            }

            // Not below nothing: the last unit can be that hair short.
            RemoveUpTo(good, count);
            RaiseChanged();
            return true;
        }

        /// <summary>Adds units, fractions included, whatever the room; nothing is raised.</summary>
        /// <param name="amount">Not negative.</param>
        protected void AddAmount(int good, double amount)
        {
            RequireGood(good);

            while (amounts.Count <= good)
            {
                amounts.Add(0d);
            }

            amounts[good] += amount;
            RoomTaken += amount * units.RoomOf(good);
        }

        /// <summary>Removes up to these units, fractions included; nothing is raised.</summary>
        /// <param name="amount">Not negative.</param>
        /// <returns>The units removed: all that is held when it is fewer.</returns>
        protected double RemoveUpTo(int good, double amount)
        {
            RequireGood(good);

            if (good >= amounts.Count)
            {
                return 0d;
            }

            double removed = Math.Min(amounts[good], amount);
            amounts[good] -= removed;
            RoomTaken -= removed * units.RoomOf(good);
            return removed;
        }

        /// <summary>The room a unit of a good takes, in barrels.</summary>
        protected int RoomOf(int good)
        {
            return units.RoomOf(good);
        }

        protected void RaiseChanged()
        {
            Changed?.Invoke();
        }

        protected void RequireGood(int good)
        {
            if (good < 0 || good >= goodCount)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }
        }

        private static void RequirePositive(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }
    }
}
