using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Moving goods from one store to another, free of charge.</summary>
    public static class Transfer
    {
        /// <summary>
        /// Moves up to <paramref name="units"/> units of a good: as many as the source holds
        /// and the destination has room for.
        /// </summary>
        /// <returns>The units moved; when none, nothing changed and nothing was raised.</returns>
        public static int Move(IGoodStore from, IGoodStore to, int good, int units)
        {
            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (to == null)
            {
                throw new ArgumentNullException(nameof(to));
            }

            int moved = Math.Min(units, Math.Min(from.UnitsOf(good), to.RoomFor(good)));

            if (moved < 1)
            {
                return 0;
            }

            from.TryRemove(good, moved);
            to.TryAdd(good, moved);
            return moved;
        }
    }
}
