using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Goods that can be taken from, counted in the units of each good.</summary>
    public interface IGoodSource
    {
        /// <summary>Units of a good held.</summary>
        int UnitsOf(int good);

        /// <returns>False when fewer are held; nothing was removed then.</returns>
        bool TryRemove(int good, int count);
    }

    /// <summary>Goods that can be taken from and added to, within the room the store has.</summary>
    public interface IGoodStore : IGoodSource
    {
        /// <summary>The whole units of a good the store still takes.</summary>
        int RoomFor(int good);

        /// <returns>False when there is no room for all of them; nothing was added then.</returns>
        bool TryAdd(int good, int count);

        /// <summary>Raised when goods were added or removed, not by a refusal.</summary>
        event Action Changed;
    }
}
