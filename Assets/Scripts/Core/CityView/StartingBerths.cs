using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Chooses the berths of a generated city: the water nearest its port that ships can sail to from the edge.</summary>
    public static class StartingBerths
    {
        /// <summary>
        /// Places up to count berths on the navigable cells nearest the port that have a way to the edge of the grid,
        /// nearest first, ties by y then x (a grid always gets the same berths). Returns how many were placed: fewer
        /// than count when the water runs out, none when the grid has no port or the port is not joined to the edge.
        /// </summary>
        public static int Place(CityGrid grid, int count)
        {
            if (grid.Port == null || count <= 0 || !HarbourPaths.IsJoinedToEdge(grid))
            {
                return 0;
            }

            float[] distance = DistancesFromPort(grid);
            var candidates = new List<(float distance, int y, int x)>();
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    float at = distance[y * grid.Width + x];
                    if (!float.IsPositiveInfinity(at) && grid.CanPlaceBerth(new Vector2Int(x, y)))
                    {
                        candidates.Add((at, y, x));
                    }
                }
            }

            candidates.Sort();

            var path = new List<Vector2Int>();
            int placed = 0;
            foreach ((float _, int y, int x) in candidates)
            {
                if (placed == count)
                {
                    break;
                }

                var cell = new Vector2Int(x, y);
                if (HarbourPaths.TryFindExit(grid, cell, path))
                {
                    grid.PlaceBerth(cell);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>
        /// The distance over navigable cells, in cells, from the navigable cells touching the port by a side (0 for
        /// those), indexed by y * width + x; infinity where none can be reached or the grid has no port.
        /// </summary>
        public static float[] DistancesFromPort(CityGrid grid)
        {
            int width = grid.Width;
            var distance = new float[width * grid.Height];
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = float.PositiveInfinity;
            }

            // The moves and steps of HarbourPaths: the nearest berths are the nearest by sea.
            var open = new SortedSet<(float distance, int index)>();
            var sides = new List<Vector2Int>();
            HarbourPaths.GetPortSides(grid, sides);
            foreach (Vector2Int side in sides)
            {
                if (grid.IsNavigable(side))
                {
                    distance[side.y * width + side.x] = 0f;
                    open.Add((0f, side.y * width + side.x));
                }
            }

            while (open.Count > 0)
            {
                (float at, int index) = open.Min;
                open.Remove((at, index));
                if (at > distance[index])
                {
                    continue;
                }

                var cell = new Vector2Int(index % width, index / width);
                foreach (Vector2Int move in HarbourPaths.Moves)
                {
                    if (!HarbourPaths.CanStep(grid, cell, move))
                    {
                        continue;
                    }

                    Vector2Int next = cell + move;
                    int nextIndex = next.y * width + next.x;
                    float nextDistance = at + HarbourPaths.StepLength(cell, next);
                    if (nextDistance < distance[nextIndex])
                    {
                        distance[nextIndex] = nextDistance;
                        open.Add((nextDistance, nextIndex));
                    }
                }
            }

            return distance;
        }
    }
}
