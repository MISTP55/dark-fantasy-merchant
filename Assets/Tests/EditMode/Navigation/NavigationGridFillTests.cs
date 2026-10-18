using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationGridFillTests
    {
        [Test]
        public void Fill_StopsAtACoastline()
        {
            var grid = new NavigationGrid(8, 8);

            // A painted coastline: column 4, top to bottom.
            for (int y = 0; y < 8; y++)
            {
                grid.SetNavigable(4, y, true);
            }

            bool changed = grid.Fill(GridAssert.CellCenter(grid, 1, 1), true);

            Assert.IsTrue(changed);
            Assert.IsTrue(grid.IsNavigable(0, 0));
            Assert.IsTrue(grid.IsNavigable(3, 7));
            Assert.IsFalse(grid.IsNavigable(5, 0), "the other side of the coastline");
            Assert.IsFalse(grid.IsNavigable(7, 7));
            Assert.AreEqual(5 * 8, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_DoesNotLeakThroughADiagonal()
        {
            var grid = new NavigationGrid(8, 8);

            // A one-cell-thick diagonal: its cells touch only by their corners.
            for (int i = 0; i < 8; i++)
            {
                grid.SetNavigable(i, i, true);
            }

            grid.Fill(GridAssert.CellCenter(grid, 0, 7), true);

            Assert.IsTrue(grid.IsNavigable(0, 1));
            Assert.IsTrue(grid.IsNavigable(6, 7));
            Assert.IsFalse(grid.IsNavigable(1, 0));
            Assert.IsFalse(grid.IsNavigable(7, 0));
            Assert.AreEqual(28 + 8, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_OnARegionAlreadyAtTheValue_ReportsNoChange()
        {
            var grid = new NavigationGrid(8, 8);

            Assert.IsFalse(grid.Fill(GridAssert.CellCenter(grid, 3, 3), false));
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_NonNavigable_ErasesARegion()
        {
            var grid = new NavigationGrid(8, 8);
            grid.Clear(true);

            Assert.IsTrue(grid.Fill(GridAssert.CellCenter(grid, 3, 3), false));
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_OfASingleEnclosedCell_FillsOnlyThatCell()
        {
            var grid = new NavigationGrid(8, 8);
            grid.Clear(true);
            grid.SetNavigable(3, 3, false);

            Assert.IsTrue(grid.Fill(GridAssert.CellCenter(grid, 3, 3), true));
            Assert.AreEqual(64, GridAssert.CountNavigable(grid));
        }

        [TestCase(-0.1f, 0.5f)]
        [TestCase(0.5f, 1.1f)]
        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.PositiveInfinity)]
        public void Fill_OutsideTheMap_DoesNothing(float u, float v)
        {
            var grid = new NavigationGrid(8, 8);

            Assert.IsFalse(grid.Fill(new Vector2(u, v), true));
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_HandlesAnOpenSeaOnAFullSizeGrid()
        {
            // The default size for the real map: 900,096 cells in one region.
            var grid = new NavigationGrid(1024, 879);

            Assert.IsTrue(grid.Fill(new Vector2(0.5f, 0.5f), true));

            foreach (byte packed in grid.ToBytes())
            {
                Assert.AreEqual(0xFF, packed);
            }
        }

        [Test]
        public void Resampled_ToTheSameSize_IsIdentical_AndIsANewGrid()
        {
            var grid = new NavigationGrid(8, 4);
            grid.PaintDisc(GridAssert.CellCenter(grid, 3, 2), 1.5f, true);

            NavigationGrid copy = grid.Resampled(8, 4);
            byte[] before = grid.ToBytes();
            copy.Clear(true);

            Assert.AreNotSame(grid, copy);
            CollectionAssert.AreEqual(before, grid.ToBytes(), "the source is untouched");
            CollectionAssert.AreEqual(before, grid.Resampled(8, 4).ToBytes());
        }

        [Test]
        public void Resampled_ToALargerGrid_KeepsTheShape()
        {
            var grid = new NavigationGrid(2, 2);
            grid.SetNavigable(0, 0, true);

            NavigationGrid larger = grid.Resampled(4, 4);

            Assert.AreEqual(4, larger.Width);
            Assert.AreEqual(4, larger.Height);
            Assert.IsTrue(larger.IsNavigable(0, 0));
            Assert.IsTrue(larger.IsNavigable(1, 0));
            Assert.IsTrue(larger.IsNavigable(0, 1));
            Assert.IsTrue(larger.IsNavigable(1, 1));
            Assert.AreEqual(4, GridAssert.CountNavigable(larger));
        }

        [Test]
        public void Resampled_ToASmallerGrid_KeepsTheShape()
        {
            var grid = new NavigationGrid(4, 4);

            // Left half navigable.
            for (int y = 0; y < 4; y++)
            {
                grid.SetNavigable(0, y, true);
                grid.SetNavigable(1, y, true);
            }

            NavigationGrid smaller = grid.Resampled(2, 2);

            Assert.IsTrue(smaller.IsNavigable(0, 0));
            Assert.IsTrue(smaller.IsNavigable(0, 1));
            Assert.IsFalse(smaller.IsNavigable(1, 0));
            Assert.IsFalse(smaller.IsNavigable(1, 1));
        }

        [Test]
        public void Resampled_ToAnotherAspectRatio_StretchesEachAxisSeparately()
        {
            var grid = new NavigationGrid(4, 2);

            // Top row navigable.
            for (int x = 0; x < 4; x++)
            {
                grid.SetNavigable(x, 1, true);
            }

            NavigationGrid tall = grid.Resampled(2, 4);

            Assert.AreEqual(4, GridAssert.CountNavigable(tall));
            Assert.IsTrue(tall.IsNavigable(0, 2));
            Assert.IsTrue(tall.IsNavigable(1, 3));
            Assert.IsFalse(tall.IsNavigable(0, 1));
        }

        [TestCase(0, 4)]
        [TestCase(4, 0)]
        [TestCase(NavigationGrid.MaxSize + 1, 4)]
        public void Resampled_RejectsInvalidDimensions(int width, int height)
        {
            var grid = new NavigationGrid(4, 4);

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.Resampled(width, height));
        }
    }
}
