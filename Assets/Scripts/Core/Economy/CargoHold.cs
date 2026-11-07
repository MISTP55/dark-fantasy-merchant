using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The goods aboard one ship, out of the room it has. Goods are indices into the
    /// catalogue and are counted in their own unit, a barrel or a bushel; the room they
    /// take is counted in barrels, a bushel taking several.
    /// </summary>
    public sealed class CargoHold : GoodInventory, IGoodStore
    {
        /// <param name="capacity">Barrels of room; zero for a ship that carries nothing.</param>
        /// <param name="units">The room a unit of each good takes. Without it, every unit takes one barrel.</param>
        public CargoHold(int capacity, GoodUnits units = null)
            : base(units)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
        }

        /// <summary>Barrels of room.</summary>
        public int Capacity { get; }

        /// <summary>Barrels of room taken, all goods together.</summary>
        public int Used => (int)RoomTaken;

        public int Free => Capacity - Used;

        /// <summary>The whole units of a good the free room still takes.</summary>
        public override int RoomFor(int good)
        {
            RequireGood(good);

            return Free / RoomOf(good);
        }
    }
}
