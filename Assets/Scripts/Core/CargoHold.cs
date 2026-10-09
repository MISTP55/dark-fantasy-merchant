using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The barrels of goods aboard one ship, out of those it has room for. Goods are
    /// indices into the catalogue; every barrel takes the same room whatever it holds.
    /// </summary>
    public sealed class CargoHold
    {
        // Barrels of each good, by index; grown when a good is first loaded.
        private readonly List<int> barrelsByGood = new List<int>();

        /// <param name="capacity">Barrels the hold has room for; zero for a ship that carries nothing.</param>
        public CargoHold(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
        }

        public int Capacity { get; }

        /// <summary>Barrels aboard, all goods together.</summary>
        public int Used { get; private set; }

        public int Free => Capacity - Used;

        /// <summary>Raised when barrels were loaded or unloaded, not by a refusal.</summary>
        public event Action Changed;

        public int BarrelsOf(int good)
        {
            RequireGood(good);

            return good < barrelsByGood.Count ? barrelsByGood[good] : 0;
        }

        /// <returns>False when the hold has no room for all of them; nothing was loaded then.</returns>
        public bool TryAdd(int good, int barrels)
        {
            RequireGood(good);
            RequirePositive(barrels);

            if (barrels > Free)
            {
                return false;
            }

            while (barrelsByGood.Count <= good)
            {
                barrelsByGood.Add(0);
            }

            barrelsByGood[good] += barrels;
            Used += barrels;
            Changed?.Invoke();
            return true;
        }

        /// <returns>False when fewer are aboard; nothing was unloaded then.</returns>
        public bool TryRemove(int good, int barrels)
        {
            RequireGood(good);
            RequirePositive(barrels);

            if (barrels > BarrelsOf(good))
            {
                return false;
            }

            barrelsByGood[good] -= barrels;
            Used -= barrels;
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

        private static void RequirePositive(int barrels)
        {
            if (barrels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }
        }
    }
}
