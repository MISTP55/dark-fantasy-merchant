using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Finds where a generated city reserves the site of the player's trading post.</summary>
    public static class TradingPostPlacement
    {
        /// <summary>
        /// The free place whose middle is nearest to <paramref name="centre"/>, in cells, among those whose footprint
        /// grown by one cell on every side lies inside the grid with every cell of that ring buildable
        /// (<see cref="CityGrid.IsBuildable"/>) and free; a tie goes to the first in the order of
        /// <see cref="CityGrid.GetPlaces"/>. The ring keeps the site off the town hall's sides, open for the roads.
        /// </summary>
        /// <returns>False when no place has such a ring.</returns>
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
                if (distance < best && HasFreeRing(grid, candidate.Origin, size))
                {
                    best = distance;
                    place = candidate;
                    found = true;
                }
            }

            return found;
        }

        // The footprint is free already (GetPlaces): only the ring around it is tested.
        private static bool HasFreeRing(CityGrid grid, Vector2Int origin, Vector2Int size)
        {
            for (int y = origin.y - 1; y <= origin.y + size.y; y++)
            {
                for (int x = origin.x - 1; x <= origin.x + size.x; x++)
                {
                    bool inside = x >= origin.x && x < origin.x + size.x && y >= origin.y && y < origin.y + size.y;
                    var cell = new Vector2Int(x, y);
                    if (!inside && (!grid.IsBuildable(cell) || grid.BuildingAt(cell) != null))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
