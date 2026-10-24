using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The cells of the road the player traces by dragging: an L.</summary>
    public static class RoadTrace
    {
        /// <summary>
        /// Fills cells (cleared first) with the L from start along x to end.x, then along y to end.y: start first,
        /// end last, no cell twice; one cell when start equals end.
        /// </summary>
        public static void Of(Vector2Int start, Vector2Int end, List<Vector2Int> cells)
        {
            cells.Clear();
            int stepX = Math.Sign(end.x - start.x);
            int stepY = Math.Sign(end.y - start.y);
            int x = start.x;
            cells.Add(new Vector2Int(x, start.y));
            while (x != end.x)
            {
                x += stepX;
                cells.Add(new Vector2Int(x, start.y));
            }

            int y = start.y;
            while (y != end.y)
            {
                y += stepY;
                cells.Add(new Vector2Int(end.x, y));
            }
        }
    }
}
