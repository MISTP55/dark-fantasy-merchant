using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationCellSearchTests
    {
        private const float Sqrt2 = 1.41421356f;

        private static readonly string[] WallWithAGapAtTheTop =
        {
            ".....",
            "..#..",
            "..#..",
            "..#..",
            "..#..",
        };

        /// <summary>
        /// Checks that every step of a path follows the movement rules, and returns the
        /// path's cost.
        /// </summary>
        private static float AssertLegalPath(NavigationGrid grid, List<int> cells)
        {
            float cost = 0f;

            for (int i = 0; i < cells.Count; i++)
            {
                int x = cells[i] % grid.Width;
                int y = cells[i] / grid.Width;

                Assert.IsTrue(grid.IsNavigable(x, y), $"cell ({x}, {y}) is land");

                if (i == 0)
                {
                    continue;
                }

                int previousX = cells[i - 1] % grid.Width;
                int previousY = cells[i - 1] / grid.Width;
                int stepX = x - previousX;
                int stepY = y - previousY;

                Assert.LessOrEqual(Math.Abs(stepX), 1, "step x");
                Assert.LessOrEqual(Math.Abs(stepY), 1, "step y");
                Assert.IsTrue(stepX != 0 || stepY != 0, "a step that does not move");

                if (stepX != 0 && stepY != 0)
                {
                    Assert.IsTrue(grid.IsNavigable(previousX + stepX, previousY), "cut a corner");
                    Assert.IsTrue(grid.IsNavigable(previousX, previousY + stepY), "cut a corner");
                    cost += Sqrt2;
                }
                else
                {
                    cost += 1f;
                }
            }

            return cost;
        }

        [Test]
        public void ANullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NavigationCellSearch(null));
        }

        [Test]
        public void ANullList_Throws()
        {
            var search = new NavigationCellSearch(GridAssert.FromRows(".."));

            Assert.Throws<ArgumentNullException>(() => search.TryFindPath(0, 0, 1, 0, null));
        }

        [Test]
        public void AStraightCorridor_IsFollowedCellByCell()
        {
            NavigationGrid grid = GridAssert.FromRows(".....");
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 4, 0, cells);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, cells);
        }

        [Test]
        public void OpenWater_IsCrossedDiagonally()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "....",
                "....",
                "....",
                "....");
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 3, 3, cells);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { 0, 5, 10, 15 }, cells);
        }

        [Test]
        public void AWall_IsSailedAround_ByTheShortestWay()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 4, 0, cells);

            Assert.IsTrue(found);
            Assert.AreEqual(0, cells[0], "starts on the start cell");
            Assert.AreEqual(4, cells[cells.Count - 1], "ends on the goal cell");

            // Up to (1, 4), east to (3, 4), down to (4, 0). The corners of the wall
            // cannot be cut, so the gap takes two side moves.
            Assert.AreEqual(8f + 2f * Sqrt2, AssertLegalPath(grid, cells), 1e-3f);
        }

        [Test]
        public void ADiagonalGap_IsNotCrossed()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "...",
                ".#.",
                "..#");
            var cells = new List<int>();

            // (1, 0) and (2, 1) touch by a corner, between two land cells.
            bool found = new NavigationCellSearch(grid).TryFindPath(1, 0, 2, 1, cells);

            Assert.IsTrue(found);
            Assert.AreEqual(6f, AssertLegalPath(grid, cells), 1e-3f);
        }

        [Test]
        public void AGoalBehindADiagonalGapOnly_IsNotReached()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#.",
                ".#");
            var cells = new List<int> { 99 };

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 1, 1, cells);

            Assert.IsFalse(found);
            Assert.IsEmpty(cells);
        }

        [Test]
        public void AGoalInAnotherRegion_IsNotReached()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..#..",
                "..#..");
            var cells = new List<int> { 99 };

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 4, 0, cells);

            Assert.IsFalse(found);
            Assert.IsEmpty(cells);
        }

        [Test]
        public void TheStartCellItself_IsAPathOfOneCell()
        {
            NavigationGrid grid = GridAssert.FromRows("...");
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(1, 0, 1, 0, cells);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { 1 }, cells);
        }

        [TestCase(1, 0, 0, 0)]
        [TestCase(0, 0, 1, 0)]
        [TestCase(-1, 0, 0, 0)]
        [TestCase(0, 0, 3, 0)]
        [TestCase(0, 0, 0, 1)]
        public void ALandOrOutsideCell_AsStartOrGoal_IsNotReached(int startX, int startY, int goalX, int goalY)
        {
            NavigationGrid grid = GridAssert.FromRows(".#.");
            var cells = new List<int> { 99 };

            bool found = new NavigationCellSearch(grid).TryFindPath(startX, startY, goalX, goalY, cells);

            Assert.IsFalse(found);
            Assert.IsEmpty(cells);
        }

        [Test]
        public void SearchesInARow_GiveTheSameResultsAsFreshSearches()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var reused = new NavigationCellSearch(grid);
            var queries = new[]
            {
                new[] { 0, 0, 4, 0 },
                new[] { 4, 4, 0, 0 },
                new[] { 1, 1, 1, 1 },
                new[] { 0, 0, 2, 0 },
                new[] { 3, 0, 0, 3 },
                new[] { 0, 0, 4, 0 },
            };

            foreach (int[] query in queries)
            {
                var expected = new List<int>();
                var actual = new List<int>();

                bool expectedFound = new NavigationCellSearch(grid)
                    .TryFindPath(query[0], query[1], query[2], query[3], expected);
                bool actualFound = reused.TryFindPath(query[0], query[1], query[2], query[3], actual);

                Assert.AreEqual(expectedFound, actualFound);
                CollectionAssert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void AFullSizeGrid_OfOpenWater_IsCrossedCornerToCorner()
        {
            var grid = new NavigationGrid(1024, 879);
            grid.Clear(true);
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 1023, 878, cells);

            Assert.IsTrue(found);

            // 878 diagonal moves and 145 side moves. The search may be 0.1 % off.
            float shortest = 878f * Sqrt2 + 145f;
            Assert.AreEqual(shortest, AssertLegalPath(grid, cells), shortest * 0.002f);
        }

        [Test]
        public void AFullSizeGrid_WithAWallAcrossIt_IsSailedAround()
        {
            var grid = new NavigationGrid(1024, 879);
            grid.Clear(true);

            // A wall from the bottom row up to the row below the top one.
            for (int y = 0; y < 878; y++)
            {
                grid.SetNavigable(512, y, false);
            }

            var cells = new List<int>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 1023, 0, cells);

            stopwatch.Stop();
            Debug.Log($"Cell search around a full-height wall: {stopwatch.ElapsedMilliseconds} ms, {cells.Count} cells.");

            Assert.IsTrue(found);
            Assert.AreEqual(0, cells[0]);
            Assert.AreEqual(1023, cells[cells.Count - 1]);

            // Up to (511, 878), east to (513, 878), down to (1023, 0).
            float shortest = (511f * Sqrt2 + 367f) + 2f + (510f * Sqrt2 + 368f);
            Assert.AreEqual(shortest, AssertLegalPath(grid, cells), shortest * 0.002f);
        }
    }
}
