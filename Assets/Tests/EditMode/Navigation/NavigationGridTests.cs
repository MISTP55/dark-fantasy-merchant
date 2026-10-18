using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationGridTests
    {
        [TestCase(0, 4)]
        [TestCase(4, 0)]
        [TestCase(-1, 4)]
        [TestCase(4, -1)]
        [TestCase(NavigationGrid.MaxSize + 1, 4)]
        [TestCase(4, NavigationGrid.MaxSize + 1)]
        public void Constructors_RejectInvalidDimensions(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationGrid(width, height));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationGrid(width, height, new byte[1]));
        }

        [Test]
        public void Constructor_RejectsANullBitArray()
        {
            Assert.Throws<ArgumentNullException>(() => new NavigationGrid(4, 3, null));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void Constructor_RejectsABitArrayOfTheWrongLength(int length)
        {
            // 4 x 3 = 12 cells = 2 bytes.
            Assert.Throws<ArgumentException>(() => new NavigationGrid(4, 3, new byte[length]));
        }

        [TestCase(4, 3, 2)]
        [TestCase(8, 1, 1)]
        [TestCase(9, 1, 2)]
        [TestCase(1024, 879, 112512)]
        public void ByteCount_IsTheCellCountRoundedUpToBytes(int width, int height, int expected)
        {
            Assert.AreEqual(expected, NavigationGrid.ByteCount(width, height));
        }

        [Test]
        public void NewGrid_HasItsDimensions_AndIsEntirelyNonNavigable()
        {
            var grid = new NavigationGrid(4, 3);

            Assert.AreEqual(4, grid.Width);
            Assert.AreEqual(3, grid.Height);

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    Assert.IsFalse(grid.IsNavigable(x, y), $"({x}, {y})");
                }
            }
        }

        [Test]
        public void SetNavigable_SetsOneCell_AndReportsTheChange()
        {
            var grid = new NavigationGrid(4, 3);

            Assert.IsTrue(grid.SetNavigable(2, 1, true));
            Assert.IsFalse(grid.SetNavigable(2, 1, true), "already navigable");

            Assert.IsTrue(grid.IsNavigable(2, 1));
            Assert.IsFalse(grid.IsNavigable(1, 1));
            Assert.IsFalse(grid.IsNavigable(3, 1));
            Assert.IsFalse(grid.IsNavigable(2, 0));
            Assert.IsFalse(grid.IsNavigable(2, 2));

            Assert.IsTrue(grid.SetNavigable(2, 1, false));
            Assert.IsFalse(grid.IsNavigable(2, 1));
        }

        [TestCase(-1, 0)]
        [TestCase(4, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 3)]
        public void CellsOutsideTheGrid_AreNotNavigable_AndCannotBeSet(int x, int y)
        {
            var grid = new NavigationGrid(4, 3);
            grid.Clear(true);

            Assert.IsFalse(grid.IsNavigable(x, y));
            Assert.IsFalse(grid.SetNavigable(x, y, false));
            CollectionAssert.AreEqual(new byte[] { 0xFF, 0x0F }, grid.ToBytes());
        }

        // A 4 x 2 grid: not square, so a swapped axis shows.
        [TestCase(0f, 0f, 0, 0)]
        [TestCase(1f, 1f, 3, 1)]
        [TestCase(1f, 0f, 3, 0)]
        [TestCase(0f, 1f, 0, 1)]
        [TestCase(0.49f, 0.49f, 1, 0)]
        [TestCase(0.5f, 0.5f, 2, 1)]
        [TestCase(0.99f, 0.2f, 3, 0)]
        public void NormalizedPoint_MapsToItsCell(float u, float v, int x, int y)
        {
            var only = new NavigationGrid(4, 2);
            only.SetNavigable(x, y, true);

            var allBut = new NavigationGrid(4, 2);
            allBut.Clear(true);
            allBut.SetNavigable(x, y, false);

            Assert.IsTrue(only.IsNavigable(new Vector2(u, v)));
            Assert.IsFalse(allBut.IsNavigable(new Vector2(u, v)));
        }

        [TestCase(-0.01f, 0.5f)]
        [TestCase(1.01f, 0.5f)]
        [TestCase(0.5f, -0.01f)]
        [TestCase(0.5f, 1.01f)]
        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.NaN)]
        [TestCase(float.PositiveInfinity, 0.5f)]
        [TestCase(0.5f, float.NegativeInfinity)]
        public void PointsOutsideTheMap_AreNotNavigable(float u, float v)
        {
            var grid = new NavigationGrid(4, 2);
            grid.Clear(true);

            Assert.IsFalse(grid.IsNavigable(new Vector2(u, v)));
        }

        [Test]
        public void Clear_SetsEveryCell()
        {
            var grid = new NavigationGrid(4, 3);

            grid.Clear(true);

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    Assert.IsTrue(grid.IsNavigable(x, y), $"({x}, {y})");
                }
            }

            grid.Clear(false);
            CollectionAssert.AreEqual(new byte[] { 0x00, 0x00 }, grid.ToBytes());
        }

        [Test]
        public void ToBytes_PacksRowsFromTheBottom_LeastSignificantBitFirst()
        {
            var grid = new NavigationGrid(4, 3);
            grid.SetNavigable(0, 0, true);   // cell index 0  -> byte 0, bit 0
            grid.SetNavigable(3, 2, true);   // cell index 11 -> byte 1, bit 3

            CollectionAssert.AreEqual(new byte[] { 0x01, 0x08 }, grid.ToBytes());
        }

        [Test]
        public void Clear_LeavesTheUnusedBitsOfTheLastByteAtZero()
        {
            var grid = new NavigationGrid(4, 3);

            grid.Clear(true);

            CollectionAssert.AreEqual(new byte[] { 0xFF, 0x0F }, grid.ToBytes());
        }

        [Test]
        public void Constructor_FromBytes_RestoresTheCells_AndDropsUnusedBits()
        {
            var grid = new NavigationGrid(4, 3, new byte[] { 0x01, 0xF8 });

            Assert.IsTrue(grid.IsNavigable(0, 0));
            Assert.IsTrue(grid.IsNavigable(3, 2));
            Assert.IsFalse(grid.IsNavigable(1, 0));
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x08 }, grid.ToBytes());
        }

        [Test]
        public void Constructor_CopiesTheBitArray()
        {
            byte[] bits = { 0x01, 0x00 };
            var grid = new NavigationGrid(4, 3, bits);

            bits[0] = 0x00;

            Assert.IsTrue(grid.IsNavigable(0, 0));
        }

        [Test]
        public void ToBytes_ReturnsACopy()
        {
            var grid = new NavigationGrid(4, 3);

            grid.ToBytes()[0] = 0xFF;

            Assert.IsFalse(grid.IsNavigable(0, 0));
        }
    }
}
