using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MapCameraSmootherTests
    {
        private const float SmoothTime = 0.12f;

        [Test]
        public void StartsAtTheGivenState()
        {
            var smoother = new MapCameraSmoother(new Vector2(3f, -2f), 7f);

            TestAssert.AreEqual(new Vector2(3f, -2f), smoother.Position);
            Assert.AreEqual(7f, smoother.OrthographicSize, 1e-4f);
        }

        [Test]
        public void Advance_MovesPartOfTheWayTowardsTheTarget()
        {
            var smoother = new MapCameraSmoother(Vector2.zero, 10f);

            smoother.Advance(new Vector2(8f, 0f), 5f, 0.05f, SmoothTime);

            Assert.Greater(smoother.Position.x, 0f);
            Assert.Less(smoother.Position.x, 8f);
            Assert.Less(smoother.OrthographicSize, 10f);
            Assert.Greater(smoother.OrthographicSize, 5f);
        }

        [Test]
        public void Advance_ReachesTheTargetExactly_AfterEnoughTime()
        {
            var smoother = new MapCameraSmoother(Vector2.zero, 10f);
            var target = new Vector2(8f, -3f);

            for (int i = 0; i < 300; i++)
            {
                smoother.Advance(target, 5f, 1f / 60f, SmoothTime);
            }

            Assert.AreEqual(target, smoother.Position);
            Assert.AreEqual(5f, smoother.OrthographicSize);
        }

        [Test]
        public void Advance_GivesTheSameResult_InOneStepOrTwoHalfSteps()
        {
            var oneStep = new MapCameraSmoother(Vector2.zero, 10f);
            var twoSteps = new MapCameraSmoother(Vector2.zero, 10f);
            var target = new Vector2(8f, -3f);

            oneStep.Advance(target, 5f, 0.04f, SmoothTime);
            twoSteps.Advance(target, 5f, 0.02f, SmoothTime);
            twoSteps.Advance(target, 5f, 0.02f, SmoothTime);

            TestAssert.AreEqual(oneStep.Position, twoSteps.Position);
            Assert.AreEqual(oneStep.OrthographicSize, twoSteps.OrthographicSize, 1e-4f);
        }

        [Test]
        public void Advance_KeepsTheZoomAnchorFixedInTheViewport_DuringTheTransition()
        {
            var map = new Rect(-20f, -10f, 40f, 20f);
            var target = new MapCameraModel(map, 1f, 2f);
            var smoother = new MapCameraSmoother(target.Position, target.OrthographicSize);
            var anchor = new Vector2(4f, 2f);
            Vector2 before = (anchor - smoother.Position) / smoother.OrthographicSize;
            target.Zoom(0.5f, anchor);

            for (int i = 0; i < 5; i++)
            {
                smoother.Advance(target.Position, target.OrthographicSize, 1f / 60f, SmoothTime);

                Vector2 during = (anchor - smoother.Position) / smoother.OrthographicSize;
                TestAssert.AreEqual(before, during);
            }

            Assert.Greater(smoother.OrthographicSize, target.OrthographicSize);
        }

        [Test]
        public void Advance_NeverShowsBeyondTheMap_DuringATransition()
        {
            // Aspect 2: from a zoomed-in view in the top-right corner to the fully zoomed-out view.
            var map = new Rect(-20f, -10f, 40f, 20f);
            var smoother = new MapCameraSmoother(new Vector2(16f, 8f), 2f);

            for (int i = 0; i < 120; i++)
            {
                smoother.Advance(Vector2.zero, 10f, 1f / 60f, SmoothTime);

                float halfHeight = smoother.OrthographicSize;
                float halfWidth = halfHeight * 2f;
                Assert.LessOrEqual(smoother.Position.x + halfWidth, map.xMax + 1e-3f);
                Assert.GreaterOrEqual(smoother.Position.x - halfWidth, map.xMin - 1e-3f);
                Assert.LessOrEqual(smoother.Position.y + halfHeight, map.yMax + 1e-3f);
                Assert.GreaterOrEqual(smoother.Position.y - halfHeight, map.yMin - 1e-3f);
            }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Advance_JumpsToTheTarget_WhenSmoothingIsDisabled(float smoothTime)
        {
            var smoother = new MapCameraSmoother(Vector2.zero, 10f);

            smoother.Advance(new Vector2(8f, -3f), 5f, 1f / 60f, smoothTime);

            Assert.AreEqual(new Vector2(8f, -3f), smoother.Position);
            Assert.AreEqual(5f, smoother.OrthographicSize);
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Advance_DoesNothing_ForAnInvalidTimeStep(float deltaTime)
        {
            var smoother = new MapCameraSmoother(Vector2.zero, 10f);

            smoother.Advance(new Vector2(8f, -3f), 5f, deltaTime, SmoothTime);

            TestAssert.AreEqual(Vector2.zero, smoother.Position);
            Assert.AreEqual(10f, smoother.OrthographicSize, 1e-4f);
        }

        [Test]
        public void Advance_DoesNothing_ForANonFiniteTarget()
        {
            var smoother = new MapCameraSmoother(Vector2.zero, 10f);

            smoother.Advance(new Vector2(float.NaN, 0f), 5f, 1f / 60f, SmoothTime);
            smoother.Advance(new Vector2(1f, 0f), float.NaN, 1f / 60f, SmoothTime);

            TestAssert.AreEqual(Vector2.zero, smoother.Position);
            Assert.AreEqual(10f, smoother.OrthographicSize, 1e-4f);
        }

        [Test]
        public void SnapTo_JumpsToTheGivenState()
        {
            var smoother = new MapCameraSmoother(Vector2.zero, 10f);

            smoother.SnapTo(new Vector2(-4f, 6f), 3f);

            TestAssert.AreEqual(new Vector2(-4f, 6f), smoother.Position);
            Assert.AreEqual(3f, smoother.OrthographicSize, 1e-4f);
        }
    }
}
