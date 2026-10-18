using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationGridPaintTests
    {
        private NavigationGrid grid;

        [SetUp]
        public void SetUp()
        {
            grid = new NavigationGrid(8, 8);
        }

        [Test]
        public void PaintDisc_SetsTheCellsWhoseCenterIsWithinTheRadius()
        {
            bool changed = grid.PaintDisc(GridAssert.CellCenter(grid, 4, 4), 1f, true);

            Assert.IsTrue(changed);
            Assert.IsTrue(grid.IsNavigable(4, 4));
            Assert.IsTrue(grid.IsNavigable(3, 4));
            Assert.IsTrue(grid.IsNavigable(5, 4));
            Assert.IsTrue(grid.IsNavigable(4, 3));
            Assert.IsTrue(grid.IsNavigable(4, 5));
            Assert.AreEqual(5, GridAssert.CountNavigable(grid), "diagonals are 1.41 cells away");
        }

        [Test]
        public void PaintDisc_WithALargerRadius_ReachesTheDiagonals()
        {
            grid.PaintDisc(GridAssert.CellCenter(grid, 4, 4), 1.5f, true);

            Assert.IsTrue(grid.IsNavigable(3, 3));
            Assert.IsTrue(grid.IsNavigable(5, 5));
            Assert.AreEqual(9, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintDisc_IsClippedAtTheEdges()
        {
            // Bottom-left corner of the map. Cell centers at 0.71, 1.58, 1.58 and 2.12 cells.
            grid.PaintDisc(Vector2.zero, 2f, true);

            Assert.IsTrue(grid.IsNavigable(0, 0));
            Assert.IsTrue(grid.IsNavigable(1, 0));
            Assert.IsTrue(grid.IsNavigable(0, 1));
            Assert.AreEqual(3, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintDisc_CenteredOutsideTheMap_PaintsThePartThatOverlapsIt()
        {
            // One cell to the left of the map, level with the center of row 4.
            var center = new Vector2(-1f / 8f, 4.5f / 8f);

            bool changed = grid.PaintDisc(center, 2f, true);

            Assert.IsTrue(changed);
            Assert.IsTrue(grid.IsNavigable(0, 3));
            Assert.IsTrue(grid.IsNavigable(0, 4));
            Assert.IsTrue(grid.IsNavigable(0, 5));
            Assert.AreEqual(3, GridAssert.CountNavigable(grid));
        }

        [TestCase(0.01f)]
        [TestCase(0f)]
        [TestCase(-3f)]
        public void PaintDisc_WithATinyRadius_PaintsTheCellUnderThePoint(float radius)
        {
            // Not on a cell center: (3.25, 6.75) in cells.
            bool changed = grid.PaintDisc(new Vector2(3.25f / 8f, 6.75f / 8f), radius, true);

            Assert.IsTrue(changed);
            Assert.IsTrue(grid.IsNavigable(3, 6));
            Assert.AreEqual(1, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintDisc_OnTheTopRightCorner_PaintsTheLastCell()
        {
            grid.PaintDisc(Vector2.one, 0f, true);

            Assert.IsTrue(grid.IsNavigable(7, 7));
            Assert.AreEqual(1, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintDisc_ReportsNoChange_WhenTheCellsAlreadyHaveTheValue()
        {
            Vector2 center = GridAssert.CellCenter(grid, 4, 4);

            Assert.IsFalse(grid.PaintDisc(center, 1f, false), "the grid starts non-navigable");
            Assert.IsTrue(grid.PaintDisc(center, 1f, true));
            Assert.IsFalse(grid.PaintDisc(center, 1f, true));
        }

        [Test]
        public void PaintDisc_NonNavigable_Erases()
        {
            grid.Clear(true);

            bool changed = grid.PaintDisc(GridAssert.CellCenter(grid, 4, 4), 1f, false);

            Assert.IsTrue(changed);
            Assert.IsFalse(grid.IsNavigable(4, 4));
            Assert.AreEqual(64 - 5, GridAssert.CountNavigable(grid));
        }

        [TestCase(float.NaN, 0.5f, 1f)]
        [TestCase(0.5f, float.NaN, 1f)]
        [TestCase(0.5f, 0.5f, float.NaN)]
        [TestCase(float.PositiveInfinity, 0.5f, 1f)]
        [TestCase(0.5f, 0.5f, float.PositiveInfinity)]
        public void PaintDisc_IgnoresNonFiniteInput(float u, float v, float radius)
        {
            Assert.IsFalse(grid.PaintDisc(new Vector2(u, v), radius, true));
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintStroke_CoversTheSegmentWithTheBrushRadius()
        {
            Vector2 from = GridAssert.CellCenter(grid, 2, 4);
            Vector2 to = GridAssert.CellCenter(grid, 5, 4);

            bool changed = grid.PaintStroke(from, to, 1f, true);

            Assert.IsTrue(changed);

            for (int x = 1; x <= 6; x++)
            {
                Assert.IsTrue(grid.IsNavigable(x, 4), $"({x}, 4)");
            }

            for (int x = 2; x <= 5; x++)
            {
                Assert.IsTrue(grid.IsNavigable(x, 3), $"({x}, 3)");
                Assert.IsTrue(grid.IsNavigable(x, 5), $"({x}, 5)");
            }

            Assert.AreEqual(14, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintStroke_BetweenDistantPoints_LeavesNoGap()
        {
            var wide = new NavigationGrid(64, 32);

            // A brush much smaller than a cell: only the cells under the samples are painted.
            wide.PaintStroke(new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f), 0.1f, true);

            for (int x = 7; x <= 57; x++)
            {
                bool columnPainted = false;

                for (int y = 0; y < wide.Height; y++)
                {
                    columnPainted |= wide.IsNavigable(x, y);
                }

                Assert.IsTrue(columnPainted, $"column {x}");
            }
        }

        [Test]
        public void PaintStroke_OfZeroLength_IsADisc()
        {
            Vector2 point = GridAssert.CellCenter(grid, 4, 4);
            var disc = new NavigationGrid(8, 8);
            disc.PaintDisc(point, 1.5f, true);

            grid.PaintStroke(point, point, 1.5f, true);

            CollectionAssert.AreEqual(disc.ToBytes(), grid.ToBytes());
        }

        [Test]
        public void PaintStroke_FromFarOutsideTheMap_PaintsOnlyThePartOverTheMap()
        {
            // A million million map widths to the left: more samples than can be counted,
            // let alone walked, unless the stroke is clipped to the map first.
            var from = new Vector2(-1e12f, 4.5f / 8f);
            Vector2 to = GridAssert.CellCenter(grid, 4, 4);

            bool changed = grid.PaintStroke(from, to, 0.1f, true);

            Assert.IsTrue(changed);

            for (int x = 0; x <= 4; x++)
            {
                Assert.IsTrue(grid.IsNavigable(x, 4), $"({x}, 4)");
            }

            Assert.AreEqual(5, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintStroke_ToFarOutsideTheMap_PaintsOnlyThePartOverTheMap()
        {
            Vector2 from = GridAssert.CellCenter(grid, 4, 4);
            var to = new Vector2(4.5f / 8f, 1e12f);

            bool changed = grid.PaintStroke(from, to, 0.1f, true);

            Assert.IsTrue(changed);

            for (int y = 4; y <= 7; y++)
            {
                Assert.IsTrue(grid.IsNavigable(4, y), $"(4, {y})");
            }

            Assert.AreEqual(4, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintStroke_EntirelyOutsideTheMap_PaintsNothing()
        {
            bool changed = grid.PaintStroke(new Vector2(-5f, 3f), new Vector2(-2f, 9f), 1f, true);

            Assert.IsFalse(changed);
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintStroke_ReportsNoChange_WhenTheCellsAlreadyHaveTheValue()
        {
            Vector2 from = GridAssert.CellCenter(grid, 2, 4);
            Vector2 to = GridAssert.CellCenter(grid, 5, 4);

            Assert.IsTrue(grid.PaintStroke(from, to, 1f, true));
            Assert.IsFalse(grid.PaintStroke(from, to, 1f, true));
        }

        [TestCase(float.NaN, 0.5f, 0.5f, 0.5f, 1f)]
        [TestCase(0.5f, 0.5f, 0.5f, float.NaN, 1f)]
        [TestCase(0.5f, 0.5f, 0.5f, 0.5f, float.NaN)]
        [TestCase(float.NegativeInfinity, 0.5f, 0.5f, 0.5f, 1f)]
        [TestCase(0.5f, 0.5f, float.PositiveInfinity, 0.5f, 1f)]
        public void PaintStroke_IgnoresNonFiniteInput(float fromU, float fromV, float toU, float toV, float radius)
        {
            bool changed = grid.PaintStroke(new Vector2(fromU, fromV), new Vector2(toU, toV), radius, true);

            Assert.IsFalse(changed);
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }
    }
}
