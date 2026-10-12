using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PortTravelTableTests
    {
        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;

        [SetUp]
        public void SetUp()
        {
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#..##",
                "...#..#.");
            navigation = new NavigationPathfinder(grid);
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(grid, x, y);
        }

        private PortTravelTable Complete(params Vector2[] anchorages)
        {
            // 5 world units a day: one cell a day.
            var table = new PortTravelTable(anchorages, navigation, projection, 5d);

            while (!table.IsComplete)
            {
                table.MeasureNext();
            }

            return table;
        }

        [Test]
        public void ItMeasuresOnePairAtATime()
        {
            var table = new PortTravelTable(new[] { Cell(0, 3), Cell(2, 3), Cell(4, 3) }, navigation, projection, 5d);
            int steps = 0;

            while (!table.IsComplete)
            {
                table.MeasureNext();
                steps++;
            }

            Assert.AreEqual(3, steps);
            Assert.DoesNotThrow(() => table.MeasureNext());
        }

        [Test]
        public void DaysAreTheLengthOverTheSpeed_BothWays()
        {
            PortTravelTable table = Complete(Cell(0, 3), Cell(2, 3));

            Assert.AreEqual(2d, table.Days[0, 1], 1e-3);
            Assert.AreEqual(2d, table.Days[1, 0], 1e-3);
            Assert.AreEqual(0d, table.Days[0, 0]);
        }

        [Test]
        public void APortInAnotherSea_IsInfinitelyFar()
        {
            PortTravelTable table = Complete(Cell(0, 3), Cell(2, 3), Cell(7, 0));

            Assert.IsTrue(double.IsPositiveInfinity(table.Days[0, 2]));
            Assert.IsTrue(double.IsPositiveInfinity(table.Days[2, 1]));
            Assert.IsTrue(table.ReachesAnother(0));
            Assert.IsFalse(table.ReachesAnother(2));
        }

        [Test]
        public void BeforeItIsMeasured_APairIsInfinitelyFar()
        {
            var table = new PortTravelTable(new[] { Cell(0, 3), Cell(2, 3) }, navigation, projection, 5d);

            Assert.IsFalse(table.IsComplete);
            Assert.IsTrue(double.IsPositiveInfinity(table.Days[0, 1]));
        }

        [Test]
        public void FewerThanTwoPorts_AreCompleteAtOnce()
        {
            Assert.IsTrue(new PortTravelTable(new Vector2[0], navigation, projection, 5d).IsComplete);

            var single = new PortTravelTable(new[] { Cell(0, 3) }, navigation, projection, 5d);
            Assert.IsTrue(single.IsComplete);
            Assert.IsFalse(single.ReachesAnother(0));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsASpeedThatIsNotPositive(double worldUnitsPerDay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PortTravelTable(new[] { Cell(0, 3) }, navigation, projection, worldUnitsPerDay));
        }
    }
}
