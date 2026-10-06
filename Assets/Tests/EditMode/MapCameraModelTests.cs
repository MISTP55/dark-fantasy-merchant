using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MapCameraModelTests
    {
        // A 40 x 20 map centered on the origin.
        private static readonly Rect Map = new Rect(-20f, -10f, 40f, 20f);

        [Test]
        public void StartsCenteredAndFullyZoomedOut()
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            TestAssert.AreEqual(Vector2.zero, model.Position);
            Assert.AreEqual(10f, model.OrthographicSize, 1e-4f);
        }

        [Test]
        public void MaxOrthographicSize_IsLimitedByHeight_WhenViewportIsNarrowerThanTheMap()
        {
            var model = new MapCameraModel(Map, 1f, 2f);

            Assert.AreEqual(10f, model.MaxOrthographicSize, 1e-4f);
        }

        [Test]
        public void MaxOrthographicSize_IsLimitedByWidth_WhenViewportIsWiderThanTheMap()
        {
            var model = new MapCameraModel(Map, 4f, 2f);

            // Visible width = 2 * size * aspect must not exceed 40.
            Assert.AreEqual(5f, model.MaxOrthographicSize, 1e-4f);
            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
        }

        [Test]
        public void Zoom_ClampsAtMaximumZoomIn()
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            model.Zoom(0.0001f, Vector2.zero);

            Assert.AreEqual(2f, model.OrthographicSize, 1e-4f);
        }

        [Test]
        public void Zoom_ClampsAtMaximumZoomOut()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.5f, Vector2.zero);

            model.Zoom(1000f, Vector2.zero);

            Assert.AreEqual(10f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [Test]
        public void Zoom_KeepsTheAnchorAtTheSameViewportPosition()
        {
            var model = new MapCameraModel(Map, 1f, 2f);
            var anchor = new Vector2(4f, 2f);
            Vector2 before = (anchor - model.Position) / model.OrthographicSize;

            model.Zoom(0.5f, anchor);

            Vector2 after = (anchor - model.Position) / model.OrthographicSize;
            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(before, after);
        }

        [TestCase(1000f, 0f, 16f, 0f)]
        [TestCase(-1000f, 0f, -16f, 0f)]
        [TestCase(0f, 1000f, 0f, 8f)]
        [TestCase(0f, -1000f, 0f, -8f)]
        public void Pan_ClampsAtEachEdge(float deltaX, float deltaY, float expectedX, float expectedY)
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.0001f, Vector2.zero); // size 2: half extents are 4 x 2.

            model.Pan(new Vector2(deltaX, deltaY));

            TestAssert.AreEqual(new Vector2(expectedX, expectedY), model.Position);
        }

        [Test]
        public void Pan_MovesFreelyInsideTheLimits()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.0001f, Vector2.zero);

            model.Pan(new Vector2(3f, -1.5f));

            TestAssert.AreEqual(new Vector2(3f, -1.5f), model.Position);
        }

        [Test]
        public void Pan_StaysCentered_OnAnAxisWhereTheViewFillsTheMap()
        {
            var model = new MapCameraModel(Map, 2f, 2f); // fully zoomed out: view == map.

            model.Pan(new Vector2(5f, 5f));

            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [Test]
        public void SetAspect_ReducesSizeAndReclampsPosition()
        {
            var model = new MapCameraModel(Map, 1f, 2f); // size 10, half width 10.
            model.Pan(new Vector2(1000f, 0f));           // x clamps to 10.

            model.SetAspect(4f);                         // max size becomes 5, half width 20.

            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [Test]
        public void ZoomRangeCollapses_WhenMaxZoomInIsLargerThanTheMapAllows()
        {
            var model = new MapCameraModel(Map, 2f, 100f);

            Assert.AreEqual(model.MaxOrthographicSize, model.MinOrthographicSize, 1e-4f);

            model.Zoom(0.5f, new Vector2(3f, 3f));
            model.Zoom(2f, new Vector2(-3f, 1f));

            Assert.AreEqual(10f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void SetAspect_RejectsInvalidValues_AndKeepsState(float aspect)
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.5f, Vector2.zero);
            model.Pan(new Vector2(2f, 1f));

            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetAspect(aspect));

            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(new Vector2(2f, 1f), model.Position);
        }

        [Test]
        public void Constructor_RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapCameraModel(Map, float.NaN, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapCameraModel(Map, 2f, 0f));
            Assert.Throws<ArgumentException>(() => new MapCameraModel(new Rect(0f, 0f, 0f, 10f), 2f, 2f));
        }

        [Test]
        public void Pan_IgnoresNonFiniteDelta()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.5f, Vector2.zero);

            model.Pan(new Vector2(float.NaN, 1f));
            model.Pan(new Vector2(1f, float.PositiveInfinity));

            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [TestCase(0f)]
        [TestCase(-2f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Zoom_IgnoresInvalidFactor(float factor)
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.5f, Vector2.zero);

            model.Zoom(factor, new Vector2(1f, 1f));

            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [Test]
        public void Zoom_IgnoresNonFiniteAnchor()
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            model.Zoom(0.5f, new Vector2(float.NaN, 0f));

            Assert.AreEqual(10f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [Test]
        public void WorldUnitsPerPixel_IsVisibleHeightDividedByScreenHeight()
        {
            var model = new MapCameraModel(Map, 2f, 2f); // size 10: 20 units visible.

            Assert.AreEqual(0.02f, model.WorldUnitsPerPixel(1000f), 1e-6f);
        }

        [TestCase(0f)]
        [TestCase(-1080f)]
        [TestCase(float.NaN)]
        public void WorldUnitsPerPixel_RejectsInvalidScreenHeight(float screenHeight)
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            Assert.Throws<ArgumentOutOfRangeException>(() => model.WorldUnitsPerPixel(screenHeight));
        }
    }
}
