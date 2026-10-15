using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The goods aboard one ship, out of the room it has. Goods are indices into the
    /// catalogue and are counted in their own unit, a barrel or a bushel; the room they
    /// take is counted in barrels, a bushel taking several.
    /// </summary>
    public sealed class CargoHold
    {
        // Units of each good, by index; grown when a good is first loaded.
        private readonly List<int> unitsByGood = new List<int>();
        private readonly GoodUnits units;

        /// <param name="capacity">Barrels of room; zero for a ship that carries nothing.</param>
        /// <param name="units">The room a unit of each good takes. Without it, every unit takes one barrel.</param>
        public CargoHold(int capacity, GoodUnits units = null)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
            this.units = units ?? GoodUnits.AllBarrels;
        }

        /// <summary>Barrels of room.</summary>
        public int Capacity { get; }

        /// <summary>Barrels of room taken, all goods together.</summary>
        public int Used { get; private set; }

        public int Free => Capacity - Used;

        /// <summary>Raised when goods were loaded or unloaded, not by a refusal.</summary>
        public event Action Changed;

        /// <summary>Units of a good aboard.</summary>
        public int UnitsOf(int good)
        {
            RequireGood(good);

            return good < unitsByGood.Count ? unitsByGood[good] : 0;
        }

        /// <summary>The whole units of a good the free room still takes.</summary>
        public int RoomFor(int good)
        {
            RequireGood(good);

            return Free / units.RoomOf(good);
        }

        /// <returns>False when the hold has no room for all of them; nothing was loaded then.</returns>
        public bool TryAdd(int good, int count)
        {
            RequireGood(good);
            RequirePositive(count);

            long room = (long)count * units.RoomOf(good);

            if (room > Free)
            {
                return false;
            }

            while (unitsByGood.Count <= good)
            {
                unitsByGood.Add(0);
            }

            unitsByGood[good] += count;
            Used += (int)room;
            Changed?.Invoke();
            return true;
        }

        /// <returns>False when fewer are aboard; nothing was unloaded then.</returns>
        public bool TryRemove(int good, int count)
        {
            RequireGood(good);
            RequirePositive(count);

            if (count > UnitsOf(good))
            {
                return false;
            }

            unitsByGood[good] -= count;
            Used -= count * units.RoomOf(good);
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
