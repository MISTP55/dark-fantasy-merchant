using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Whether a ship can sail in a straight line between two points of the map: every
    /// cell the segment touches, even by an edge or a corner, must be navigable.
    /// </summary>
    public static class NavigationLineOfSight
    {
        // In cells. Positions are floats, so a cell center is only known to about a
        // ten-thousandth of a cell: without a margin, whether a segment touches a
        // corner would depend on rounding.
        private const double Margin = 1e-3;

        /// <param name="from">Normalized map position.</param>
        /// <param name="to">Normalized map position.</param>
        /// <returns>False when either point is outside the map or NaN.</returns>
        public static bool IsClear(NavigationGrid grid, Vector2 from, Vector2 to)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            // Also false for a NaN point.
            if (!MapProjection.IsInsideMap(from) || !MapProjection.IsInsideMap(to))
            {
                return false;
            }

            // In cells: cell (x, y) spans x to x + 1.
            double startX = (double)from.x * grid.Width;
            double startY = (double)from.y * grid.Height;
            double deltaX = (double)to.x * grid.Width - startX;
            double deltaY = (double)to.y * grid.Height - startY;

            // Cells outside the grid are skipped: the edge of the map is not land.
            int firstColumn = Math.Max((int)Math.Floor(Math.Min(startX, startX + deltaX) - Margin), 0);
            int lastColumn = Math.Min((int)Math.Floor(Math.Max(startX, startX + deltaX) + Margin), grid.Width - 1);

            for (int x = firstColumn; x <= lastColumn; x++)
            {
                // The part [first, last] of the segment that lies over this column.
                double first = 0.0;
                double last = 1.0;

                if (deltaX != 0.0)
                {
                    double enter = (x - Margin - startX) / deltaX;
                    double exit = (x + 1 + Margin - startX) / deltaX;

                    if (enter > exit)
                    {
                        double swap = enter;
                        enter = exit;
                        exit = swap;
                    }

                    first = Math.Max(first, enter);
                    last = Math.Min(last, exit);

                    if (first > last)
                    {
                        continue;
                    }
                }

                double firstY = startY + deltaY * first;
                double lastY = startY + deltaY * last;
                int firstRow = Math.Max((int)Math.Floor(Math.Min(firstY, lastY) - Margin), 0);
                int lastRow = Math.Min((int)Math.Floor(Math.Max(firstY, lastY) + Margin), grid.Height - 1);

                for (int y = firstRow; y <= lastRow; y++)
                {
                    if (!grid.IsNavigable(x, y))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
