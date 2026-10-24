using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Chooses, with equal chance, any place where the footprint fits.</summary>
    public sealed class RandomPlacement : IPlacementStrategy
    {
        private readonly Random random;
        private readonly List<GridPlace> places = new List<GridPlace>();

        public RandomPlacement(Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public bool TryChoose(CityGrid grid, Footprint footprint, out GridPlace place)
        {
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
