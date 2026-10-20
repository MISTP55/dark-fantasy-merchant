using System;
using System.Collections.Generic;
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
        public void TheRouteIsTheOneThePathfinderFinds()
        {
            PortTravelTable table = Complete(Cell(0, 0), Cell(5, 0));
            var expected = new List<Vector2>();
            navigation.TryFindPath(Cell(0, 0), Cell(5, 0), expected);

            Assert.Greater(expected.Count, 1);
            CollectionAssert.AreEqual(expected, table.RouteBetween(0, 1));
        }

        [Test]
        public void TheWayBack_IsTheSameRouteTheOtherWay()
        {
            PortTravelTable table = Complete(Cell(0, 0), Cell(5, 0));
            var there = new List<Vector2>(table.RouteBetween(0, 1));
            var back = new List<Vector2>(table.RouteBetween(1, 0));

            // Without where each starts: there ends on 1's anchorage, back on 0's.
            there.Insert(0, Cell(0, 0));
            back.Insert(0, Cell(5, 0));
            back.Reverse();

            CollectionAssert.AreEqual(there, back);
        }

        [Test]
        public void ThereIsNoRoute_ToAnotherSea_ToItself_OrBeforeThePairIsMeasured()
        {
            PortTravelTable table = Complete(Cell(0, 3), Cell(2, 3), Cell(7, 0));

            Assert.IsNull(table.RouteBetween(0, 2));
            Assert.IsNull(table.RouteBetween(2, 1));
            Assert.IsNull(table.RouteBetween(1, 1));
            Assert.IsNull(new PortTravelTable(new[] { Cell(0, 3), Cell(2, 3) }, navigation, projection, 5d).RouteBetween(0, 1));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 2)]
        public void RouteBetween_RejectsAPortOutOfRange(int from, int to)
        {
            PortTravelTable table = Complete(Cell(0, 3), Cell(2, 3));

            Assert.Throws<ArgumentOutOfRangeException>(() => table.RouteBetween(from, to));
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
