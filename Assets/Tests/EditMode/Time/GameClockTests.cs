using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GameClockTests
    {
        // 30 seconds per day, fast forward x60, the game's calendar.
        private static GameClock NewClock()
        {
            return new GameClock(30f, 60f, 932, 12, 30);
        }

        [Test]
        public void StartsOnTheFirstDay_AtNormalSpeed()
        {
            GameClock clock = NewClock();

            Assert.AreEqual(new GameDate(932, 1, 1), clock.Date);
            Assert.AreEqual(0, clock.ElapsedDays);
            Assert.AreEqual(0f, clock.DeltaTime);
            Assert.IsFalse(clock.IsFastForward);
        }

        [Test]
        public void ADay_LastsTheConfiguredSeconds()
        {
            GameClock clock = NewClock();

            clock.Advance(29f);
            Assert.AreEqual(new GameDate(932, 1, 1), clock.Date);

            clock.Advance(1f);
            Assert.AreEqual(new GameDate(932, 1, 2), clock.Date);
            Assert.AreEqual(1, clock.ElapsedDays);
        }

        [Test]
        public void DeltaTime_IsTheRealDelta_AtNormalSpeed()
        {
            GameClock clock = NewClock();

            clock.Advance(0.25f);

            Assert.AreEqual(0.25f, clock.DeltaTime, 1e-6f);
        }

        [Test]
        public void FastForward_MultipliesDeltaTimeAndThePace()
        {
            GameClock clock = NewClock();
            clock.SetFastForward(true);

            clock.Advance(0.5f);

            Assert.AreEqual(30f, clock.DeltaTime, 1e-4f);
            Assert.AreEqual(new GameDate(932, 1, 2), clock.Date);
        }

        [Test]
        public void ChangingSpeed_KeepsTheTimeAlreadyElapsed()
        {
            GameClock clock = NewClock();
            clock.Advance(15f);
            clock.SetFastForward(true);

            // Half a day was left: a quarter of a second at x60.
            clock.Advance(0.25f);

            Assert.AreEqual(new GameDate(932, 1, 2), clock.Date);
        }

        [Test]
        public void AYear_PassesInThreeMinutesOfFastForward()
        {
            GameClock clock = NewClock();
            clock.SetFastForward(true);

            clock.Advance(180f);

            Assert.AreEqual(new GameDate(933, 1, 1), clock.Date);
        }

        [Test]
        public void SeveralDaysInOneStep_AreEachAnnounced_InOrder()
        {
            GameClock clock = NewClock();
            var announced = new List<GameDate>();
            var datesSeenByTheListener = new List<GameDate>();
            clock.DayStarted += date =>
            {
                announced.Add(date);
                datesSeenByTheListener.Add(clock.Date);
            };

            clock.Advance(95f);

            var expected = new[] { new GameDate(932, 1, 2), new GameDate(932, 1, 3), new GameDate(932, 1, 4) };
            CollectionAssert.AreEqual(expected, announced);

            // The clock is on the announced day while it announces it.
            CollectionAssert.AreEqual(expected, datesSeenByTheListener);
        }

        [Test]
        public void NoDayIsAnnounced_WithoutADayChange()
        {
            GameClock clock = NewClock();
            int announced = 0;
            clock.DayStarted += _ => announced++;

            clock.Advance(10f);
            clock.Advance(10f);

            Assert.AreEqual(0, announced);
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void AnInvalidDelta_MovesNothing(float realDeltaSeconds)
        {
            GameClock clock = NewClock();
            clock.Advance(12f);

            clock.Advance(realDeltaSeconds);

            Assert.AreEqual(0f, clock.DeltaTime);
            Assert.AreEqual(new GameDate(932, 1, 1), clock.Date);

            // The 12 seconds are still there: 18 more end the day.
            clock.Advance(18f);
            Assert.AreEqual(new GameDate(932, 1, 2), clock.Date);
        }

        [Test]
        public void AStepLongerThanAnHour_IsClampedToAnHour()
        {
            GameClock clock = NewClock();
            int announced = 0;
            clock.DayStarted += _ => announced++;

            clock.Advance(float.MaxValue);

            // An hour of real time is 120 days of 30 seconds.
            Assert.AreEqual(GameClock.MaxRealDeltaSeconds, clock.DeltaTime, 1e-3f);
            Assert.AreEqual(120, clock.ElapsedDays);
            Assert.AreEqual(120, announced);
        }

        [Test]
        public void FastForwardChanged_IsRaisedOnlyOnAnActualChange()
        {
            GameClock clock = NewClock();
            var changes = new List<bool>();
            clock.FastForwardChanged += changes.Add;

            clock.SetFastForward(false);
            clock.SetFastForward(true);
            clock.SetFastForward(true);
            clock.SetFastForward(false);

            CollectionAssert.AreEqual(new[] { true, false }, changes);
            Assert.IsFalse(clock.IsFastForward);
        }

        [Test]
        public void AListener_CanChangeTheSpeedWhileADayIsAnnounced()
        {
            GameClock clock = NewClock();
            clock.SetFastForward(true);
            clock.DayStarted += _ => clock.SetFastForward(false);

            // Two days at x60: both are announced, the speed changes for the next step.
            clock.Advance(1f);

            Assert.AreEqual(2, clock.ElapsedDays);
            Assert.IsFalse(clock.IsFastForward);

            clock.Advance(1f);
            Assert.AreEqual(1f, clock.DeltaTime, 1e-6f);
        }

        [TestCase(0f, 60f, 12, 30)]
        [TestCase(-30f, 60f, 12, 30)]
        [TestCase(float.NaN, 60f, 12, 30)]
        [TestCase(float.PositiveInfinity, 60f, 12, 30)]
        [TestCase(30f, 0f, 12, 30)]
        [TestCase(30f, float.NaN, 12, 30)]
        [TestCase(30f, float.PositiveInfinity, 12, 30)]
        [TestCase(30f, 60f, 0, 30)]
        [TestCase(30f, 60f, 12, 0)]
        public void InvalidSettings_AreRejected(
            float secondsPerDay, float fastForwardMultiplier, int monthsPerYear, int daysPerMonth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GameClock(secondsPerDay, fastForwardMultiplier, 932, monthsPerYear, daysPerMonth));
        }
    }
}
