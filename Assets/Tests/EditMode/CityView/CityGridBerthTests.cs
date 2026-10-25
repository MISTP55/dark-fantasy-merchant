using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityGridBerthTests
    {
        // The port's water at x 1..4, y 3..4 and land above it; the sea in the three rows below.
        private static CityGrid Harbour()
        {
            CityGrid grid = TestHarbours.FromRows(
                "........",
                "........",
                "........",
                ".~~~~...",
                ".~~~~...",
                "~~~~~~~~",
                "~~~~~~~~",
                "~~~~~~~~");
            TestHarbours.PortSouth(grid, 1, 3);
            return grid;
        }

        [Test]
        public void PlaceBerth_OnWater_IsListed()
        {
            CityGrid grid = Harbour();

            grid.PlaceBerth(new Vector2Int(2, 1));

            CollectionAssert.AreEqual(new[] { new Vector2Int(2, 1) }, grid.Berths);
            Assert.IsTrue(grid.IsBerth(new Vector2Int(2, 1)));
            Assert.IsFalse(grid.IsBerth(new Vector2Int(3, 1)));
        }

        [Test]
        public void CanPlaceBerth_RefusesLandOutsideThePortAndATakenCell()
        {
            CityGrid grid = Harbour();
            grid.PlaceBerth(new Vector2Int(2, 1));

            Assert.IsFalse(grid.CanPlaceBerth(new Vector2Int(6, 6)), "land");
            Assert.IsFalse(grid.CanPlaceBerth(new Vector2Int(-1, 0)), "outside");
            Assert.IsFalse(grid.CanPlaceBerth(new Vector2Int(2, 3)), "under the port");
            Assert.IsFalse(grid.CanPlaceBerth(new Vector2Int(2, 1)), "taken");
            Assert.IsTrue(grid.CanPlaceBerth(new Vector2Int(3, 1)));
        }

        [Test]
        public void PlaceBerth_WhereRefused_Throws()
        {
            CityGrid grid = Harbour();

            Assert.Throws<System.ArgumentException>(() => grid.PlaceBerth(new Vector2Int(6, 6)));
            Assert.Throws<System.ArgumentException>(() => grid.PlaceBerth(new Vector2Int(2, 3)));
            CollectionAssert.IsEmpty(grid.Berths);
        }

        [Test]
        public void IsNavigable_WaterIsLandAndPortAreNot()
        {
            CityGrid grid = Harbour();
            grid.PlaceBerth(new Vector2Int(2, 1));

            Assert.IsTrue(grid.IsNavigable(new Vector2Int(5, 1)));
            Assert.IsTrue(grid.IsNavigable(new Vector2Int(2, 1)), "a berth");
            Assert.IsFalse(grid.IsNavigable(new Vector2Int(6, 6)), "land");
            Assert.IsFalse(grid.IsNavigable(new Vector2Int(2, 3)), "port");
            Assert.IsFalse(grid.IsNavigable(new Vector2Int(8, 1)), "outside");
        }

        [Test]
        public void CanPlacePort_OverABerth_IsRefused()
        {
            CityGrid grid = TestHarbours.FromRows(
                "........",
                "........",
                "........",
                ".~~~~...",
                ".~~~~...",
                "~~~~~~~~",
                "~~~~~~~~");
            var site = new PortSite(new Vector2Int(1, 2), PortFacing.South);
            Assert.IsTrue(grid.CanPlacePort(TestHarbours.Port, site));

            grid.PlaceBerth(new Vector2Int(2, 3));

            Assert.IsFalse(grid.CanPlacePort(TestHarbours.Port, site));
        }
    }
}
