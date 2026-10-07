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
    }
}
