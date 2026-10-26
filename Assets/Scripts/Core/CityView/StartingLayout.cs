using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Makes a whole city grid from nothing: its water, its port, its berths, its town hall, its walls, then its houses with their roads.</summary>
    public static class StartingLayout
    {
        /// <summary>
        /// A grid with a band of water along the shore's edge, the port on the middle of that shore facing the water,
        /// the town hall inland of the port, walls on the outline of the square of wallSide cells centred on it, then houses[c] houses of each class drawn around it, each connected by
        /// roads (<see cref="StartingHouses.TryPlace(CityGrid, PerClass{int}, PerClass{Footprint}, RectInt, System.Random)"/>).
        /// </summary>
        /// <param name="berths">How many berths to place on the water nearest the port (<see cref="StartingBerths.Place"/>).</param>
        /// <param name="shore">The edge the water runs along.</param>
        /// <param name="waterRows">The depth of the band of water, in cells.</param>
        /// <param name="townHallDistance">
        /// The rows of land between the water and the town hall, in the middle of the shore and lying as the port does
        /// (its width along the shore); nearest that place when it is taken or off the grid.
        /// </param>
        /// <param name="houseArea">
        /// The side, in cells, of the square centred on the town hall that the houses are drawn in at random.
        /// </param>
        /// <param name="wallSide">
        /// The side, in cells, of the square centred on the town hall whose outline is walled, on every cell of it that
        /// is inside the grid, land and free; the water closes it where it crosses the water. The houses and their roads
        /// stand inside, and the grid returned keeps inside the walls (<see cref="CityGrid.KeepsInsideWalls"/>).
        /// </param>
        /// <param name="problem">What did not fit, for the logs; null when everything did.</param>
        /// <returns>False, and no grid, when the port, a berth, the town hall or a house does not fit, or the walls do not close.</returns>
        public static bool TryBuild(
            int width, int height, PortFacing shore, int waterRows, int townHallDistance, int houseArea, int wallSide,
            PortFootprint port, int berths, Footprint townHall, PerClass<int> houses, PerClass<Footprint> houseFootprints,
            RoadContact contact, System.Random random,
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

            if (StartingBerths.Place(created, berths) == 0)
            {
                problem = $"its port is not joined to the edge of a {width} by {height} grid";
                return false;
            }

            GridPlace hall = TownHallPlace(shore, waterRows + townHallDistance, townHall, width, height);
            if (!created.CanPlace(townHall, hall) && !TownHallPlacement.TryNear(created, townHall, CentreOf(hall, townHall), out hall))
            {
                problem = $"its town hall does not fit on a {width} by {height} grid";
                return false;
            }

            created.Place(BuildingKind.TownHall(), BuildingOwner.City, townHall, hall);

            var wall = new Footprint(1, 1);
            foreach (Vector2Int cell in Outline(SquareAround(created.TownHall, wallSide)))
            {
                var place = new GridPlace(cell, BuildingOrientation.Along);
                if (created.CanPlace(wall, place))
                {
                    created.Place(BuildingKind.Wall(), BuildingOwner.City, wall, place);
                }
            }

            // The port stands already: walls that close short of it would leave it outside.
            if (!created.TryValidateWalls(out problem))
            {
                return false;
            }

            created.KeepsInsideWalls = true;

            if (!StartingHouses.TryPlace(created, houses, houseFootprints, SquareAround(created.TownHall, houseArea), random))
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

        // The town hall whose side nearest the shore is `fromEdge` cells from the shore's edge, its width along the
        // shore, in the middle of it; a cell nearer the origin when it cannot be exactly in the middle.
        private static GridPlace TownHallPlace(PortFacing shore, int fromEdge, Footprint townHall, int width, int height)
        {
            BuildingOrientation orientation = PortFacings.OrientationOf(shore);
            Vector2Int size = townHall.SizeFor(orientation);
            switch (shore)
            {
                case PortFacing.North:
                    return new GridPlace(new Vector2Int((width - size.x) / 2, height - fromEdge - size.y), orientation);
                case PortFacing.South:
                    return new GridPlace(new Vector2Int((width - size.x) / 2, fromEdge), orientation);
                case PortFacing.East:
                    return new GridPlace(new Vector2Int(width - fromEdge - size.x, (height - size.y) / 2), orientation);
                default:
                    return new GridPlace(new Vector2Int(fromEdge, (height - size.y) / 2), orientation);
            }
        }

        private static Vector2 CentreOf(GridPlace place, Footprint footprint)
        {
            Vector2Int size = footprint.SizeFor(place.Orientation);
            return new Vector2(place.Origin.x + size.x / 2f, place.Origin.y + size.y / 2f);
        }

        // The square of `side` cells centred on the town hall; a cell more on the far side when they do not split evenly.
        private static RectInt SquareAround(PlacedBuilding townHall, int side)
        {
            Vector2Int size = townHall.Size;
            Vector2Int origin = townHall.Place.Origin;
            int x = origin.x + (size.x - side) / 2;
            int y = origin.y + (size.y - side) / 2;
            return new RectInt(x, y, side, side);
        }

        // The cells on the rectangle's border.
        private static IEnumerable<Vector2Int> Outline(RectInt rect)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (x == rect.xMin || y == rect.yMin || x == rect.xMax - 1 || y == rect.yMax - 1)
                    {
                        yield return new Vector2Int(x, y);
                    }
                }
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
