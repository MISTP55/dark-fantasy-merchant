using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// The editing operations of a city's layout. Each records an undo and marks the asset dirty.
    /// The footprint of a house is the catalogue's, so the operations that need it take the catalogue.
    /// </summary>
    public static class CityLayoutEditing
    {
        /// <summary>The footprint of the house of the class in the catalogue.</summary>
        public static Footprint HouseFootprint(BuildingsDefinition buildings, SocialClass housedClass)
        {
            return buildings.HouseOf(housedClass).Footprint;
        }

        /// <summary>Gives the layout the size; the forbidden cells and the houses that fall outside are dropped.</summary>
        public static void Resize(CityLayoutDefinition layout, BuildingsDefinition buildings, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            var forbidden = new List<Vector2Int>();

            foreach (Vector2Int cell in layout.ForbiddenCells)
            {
                if (cell.x < width && cell.y < height)
                {
                    forbidden.Add(cell);
                }
            }

            var houses = new List<CityLayoutDefinition.StartingHouse>();

            foreach (CityLayoutDefinition.StartingHouse house in layout.StartingHouses)
            {
                Vector2Int size = HouseFootprint(buildings, house.socialClass).SizeFor(house.orientation);

                if (house.origin.x + size.x <= width && house.origin.y + size.y <= height)
                {
                    houses.Add(house);
                }
            }

            GridPlace? townHall = null;

            if (layout.HasTownHall)
            {
                GridPlace hall = layout.TownHallPlace;
                Vector2Int hallSize = TownHallSize(buildings, hall);

                if (hall.Origin.x + hallSize.x <= width && hall.Origin.y + hallSize.y <= height)
                {
                    townHall = hall;
                }
            }

            var roads = new List<Vector2Int>();

            foreach (Vector2Int road in layout.Roads)
            {
                if (road.x < width && road.y < height)
                {
                    roads.Add(road);
                }
            }

            Apply(layout, "Resize city layout", width, height, forbidden, houses, townHall, roads);
        }

        /// <summary>The cells the town hall covers when placed with this orientation.</summary>
        public static Vector2Int TownHallSize(BuildingsDefinition buildings, GridPlace place)
        {
            return buildings.TownHall.Footprint.SizeFor(place.Orientation);
        }

        /// <summary>
        /// Whether the town hall can stand at the place: inside the grid, on no forbidden cell, road or house. Its
        /// own current place does not count.
        /// </summary>
        public static bool CanPlaceTownHall(CityLayoutDefinition layout, BuildingsDefinition buildings, GridPlace place)
        {
            Vector2Int size = TownHallSize(buildings, place);

            if (size.x < 1 || size.y < 1)
            {
                return false;
            }

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var cell = new Vector2Int(place.Origin.x + x, place.Origin.y + y);

                    if (!IsInside(layout, cell) || IsForbiddenIn(layout, cell)
                        || HouseIndexAt(layout, buildings, cell) >= 0 || IsRoadIn(layout, cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Places the town hall or moves it; false, and nothing recorded, when it cannot stand there.</summary>
        public static bool SetTownHall(CityLayoutDefinition layout, BuildingsDefinition buildings, GridPlace place)
        {
            if (!CanPlaceTownHall(layout, buildings, place))
            {
                return false;
            }

            if (layout.HasTownHall && layout.TownHallPlace.Equals(place))
            {
                return true;
            }

            Apply(layout, "Place town hall", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses, place, layout.Roads);
            return true;
        }

        /// <summary>Takes the town hall away; records nothing when there is none.</summary>
        public static void RemoveTownHall(CityLayoutDefinition layout)
        {
            if (!layout.HasTownHall)
            {
                return;
            }

            Apply(layout, "Remove town hall", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses, null, layout.Roads);
        }

        /// <summary>
        /// Adds roads or removes them. The cells outside the grid, forbidden, or under the town hall or a house are
        /// skipped. Records nothing when no cell changes.
        /// </summary>
        public static void SetRoads(CityLayoutDefinition layout, BuildingsDefinition buildings, IEnumerable<Vector2Int> cells, bool add)
        {
            var roads = new List<Vector2Int>(layout.Roads);
            bool changed = false;

            foreach (Vector2Int cell in cells)
            {
                if (!IsInside(layout, cell))
                {
                    continue;
                }

                if (!add)
                {
                    changed |= roads.Remove(cell);
                }
                else if (!roads.Contains(cell) && !IsForbiddenIn(layout, cell)
                    && HouseIndexAt(layout, buildings, cell) < 0 && !IsUnderTownHall(layout, buildings, cell))
                {
                    roads.Add(cell);
                    changed = true;
                }
            }

            if (changed)
            {
                Apply(
                    layout, add ? "Lay roads" : "Remove roads", layout.Width, layout.Height,
                    layout.ForbiddenCells, layout.StartingHouses, TownHallOf(layout), roads);
            }
        }

        /// <summary>
        /// Places the town hall when the layout has none (nearest to the mean centre of the starting houses, to the
        /// grid's centre without houses), then lays the roads that connect each starting house not yet connected, in
        /// list order. Records nothing when nothing changes.
        /// </summary>
        /// <returns>The number of starting houses left unconnected: walled in, no town hall could stand, or the layout's content overlaps (nothing is changed then).</returns>
        public static int AutoConnect(CityLayoutDefinition layout, BuildingsDefinition buildings)
        {
            // A hand-edited layout whose content overlaps cannot be placed on a grid: leave it as it is.
            if (!TryMakeGrid(layout, buildings, out _))
            {
                return layout.StartingHouses.Count;
            }

            GridPlace? townHall = TownHallOf(layout);
            var roads = new List<Vector2Int>(layout.Roads);

            if (townHall == null)
            {
                var bare = new CityGrid(layout.Width, layout.Height, layout.ForbiddenCells);
                var centre = new Vector2(layout.Width / 2f, layout.Height / 2f);

                if (layout.StartingHouses.Count > 0)
                {
                    centre = Vector2.zero;

                    foreach (CityLayoutDefinition.StartingHouse house in layout.StartingHouses)
                    {
                        Vector2Int size = HouseFootprint(buildings, house.socialClass).SizeFor(house.orientation);
                        centre += new Vector2(house.origin.x + size.x / 2f, house.origin.y + size.y / 2f);
                    }

                    centre /= layout.StartingHouses.Count;
                }

                foreach (CityLayoutDefinition.StartingHouse house in layout.StartingHouses)
                {
                    bare.Place(
                        BuildingKind.House(house.socialClass), BuildingOwner.City,
                        HouseFootprint(buildings, house.socialClass), new GridPlace(house.origin, house.orientation));
                }

                foreach (Vector2Int road in roads)
                {
                    PlaceRoad(bare, road);
                }

                if (!TownHallPlacement.TryNear(bare, buildings.TownHall.Footprint, centre, out GridPlace nearest))
                {
                    return layout.StartingHouses.Count;
                }

                townHall = nearest;
            }

            var grid = new CityGrid(layout.Width, layout.Height, layout.ForbiddenCells);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, buildings.TownHall.Footprint, townHall.Value);

            foreach (Vector2Int road in roads)
            {
                PlaceRoad(grid, road);
            }

            var placed = new List<PlacedBuilding>();

            foreach (CityLayoutDefinition.StartingHouse house in layout.StartingHouses)
            {
                placed.Add(grid.Place(
                    BuildingKind.House(house.socialClass), BuildingOwner.City,
                    HouseFootprint(buildings, house.socialClass), new GridPlace(house.origin, house.orientation)));
            }

            int unconnected = 0;
            int existingRoads = roads.Count;
            var found = new List<Vector2Int>();

            foreach (PlacedBuilding house in placed)
            {
                if (house.IsConnected)
                {
                    continue;
                }

                var cells = new List<Vector2Int>();

                for (int y = 0; y < house.Size.y; y++)
                {
                    for (int x = 0; x < house.Size.x; x++)
                    {
                        cells.Add(new Vector2Int(house.Place.Origin.x + x, house.Place.Origin.y + y));
                    }
                }

                if (!RoadPlanner.TryFindRoads(grid, cells, found))
                {
                    unconnected++;
                    continue;
                }

                foreach (Vector2Int road in found)
                {
                    PlaceRoad(grid, road);
                    roads.Add(road);
                }
            }

            if (!layout.HasTownHall || roads.Count != existingRoads)
            {
                Apply(
                    layout, "Town hall and roads", layout.Width, layout.Height,
                    layout.ForbiddenCells, layout.StartingHouses, townHall, roads);
            }

            return unconnected;
        }

        /// <summary>Whether the layout's town hall covers the cell.</summary>
        public static bool IsUnderTownHall(CityLayoutDefinition layout, BuildingsDefinition buildings, Vector2Int cell)
        {
            if (!layout.HasTownHall)
            {
                return false;
            }

            GridPlace hall = layout.TownHallPlace;
            Vector2Int size = TownHallSize(buildings, hall);

            return cell.x >= hall.Origin.x && cell.x < hall.Origin.x + size.x
                && cell.y >= hall.Origin.y && cell.y < hall.Origin.y + size.y;
        }

        private static GridPlace? TownHallOf(CityLayoutDefinition layout)
        {
            return layout.HasTownHall ? layout.TownHallPlace : (GridPlace?)null;
        }

        private static bool IsForbiddenIn(CityLayoutDefinition layout, Vector2Int cell)
        {
            foreach (Vector2Int forbidden in layout.ForbiddenCells)
            {
                if (forbidden == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRoadIn(CityLayoutDefinition layout, Vector2Int cell)
        {
            foreach (Vector2Int road in layout.Roads)
            {
                if (road == cell)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Forbids the cells or frees them. The cells outside the grid, and when forbidding those a house covers, are
        /// skipped. Records nothing when no cell changes.
        /// </summary>
        public static void SetForbidden(CityLayoutDefinition layout, BuildingsDefinition buildings, IEnumerable<Vector2Int> cells, bool forbid)
        {
            var forbidden = new List<Vector2Int>(layout.ForbiddenCells);
            bool changed = false;

            foreach (Vector2Int cell in cells)
            {
                if (!IsInside(layout, cell))
                {
                    continue;
                }

                if (!forbid)
                {
                    changed |= forbidden.Remove(cell);
                }
                else if (!forbidden.Contains(cell) && HouseIndexAt(layout, buildings, cell) < 0
                    && !IsUnderTownHall(layout, buildings, cell) && !IsRoadIn(layout, cell))
                {
                    forbidden.Add(cell);
                    changed = true;
                }
            }

            if (changed)
            {
                Apply(layout, forbid ? "Forbid cells" : "Free cells", layout.Width, layout.Height, forbidden, layout.StartingHouses);
            }
        }

        /// <summary>Adds a starting house; false when it does not fit, or the layout cannot make its grid.</summary>
        public static bool TryAddHouse(CityLayoutDefinition layout, BuildingsDefinition buildings, SocialClass housedClass, GridPlace place)
        {
            if (!TryMakeGrid(layout, buildings, out CityGrid grid)
                || !grid.CanPlace(HouseFootprint(buildings, housedClass), place))
            {
                return false;
            }

            var houses = new List<CityLayoutDefinition.StartingHouse>(layout.StartingHouses)
            {
                new CityLayoutDefinition.StartingHouse(housedClass, place.Origin, place.Orientation),
            };

            Apply(layout, "Add starting house", layout.Width, layout.Height, layout.ForbiddenCells, houses);
            return true;
        }

        /// <summary>
        /// Removes the starting houses covering any of the cells, whichever of their cells it is; only those of
        /// <paramref name="onlyClass"/> when it is given. Records nothing when no house is removed.
        /// </summary>
        public static void RemoveHouses(
            CityLayoutDefinition layout, BuildingsDefinition buildings, IEnumerable<Vector2Int> cells, SocialClass? onlyClass = null)
        {
            var removed = new HashSet<int>();

            foreach (Vector2Int cell in cells)
            {
                int index = HouseIndexAt(layout, buildings, cell);

                if (index >= 0 && (onlyClass == null || layout.StartingHouses[index].socialClass == onlyClass.Value))
                {
                    removed.Add(index);
                }
            }

            if (removed.Count == 0)
            {
                return;
            }

            var houses = new List<CityLayoutDefinition.StartingHouse>();

            for (int i = 0; i < layout.StartingHouses.Count; i++)
            {
                if (!removed.Contains(i))
                {
                    houses.Add(layout.StartingHouses[i]);
                }
            }

            Apply(layout, "Remove starting houses", layout.Width, layout.Height, layout.ForbiddenCells, houses);
        }

        private static readonly Footprint RoadFootprint = new Footprint(1, 1);

        private static GridPlace RoadPlaceAt(Vector2Int cell)
        {
            return new GridPlace(cell, BuildingOrientation.Along);
        }

        private static void PlaceRoad(CityGrid grid, Vector2Int cell)
        {
            grid.Place(BuildingKind.Road(), BuildingOwner.City, RoadFootprint, RoadPlaceAt(cell));
        }

        /// <summary>
        /// The grid of the layout's content, without requiring a town hall (a layout being edited may have none yet);
        /// false when something in it cannot stand.
        /// </summary>
        public static bool TryMakeGrid(CityLayoutDefinition layout, BuildingsDefinition buildings, out CityGrid grid)
        {
            grid = new CityGrid(layout.Width, layout.Height, layout.ForbiddenCells);

            if (layout.HasTownHall)
            {
                Footprint hall = buildings.TownHall.Footprint;

                if (!grid.CanPlace(hall, layout.TownHallPlace))
                {
                    return false;
                }

                grid.Place(BuildingKind.TownHall(), BuildingOwner.City, hall, layout.TownHallPlace);
            }

            foreach (Vector2Int road in layout.Roads)
            {
                if (!grid.CanPlace(RoadFootprint, RoadPlaceAt(road)))
                {
                    return false;
                }

                PlaceRoad(grid, road);
            }

            foreach (CityLayoutDefinition.StartingHouse house in layout.StartingHouses)
            {
                Footprint footprint = HouseFootprint(buildings, house.socialClass);
                var housePlace = new GridPlace(house.origin, house.orientation);

                if (!grid.CanPlace(footprint, housePlace))
                {
                    return false;
                }

                grid.Place(BuildingKind.House(house.socialClass), BuildingOwner.City, footprint, housePlace);
            }

            return true;
        }

        /// <summary>The index of the starting house covering the cell; -1 when there is none.</summary>
        public static int HouseIndexAt(CityLayoutDefinition layout, BuildingsDefinition buildings, Vector2Int cell)
        {
            for (int i = 0; i < layout.StartingHouses.Count; i++)
            {
                CityLayoutDefinition.StartingHouse house = layout.StartingHouses[i];
                Vector2Int size = HouseFootprint(buildings, house.socialClass).SizeFor(house.orientation);

                if (cell.x >= house.origin.x && cell.x < house.origin.x + size.x
                    && cell.y >= house.origin.y && cell.y < house.origin.y + size.y)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// The cells of a brush of <paramref name="size"/> cells across around the cell (one cell more on the low side when
        /// the size is even), cut to a grid of the size given. A round brush keeps the cells whose centre is close enough
        /// to the brush's: 3 across is a cross, 2 across a full square.
        /// </summary>
        public static List<Vector2Int> BrushCells(Vector2Int centre, int size, bool round, int width, int height)
        {
            size = Mathf.Max(1, size);
            int below = size / 2;

            // The brush's centre, in cells from its low corner; a cell is in when its centre is within the reach of it.
            float radius = (size - 1) / 2f;
            float reach = radius * radius + radius / 2f;
            var cells = new List<Vector2Int>();

            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    var cell = new Vector2Int(centre.x - below + dx, centre.y - below + dy);
                    float offX = dx - radius;
                    float offY = dy - radius;

                    if (cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height
                        && (!round || offX * offX + offY * offY <= reach))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        private static bool IsInside(CityLayoutDefinition layout, Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < layout.Width && cell.y < layout.Height;
        }

        private static void Apply(
            CityLayoutDefinition layout, string undoName, int width, int height,
            IEnumerable<Vector2Int> forbidden, IEnumerable<CityLayoutDefinition.StartingHouse> houses)
        {
            Apply(layout, undoName, width, height, forbidden, houses, TownHallOf(layout), layout.Roads);
        }

        private static void Apply(
            CityLayoutDefinition layout, string undoName, int width, int height,
            IEnumerable<Vector2Int> forbidden, IEnumerable<CityLayoutDefinition.StartingHouse> houses,
            GridPlace? townHall, IEnumerable<Vector2Int> roads)
        {
            Undo.RecordObject(layout, undoName);
            layout.SetContent(width, height, forbidden, houses, townHall, new List<Vector2Int>(roads));
            EditorUtility.SetDirty(layout);
        }
    }
}
