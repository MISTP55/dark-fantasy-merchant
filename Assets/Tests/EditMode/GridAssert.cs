using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    internal static class GridAssert
    {
        public static int CountNavigable(NavigationGrid grid)
        {
            int count = 0;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsNavigable(x, y))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>Normalized position of the center of a cell.</summary>
        public static Vector2 CellCenter(NavigationGrid grid, int x, int y)
        {
            return new Vector2((x + 0.5f) / grid.Width, (y + 0.5f) / grid.Height);
        }

        /// <summary>
        /// Normalized position of a point given in cells: cell (x, y) spans x to x + 1.
        /// </summary>
        public static Vector2 CellPoint(NavigationGrid grid, float x, float y)
        {
            return new Vector2(x / grid.Width, y / grid.Height);
        }

        /// <summary>
        /// Builds a grid from rows of text, the top row first: '.' is water, any other
        /// character is land.
        /// </summary>
        public static NavigationGrid FromRows(params string[] rows)
        {
            var grid = new NavigationGrid(rows[0].Length, rows.Length);

            for (int row = 0; row < rows.Length; row++)
            {
                int y = rows.Length - 1 - row;

                for (int x = 0; x < rows[row].Length; x++)
                {
                    if (rows[row][x] == '.')
                    {
                        grid.SetNavigable(x, y, true);
                    }
                }
            }

            return grid;
        }
    }
}
