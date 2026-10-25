using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class StartingBerthsTests
    {
        private const string Land = "....................";
        private const string Sea = "~~~~~~~~~~~~~~~~~~~~";

        // 20 by 12, water on the six bottom rows, the port facing south at (8, 4): water rows 4 and 5, land row 6.
        private static CityGrid SeaGrid()
        {
            CityGrid grid = TestHarbours.FromRows(Land, Land, Land, Land, Land, Land, Sea, Sea, Sea, Sea, Sea, Sea);
            TestHarbours.PortSouth(grid, 8, 4);
            return grid;
        }

        [Test]
        public void Place_PutsTheCountNearestThePort()
        {
            CityGrid grid = SeaGrid();

            Assert.AreEqual(10, StartingBerths.Place(grid, 10));
            Assert.AreEqual(10, grid.Berths.Count);

            float[] distance = StartingBerths.DistancesFromPort(grid);
            var path = new List<Vector2Int>();
            float farthest = 0f;
            foreach (Vector2Int berth in grid.Berths)
            {
                Assert.IsTrue(grid.IsNavigable(berth), berth.ToString());
                Assert.IsTrue(HarbourPaths.TryFindExit(grid, berth, path), berth.ToString());
                farthest = Mathf.Max(farthest, distance[berth.y * grid.Width + berth.x]);
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (grid.IsNavigable(cell) && !grid.IsBerth(cell))
                    {
                        Assert.GreaterOrEqual(distance[y * grid.Width + x], farthest, cell.ToString());
                    }
                }
            }
        }

        [Test]
        public void Place_NotEnoughWater_PlacesWhatFits()
        {
            // The port fills the grid's width; the only free water is the 4 cells of the bottom row, on the edge.
            CityGrid grid = TestHarbours.FromRows(
                "....",
                "~~~~",
                "~~~~",
                "~~~~");
            TestHarbours.PortSouth(grid, 0, 1);

            Assert.AreEqual(4, StartingBerths.Place(grid, 10));
            Assert.AreEqual(4, grid.Berths.Count);
        }

        [Test]
        public void Place_PortNotJoined_PlacesNone()
        {
            // A pond closed in by land: the port faces its water but the water never reaches the edge.
            CityGrid grid = TestHarbours.FromRows(
                "..........",
                "..........",
                "..........",
                "..~~~~~~..",
                "..~~~~~~..",
                "..........",
                "..........");
            TestHarbours.PortSouth(grid, 2, 2);

            Assert.AreEqual(0, StartingBerths.Place(grid, 10));
            Assert.AreEqual(0, grid.Berths.Count);
        }

        [Test]
        public void Place_SameGrid_SameBerths()
        {
            CityGrid first = SeaGrid();
            CityGrid second = SeaGrid();

            StartingBerths.Place(first, 10);
            StartingBerths.Place(second, 10);

            CollectionAssert.AreEqual(first.Berths, second.Berths);
        }
    }
}
