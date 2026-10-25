using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The cells of a city and the buildings on them. It knows footprints and places, never who builds or why.
    /// </summary>
    public sealed class CityGrid
    {
        private readonly bool[] forbidden;
        private readonly PlacedBuilding[] occupancy;
        private readonly List<PlacedBuilding> buildings = new List<PlacedBuilding>();
        private readonly bool[] network;

        /// <param name="forbidden">The water cells: only the port may cover them.</param>
        /// <param name="contact">When a road touches a building or another road, for the whole grid.</param>
        public CityGrid(int width, int height, IEnumerable<Vector2Int> forbidden, RoadContact contact = RoadContact.SidesAndCorners)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
            Contact = contact;
            this.forbidden = new bool[width * height];
            occupancy = new PlacedBuilding[width * height];
            network = new bool[width * height];
            foreach (Vector2Int cell in forbidden)
            {
                if (!Contains(cell))
                {
                    throw new ArgumentOutOfRangeException(nameof(forbidden), "A forbidden cell is outside the grid.");
                }

                this.forbidden[Index(cell)] = true;
            }
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>When a road touches a building or another road.</summary>
        public RoadContact Contact { get; }

        /// <summary>Buildings in the order they were placed.</summary>
        public IReadOnlyList<PlacedBuilding> Buildings => buildings;

        /// <summary>The town hall; null when none is placed.</summary>
        public PlacedBuilding TownHall { get; private set; }

        /// <summary>The port; null when none is placed.</summary>
        public PlacedBuilding Port { get; private set; }

        public event Action<PlacedBuilding> BuildingPlaced;

        /// <summary>
        /// Raised once per house or workshop when it becomes connected, after the <see cref="BuildingPlaced"/> of the
        /// building that caused it (itself, a road or the town hall), in the order the buildings were placed.
        /// </summary>
        public event Action<PlacedBuilding> BuildingConnected;

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        /// <summary>Whether the cell is water: only the port may cover it.</summary>
        public bool IsForbidden(Vector2Int cell)
        {
            return Contains(cell) && forbidden[Index(cell)];
        }

        /// <summary>The building covering the cell; null when it is free or outside the grid.</summary>
        public PlacedBuilding BuildingAt(Vector2Int cell)
        {
            return Contains(cell) ? occupancy[Index(cell)] : null;
        }

        public bool CanPlace(Footprint footprint, GridPlace place)
        {
            Vector2Int size = footprint.SizeFor(place.Orientation);
            Vector2Int origin = place.Origin;

            // default(Footprint) is 0 by 0: it covers no cell and would give room with no place.
            if (size.x < 1 || size.y < 1)
            {
                return false;
            }

            if (origin.x < 0 || origin.y < 0 || origin.x + size.x > Width || origin.y + size.y > Height)
            {
                return false;
            }

            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                for (int x = origin.x; x < origin.x + size.x; x++)
                {
                    int index = y * Width + x;
                    if (forbidden[index] || occupancy[index] != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public PlacedBuilding Place(BuildingKind kind, BuildingOwner owner, Footprint footprint, GridPlace place)
        {
            if (kind.IsPort)
            {
                throw new ArgumentException("Place a port with PlacePort.", nameof(kind));
            }

            if (!CanPlace(footprint, place))
            {
                throw new InvalidOperationException("The footprint does not fit at this place.");
            }

            PlacedBuilding building = PlaceWithoutNetwork(new PlacedBuilding(kind, owner, footprint, place));
            UpdateNetwork(building);
            return building;
        }

        /// <summary>
        /// Whether the port can stand at the site: inside the grid, on no building, its front rows all on water and
        /// its back rows all on land.
        /// </summary>
        public bool CanPlacePort(PortFootprint footprint, PortSite site)
        {
            Vector2Int size = footprint.Footprint.SizeFor(site.Place.Orientation);
            Vector2Int origin = site.Origin;

            if (origin.x < 0 || origin.y < 0 || origin.x + size.x > Width || origin.y + size.y > Height)
            {
                return false;
            }

            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                for (int x = origin.x; x < origin.x + size.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    int index = Index(cell);
                    if (occupancy[index] != null || forbidden[index] != footprint.IsWater(site, cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Places the city's port; it is not part of the network and connects nothing.</summary>
        public PlacedBuilding PlacePort(PortFootprint footprint, PortSite site)
        {
            if (Port != null)
            {
                throw new InvalidOperationException("The grid already has a port.");
            }

            if (!CanPlacePort(footprint, site))
            {
                throw new InvalidOperationException("The port cannot stand at this site.");
            }

            return PlaceWithoutNetwork(new PlacedBuilding(
                BuildingKind.Port(), BuildingOwner.City, footprint.Footprint, site.Place, site.Facing));
        }

        /// <summary>
        /// Places a road on each cell, then updates the network once: a trace of n roads floods the grid once,
        /// not n times. The cells must be free and distinct, or nothing is placed.
        /// </summary>
        internal void PlaceRoads(IReadOnlyList<Vector2Int> cells, BuildingOwner owner)
        {
            var road = new Footprint(1, 1);
            var seen = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in cells)
            {
                if (!seen.Add(cell) || !CanPlace(road, new GridPlace(cell, BuildingOrientation.Along)))
                {
                    throw new InvalidOperationException("A road does not fit at this cell.");
                }
            }

            foreach (Vector2Int cell in cells)
            {
                PlaceWithoutNetwork(new PlacedBuilding(BuildingKind.Road(), owner, road, new GridPlace(cell, BuildingOrientation.Along)));
            }

            if (cells.Count > 0)
            {
                FloodNetwork();
                ConnectTouchingBuildings();
            }
        }

        // The caller has checked that the building fits.
        private PlacedBuilding PlaceWithoutNetwork(PlacedBuilding building)
        {
            BuildingKind kind = building.Kind;
            GridPlace place = building.Place;
            if (kind.IsRoad && (building.Footprint.Width != 1 || building.Footprint.Depth != 1))
            {
                throw new ArgumentException("A road covers exactly one cell.", nameof(building));
            }

            if (kind.IsTownHall && TownHall != null)
            {
                throw new InvalidOperationException("The grid already has a town hall.");
            }

            Vector2Int size = building.Size;
            for (int y = place.Origin.y; y < place.Origin.y + size.y; y++)
            {
                for (int x = place.Origin.x; x < place.Origin.x + size.x; x++)
                {
                    occupancy[y * Width + x] = building;
                }
            }

            buildings.Add(building);
            if (kind.IsTownHall)
            {
                TownHall = building;
            }
            else if (kind.IsPort)
            {
                Port = building;
            }

            BuildingPlaced?.Invoke(building);
            return building;
        }

        private void UpdateNetwork(PlacedBuilding placed)
        {
            if (placed.Kind.IsRoad || placed.Kind.IsTownHall)
            {
                FloodNetwork();
                ConnectTouchingBuildings();
            }
            else if (TouchesNetwork(CellsOf(placed)))
            {
                Connect(placed);
            }
        }

        private void Connect(PlacedBuilding building)
        {
            building.IsConnected = true;
            BuildingConnected?.Invoke(building);
        }

        private void ConnectTouchingBuildings()
        {
            // Index-based: a handler of the events may place buildings, which appends to the list.
            for (int i = 0; i < buildings.Count; i++)
            {
                PlacedBuilding building = buildings[i];
                if (!building.IsConnected
                    && (building.Kind.IsHouse || building.Kind.IsWorkshop)
                    && TouchesNetwork(CellsOf(building)))
                {
                    Connect(building);
                }
            }
        }

        /// <summary>Marks every road joined to the town hall, through the neighbours each cell touches.</summary>
        private void FloodNetwork()
        {
            Array.Clear(network, 0, network.Length);
            if (TownHall == null)
            {
                return;
            }

            var queue = new Queue<Vector2Int>();
            foreach (Vector2Int cell in CellsOf(TownHall))
            {
                Spread(cell, queue);
            }

            while (queue.Count > 0)
            {
                Spread(queue.Dequeue(), queue);
            }
        }

        private void Spread(Vector2Int from, Queue<Vector2Int> queue)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new Vector2Int(from.x + dx, from.y + dy);
                    if (!Touches(dx, dy) || !Contains(cell) || network[Index(cell)])
                    {
                        continue;
                    }

                    PlacedBuilding there = occupancy[Index(cell)];
                    if (there != null && there.Kind.IsRoad)
                    {
                        network[Index(cell)] = true;
                        queue.Enqueue(cell);
                    }
                }
            }
        }

        private static IEnumerable<Vector2Int> CellsOf(PlacedBuilding building)
        {
            Vector2Int size = building.Size;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    yield return new Vector2Int(building.Place.Origin.x + x, building.Place.Origin.y + y);
                }
            }
        }

        /// <summary>A road of the network: a road joined to the town hall.</summary>
        public bool IsNetworkRoad(Vector2Int cell)
        {
            return Contains(cell) && network[Index(cell)];
        }

        /// <summary>
        /// Whether one of the cells is a network road or touches one. The cells are not counted
        /// as roads themselves.
        /// </summary>
        public bool TouchesNetwork(IEnumerable<Vector2Int> cells)
        {
            foreach (Vector2Int cell in cells)
            {
                if (IsNetworkRoad(cell) || HasNeighbour(cell, false))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a road placed on the cell would be a network road: it touches the town hall or a network road.</summary>
        public bool JoinsNetwork(Vector2Int cell)
        {
            return HasNeighbour(cell, true) || HasNeighbour(cell, false);
        }

        /// <summary>Whether a building with this footprint, placed here, would be connected.</summary>
        public bool WouldConnect(Footprint footprint, GridPlace place)
        {
            Vector2Int size = footprint.SizeFor(place.Orientation);
            var cells = new List<Vector2Int>(Math.Max(0, size.x * size.y));
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    cells.Add(new Vector2Int(place.Origin.x + x, place.Origin.y + y));
                }
            }

            return TouchesNetwork(cells);
        }

        /// <summary>Whether a cell touches the one at this offset from it (each of dx and dy between -1 and 1), by the grid's <see cref="Contact"/>.</summary>
        public bool Touches(int dx, int dy)
        {
            return (dx != 0 || dy != 0) && (Contact == RoadContact.SidesAndCorners || dx == 0 || dy == 0);
        }

        // Among the neighbours the cell touches: a cell of the town hall, or a network road.
        private bool HasNeighbour(Vector2Int cell, bool townHall)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var other = new Vector2Int(cell.x + dx, cell.y + dy);
                    if (!Touches(dx, dy) || !Contains(other))
                    {
                        continue;
                    }

                    if (townHall)
                    {
                        PlacedBuilding there = occupancy[Index(other)];
                        if (there != null && there.Kind.IsTownHall)
                        {
                            return true;
                        }
                    }
                    else if (network[Index(other)])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Fills results with every place where the footprint fits, by x then y, Along before Across;
        /// a square footprint is listed Along only.
        /// </summary>
        public void GetPlaces(Footprint footprint, List<GridPlace> results)
        {
            results.Clear();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var along = new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
                    if (CanPlace(footprint, along))
                    {
                        results.Add(along);
                    }

                    if (footprint.IsSquare)
                    {
                        continue;
                    }

                    var across = new GridPlace(new Vector2Int(x, y), BuildingOrientation.Across);
                    if (CanPlace(footprint, across))
                    {
                        results.Add(across);
                    }
                }
            }
        }

        /// <summary>The houses of the class on the grid, the city's and the player's.</summary>
        public int HousesOf(SocialClass housedClass)
        {
            int count = 0;
            foreach (PlacedBuilding building in buildings)
            {
                if (building.Kind.IsHouse && building.Kind.HousedClass == housedClass)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The houses of the class that touch a road of the network.</summary>
        public int ConnectedHousesOf(SocialClass housedClass)
        {
            int count = 0;
            foreach (PlacedBuilding building in buildings)
            {
                if (building.Kind.IsHouse && building.Kind.HousedClass == housedClass && building.IsConnected)
                {
                    count++;
                }
            }

            return count;
        }

        private int Index(Vector2Int cell)
        {
            return cell.y * Width + cell.x;
        }
    }
}
