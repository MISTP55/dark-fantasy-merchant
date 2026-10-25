using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Finds where a city's port stands when nobody has chosen.</summary>
    public static class PortPlacement
    {
        private static readonly PortFacing[] Facings = { PortFacing.North, PortFacing.East, PortFacing.South, PortFacing.West };

        /// <summary>
        /// The site where the port can stand whose centre is nearest to <paramref name="centre"/>, in cells; a tie goes
        /// to the first by x, then y, then facing (north, east, south, west).
        /// </summary>
        /// <returns>False when the port can stand nowhere.</returns>
        public static bool TryNear(CityGrid grid, PortFootprint footprint, Vector2 centre, out PortSite site)
        {
            site = default;
            float best = float.MaxValue;
            bool found = false;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    foreach (PortFacing facing in Facings)
                    {
                        var candidate = new PortSite(new Vector2Int(x, y), facing);
                        if (!grid.CanPlacePort(footprint, candidate))
                        {
                            continue;
                        }

                        Vector2Int size = footprint.Footprint.SizeFor(candidate.Place.Orientation);
                        var middle = new Vector2(x + size.x / 2f, y + size.y / 2f);
                        float distance = (middle - centre).sqrMagnitude;
                        if (distance < best)
                        {
                            best = distance;
                            site = candidate;
                            found = true;
                        }
                    }
                }
            }

            return found;
        }
    }
}
