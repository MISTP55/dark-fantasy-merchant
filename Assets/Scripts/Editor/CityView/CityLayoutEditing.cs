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

        /// <summary>Gives the layout the size; the water cells, the houses, the town hall, the port, the roads and the walls that fall outside are dropped.</summary>
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

            PortSite? port = null;

            if (layout.HasPort)
            {
                PortSite site = layout.PortSite;

                // Without a usable port in the catalogue, its size is unknown: it is kept as it is.
                if (!TryGetPortFootprint(buildings, out PortFootprint footprint)
                    || FitsInside(site, footprint.Footprint.SizeFor(site.Place.Orientation), width, height))
                {
                    port = site;
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

            Apply(layout, "Resize city layout", width, height, forbidden, houses, townHall, roads, port, buildings);
        }

        /// <summary>The cells the town hall covers when placed with this orientation.</summary>
        public static Vector2Int TownHallSize(BuildingsDefinition buildings, GridPlace place)
        {
            return buildings.TownHall.Footprint.SizeFor(place.Orientation);
        }

        /// <summary>
        /// Whether the town hall can stand at the place: inside the grid, on no water cell, road, house, wall or port.
        /// Its own current place does not count.
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
                        || HouseIndexAt(layout, buildings, cell) >= 0 || IsRoadIn(layout, cell)
                        || IsWallIn(layout, cell) || IsUnderPort(layout, buildings, cell)
                        || IsUnderTradingPostSite(layout, buildings, cell))
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

            Apply(layout, "Place town hall", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses, place, layout.Roads, PortOf(layout), buildings);
            return true;
        }

        /// <summary>Takes the town hall away; records nothing when there is none.</summary>
        public static void RemoveTownHall(CityLayoutDefinition layout)
        {
            if (!layout.HasTownHall)
            {
                return;
            }

            Apply(layout, "Remove town hall", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses, null, layout.Roads, PortOf(layout), null);
        }

        /// <summary>The port's footprint split into water and land rows, from the catalogue. Only for a catalogue whose port is usable (<see cref="TryGetPortFootprint"/>).</summary>
        public static PortFootprint PortFootprintOf(BuildingsDefinition buildings)
        {
            return buildings.Port.PortFootprint;
        }

        /// <summary>The port's footprint; false when the catalogue has no usable port.</summary>
        public static bool TryGetPortFootprint(BuildingsDefinition buildings, out PortFootprint footprint)
        {
            footprint = default;

            if (buildings.Port == null || !buildings.Port.TryValidate(out _))
            {
                return false;
            }

            footprint = buildings.Port.PortFootprint;
            return true;
        }

        private static bool FitsInside(PortSite site, Vector2Int size, int width, int height)
        {
            return site.Origin.x + size.x <= width && site.Origin.y + size.y <= height;
        }

        /// <summary>The cells the port covers when it stands at the site.</summary>
        public static Vector2Int PortSize(BuildingsDefinition buildings, PortSite site)
        {
            return buildings.Port.Footprint.SizeFor(site.Place.Orientation);
        }

        /// <summary>
        /// Whether the port can stand at the site: inside the grid, on no building, its front rows on water and its
        /// back rows on land. Its own current place does not count.
        /// </summary>
        public static bool CanPlacePort(CityLayoutDefinition layout, BuildingsDefinition buildings, PortSite site)
        {
            return TryGetPortFootprint(buildings, out PortFootprint footprint)
                && TryMakeGrid(layout, buildings, false, out CityGrid grid) && grid.CanPlacePort(footprint, site);
        }

        /// <summary>Places the port or moves it; false, and nothing recorded, when it cannot stand there.</summary>
        public static bool SetPort(CityLayoutDefinition layout, BuildingsDefinition buildings, PortSite site)
        {
            if (!CanPlacePort(layout, buildings, site))
            {
                return false;
            }

            if (layout.HasPort && layout.PortSite.Origin == site.Origin && layout.PortSite.Facing == site.Facing)
            {
                return true;
            }

            Apply(layout, "Place port", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses, TownHallOf(layout), layout.Roads, site, buildings);
            return true;
        }

        /// <summary>Takes the port away; records nothing when there is none.</summary>
        public static void RemovePort(CityLayoutDefinition layout)
        {
            if (!layout.HasPort)
            {
                return;
            }

            Apply(layout, "Remove port", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses, TownHallOf(layout), layout.Roads, null, null);
        }

        /// <summary>Whether the layout's port covers the cell.</summary>
        public static bool IsUnderPort(CityLayoutDefinition layout, BuildingsDefinition buildings, Vector2Int cell)
        {
            if (!layout.HasPort || !TryGetPortFootprint(buildings, out PortFootprint footprint))
            {
                return false;
            }

            PortSite site = layout.PortSite;
            Vector2Int size = footprint.Footprint.SizeFor(site.Place.Orientation);

            return cell.x >= site.Origin.x && cell.x < site.Origin.x + size.x
                && cell.y >= site.Origin.y && cell.y < site.Origin.y + size.y;
        }

        /// <summary>
        /// Adds roads or removes them. The cells outside the grid, on water, or under the town hall, the port, a house or a
        /// wall are skipped. Records nothing when no cell changes.
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
                    && HouseIndexAt(layout, buildings, cell) < 0 && !IsUnderTownHall(layout, buildings, cell)
                    && !IsUnderPort(layout, buildings, cell) && !IsWallIn(layout, cell)
                    && !IsUnderTradingPostSite(layout, buildings, cell))
                {
                    roads.Add(cell);
                    changed = true;
                }
            }

            if (changed)
            {
                Apply(
                    layout, add ? "Lay roads" : "Remove roads", layout.Width, layout.Height,
                    layout.ForbiddenCells, layout.StartingHouses, TownHallOf(layout), roads, PortOf(layout), buildings);
            }
        }

        /// <summary>
        /// Adds walls or removes them. When adding, the cells outside the grid, on water (a berth included), or under the
        /// town hall, the port, the trading post site, a house or a road are skipped: a cell is one thing at a time. Records nothing when no
        /// cell changes.
        /// </summary>
        public static void SetWalls(CityLayoutDefinition layout, BuildingsDefinition buildings, IEnumerable<Vector2Int> cells, bool add)
        {
            var walls = new List<Vector2Int>(layout.Walls);
            bool changed = false;

            foreach (Vector2Int cell in cells)
            {
                if (!IsInside(layout, cell))
                {
                    continue;
                }

                if (!add)
                {
                    changed |= walls.Remove(cell);
                }
                else if (!walls.Contains(cell) && !IsForbiddenIn(layout, cell)
                    && HouseIndexAt(layout, buildings, cell) < 0 && !IsUnderTownHall(layout, buildings, cell)
                    && !IsUnderPort(layout, buildings, cell) && !IsRoadIn(layout, cell)
                    && !IsUnderTradingPostSite(layout, buildings, cell))
                {
                    walls.Add(cell);
                    changed = true;
                }
            }

            if (changed)
            {
                Apply(
                    layout, add ? "Build walls" : "Remove walls", layout.Width, layout.Height,
                    layout.ForbiddenCells, layout.StartingHouses, TownHallOf(layout), layout.Roads, PortOf(layout), buildings,
                    walls: walls);
            }
        }

        /// <summary>The cells the trading post site covers when placed with this orientation.</summary>
        public static Vector2Int TradingPostSize(BuildingsDefinition buildings, GridPlace place)
        {
            return buildings.TradingPost.Footprint.SizeFor(place.Orientation);
        }

        /// <summary>
        /// Whether the trading post site can lie at the place: on land inside the grid, under no building. The current
        /// site does not count, and the walls are not enforced.
        /// </summary>
        public static bool CanPlaceTradingPostSite(CityLayoutDefinition layout, BuildingsDefinition buildings, GridPlace place)
        {
            return buildings.TradingPost != null
                && TryMakeGrid(layout, buildings, true, false, out CityGrid grid)
                && grid.CanReserveTradingPostSite(buildings.TradingPost.Footprint, place);
        }

        /// <summary>Places the trading post site or moves it; false, and nothing recorded, when it cannot stand there.</summary>
        public static bool SetTradingPostSite(CityLayoutDefinition layout, BuildingsDefinition buildings, GridPlace place)
        {
            if (!CanPlaceTradingPostSite(layout, buildings, place))
            {
                return false;
            }

            if (layout.HasTradingPostSite && layout.TradingPostSite.Equals(place))
            {
                return true;
            }

            Apply(
                layout, "Place trading post site", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses,
                TownHallOf(layout), layout.Roads, PortOf(layout), buildings, tradingPostSite: place, setSite: true);
            return true;
        }

        /// <summary>Takes the trading post site away; records nothing when there is none.</summary>
        public static void RemoveTradingPostSite(CityLayoutDefinition layout)
        {
            if (!layout.HasTradingPostSite)
            {
                return;
            }

            Apply(
                layout, "Remove trading post site", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses,
                TownHallOf(layout), layout.Roads, PortOf(layout), null, tradingPostSite: null, setSite: true);
        }

        /// <summary>Whether the layout's trading post site covers the cell.</summary>
        public static bool IsUnderTradingPostSite(CityLayoutDefinition layout, BuildingsDefinition buildings, Vector2Int cell)
        {
            if (!layout.HasTradingPostSite || buildings.TradingPost == null)
            {
                return false;
            }

            GridPlace site = layout.TradingPostSite;
            Vector2Int size = TradingPostSize(buildings, site);

            return cell.x >= site.Origin.x && cell.x < site.Origin.x + size.x
                && cell.y >= site.Origin.y && cell.y < site.Origin.y + size.y;
        }

        /// <summary>Whether a berth can be added at the cell: water, not under the port, no berth there yet.</summary>
        public static bool CanPlaceBerth(CityLayoutDefinition layout, BuildingsDefinition buildings, Vector2Int cell)
        {
            return TryMakeGrid(layout, buildings, out CityGrid grid) && grid.CanPlaceBerth(cell);
        }

        /// <summary>Adds a berth; false, and nothing recorded, when it cannot stand there.</summary>
        public static bool AddBerth(CityLayoutDefinition layout, BuildingsDefinition buildings, Vector2Int cell)
        {
            if (!CanPlaceBerth(layout, buildings, cell))
            {
                return false;
            }

            var berths = new List<Vector2Int>(layout.Berths) { cell };
            Apply(
                layout, "Add berth", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses,
                TownHallOf(layout), layout.Roads, PortOf(layout), buildings, berths);
            return true;
        }

        /// <summary>Removes the berth at the cell; false, and nothing recorded, when there is none.</summary>
        public static bool RemoveBerth(CityLayoutDefinition layout, Vector2Int cell)
        {
            var berths = new List<Vector2Int>(layout.Berths);

            if (!berths.Remove(cell))
            {
                return false;
            }

            Apply(
                layout, "Remove berth", layout.Width, layout.Height, layout.ForbiddenCells, layout.StartingHouses,
                TownHallOf(layout), layout.Roads, PortOf(layout), null, berths);
            return true;
        }

        /// <summary>The depth of the band of water a generated layout gets, in cells.</summary>
        public const int GeneratedWaterRows = 12;

        /// <summary>The berths a generated layout gets, on the water nearest its port.</summary>
        public const int GeneratedBerths = 10;

        /// <summary>The rows of land between the water and the town hall of a generated layout.</summary>
        public const int GeneratedTownHallDistance = 16;

        /// <summary>The side, in cells, of the square around the town hall a generated layout's houses are drawn in.</summary>
        public const int GeneratedHouseArea = 40;

        /// <summary>The side, in cells, of the square around the town hall whose outline a generated layout walls.</summary>
        public const int GeneratedWallSide = 45;

        // How far around the city, in cells of the navigation mask, the water is looked for.
        private const float ShoreRadius = 10f;

        /// <summary>
        /// A whole city grid of the size, from nothing: water along the side of the sea (where the mask's water lies
        /// around the city on the map; south without a mask), the port on that shore, the town hall inland of the port,
        /// walls around it (<see cref="GeneratedWallSide"/>), the trading post site inside them near the town hall, then just the houses the city's starting population needs, drawn around the town hall with the seed and
        /// connected by roads (<see cref="StartingLayout.TryBuild"/>). They are connected by sides, whatever the
        /// catalogue's contact, so that the layout works under either.
        /// </summary>
        /// <param name="problem">What did not fit, for the logs; null when everything did.</param>
        public static bool TryGenerate(
            CityDefinition city, EconomyDefinition economy, BuildingsDefinition buildings, NavigationGrid mask, int seed,
            int width, int height, out CityGrid grid, out string problem)
        {
            PerClass<int> roomPerHouse = buildings.HouseRooms;
            PerClass<int> room = economy.StartingRoomOf(city, roomPerHouse);
            var houses = new PerClass<int>(
                room[SocialClass.Rich] / roomPerHouse[SocialClass.Rich],
                room[SocialClass.Wealthy] / roomPerHouse[SocialClass.Wealthy],
                room[SocialClass.Poor] / roomPerHouse[SocialClass.Poor]);
            var footprints = new PerClass<Footprint>(
                HouseFootprint(buildings, SocialClass.Rich),
                HouseFootprint(buildings, SocialClass.Wealthy),
                HouseFootprint(buildings, SocialClass.Poor));
            PortFacing shore = ShoreSide.Of(mask, city.MapPosition, ShoreRadius);

            return StartingLayout.TryBuild(
                width, height, shore, GeneratedWaterRows, GeneratedTownHallDistance, GeneratedHouseArea, GeneratedWallSide,
                PortFootprintOf(buildings), GeneratedBerths, buildings.TownHall.Footprint, buildings.TradingPost.Footprint,
                houses, footprints, RoadContact.SidesOnly, new System.Random(seed),
                out grid, out problem);
        }

        /// <summary>
        /// Replaces the whole content of the layout by a generated city of the same size (<see cref="TryGenerate"/>),
        /// in one undo step; nothing is changed when it does not fit.
        /// </summary>
        public static bool Regenerate(
            CityLayoutDefinition layout, CityDefinition city, EconomyDefinition economy, BuildingsDefinition buildings,
            NavigationGrid mask, int seed, out string problem)
        {
            if (!TryGenerate(city, economy, buildings, mask, seed, layout.Width, layout.Height, out CityGrid grid, out problem))
            {
                return false;
            }

            Undo.RecordObject(layout, "Regenerate city layout");
            layout.SetContent(grid);
            EditorUtility.SetDirty(layout);
            return true;
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

        private static PortSite? PortOf(CityLayoutDefinition layout)
        {
            return layout.HasPort ? layout.PortSite : (PortSite?)null;
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

        private static bool IsWallIn(CityLayoutDefinition layout, Vector2Int cell)
        {
            foreach (Vector2Int wall in layout.Walls)
            {
                if (wall == cell)
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
        /// Makes the cells water or frees them. The cells outside the grid or under the port or the trading post site, and when making water
        /// those a house, the town hall, a road or a wall covers, are skipped. Records nothing when no cell changes.
        /// The port's cells are left as they are, so that it never ends up standing wrong.
        /// </summary>
        public static void SetForbidden(CityLayoutDefinition layout, BuildingsDefinition buildings, IEnumerable<Vector2Int> cells, bool forbid)
        {
            var forbidden = new List<Vector2Int>(layout.ForbiddenCells);
            bool changed = false;

            foreach (Vector2Int cell in cells)
            {
                if (!IsInside(layout, cell) || IsUnderPort(layout, buildings, cell)
                    || IsUnderTradingPostSite(layout, buildings, cell))
                {
                    continue;
                }

                if (!forbid)
                {
                    changed |= forbidden.Remove(cell);
                }
                else if (!forbidden.Contains(cell) && HouseIndexAt(layout, buildings, cell) < 0
                    && !IsUnderTownHall(layout, buildings, cell) && !IsRoadIn(layout, cell) && !IsWallIn(layout, cell))
                {
                    forbidden.Add(cell);
                    changed = true;
                }
            }

            if (changed)
            {
                Apply(layout, forbid ? "Forbid cells" : "Free cells", layout.Width, layout.Height, forbidden, layout.StartingHouses, buildings);
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

            Apply(layout, "Add starting house", layout.Width, layout.Height, layout.ForbiddenCells, houses, buildings);
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

            Apply(layout, "Remove starting houses", layout.Width, layout.Height, layout.ForbiddenCells, houses, buildings);
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
        /// The grid of the layout's content, without requiring a town hall or a port (a layout being edited may have
        /// none yet) nor closed walls, placing anywhere (<see cref="CityGrid.KeepsInsideWalls"/> off: a building may
        /// stand outside the walls while the layout is edited); false when something in it cannot stand.
        /// </summary>
        public static bool TryMakeGrid(CityLayoutDefinition layout, BuildingsDefinition buildings, out CityGrid grid)
        {
            return TryMakeGrid(layout, buildings, true, true, out grid);
        }

        private static bool TryMakeGrid(CityLayoutDefinition layout, BuildingsDefinition buildings, bool withPort, out CityGrid grid)
        {
            return TryMakeGrid(layout, buildings, withPort, true, out grid);
        }

        private static bool TryMakeGrid(
            CityLayoutDefinition layout, BuildingsDefinition buildings, bool withPort, bool withSite, out CityGrid grid)
        {
            grid = new CityGrid(layout.Width, layout.Height, layout.ForbiddenCells, buildings.RoadContact);

            // Reserved first, so that every other placement is refused on the site by the grid itself.
            if (withSite && layout.HasTradingPostSite)
            {
                if (buildings.TradingPost == null
                    || !grid.CanReserveTradingPostSite(buildings.TradingPost.Footprint, layout.TradingPostSite))
                {
                    return false;
                }

                grid.ReserveTradingPostSite(buildings.TradingPost.Footprint, layout.TradingPostSite);
            }

            if (layout.HasTownHall)
            {
                Footprint hall = buildings.TownHall.Footprint;

                if (!grid.CanPlace(hall, layout.TownHallPlace))
                {
                    return false;
                }

                grid.Place(BuildingKind.TownHall(), BuildingOwner.City, hall, layout.TownHallPlace);
            }

            foreach (Vector2Int wall in layout.Walls)
            {
                if (!grid.CanPlace(RoadFootprint, RoadPlaceAt(wall)))
                {
                    return false;
                }

                grid.Place(BuildingKind.Wall(), BuildingOwner.City, RoadFootprint, RoadPlaceAt(wall));
            }

            if (withPort && layout.HasPort)
            {
                if (!TryGetPortFootprint(buildings, out PortFootprint footprint) || !grid.CanPlacePort(footprint, layout.PortSite))
                {
                    return false;
                }

                grid.PlacePort(footprint, layout.PortSite);
            }

            foreach (Vector2Int road in layout.Roads)
            {
                if (!grid.CanPlace(RoadFootprint, RoadPlaceAt(road)))
                {
                    return false;
                }

                PlaceRoad(grid, road);
            }

            if (withPort)
            {
                foreach (Vector2Int berth in layout.Berths)
                {
                    if (!grid.CanPlaceBerth(berth))
                    {
                        return false;
                    }

                    grid.PlaceBerth(berth);
                }
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
            IEnumerable<Vector2Int> forbidden, IEnumerable<CityLayoutDefinition.StartingHouse> houses,
            BuildingsDefinition buildings)
        {
            Apply(layout, undoName, width, height, forbidden, houses, TownHallOf(layout), layout.Roads, PortOf(layout), buildings);
        }

        // Keeps of the berths those still inside the new size, on water and not under the new port, so that every edit
        // that invalidates a berth drops it in its own undo step. The catalogue is needed for the port's size: without
        // it (the port is unchanged or gone) only the size and the water are checked. The walls, the layout's when
        // none are given, are kept inside the new size.
        private static void Apply(
            CityLayoutDefinition layout, string undoName, int width, int height,
            IEnumerable<Vector2Int> forbidden, IEnumerable<CityLayoutDefinition.StartingHouse> houses,
            GridPlace? townHall, IEnumerable<Vector2Int> roads, PortSite? port, BuildingsDefinition buildings,
            IEnumerable<Vector2Int> berths = null, IEnumerable<Vector2Int> walls = null,
            GridPlace? tradingPostSite = null, bool setSite = false)
        {
            // The site is the layout's unless given; without the catalogue its size is unknown and it is kept as it is.
            GridPlace? site = setSite ? tradingPostSite : layout.HasTradingPostSite ? layout.TradingPostSite : (GridPlace?)null;

            if (site != null && buildings != null && buildings.TradingPost != null)
            {
                Vector2Int siteSize = TradingPostSize(buildings, site.Value);
                Vector2Int origin = site.Value.Origin;
                var forbiddenSet = new HashSet<Vector2Int>(forbidden);
                bool fits = origin.x >= 0 && origin.y >= 0 && origin.x + siteSize.x <= width && origin.y + siteSize.y <= height;

                for (int y = origin.y; fits && y < origin.y + siteSize.y; y++)
                {
                    for (int x = origin.x; fits && x < origin.x + siteSize.x; x++)
                    {
                        fits = !forbiddenSet.Contains(new Vector2Int(x, y));
                    }
                }

                if (!fits)
                {
                    site = null;
                }
            }

            var keptWalls = new List<Vector2Int>();

            foreach (Vector2Int wall in walls ?? layout.Walls)
            {
                if (wall.x >= 0 && wall.y >= 0 && wall.x < width && wall.y < height)
                {
                    keptWalls.Add(wall);
                }
            }

            var forbiddenCells = new List<Vector2Int>(forbidden);
            var water = new HashSet<Vector2Int>(forbiddenCells);
            var keptBerths = new List<Vector2Int>();
            Vector2Int portSize = default;
            bool portKnown = port != null && buildings != null && TryGetPortFootprint(buildings, out PortFootprint footprint)
                && (portSize = footprint.Footprint.SizeFor(port.Value.Place.Orientation)).x > 0;

            foreach (Vector2Int berth in berths ?? layout.Berths)
            {
                bool underPort = portKnown
                    && berth.x >= port.Value.Origin.x && berth.x < port.Value.Origin.x + portSize.x
                    && berth.y >= port.Value.Origin.y && berth.y < port.Value.Origin.y + portSize.y;

                if (berth.x >= 0 && berth.y >= 0 && berth.x < width && berth.y < height && water.Contains(berth) && !underPort)
                {
                    keptBerths.Add(berth);
                }
            }

            Undo.RecordObject(layout, undoName);
            layout.SetContent(width, height, forbiddenCells, houses, townHall, new List<Vector2Int>(roads), port, keptBerths, keptWalls, site);
            EditorUtility.SetDirty(layout);
        }
    }
}
