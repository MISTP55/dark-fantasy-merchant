using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The goods stored in a building, out of a capacity that limits nothing: what is stored
    /// above it is the excess, which is charged a rent. Goods are counted in their own unit;
    /// the room they take is counted in barrels, a bushel taking several.
    /// </summary>
    public sealed class Warehouse : GoodInventory, IGoodStore
    {
        /// <param name="capacity">Barrels stored free of rent.</param>
        /// <param name="units">The room a unit of each good takes. Without it, every unit takes one barrel.</param>
        public Warehouse(int capacity, GoodUnits units = null)
            : base(units)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
        }

        /// <summary>Barrels stored free of rent.</summary>
        public int Capacity { get; }

        /// <summary>Barrels of room taken, all goods together; may be far above the capacity.</summary>
        public long Used => (long)RoomTaken;

        /// <summary>Barrels stored above the capacity.</summary>
        public long Excess => Math.Max(0, Used - Capacity);

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
    }
}
