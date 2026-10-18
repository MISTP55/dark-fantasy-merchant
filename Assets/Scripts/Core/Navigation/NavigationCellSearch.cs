using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Finds the shortest sequence of cells between two cells of a
    /// <see cref="NavigationGrid"/> (A*). A ship moves to a side neighbour, or to a
    /// diagonal one when both cells beside the move are navigable, so it never cuts
    /// the corner of a land cell. The grid must not change between searches.
    /// </summary>
    public sealed class NavigationCellSearch
    {
        private const float DiagonalCost = 1.41421356f;

        // Slightly above 1: in open water every cell on the way has the same estimate,
        // and the search would expand all of them. The weight makes it prefer the cells
        // nearer the goal, for a path at most 0.1 % longer than the shortest one.
        private const float HeuristicWeight = 1.001f;

        private readonly NavigationGrid grid;
        private readonly int width;

        // One entry per cell, allocated by the first search and reused by the next ones.
        private float[] costs;
        private int[] parents;

        // generation * 2 for a cell seen by the current search, + 1 once it is settled.
        // Anything else is left over from an earlier search.
        private int[] marks;
        private int generation;

        // Binary heap of cells ordered by estimated total cost. A cell whose cost
        // improves is pushed again; its older entries are skipped when popped.
        private float[] heapKeys = new float[256];
        private int[] heapCells = new int[256];
        private int heapCount;

        public NavigationCellSearch(NavigationGrid grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            width = grid.Width;
        }

        /// <param name="cells">
        /// Cleared, then filled with the indexes (y * width + x) of the cells from the
        /// start to the goal, both included.
        /// </param>
        /// <returns>
        /// False, leaving <paramref name="cells"/> empty, when either cell is not
        /// navigable or the goal cannot be reached.
        /// </returns>
        public bool TryFindPath(int startX, int startY, int goalX, int goalY, List<int> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            cells.Clear();

            if (!grid.IsNavigable(startX, startY) || !grid.IsNavigable(goalX, goalY))
            {
                return false;
            }

            BeginSearch();

            int start = startY * width + startX;
            int goal = goalY * width + goalX;
            int settled = generation * 2 + 1;

            costs[start] = 0f;
            parents[start] = -1;
            marks[start] = generation * 2;
            Push(Estimate(startX, startY, goalX, goalY), start);

            while (heapCount > 0)
            {
                int cell = Pop();

                if (marks[cell] == settled)
                {
                    continue;
                }

                marks[cell] = settled;

                if (cell == goal)
                {
                    for (int step = goal; step >= 0; step = parents[step])
                    {
                        cells.Add(step);
                    }

                    cells.Reverse();
                    return true;
                }

                int x = cell % width;
                int y = cell / width;
                float cost = costs[cell];

                bool west = grid.IsNavigable(x - 1, y);
                bool east = grid.IsNavigable(x + 1, y);
                bool south = grid.IsNavigable(x, y - 1);
                bool north = grid.IsNavigable(x, y + 1);

                if (west)
                {
                    Reach(x - 1, y, cell, cost + 1f, goalX, goalY);
                }

                if (east)
                {
                    Reach(x + 1, y, cell, cost + 1f, goalX, goalY);
                }

                if (south)
                {
                    Reach(x, y - 1, cell, cost + 1f, goalX, goalY);
                }

                if (north)
                {
                    Reach(x, y + 1, cell, cost + 1f, goalX, goalY);
                }

                // A diagonal move needs both cells beside it.
                if (west && south && grid.IsNavigable(x - 1, y - 1))
                {
                    Reach(x - 1, y - 1, cell, cost + DiagonalCost, goalX, goalY);
                }

                if (east && south && grid.IsNavigable(x + 1, y - 1))
                {
                    Reach(x + 1, y - 1, cell, cost + DiagonalCost, goalX, goalY);
                }

                if (west && north && grid.IsNavigable(x - 1, y + 1))
                {
                    Reach(x - 1, y + 1, cell, cost + DiagonalCost, goalX, goalY);
                }

                if (east && north && grid.IsNavigable(x + 1, y + 1))
                {
                    Reach(x + 1, y + 1, cell, cost + DiagonalCost, goalX, goalY);
                }
            }

            return false;
        }

        private void BeginSearch()
        {
            if (costs == null)
            {
                int cellCount = width * grid.Height;

                costs = new float[cellCount];
                parents = new int[cellCount];
                marks = new int[cellCount];
            }

            // Marks hold generation * 2 + 1 at most.
            if (generation >= int.MaxValue / 2 - 1)
            {
                Array.Clear(marks, 0, marks.Length);
                generation = 0;
            }

            generation++;
            heapCount = 0;
        }

        /// <summary>Records a way to a navigable cell if it is the best one so far.</summary>
        private void Reach(int x, int y, int from, float cost, int goalX, int goalY)
        {
            int cell = y * width + x;
            int seen = generation * 2;
            int mark = marks[cell];

            if (mark == seen + 1 || (mark == seen && cost >= costs[cell]))
            {
                return;
            }

            costs[cell] = cost;
            parents[cell] = from;
            marks[cell] = seen;
            Push(cost + Estimate(x, y, goalX, goalY), cell);
        }

        /// <summary>Cost of the way to the goal over open water (octile distance).</summary>
        private static float Estimate(int x, int y, int goalX, int goalY)
        {
            int deltaX = Math.Abs(goalX - x);
            int deltaY = Math.Abs(goalY - y);
            int diagonal = Math.Min(deltaX, deltaY);

            return HeuristicWeight * (deltaX + deltaY - 2 * diagonal + diagonal * DiagonalCost);
        }

        private void Push(float key, int cell)
        {
            if (heapCount == heapKeys.Length)
            {
                Array.Resize(ref heapKeys, heapCount * 2);
                Array.Resize(ref heapCells, heapCount * 2);
            }

            int index = heapCount++;

            while (index > 0)
            {
                int parent = (index - 1) / 2;

                if (heapKeys[parent] <= key)
                {
                    break;
                }

                heapKeys[index] = heapKeys[parent];
                heapCells[index] = heapCells[parent];
                index = parent;
            }

            heapKeys[index] = key;
            heapCells[index] = cell;
        }

        private int Pop()
        {
            int top = heapCells[0];

            heapCount--;

            // The last entry is moved down from the root to its place.
            float key = heapKeys[heapCount];
            int cell = heapCells[heapCount];
            int index = 0;

            while (true)
            {
                int child = index * 2 + 1;

                if (child >= heapCount)
                {
                    break;
                }

                if (child + 1 < heapCount && heapKeys[child + 1] < heapKeys[child])
                {
                    child++;
                }

                if (heapKeys[child] >= key)
                {
                    break;
                }

                heapKeys[index] = heapKeys[child];
                heapCells[index] = heapCells[child];
                index = child;
            }

            heapKeys[index] = key;
            heapCells[index] = cell;
            return top;
        }
    }
}
