using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>Small city grids with water, for the harbour tests.</summary>
    public static class TestHarbours
    {
        /// <summary>The 4 by 3 port, its 2 front rows on water.</summary>
        public static readonly PortFootprint Port = new PortFootprint(new Footprint(4, 3), 2);

        /// <summary>A grid from rows of equal length, the top row having the largest y: '~' is water, '.' is land.</summary>
        public static CityGrid FromRows(params string[] rows)
        {
            var water = new List<Vector2Int>();
            for (int row = 0; row < rows.Length; row++)
            {
                int y = rows.Length - 1 - row;
                for (int x = 0; x < rows[row].Length; x++)
                {
                    if (rows[row][x] == '~')
                    {
                        water.Add(new Vector2Int(x, y));
                    }
                }
            }

            return new CityGrid(rows[0].Length, rows.Length, water, RoadContact.SidesOnly);
        }

        /// <summary>
        /// A harbour of one berth, 2 cells from the edge of the grid: at 2 cells per second a ship
        /// crosses it in 1 second either way.
        /// </summary>
        public static Harbour OneBerth(float cellsPerSecond = 2f)
        {
            CityGrid grid = FromRows(
                "........",
                ".~~~~...",
                ".~~~~...",
                "~~~~~~~~",
                "~~~~~~~~",
                "~~~~~~~~",
                "~~~~~~~~",
                "~~~~~~~~");
            PortSouth(grid, 1, 5);
            grid.PlaceBerth(new Vector2Int(3, 2));
            return new Harbour(grid, cellsPerSecond);
        }

        /// <summary>Places the port facing south with its origin at (x, y): water on its rows y and y + 1, land on y + 2.</summary>
        public static PlacedBuilding PortSouth(CityGrid grid, int x, int y)
        {
            return grid.PlacePort(Port, new PortSite(new Vector2Int(x, y), PortFacing.South));
        }
    }
}
