using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Which side of a city's grid the water is on, from where the water lies around the city on the map.</summary>
    public static class ShoreSide
    {
        /// <summary>
        /// The side, among north (up the map), east, south and west, towards which the navigable cells within the
        /// radius lie on average; the larger of the two axes wins, north and south on a tie. South when the mask is
        /// missing or no navigable cell is near.
        /// </summary>
        /// <param name="position">The city on the map, normalized.</param>
        public static PortFacing Of(NavigationGrid mask, Vector2 position, float radiusInCells)
        {
            if (mask == null)
            {
                return PortFacing.South;
            }

            int cityX = Mathf.FloorToInt(position.x * mask.Width);
            int cityY = Mathf.FloorToInt(position.y * mask.Height);
            int reach = Mathf.CeilToInt(radiusInCells);
            float radiusSquared = radiusInCells * radiusInCells;
            long sumX = 0;
            long sumY = 0;

            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    if (dx * dx + dy * dy <= radiusSquared && mask.IsNavigable(cityX + dx, cityY + dy))
                    {
                        sumX += dx;
                        sumY += dy;
                    }
                }
            }

            if (sumX == 0 && sumY == 0)
            {
                return PortFacing.South;
            }

            if (System.Math.Abs(sumX) > System.Math.Abs(sumY))
            {
                return sumX > 0 ? PortFacing.East : PortFacing.West;
            }

            return sumY > 0 ? PortFacing.North : PortFacing.South;
        }
    }
}
