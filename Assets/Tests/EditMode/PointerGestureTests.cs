using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PointerGestureTests
    {
        [Test]
        public void PressThenRelease_IsAClick()
        {
            var gesture = new PointerGesture(6f);

            gesture.Press(new Vector2(100f, 100f));
            bool isClick = gesture.Release();

            Assert.IsTrue(isClick);
            Assert.IsFalse(gesture.IsPressed);
        }

        [Test]
        public void MovementWithinTheThreshold_IsStillAClick_AndProducesNoDrag()
        {
            var gesture = new PointerGesture(6f);
            gesture.Press(new Vector2(100f, 100f));

            Vector2 delta = gesture.Move(new Vector2(104f, 103f)); // distance 5

            TestAssert.AreEqual(Vector2.zero, delta);
            Assert.IsFalse(gesture.IsDragging);
            Assert.IsTrue(gesture.Release());
        }

        [Test]
        public void MovementBeyondTheThreshold_IsADrag_AndNotAClick()
        {
            var gesture = new PointerGesture(6f);
            gesture.Press(new Vector2(100f, 100f));

            Vector2 delta = gesture.Move(new Vector2(110f, 100f));

            TestAssert.AreEqual(new Vector2(10f, 0f), delta);
            Assert.IsTrue(gesture.IsDragging);
            Assert.IsFalse(gesture.Release());
        }

        [Test]
        public void FirstDragDelta_IncludesTheMotionAccumulatedBeforeTheThreshold()
        {
            var gesture = new PointerGesture(6f);
            gesture.Press(new Vector2(0f, 0f));
            gesture.Move(new Vector2(3f, 0f));

            Vector2 delta = gesture.Move(new Vector2(8f, 0f));

            TestAssert.AreEqual(new Vector2(8f, 0f), delta);
        }

        [Test]
        public void SubsequentMoves_ReturnIncrementalDeltas()
        {
            var gesture = new PointerGesture(6f);
            gesture.Press(new Vector2(0f, 0f));
            gesture.Move(new Vector2(10f, 0f));

            Vector2 delta = gesture.Move(new Vector2(13f, -4f));

            TestAssert.AreEqual(new Vector2(3f, -4f), delta);
        }

        [Test]
        public void ReturningToThePressPosition_AfterDragging_IsStillNotAClick()
        {
            var gesture = new PointerGesture(6f);
            gesture.Press(new Vector2(0f, 0f));
            gesture.Move(new Vector2(20f, 0f));
            gesture.Move(new Vector2(0f, 0f));

            Assert.IsFalse(gesture.Release());
        }

        [Test]
        public void Release_WithoutAPress_IsNotAClick()
        {
            var gesture = new PointerGesture(6f);

            Assert.IsFalse(gesture.Release());
        }

        [Test]
        public void Move_WithoutAPress_ProducesNoDrag()
        {
            var gesture = new PointerGesture(6f);

            Vector2 delta = gesture.Move(new Vector2(500f, 500f));

            TestAssert.AreEqual(Vector2.zero, delta);
            Assert.IsFalse(gesture.IsDragging);
        }

        [Test]
        public void Cancel_AbandonsTheGesture()
        {
            var gesture = new PointerGesture(6f);
            gesture.Press(new Vector2(0f, 0f));

            gesture.Cancel();

            Assert.IsFalse(gesture.IsPressed);
            TestAssert.AreEqual(Vector2.zero, gesture.Move(new Vector2(50f, 0f)));
            Assert.IsFalse(gesture.Release());
        }

        [Test]
        public void ASecondGesture_StartsClean()
        {
            var gesture = new PointerGesture(6f);
            gesture.Press(new Vector2(0f, 0f));
            gesture.Move(new Vector2(50f, 0f));
            gesture.Release();

            gesture.Press(new Vector2(200f, 200f));

            Assert.IsFalse(gesture.IsDragging);
            Assert.IsTrue(gesture.Release());
        }

        [Test]
        public void ZeroThreshold_DragsOnAnyMovement()
        {
            var gesture = new PointerGesture(0f);
            gesture.Press(new Vector2(0f, 0f));

            Vector2 delta = gesture.Move(new Vector2(1f, 0f));

            TestAssert.AreEqual(new Vector2(1f, 0f), delta);
        }

        [Test]
        public void APressWithoutClick_IsNotAClick_WhenReleasedInPlace()
        {
            var gesture = new PointerGesture(6f);

            gesture.PressWithoutClick(new Vector2(100f, 100f));

            Assert.IsTrue(gesture.IsPressed);
            Assert.IsFalse(gesture.Release());
        }

        [Test]
        public void APressWithoutClick_StillDrags()
        {
            var gesture = new PointerGesture(6f);
            gesture.PressWithoutClick(new Vector2(100f, 100f));

            Vector2 delta = gesture.Move(new Vector2(110f, 100f));

            Assert.IsTrue(gesture.IsDragging);
            TestAssert.AreEqual(new Vector2(10f, 0f), delta);
            Assert.IsFalse(gesture.Release());
        }

        [Test]
        public void APressAfterAPressWithoutClick_IsAClickAgain()
        {
            var gesture = new PointerGesture(6f);
            gesture.PressWithoutClick(new Vector2(100f, 100f));
            gesture.Release();

            gesture.Press(new Vector2(100f, 100f));

            Assert.IsTrue(gesture.Release());
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Constructor_RejectsInvalidThreshold(float threshold)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PointerGesture(threshold));
        }
    }
}
