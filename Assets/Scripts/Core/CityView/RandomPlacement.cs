using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Chooses, with equal chance, any place where the footprint fits, inside its area when it has one; it lays no road.
    /// </summary>
    public sealed class RandomPlacement : IPlacementStrategy
    {
        private readonly System.Random random;
        private readonly RectInt? area;
        private readonly List<GridPlace> places = new List<GridPlace>();

        public RandomPlacement(System.Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <param name="area">The cells a chosen place must lie in, whole: a place partly outside is never chosen.</param>
        public RandomPlacement(System.Random random, RectInt area)
            : this(random)
        {
            this.area = area;
        }

        public bool TryChoose(CityGrid grid, Footprint footprint, out GridPlace place, List<Vector2Int> roads)
        {
            roads.Clear();
            grid.GetPlaces(footprint, places);
            if (area.HasValue)
            {
                RectInt inside = area.Value;
                places.RemoveAll(candidate => !IsInside(inside, candidate.Origin, footprint.SizeFor(candidate.Orientation)));
            }

            if (places.Count == 0)
            {
                place = default;
                return false;
            }

            place = places[random.Next(places.Count)];
            return true;
        }

        private static bool IsInside(RectInt inside, Vector2Int origin, Vector2Int size)
        {
            return origin.x >= inside.xMin && origin.y >= inside.yMin
                && origin.x + size.x <= inside.xMax && origin.y + size.y <= inside.yMax;
        }
    }
}
