using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationLineOfSightTests
    {
        private static bool IsClear(NavigationGrid grid, int fromX, int fromY, int toX, int toY)
        {
            return NavigationLineOfSight.IsClear(
                grid,
                GridAssert.CellCenter(grid, fromX, fromY),
                GridAssert.CellCenter(grid, toX, toY));
        }

        [Test]
        public void ANullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => NavigationLineOfSight.IsClear(null, Vector2.zero, Vector2.one));
        }

        [Test]
        public void OpenWater_IsClear()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "....",
                "....",
                "....");

            Assert.IsTrue(IsClear(grid, 0, 0, 3, 2));
            Assert.IsTrue(IsClear(grid, 3, 2, 0, 0));
            Assert.IsTrue(IsClear(grid, 0, 2, 3, 0));
        }

        [Test]
        public void ALandCellOnTheWay_Blocks()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "....",
                "..#.",
                "....");

            Assert.IsFalse(IsClear(grid, 0, 1, 3, 1));
            Assert.IsFalse(IsClear(grid, 3, 1, 0, 1));
        }

        [Test]
        public void LandBesideTheLine_DoesNotBlock()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "####",
                "....",
                "####");

            Assert.IsTrue(IsClear(grid, 0, 1, 3, 1));
        }

        [Test]
        public void ADiagonalGapBetweenTwoLandCells_Blocks()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#.",
                ".#");

            Assert.IsFalse(IsClear(grid, 0, 0, 1, 1));
            Assert.IsFalse(IsClear(grid, 1, 1, 0, 0));
        }

        [Test]
        public void GrazingTheCornerOfALandCell_Blocks()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..",
                ".#");

            Assert.IsFalse(IsClear(grid, 0, 0, 1, 1));
            Assert.IsFalse(IsClear(grid, 1, 1, 0, 0));
        }

        [Test]
        public void ADiagonalThroughOpenWater_IsClear()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..",
                "..");

            Assert.IsTrue(IsClear(grid, 0, 0, 1, 1));
            Assert.IsTrue(IsClear(grid, 0, 1, 1, 0));
        }

        [Test]
        public void ALineAlongTheEdgeOfLand_Blocks()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "####",
                "....");

            // On the border between the water row and the land row.
            Assert.IsFalse(NavigationLineOfSight.IsClear(
                grid, GridAssert.CellPoint(grid, 0.5f, 1f), GridAssert.CellPoint(grid, 3.5f, 1f)));
        }

        [Test]
        public void AVerticalLine_IsCheckedCellByCell()
        {
            NavigationGrid open = GridAssert.FromRows(".", ".", ".");
            NavigationGrid blocked = GridAssert.FromRows(".", "#", ".");

            Assert.IsTrue(IsClear(open, 0, 0, 0, 2));
            Assert.IsFalse(IsClear(blocked, 0, 0, 0, 2));
            Assert.IsFalse(IsClear(blocked, 0, 2, 0, 0));
        }

        [Test]
        public void ASteepLine_SeesLandInTheColumnsItCrosses()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..",
                ".#",
                ".#",
                "..");

            // From the bottom-left cell to the top-right one: through the right column
            // only near the top, but it enters that column inside the land.
            Assert.IsFalse(IsClear(grid, 0, 0, 1, 3));
            Assert.IsTrue(IsClear(grid, 0, 0, 0, 3));
        }

        [Test]
        public void APoint_IsClearOnWater_AndNotOnLand()
        {
            NavigationGrid grid = GridAssert.FromRows(".#");
            Vector2 water = GridAssert.CellCenter(grid, 0, 0);
            Vector2 land = GridAssert.CellCenter(grid, 1, 0);

            Assert.IsTrue(NavigationLineOfSight.IsClear(grid, water, water));
            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, land, land));
        }

        [Test]
        public void AnEndOutsideTheMap_IsNotClear()
        {
            NavigationGrid grid = GridAssert.FromRows("....");
            Vector2 inside = GridAssert.CellCenter(grid, 1, 0);

            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, inside, new Vector2(1.5f, 0.5f)));
            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, new Vector2(0.5f, -0.1f), inside));
        }

        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.NaN)]
        public void ANaNEnd_IsNotClear(float x, float y)
        {
            NavigationGrid grid = GridAssert.FromRows("....");
            Vector2 inside = GridAssert.CellCenter(grid, 1, 0);

            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, inside, new Vector2(x, y)));
            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, new Vector2(x, y), inside));
        }

        [Test]
        public void TheEdgeOfTheMap_IsNotLand()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..",
                "..");

            Assert.IsTrue(NavigationLineOfSight.IsClear(grid, new Vector2(0f, 0f), new Vector2(1f, 1f)));
            Assert.IsTrue(NavigationLineOfSight.IsClear(grid, new Vector2(0f, 0f), new Vector2(1f, 0f)));
            Assert.IsTrue(NavigationLineOfSight.IsClear(grid, new Vector2(1f, 0f), new Vector2(1f, 1f)));
        }

        [Test]
        public void AGridThatIsNotSquare_IsMeasuredInCells()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "........",
                "...#....");

            Assert.IsTrue(IsClear(grid, 0, 1, 7, 1));
            Assert.IsFalse(IsClear(grid, 0, 0, 7, 0));
        }
    }
}
