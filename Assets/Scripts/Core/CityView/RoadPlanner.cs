using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Finds the roads a building needs to touch the road network.</summary>
    public static class RoadPlanner
    {
        /// <summary>
        /// Fills roads (cleared first) with the fewest free cells which, laid as roads, make a building covering
        /// buildingCells touch the network, listed from the network outwards. The cells may be those of a building
        /// already placed or of one being planned: either way they are blocked. Empty and true when the building
        /// already touches a network road; false when the grid has no town hall or no path exists. Ties are broken
        /// by the order of a search that visits the neighbours a cell touches by x then y, so the result is deterministic.
        /// </summary>
        public static bool TryFindRoads(CityGrid grid, IReadOnlyCollection<Vector2Int> buildingCells, List<Vector2Int> roads)
        {
            roads.Clear();
            if (grid.TownHall == null)
            {
                return false;
            }

            if (grid.TouchesNetwork(buildingCells))
            {
                return true;
            }

            int width = grid.Width;
            int count = width * grid.Height;
            var blocked = new HashSet<Vector2Int>(buildingCells);
            var goal = new bool[count];
            foreach (Vector2Int cell in buildingCells)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var near = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (grid.Touches(dx, dy) && grid.Contains(near))
                        {
                            goal[near.y * width + near.x] = true;
                        }
                    }
                }
            }

            var parent = new int[count];
            var seen = new bool[count];
            var queue = new Queue<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (IsOpen(grid, blocked, cell) && grid.JoinsNetwork(cell))
                    {
                        seen[y * width + x] = true;
                        parent[y * width + x] = -1;
                        queue.Enqueue(cell);
                    }
                }
            }

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                int index = cell.y * width + cell.x;
                if (goal[index])
                {
                    for (int at = index; at >= 0; at = parent[at])
                    {
                        roads.Add(new Vector2Int(at % width, at / width));
                    }

                    roads.Reverse();
                    return true;
                }

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var next = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (!grid.Touches(dx, dy) || !IsOpen(grid, blocked, next) || seen[next.y * width + next.x])
                        {
                            continue;
                        }

                        seen[next.y * width + next.x] = true;
                        parent[next.y * width + next.x] = index;
                        queue.Enqueue(next);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Fills distances (Width * Height, index y * Width + x): for each free cell, the number of road cells, itself
        /// included, that join it to the network (1 where <see cref="CityGrid.JoinsNetwork"/>); int.MaxValue when it
        /// is not free or cannot be reached.
        /// </summary>
        public static void DistancesFromNetwork(CityGrid grid, int[] distances)
        {
            int width = grid.Width;
            if (distances.Length != width * grid.Height)
            {
                throw new ArgumentException("distances must hold Width * Height values.", nameof(distances));
            }

            Array.Fill(distances, int.MaxValue);
            var queue = new Queue<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (IsOpen(grid, null, cell) && grid.JoinsNetwork(cell))
                    {
                        distances[y * width + x] = 1;
                        queue.Enqueue(cell);
                    }
                }
            }

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                int next = distances[cell.y * width + cell.x] + 1;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var other = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (grid.Touches(dx, dy) && IsOpen(grid, null, other) && distances[other.y * width + other.x] == int.MaxValue)
                        {
                            distances[other.y * width + other.x] = next;
                            queue.Enqueue(other);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Whether laying the roads, in their order, would connect a building covering buildingCells: every road is a
        /// distinct free cell outside the building that joins the network or touches a road laid
        /// before it, and the building touches a network road or one of the roads. With no road, whether the
        /// building already touches the network.
        /// </summary>
        public static bool Connects(CityGrid grid, IReadOnlyCollection<Vector2Int> buildingCells, IReadOnlyList<Vector2Int> roads)
        {
            if (roads.Count == 0)
            {
                return grid.TouchesNetwork(buildingCells);
            }

            var building = new HashSet<Vector2Int>(buildingCells);
            var laid = new HashSet<Vector2Int>();
            foreach (Vector2Int road in roads)
            {
                if (!IsOpen(grid, building, road) || laid.Contains(road))
                {
                    return false;
                }

                if (!grid.JoinsNetwork(road) && !TouchesAny(grid, road, laid))
                {
                    return false;
                }

                laid.Add(road);
            }

            if (grid.TouchesNetwork(buildingCells))
            {
                return true;
            }

            foreach (Vector2Int cell in buildingCells)
            {
                if (TouchesAny(grid, cell, laid))
                {
                    return true;
                }
            }

            return false;
        }

        // Whether the cell touches one of the cells, by the grid's contact.
        private static bool TouchesAny(CityGrid grid, Vector2Int cell, HashSet<Vector2Int> cells)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (grid.Touches(dx, dy) && cells.Contains(new Vector2Int(cell.x + dx, cell.y + dy)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsOpen(CityGrid grid, HashSet<Vector2Int> blocked, Vector2Int cell)
        {
            return grid.IsBuildable(cell)
                && grid.BuildingAt(cell) == null
                && (blocked == null || !blocked.Contains(cell));
        }
    }
}
