using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationPathfinderTests
    {
        [Test]
        public void ANullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NavigationPathfinder(null));
        }

        [Test]
        public void HasNavigableCells_TellsWhetherTheGridHasWater()
        {
            Assert.IsFalse(new NavigationPathfinder(new NavigationGrid(4, 3)).HasNavigableCells);
            Assert.IsTrue(new NavigationPathfinder(GridAssert.FromRows("###", "#.#")).HasNavigableCells);
        }

        [Test]
        public void NearestNavigable_OfAPointOnWater_IsThePointItself()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#..",
                "###");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 point = GridAssert.CellPoint(grid, 1.2f, 1.7f);

            bool found = pathfinder.TryGetNearestNavigable(point, out Vector2 nearest);

            Assert.IsTrue(found);
            Assert.AreEqual(point.x, nearest.x);
            Assert.AreEqual(point.y, nearest.y);
        }

        [Test]
        public void NearestNavigable_OfAPointOnLand_IsTheCenterOfTheNearestWaterCell()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#..",
                "###");
            var pathfinder = new NavigationPathfinder(grid);

            bool found = pathfinder.TryGetNearestNavigable(
                GridAssert.CellPoint(grid, 0.8f, 1.5f), out Vector2 nearest);

            Assert.IsTrue(found);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 1), nearest);
        }

        [Test]
        public void NearestNavigable_IsMeasuredInCells_NotInNormalizedSpace()
        {
            // Eight cells wide, two high: one cell up is 0.5 away in normalized space,
            // three cells to the right only 0.375.
            NavigationGrid grid = GridAssert.FromRows(
                ".#######",
                "###.####");
            var pathfinder = new NavigationPathfinder(grid);

            pathfinder.TryGetNearestNavigable(GridAssert.CellCenter(grid, 0, 0), out Vector2 nearest);

            TestAssert.AreEqual(GridAssert.CellCenter(grid, 0, 1), nearest);
        }

        [Test]
        public void NearestNavigable_OfAPointOutsideTheMap_StartsFromTheClampedPoint()
        {
            NavigationGrid grid = GridAssert.FromRows("#.");
            var pathfinder = new NavigationPathfinder(grid);

            bool found = pathfinder.TryGetNearestNavigable(new Vector2(5f, 0.25f), out Vector2 nearest);

            Assert.IsTrue(found);
            TestAssert.AreEqual(new Vector2(1f, 0.25f), nearest);
        }

        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.NaN)]
        public void NearestNavigable_OfANaNPoint_IsNotFound(float x, float y)
        {
            var pathfinder = new NavigationPathfinder(GridAssert.FromRows(".."));

            Assert.IsFalse(pathfinder.TryGetNearestNavigable(new Vector2(x, y), out _));
        }

        [Test]
        public void NearestNavigable_OnAGridWithoutWater_IsNotFound_AndGivesTheClampedPoint()
        {
            var pathfinder = new NavigationPathfinder(new NavigationGrid(4, 3));

            bool found = pathfinder.TryGetNearestNavigable(new Vector2(-2f, 0.5f), out Vector2 nearest);

            Assert.IsFalse(found);
            TestAssert.AreEqual(new Vector2(0f, 0.5f), nearest);
        }

        private static readonly string[] WallWithAGapAtTheTop =
        {
            ".....",
            "..#..",
            "..#..",
            "..#..",
            "..#..",
        };

        private static void AssertSamePoint(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, "x");
            Assert.AreEqual(expected.y, actual.y, "y");
        }

        /// <summary>Checks that every leg of a route follows the movement rules.</summary>
        private static void AssertRouteIsOnWater(NavigationGrid grid, Vector2 from, List<Vector2> waypoints)
        {
            Vector2 previous = from;

            for (int i = 0; i < waypoints.Count; i++)
            {
                Assert.IsTrue(
                    NavigationLineOfSight.IsClear(grid, previous, waypoints[i]),
                    $"leg {i}, from {previous} to {waypoints[i]}, touches land");
                previous = waypoints[i];
            }
        }

        [Test]
        public void TryFindPath_RejectsANullList()
        {
            var pathfinder = new NavigationPathfinder(GridAssert.FromRows(".."));

            Assert.Throws<ArgumentNullException>(
                () => pathfinder.TryFindPath(Vector2.zero, Vector2.one, null));
        }

        [Test]
        public void OpenWater_IsOneLeg_ToTheDestinationItself()
        {
            var grid = new NavigationGrid(6, 6);
            grid.Clear(true);
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 to = GridAssert.CellPoint(grid, 4.3f, 2.7f);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(GridAssert.CellCenter(grid, 0, 0), to, waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            AssertSamePoint(to, waypoints[0]);
        }

        [Test]
        public void AWall_IsSailedAround_InStraightLegs()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellCenter(grid, 0, 0);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, GridAssert.CellCenter(grid, 4, 0), waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);

            // One turn on each side of the gap; everything else is skipped.
            Assert.AreEqual(3, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 4), waypoints[0]);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 3, 4), waypoints[1]);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 4, 0), waypoints[2]);
        }

        [Test]
        public void ADiagonalGap_IsNotCrossed_WhenThereIsAWayAround()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "...",
                ".#.",
                "..#");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellCenter(grid, 1, 0);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, GridAssert.CellCenter(grid, 2, 1), waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);
            Assert.Greater(waypoints.Count, 1, "a single leg would cross the gap");
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 2, 1), waypoints[waypoints.Count - 1]);
        }

        [Test]
        public void ADiagonalGap_IsNotCrossed_WhenItIsTheOnlyWay()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#.",
                ".#");
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            // The two water cells are separate regions: the nearest cell the start can
            // reach is its own.
            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), GridAssert.CellCenter(grid, 1, 1), waypoints);

            Assert.IsTrue(found);
            Assert.IsEmpty(waypoints);
        }

        [Test]
        public void AChannelOneCellWide_IsFollowedThroughItsBend()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#####",
                "#...#",
                "#.###",
                "#.###",
                "#####");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellCenter(grid, 1, 1);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, GridAssert.CellCenter(grid, 3, 3), waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);
            Assert.AreEqual(2, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 3), waypoints[0]);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 3, 3), waypoints[1]);
        }

        [Test]
        public void ADestinationOnLand_EndsOnTheNearestWaterCell()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            // In the wall, nearer its west side.
            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), GridAssert.CellPoint(grid, 2.2f, 0.5f), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 0), waypoints[0]);
        }

        [Test]
        public void ADestinationInAnotherRegion_EndsOnTheNearestCellOfTheStartsRegion()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..#..",
                "..#..");
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), GridAssert.CellCenter(grid, 4, 0), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 0), waypoints[0]);
        }

        [Test]
        public void ADestinationOutsideTheMap_IsClampedToIt()
        {
            NavigationGrid grid = GridAssert.FromRows("....");
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), new Vector2(3f, 0.5f), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            AssertSamePoint(new Vector2(1f, 0.5f), waypoints[0]);
        }

        [Test]
        public void ADestinationOnTheCornerOfTheMap_IsReachedExactly()
        {
            var grid = new NavigationGrid(4, 4);
            grid.Clear(true);
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), new Vector2(1f, 1f), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            AssertSamePoint(new Vector2(1f, 1f), waypoints[0]);
        }

        [Test]
        public void AStartOnLand_FirstGoesToTheNearestWaterCell()
        {
            NavigationGrid grid = GridAssert.FromRows("#..");
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), GridAssert.CellCenter(grid, 2, 0), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(2, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 0), waypoints[0]);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 2, 0), waypoints[1]);
        }

        [TestCase(0.2f, 0.3f)]
        [TestCase(1.9f, 2.6f)]
        [TestCase(0.05f, 4.95f)]
        [TestCase(1.5f, 0.999f)]
        public void AStartThatIsNotACellCenter_StillGivesARouteOnWater(float cellX, float cellY)
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellPoint(grid, cellX, cellY);
            Vector2 to = GridAssert.CellPoint(grid, 4.8f, 0.1f);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, to, waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);
            AssertSamePoint(to, waypoints[waypoints.Count - 1]);
        }

        [Test]
        public void ARouteToTheStartItself_IsFound_AndEmpty()
        {
            NavigationGrid grid = GridAssert.FromRows("....");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 point = GridAssert.CellPoint(grid, 1.3f, 0.4f);
            var waypoints = new List<Vector2> { Vector2.one };

            bool found = pathfinder.TryFindPath(point, point, waypoints);

            Assert.IsTrue(found);
            Assert.IsEmpty(waypoints);
        }

        [TestCase(float.NaN, 0.5f, 0.5f, 0.5f)]
        [TestCase(0.5f, float.NaN, 0.5f, 0.5f)]
        [TestCase(0.5f, 0.5f, float.NaN, 0.5f)]
        [TestCase(0.5f, 0.5f, 0.5f, float.NaN)]
        public void ANaNPoint_GivesNoRoute(float fromX, float fromY, float toX, float toY)
        {
            var pathfinder = new NavigationPathfinder(GridAssert.FromRows("...."));
            var waypoints = new List<Vector2> { Vector2.one };

            bool found = pathfinder.TryFindPath(
                new Vector2(fromX, fromY), new Vector2(toX, toY), waypoints);

            Assert.IsFalse(found);
            Assert.IsEmpty(waypoints);
        }

        [Test]
        public void AGridWithoutWater_GivesNoRoute()
        {
            var pathfinder = new NavigationPathfinder(new NavigationGrid(4, 3));
            var waypoints = new List<Vector2> { Vector2.one };

            bool found = pathfinder.TryFindPath(new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), waypoints);

            Assert.IsFalse(found);
            Assert.IsEmpty(waypoints);
        }

        [Test]
        public void RoutesInARow_AreTheSameAsRoutesFromFreshPathfinders()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var reused = new NavigationPathfinder(grid);
            var points = new[]
            {
                GridAssert.CellCenter(grid, 0, 0),
                GridAssert.CellCenter(grid, 4, 0),
                GridAssert.CellPoint(grid, 2.2f, 0.5f),
                GridAssert.CellPoint(grid, 3.7f, 4.2f),
                GridAssert.CellCenter(grid, 0, 0),
            };

            for (int i = 1; i < points.Length; i++)
            {
                var expected = new List<Vector2>();
                var actual = new List<Vector2>();

                new NavigationPathfinder(grid).TryFindPath(points[i - 1], points[i], expected);
                reused.TryFindPath(points[i - 1], points[i], actual);

                CollectionAssert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void AFullSizeGrid_OfOpenWater_IsCrossedInOneLeg()
        {
            var grid = new NavigationGrid(1024, 879);
            grid.Clear(true);
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 to = GridAssert.CellCenter(grid, 1023, 878);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(GridAssert.CellCenter(grid, 0, 0), to, waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            AssertSamePoint(to, waypoints[0]);
        }

        [Test]
        public void AGridThatIsNotSquare_IsSailedAroundItsLand()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "........",
                "...#....");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellCenter(grid, 0, 0);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, GridAssert.CellCenter(grid, 7, 0), waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);
            Assert.Greater(waypoints.Count, 1);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 7, 0), waypoints[waypoints.Count - 1]);
        }
    }
}
