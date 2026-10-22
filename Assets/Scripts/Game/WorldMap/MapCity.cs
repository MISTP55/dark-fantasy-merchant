using System;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// A city of the world map and whether it is active. A city that is not active does not
    /// exist in the game: it has no marker, no market and no AI ships.
    /// </summary>
    [Serializable]
    public struct MapCity
    {
        [SerializeField] private CityDefinition city;

        [SerializeField] private bool active;

        public MapCity(CityDefinition city, bool active)
        {
            this.city = city;
            this.active = active;
        }

        public CityDefinition City => city;

        public bool Active => active;
    }
}
