using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class SeaRoutesTests
    {
        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;
        private List<Vector2> waypoints;

        [SetUp]
        public void SetUp()
        {
            // 40 x 20 world units over 8 x 4 cells of 5 units. Cell (7, 0) is a pond of its own.
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#..##",
                "...#..#.");
            navigation = new NavigationPathfinder(grid);
            waypoints = new List<Vector2>();
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(grid, x, y);
        }

        [Test]
        public void WithoutAPathfinder_ItIsAStraightLine()
        {
            bool reached = SeaRoutes.TryMeasure(
                null, projection, new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.5f), waypoints, out float length);

            Assert.IsTrue(reached);
            Assert.AreEqual(20f, length, 1e-4f);
        }

        [Test]
        public void OverOpenWater_ItIsTheDistance()
        {
            bool reached = SeaRoutes.TryMeasure(navigation, projection, Cell(0, 3), Cell(2, 3), waypoints, out float length);

            Assert.IsTrue(reached);
            Assert.AreEqual(10f, length, 1e-3f);
        }

        [Test]
        public void AroundLand_ItIsLongerThanTheStraightLine()
        {
            bool reached = SeaRoutes.TryMeasure(navigation, projection, Cell(2, 0), Cell(4, 0), waypoints, out float length);

            Assert.IsTrue(reached);
            Assert.Greater(length, 10f + 1f);
        }

        [Test]
        public void APointInAnotherSea_IsNotReached()
        {
            Assert.IsFalse(SeaRoutes.TryMeasure(navigation, projection, Cell(0, 3), Cell(7, 0), waypoints, out _));
        }

        [Test]
        public void TheSamePoint_IsReachedAtNoDistance()
        {
            bool reached = SeaRoutes.TryMeasure(navigation, projection, Cell(0, 3), Cell(0, 3), waypoints, out float length);

            Assert.IsTrue(reached);
            Assert.AreEqual(0f, length);
        }

        [Test]
        public void ANaNPoint_IsNotReached()
        {
            var nowhere = new Vector2(float.NaN, 0.5f);

            Assert.IsFalse(SeaRoutes.TryMeasure(navigation, projection, Cell(0, 3), nowhere, waypoints, out _));
            Assert.IsFalse(SeaRoutes.TryMeasure(null, projection, nowhere, Cell(0, 3), waypoints, out _));
        }
    }
}
