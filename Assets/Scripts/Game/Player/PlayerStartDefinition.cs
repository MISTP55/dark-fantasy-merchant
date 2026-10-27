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

        [Tooltip("The player's home city: the player ship starts in its port and the game opens on its view. "
            + "Empty, or an inactive city, uses the first active city of the map.")]
        [SerializeField] private CityDefinition homeCity;

        public long StartingGold => startingGold;

        public int StartingCrew => startingCrew;

        public CityDefinition HomeCity => homeCity;

        /// <summary>
        /// The home city the player starts in on <paramref name="map"/>: the start's, or the first active city of
        /// the map when the start has none, or an inactive one; null when the map has no city.
        /// </summary>
        /// <param name="start">Optional: without it, the first active city.</param>
        public static CityDefinition HomeCityOn(PlayerStartDefinition start, WorldMapView map)
        {
            // An inactive city does not exist in the game: the player cannot start there.
            if (start != null && map.HasCity(start.homeCity))
            {
                return start.homeCity;
            }

            return map.Cities.Count > 0 ? map.Cities[0] : null;
        }

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
