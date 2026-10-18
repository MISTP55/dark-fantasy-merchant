using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of what the player starts the game with. Holds no runtime state:
    /// the current gold is in a <see cref="Treasury"/>, the sailors in a <see cref="ShipCrew"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStart", menuName = "Dark Fantasy Merchant/Player Start")]
    public sealed class PlayerStartDefinition : ScriptableObject
    {
        [Tooltip("Gold coins in the treasury on the first day. Negative to start in debt.")]
        [SerializeField] private long startingGold = 10000;

        [Tooltip("Sailors aboard the player ship on the first day.")]
        [SerializeField, Min(0)] private int startingCrew = 12;

        public long StartingGold => startingGold;

        public int StartingCrew => startingCrew;

        /// <summary>
        /// The crew the player ship starts with, in a ship with room for
        /// <paramref name="capacity"/> sailors: the starting crew, or as many of it as
        /// the ship has room for.
        /// </summary>
        public ShipCrew CreateShipCrew(int capacity)
        {
            return new ShipCrew(capacity, Mathf.Clamp(startingCrew, 0, capacity));
        }

        public Treasury CreateTreasury()
        {
            return new Treasury(startingGold);
        }
    }
}
