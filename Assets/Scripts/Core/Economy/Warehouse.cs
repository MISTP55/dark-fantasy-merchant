using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The goods stored in a building, out of a capacity that limits nothing: what is stored
    /// above it is the excess, which is charged a rent. Goods are counted in their own unit;
    /// the room they take is counted in barrels, a bushel taking several.
    /// </summary>
    public sealed class Warehouse : IGoodStore
    {
        // Units of each good, by index; grown when a good is first stored.
        private readonly List<long> unitsByGood = new List<long>();
        private readonly GoodUnits units;

        /// <param name="capacity">Barrels stored free of rent.</param>
        /// <param name="units">The room a unit of each good takes. Without it, every unit takes one barrel.</param>
        public Warehouse(int capacity, GoodUnits units = null)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
            this.units = units ?? GoodUnits.AllBarrels;
        }

        /// <summary>Barrels stored free of rent.</summary>
        public int Capacity { get; }

        /// <summary>Barrels of room taken, all goods together; may be far above the capacity.</summary>
        public long Used { get; private set; }

        /// <summary>Barrels stored above the capacity.</summary>
        public long Excess => Math.Max(0, Used - Capacity);

        /// <summary>Raised when goods were added or removed, not by a refusal.</summary>
        public event Action Changed;

        /// <summary>The rent due for the excess of all the warehouses, a week.</summary>
        public static long WeeklyRentOf(IEnumerable<Warehouse> warehouses, long rentPerBarrel)
        {
            if (warehouses == null)
            {
                throw new ArgumentNullException(nameof(warehouses));
            }

            long rent = 0;

            foreach (Warehouse warehouse in warehouses)
            {
                rent += warehouse.Excess * rentPerBarrel;
            }

            return rent;
        }

        /// <summary>Units of a good stored, at most <see cref="int.MaxValue"/>.</summary>
        public int UnitsOf(int good)
        {
            RequireGood(good);

            return good < unitsByGood.Count ? (int)Math.Min(unitsByGood[good], int.MaxValue) : 0;
        }

        /// <summary>A warehouse takes everything.</summary>
        public int RoomFor(int good)
        {
            RequireGood(good);

            return int.MaxValue;
        }

        /// <summary>Stores the units, whatever the capacity.</summary>
        public bool TryAdd(int good, int count)
        {
            RequireGood(good);
            RequirePositive(count);

            while (unitsByGood.Count <= good)
            {
                unitsByGood.Add(0);
            }

            unitsByGood[good] += count;
            Used += (long)count * units.RoomOf(good);
            Changed?.Invoke();
            return true;
        }

        /// <returns>False when fewer are stored; nothing was removed then.</returns>
        public bool TryRemove(int good, int count)
        {
            RequireGood(good);
            RequirePositive(count);

            if (good >= unitsByGood.Count || count > unitsByGood[good])
            {
                return false;
            }

            unitsByGood[good] -= count;
            Used -= (long)count * units.RoomOf(good);
            Changed?.Invoke();
            return true;
        }

        private static void RequireGood(int good)
        {
            if (good < 0)
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
