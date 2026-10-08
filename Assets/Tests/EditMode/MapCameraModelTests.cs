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
        public void ViewportToWorld_MapsTheViewCenterAndCorners()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.5f, Vector2.zero); // size 5: half extents are 10 x 5.
            model.Pan(new Vector2(3f, 1f));

            TestAssert.AreEqual(new Vector2(3f, 1f), model.ViewportToWorld(new Vector2(0.5f, 0.5f)));
            TestAssert.AreEqual(new Vector2(13f, 6f), model.ViewportToWorld(new Vector2(1f, 1f)));
            TestAssert.AreEqual(new Vector2(-7f, -4f), model.ViewportToWorld(new Vector2(0f, 0f)));
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

        [Test]
        public void ZoomOutFully_ShowsTheWholeMap_FromAnyView()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.5f, Vector2.zero);
            model.Pan(new Vector2(5f, 2f));

            model.ZoomOutFully();

            Assert.AreEqual(10f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [Test]
        public void SetView_GoesToTheGivenView()
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            model.SetView(new Vector2(3f, 1f), 5f);

            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(new Vector2(3f, 1f), model.Position);
        }

        [Test]
        public void SetView_RestoresTheViewLeftByZoomOutFully()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.3f, new Vector2(4f, -2f));
            model.Pan(new Vector2(-6f, 1f));
            Vector2 savedPosition = model.Position;
            float savedSize = model.OrthographicSize;

            model.ZoomOutFully();
            model.SetView(savedPosition, savedSize);

            Assert.AreEqual(savedSize, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(savedPosition, model.Position);
        }

        [TestCase(0.5f, 2f)]
        [TestCase(100f, 10f)]
        public void SetView_ClampsTheSize(float requestedSize, float expectedSize)
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            model.SetView(Vector2.zero, requestedSize);

            Assert.AreEqual(expectedSize, model.OrthographicSize, 1e-4f);
        }

        [Test]
        public void SetView_ClampsThePositionToTheMap()
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            // Size 5: half extents are 10 x 5, so the center stays within (-10..10, -5..5).
            model.SetView(new Vector2(100f, -100f), 5f);

            TestAssert.AreEqual(new Vector2(10f, -5f), model.Position);
        }

        [Test]
        public void SetView_AfterTheAspectChanged_GivesTheNearestValidView()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.SetView(new Vector2(8f, 0f), 8f);
            Vector2 savedPosition = model.Position;
            float savedSize = model.OrthographicSize;

            // A wider window: the largest size becomes 40 / (2 * 4) = 5.
            model.ZoomOutFully();
            model.SetAspect(4f);
            model.SetView(savedPosition, savedSize);

            // The view is as wide as the map: it can only be centered.
            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [Test]
        public void SetView_IgnoresAnInvalidView()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.SetView(new Vector2(3f, 1f), 5f);

            model.SetView(new Vector2(float.NaN, 0f), 4f);
            model.SetView(Vector2.zero, 0f);
            model.SetView(Vector2.zero, float.NaN);
            model.SetView(Vector2.zero, float.PositiveInfinity);

            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(new Vector2(3f, 1f), model.Position);
        }

        [Test]
        public void ZoomInFullyOn_CentersTheClosestViewOnThePoint()
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            model.ZoomInFullyOn(new Vector2(3f, 1f));

            Assert.AreEqual(2f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(new Vector2(3f, 1f), model.Position);
        }

        [Test]
        public void ZoomInFullyOn_ClampsThePositionToTheMap()
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            // Size 2: half extents are 4 x 2, so the center stays within (-16..16, -8..8).
            model.ZoomInFullyOn(new Vector2(19f, -9.5f));

            Assert.AreEqual(2f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(new Vector2(16f, -8f), model.Position);
        }

        [Test]
        public void ZoomInFullyOn_IgnoresAnInvalidPosition()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.SetView(new Vector2(3f, 1f), 5f);

            model.ZoomInFullyOn(new Vector2(float.NaN, 0f));
            model.ZoomInFullyOn(new Vector2(0f, float.PositiveInfinity));

            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(new Vector2(3f, 1f), model.Position);
        }
    }
}
