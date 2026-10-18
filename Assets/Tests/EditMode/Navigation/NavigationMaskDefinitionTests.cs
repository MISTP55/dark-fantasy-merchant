using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationMaskDefinitionTests
    {
        private NavigationMaskDefinition mask;

        [SetUp]
        public void SetUp()
        {
            mask = ScriptableObject.CreateInstance<NavigationMaskDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(mask);
        }

        private static NavigationGrid SampleGrid()
        {
            var grid = new NavigationGrid(5, 3);
            grid.SetNavigable(0, 0, true);
            grid.SetNavigable(4, 2, true);
            grid.SetNavigable(2, 1, true);
            return grid;
        }

        private void SetSerialized(int width, int height, int byteCount)
        {
            var serialized = new SerializedObject(mask);
            serialized.FindProperty("width").intValue = width;
            serialized.FindProperty("height").intValue = height;

            // Emptied and applied first: Unity does not apply a byte array that is only
            // shortened to a non-zero length.
            serialized.FindProperty("bits").ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.FindProperty("bits").arraySize = byteCount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void NewDefinition_CreatesAnEmptyGrid()
        {
            NavigationGrid grid = mask.CreateGrid();

            Assert.IsNotNull(grid);
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void SetGrid_ThenCreateGrid_RoundTrips()
        {
            NavigationGrid source = SampleGrid();

            mask.SetGrid(source);
            NavigationGrid restored = mask.CreateGrid();

            Assert.AreEqual(5, mask.Width);
            Assert.AreEqual(3, mask.Height);
            Assert.AreEqual(5, restored.Width);
            Assert.AreEqual(3, restored.Height);
            CollectionAssert.AreEqual(source.ToBytes(), restored.ToBytes());
        }

        [Test]
        public void SetGrid_CopiesTheGrid()
        {
            NavigationGrid source = SampleGrid();
            byte[] expected = source.ToBytes();

            mask.SetGrid(source);
            source.Clear(true);

            CollectionAssert.AreEqual(expected, mask.CreateGrid().ToBytes());
        }

        [Test]
        public void CreateGrid_ReturnsAnIndependentGridEachTime()
        {
            mask.SetGrid(SampleGrid());

            NavigationGrid first = mask.CreateGrid();
            first.Clear(true);

            Assert.AreNotSame(first, mask.CreateGrid());
            Assert.AreEqual(3, GridAssert.CountNavigable(mask.CreateGrid()));
        }

        [Test]
        public void SetGrid_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => mask.SetGrid(null));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void CreateGrid_WithBitsOfTheWrongLength_ReturnsAnEmptyGridOfTheStoredSize(int byteCount)
        {
            mask.SetGrid(SampleGrid());

            // 5 x 3 = 15 cells = 2 bytes.
            SetSerialized(5, 3, byteCount);
            NavigationGrid grid = mask.CreateGrid();

            Assert.AreEqual(5, grid.Width);
            Assert.AreEqual(3, grid.Height);
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [TestCase(0, 3)]
        [TestCase(5, 0)]
        [TestCase(-2, 3)]
        [TestCase(NavigationGrid.MaxSize + 1, 3)]
        public void CreateGrid_WithAnInvalidStoredSize_ReturnsAnEmptyOneCellGrid(int width, int height)
        {
            SetSerialized(width, height, 2);

            NavigationGrid grid = mask.CreateGrid();

            Assert.AreEqual(1, grid.Width);
            Assert.AreEqual(1, grid.Height);
            Assert.IsFalse(grid.IsNavigable(0, 0));
        }

        [Test]
        public void WorldMapDefinition_HasNoMaskByDefault_AndExposesTheAssignedOne()
        {
            var map = ScriptableObject.CreateInstance<WorldMapDefinition>();

            try
            {
                Assert.IsNull(map.NavigationMask);

                var serialized = new SerializedObject(map);
                serialized.FindProperty("navigationMask").objectReferenceValue = mask;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreSame(mask, map.NavigationMask);
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }
    }
}
