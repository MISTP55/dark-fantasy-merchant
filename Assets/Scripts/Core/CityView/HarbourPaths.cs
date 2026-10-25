using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The paths of ships over the navigable water of a city's grid, between its edge and its berths.</summary>
    public static class HarbourPaths
    {
        // The order is fixed, so that a grid always gives the same path.
        private static readonly Vector2Int[] Moves =
        {
            new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(-1, -1), new Vector2Int(1, -1),
        };

        private static readonly float Diagonal = Mathf.Sqrt(2f);

        /// <summary>
        /// Fills path with the shortest way from the cell to the nearest navigable cell on the edge of the grid, both
        /// ends included. Eight moves; a diagonal only when both cells it passes between are navigable. Always the same
        /// path for a grid. Returns false, with path empty, when the cell is not navigable or no edge cell can be reached.
        /// </summary>
        public static bool TryFindExit(CityGrid grid, Vector2Int from, List<Vector2Int> path)
        {
            path.Clear();
            if (!grid.IsNavigable(from))
            {
                return false;
            }

            int width = grid.Width;
            int count = width * grid.Height;
            var cost = new float[count];
            var parent = new int[count];
            var closed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                cost[i] = float.PositiveInfinity;
                parent[i] = -1;
            }

            // Ordered by estimate, then by insertion.
            var open = new SortedSet<(float estimate, int sequence, int index)>();
            int sequence = 0;
            int start = from.y * width + from.x;
            cost[start] = 0f;
            open.Add((DistanceToEdge(grid, from), sequence++, start));

            while (open.Count > 0)
            {
                (float estimate, int order, int index) = open.Min;
                open.Remove((estimate, order, index));
                if (closed[index])
                {
                    continue;
                }

                closed[index] = true;
                var cell = new Vector2Int(index % width, index / width);
                if (DistanceToEdge(grid, cell) == 0)
                {
                    for (int at = index; at != -1; at = parent[at])
                    {
                        path.Add(new Vector2Int(at % width, at / width));
                    }

                    path.Reverse();
                    return true;
                }

                foreach (Vector2Int move in Moves)
                {
                    var next = new Vector2Int(cell.x + move.x, cell.y + move.y);
                    if (!grid.IsNavigable(next))
                    {
                        continue;
                    }

                    bool diagonal = move.x != 0 && move.y != 0;
                    if (diagonal
                        && (!grid.IsNavigable(new Vector2Int(cell.x + move.x, cell.y))
                            || !grid.IsNavigable(new Vector2Int(cell.x, cell.y + move.y))))
                    {
                        continue;
                    }

                    int nextIndex = next.y * width + next.x;
                    float nextCost = cost[index] + (diagonal ? Diagonal : 1f);
                    if (closed[nextIndex] || nextCost >= cost[nextIndex])
                    {
                        continue;
                    }

                    cost[nextIndex] = nextCost;
                    parent[nextIndex] = index;
                    open.Add((nextCost + DistanceToEdge(grid, next), sequence++, nextIndex));
                }
            }

            return false;
        }

        /// <summary>The length of a path in cells: 1 for a straight step, the square root of 2 for a diagonal one.</summary>
        public static float LengthOf(IReadOnlyList<Vector2Int> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                bool diagonal = path[i].x != path[i - 1].x && path[i].y != path[i - 1].y;
                length += diagonal ? Diagonal : 1f;
            }

            return length;
        }

        /// <summary>Whether the port, when the grid has one, touches by a side a navigable cell that has an exit.</summary>
        public static bool IsJoinedToEdge(CityGrid grid)
        {
            PlacedBuilding port = grid.Port;
            if (port == null)
            {
                return true;
            }

            var path = new List<Vector2Int>();
            Vector2Int origin = port.Place.Origin;
            Vector2Int size = port.Size;
            for (int y = origin.y - 1; y <= origin.y + size.y; y++)
            {
                for (int x = origin.x - 1; x <= origin.x + size.x; x++)
                {
                    bool insideX = x >= origin.x && x < origin.x + size.x;
                    bool insideY = y >= origin.y && y < origin.y + size.y;

                    // A side neighbour is in the port's columns or in its rows, not both (inside) nor neither (a corner).
                    if (insideX == insideY)
                    {
                        continue;
                    }

                    if (TryFindExit(grid, new Vector2Int(x, y), path))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether ships can use the grid: it has a berth, its port is joined to the edge, every berth can be reached from the edge.</summary>
        public static bool TryValidate(CityGrid grid, out string problem)
        {
            if (grid.Berths.Count == 0)
            {
                problem = "it has no berth";
                return false;
            }

            if (!IsJoinedToEdge(grid))
            {
                problem = "its port is not joined to the edge of the grid by water";
                return false;
            }

            var path = new List<Vector2Int>();
            foreach (Vector2Int berth in grid.Berths)
            {
                if (!TryFindExit(grid, berth, path))
                {
                    problem = $"the berth at ({berth.x}, {berth.y}) cannot be reached from the edge of the grid";
                    return false;
                }
            }

            problem = null;
            return true;
        }

        // The straight distance in cells to the nearest edge: 0 on the edge, never more than the real way there.
        private static float DistanceToEdge(CityGrid grid, Vector2Int cell)
        {
            return Math.Min(
                Math.Min(cell.x, cell.y),
                Math.Min(grid.Width - 1 - cell.x, grid.Height - 1 - cell.y));
        }
    }
}
