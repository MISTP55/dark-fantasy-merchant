using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The room a unit of each good takes in a hold, in barrels: one for a good counted in
    /// barrels, more for one counted in bushels. Goods are indices into the catalogue; a
    /// good beyond the list takes one barrel.
    /// </summary>
    public sealed class GoodUnits
    {
        private readonly int[] rooms;

        /// <param name="rooms">Barrels of room a unit of each good takes, by index, at least 1. Copied.</param>
        public GoodUnits(IReadOnlyList<int> rooms)
        {
            if (rooms == null)
            {
                throw new ArgumentNullException(nameof(rooms));
            }

            this.rooms = new int[rooms.Count];

            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(rooms), "A unit takes at least one barrel of room.");
                }

                this.rooms[i] = rooms[i];
            }
        }

        /// <summary>Every good counted in barrels.</summary>
        public static GoodUnits AllBarrels { get; } = new GoodUnits(new int[0]);

        public int Count => rooms.Length;

        /// <summary>Barrels of room a unit of a good takes.</summary>
        public int RoomOf(int good)
        {
            if (good < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }

            return good < rooms.Length ? rooms[good] : 1;
        }
    }
}
