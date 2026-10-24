using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Chooses, with equal chance, any place where the footprint fits; it lays no road.</summary>
    public sealed class RandomPlacement : IPlacementStrategy
    {
        private readonly System.Random random;
        private readonly List<GridPlace> places = new List<GridPlace>();

        public RandomPlacement(System.Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public bool TryChoose(CityGrid grid, Footprint footprint, out GridPlace place, List<Vector2Int> roads)
        {
            roads.Clear();
            grid.GetPlaces(footprint, places);
            if (places.Count == 0)
            {
                place = default;
                return false;
            }

            place = places[random.Next(places.Count)];
            return true;
        }
    }
}
