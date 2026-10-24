using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Finds where a city's town hall stands when nobody has chosen.</summary>
    public static class TownHallPlacement
    {
        /// <summary>
        /// The free place whose centre is nearest to <paramref name="centre"/>, in cells; a tie goes to the first
        /// in the order of <see cref="CityGrid.GetPlaces"/>.
        /// </summary>
        /// <returns>False when the footprint fits nowhere.</returns>
        public static bool TryNear(CityGrid grid, Footprint footprint, Vector2 centre, out GridPlace place)
        {
            var places = new List<GridPlace>();
            grid.GetPlaces(footprint, places);
            place = default;
            float best = float.MaxValue;
            bool found = false;
            foreach (GridPlace candidate in places)
            {
                Vector2Int size = footprint.SizeFor(candidate.Orientation);
                Vector2 middle = new Vector2(candidate.Origin.x + size.x / 2f, candidate.Origin.y + size.y / 2f);
                float distance = (middle - centre).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    place = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
