using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Finds routes for ships over the navigable cells of a <see cref="NavigationGrid"/>.
    /// The grid must not be modified once the pathfinder is built.
    /// </summary>
    public sealed class NavigationPathfinder
    {
        private const int NoRegion = 0;
        private const int AnyRegion = -1;

        private readonly NavigationGrid grid;
        private readonly int width;
        private readonly int height;

        // Region of each cell, NoRegion for land. Two cells are in the same region when
        // a ship can sail from one to the other: regions are connected by cell sides,
        // because a diagonal move needs the cells beside it.
        private readonly int[] regions;

        private readonly NavigationCellSearch search;

        // Reused by every search.
        private readonly List<int> cells = new List<int>();
        private readonly List<Vector2> route = new List<Vector2>();

        public NavigationPathfinder(NavigationGrid grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            width = grid.Width;
            height = grid.Height;
            regions = new int[width * height];
            HasNavigableCells = LabelRegions() > 0;
            search = new NavigationCellSearch(grid);
        }

        /// <summary>False for a grid that is all land: no ship can be placed or moved.</summary>
        public bool HasNavigableCells { get; }

        /// <summary>
        /// The point itself when it is on water, otherwise the center of the nearest
        /// navigable cell. A point outside the map is clamped to it first.
        /// </summary>
        /// <returns>
        /// False, with <paramref name="nearest"/> set to the clamped point, for a NaN
        /// point and for a grid with no navigable cell.
        /// </returns>
        public bool TryGetNearestNavigable(Vector2 point, out Vector2 nearest)
        {
            nearest = ClampToMap(point);

            if (IsNaN(point) || !HasNavigableCells)
            {
                return false;
            }

            if (regions[CellAt(nearest)] == NoRegion)
            {
                nearest = CellCenter(NearestCell(nearest, AnyRegion));
            }

            return true;
        }

        /// <summary>
        /// Finds the route a ship sails from one point of the map to another. The route
        /// ends on the destination when the ship can reach it, otherwise on the nearest
        /// navigable cell it can reach. Points outside the map are clamped to it.
        /// </summary>
        /// <param name="waypoints">
        /// Cleared, then filled with the points to sail through in order, not including
        /// <paramref name="from"/>. Empty when the ship is already where the route ends.
        /// </param>
        /// <returns>
        /// False, leaving <paramref name="waypoints"/> empty, for a NaN point and for a
        /// grid with no navigable cell.
        /// </returns>
        public bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> waypoints)
        {
            if (waypoints == null)
            {
                throw new ArgumentNullException(nameof(waypoints));
            }

            waypoints.Clear();

            if (IsNaN(from) || IsNaN(to) || !HasNavigableCells)
            {
                return false;
            }

            from = ClampToMap(from);
            to = ClampToMap(to);

            int fromCell = CellAt(from);
            int startCell = regions[fromCell] != NoRegion ? fromCell : NearestCell(from, AnyRegion);
            int region = regions[startCell];

            int toCell = CellAt(to);
            int goalCell = regions[toCell] == region ? toCell : NearestCell(to, region);

            // A destination on reachable water is reached exactly, not rounded to a cell.
            Vector2 end = goalCell == toCell ? to : CellCenter(goalCell);

            // Cannot fail: both cells are in the same region.
            if (!search.TryFindPath(
                startCell % width, startCell / width, goalCell % width, goalCell / width, cells))
            {
                return false;
            }

            route.Clear();
            route.Add(from);

            // A start that is not on water first sails to the nearest water.
            if (startCell != fromCell)
            {
                route.Add(CellCenter(startCell));
            }

            for (int i = 1; i < cells.Count; i++)
            {
                route.Add(CellCenter(cells[i]));
            }

            route.Add(end);
            Smooth(from, waypoints);
            return true;
        }

        /// <summary>
        /// Copies the route without the points a straight leg can skip. Two consecutive
        /// points of the route are always an acceptable leg, so a point is only skipped
        /// when the leg that replaces it is clear.
        /// </summary>
        private void Smooth(Vector2 from, List<Vector2> waypoints)
        {
            Vector2 anchor = from;

            for (int i = 1; i < route.Count - 1; i++)
            {
                if (!NavigationLineOfSight.IsClear(grid, anchor, route[i + 1]))
                {
                    AddWaypoint(waypoints, from, route[i]);
                    anchor = route[i];
                }
            }

            AddWaypoint(waypoints, from, route[route.Count - 1]);
        }

        private static void AddWaypoint(List<Vector2> waypoints, Vector2 from, Vector2 point)
        {
            Vector2 previous = waypoints.Count > 0 ? waypoints[waypoints.Count - 1] : from;

            // Compared per component: Vector2 equality is approximate and would drop a
            // very short leg.
            if (point.x != previous.x || point.y != previous.y)
            {
                waypoints.Add(point);
            }
        }

        /// <returns>The number of regions.</returns>
        private int LabelRegions()
        {
            int count = 0;
            var pending = new Stack<int>();

            for (int cell = 0; cell < regions.Length; cell++)
            {
                if (regions[cell] != NoRegion || !grid.IsNavigable(cell % width, cell / width))
                {
                    continue;
                }

                count++;
                regions[cell] = count;
                pending.Push(cell);

                // Iterative: a sea can hold hundreds of thousands of cells.
                while (pending.Count > 0)
                {
                    int current = pending.Pop();
                    int x = current % width;
                    int y = current / width;

                    Label(x - 1, y, count, pending);
                    Label(x + 1, y, count, pending);
                    Label(x, y - 1, count, pending);
                    Label(x, y + 1, count, pending);
                }
            }

            return count;
        }

        private void Label(int x, int y, int region, Stack<int> pending)
        {
            // False outside the grid.
            if (!grid.IsNavigable(x, y))
            {
                return;
            }

            int cell = y * width + x;

            if (regions[cell] == NoRegion)
            {
                regions[cell] = region;
                pending.Push(cell);
            }
        }

        /// <summary>
        /// Index of the cell of a region, or of any region, whose center is nearest to a
        /// point of the map, in cells. The lowest index wins a tie. -1 when there is none.
        /// </summary>
        private int NearestCell(Vector2 point, int region)
        {
            double pointX = (double)point.x * width;
            double pointY = (double)point.y * height;
            double nearestDistance = double.MaxValue;
            int nearest = -1;

            for (int y = 0; y < height; y++)
            {
                double offsetY = y + 0.5 - pointY;
                double squaredY = offsetY * offsetY;

                // No cell of a row this far away can be nearer.
                if (squaredY >= nearestDistance)
                {
                    continue;
                }

                int row = y * width;

                for (int x = 0; x < width; x++)
                {
                    int cellRegion = regions[row + x];

                    if (cellRegion == NoRegion || (region != AnyRegion && cellRegion != region))
                    {
                        continue;
                    }

                    double offsetX = x + 0.5 - pointX;
                    double distance = offsetX * offsetX + squaredY;

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = row + x;
                    }
                }
            }

            return nearest;
        }

        /// <summary>Index of the cell under a point of the map.</summary>
        private int CellAt(Vector2 point)
        {
            // The same rounding as NavigationGrid: a coordinate of exactly 1 belongs to
            // the last cell.
            int x = Mathf.Min((int)(point.x * width), width - 1);
            int y = Mathf.Min((int)(point.y * height), height - 1);

            return y * width + x;
        }

        private Vector2 CellCenter(int cell)
        {
            return new Vector2((cell % width + 0.5f) / width, (cell / width + 0.5f) / height);
        }

        private static Vector2 ClampToMap(Vector2 point)
        {
            return new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
        }

        private static bool IsNaN(Vector2 value)
        {
            return float.IsNaN(value.x) || float.IsNaN(value.y);
        }
    }
}
