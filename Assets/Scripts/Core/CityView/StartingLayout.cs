using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Makes a whole city grid from nothing: its water, its port, its town hall, then its houses with their roads.</summary>
    public static class StartingLayout
    {
        /// <summary>
        /// A grid with a band of water along the shore's edge, the port on the middle of that shore facing the water,
        /// the town hall near the centre, then houses[c] houses of each class, each connected by roads
        /// (<see cref="StartingHouses.TryPlace"/>).
        /// </summary>
        /// <param name="shore">The edge the water runs along.</param>
        /// <param name="waterRows">The depth of the band of water, in cells.</param>
        /// <param name="problem">What did not fit, for the logs; null when everything did.</param>
        /// <returns>False, and no grid, when the port, the town hall or a house does not fit.</returns>
        public static bool TryBuild(
            int width, int height, PortFacing shore, int waterRows, PortFootprint port, Footprint townHall,
            PerClass<int> houses, PerClass<Footprint> houseFootprints, RoadContact contact, System.Random random,
            out CityGrid grid, out string problem)
        {
            grid = null;
            var water = new List<Vector2Int>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsInBand(shore, waterRows, width, height, x, y))
                    {
                        water.Add(new Vector2Int(x, y));
                    }
                }
            }

            var created = new CityGrid(width, height, water, contact);

            if (!PortPlacement.TryNear(created, port, ShoreMiddle(shore, waterRows, width, height), out PortSite site))
            {
                problem = $"its port does not fit on the shore of a {width} by {height} grid";
                return false;
            }

            created.PlacePort(port, site);

            if (!TownHallPlacement.TryNear(created, townHall, new Vector2(width / 2f, height / 2f), out GridPlace hall))
            {
                problem = $"its town hall does not fit on a {width} by {height} grid";
                return false;
            }

            created.Place(BuildingKind.TownHall(), BuildingOwner.City, townHall, hall);

            if (!StartingHouses.TryPlace(created, houses, houseFootprints, random))
            {
                problem = $"its starting houses do not fit on a {width} by {height} grid";
                return false;
            }

            grid = created;
            problem = null;
            return true;
        }

        private static bool IsInBand(PortFacing shore, int waterRows, int width, int height, int x, int y)
        {
            switch (shore)
            {
                case PortFacing.North:
                    return y >= height - waterRows;
                case PortFacing.South:
                    return y < waterRows;
                case PortFacing.East:
                    return x >= width - waterRows;
                default:
                    return x < waterRows;
            }
        }

        // The middle of the line between the water and the land.
        private static Vector2 ShoreMiddle(PortFacing shore, int waterRows, int width, int height)
        {
            switch (shore)
            {
                case PortFacing.North:
                    return new Vector2(width / 2f, height - waterRows);
                case PortFacing.South:
                    return new Vector2(width / 2f, waterRows);
                case PortFacing.East:
                    return new Vector2(width - waterRows, height / 2f);
                default:
                    return new Vector2(waterRows, height / 2f);
            }
        }
    }
}
