using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GameClockTests
    {
        // 30 seconds per day, fast forward x60, turbo x1800, the game's calendar.
        private static GameClock NewClock()
        {
            return new GameClock(30f, 60f, 1800f, 932, 12, 30);
        }

        [Test]
        public void StartsOnTheFirstDay_AtNormalSpeed()
        {
            GameClock clock = NewClock();

            Assert.AreEqual(new GameDate(932, 1, 1), clock.Date);
            Assert.AreEqual(0, clock.ElapsedDays);
            Assert.AreEqual(0f, clock.DeltaTime);
            Assert.AreEqual(GameSpeed.Normal, clock.Speed);
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

            // Two days at x60, but the first ends the fast forward: the second half of the
            // real second passes at normal speed, from the next step.
            clock.Advance(1f);

            Assert.AreEqual(1, clock.ElapsedDays);
            Assert.IsFalse(clock.IsFastForward);
            Assert.AreEqual(30.5f, clock.DeltaTime, 1e-4f);

            clock.Advance(1f);
            Assert.AreEqual(1f, clock.DeltaTime, 1e-6f);
        }

        [Test]
        public void Turbo_MultipliesThePaceByItsOwnMultiplier()
        {
            GameClock clock = NewClock();
            clock.SetSpeed(GameSpeed.Turbo);

            // 60 days a second.
            clock.Advance(1f);

            Assert.IsTrue(clock.IsFastForward);
            Assert.AreEqual(60, clock.ElapsedDays);
            Assert.AreEqual(1800f, clock.DeltaTime, 1e-2f);
        }

        [TestCase(GameSpeed.Normal, 7f)]
        [TestCase(GameSpeed.FastForward, 0.5f)]
        [TestCase(GameSpeed.Turbo, 1f)]
        public void AnAdvance_IsCutIntoEqualSteps_OfATenthOfADayAtMost(GameSpeed speed, float realSeconds)
        {
            GameClock clock = NewClock();
            clock.SetSpeed(speed);
            var steps = new List<float>();
            clock.Stepped += steps.Add;

            clock.Advance(realSeconds);

            float maxStep = 30f * (float)GameClock.MaxStepDays;
            float total = 0f;

            foreach (float step in steps)
            {
                Assert.LessOrEqual(step, maxStep + 1e-3f);
                Assert.AreEqual(steps[0], step, 1e-3f);
                total += step;
            }

            Assert.AreEqual(clock.DeltaTime, total, 1e-2f);
        }

        [Test]
        public void AStep_IsAnnouncedAfterTheDaysItStarts()
        {
            GameClock clock = NewClock();
            var events = new List<string>();
            clock.Advance(28f);
            clock.DayStarted += date => events.Add($"day {date.Day}");
            clock.Stepped += _ => events.Add($"step on day {clock.Date.Day}");

            // Two steps of 2 seconds: the first ends the first day.
            clock.Advance(4f);

            CollectionAssert.AreEqual(new[] { "day 2", "step on day 2", "step on day 2" }, events);
        }

        [Test]
        public void AFastAdvance_HasStepsOnEachOfItsDays()
        {
            GameClock clock = NewClock();
            clock.SetSpeed(GameSpeed.Turbo);
            var daysSeenByTheSteps = new HashSet<int>();
            clock.Stepped += _ => daysSeenByTheSteps.Add(clock.ElapsedDays);

            // 60 days in one frame: the steps run through each of them, not over them.
            clock.Advance(1f);

            for (int day = 1; day <= 60; day++)
            {
                Assert.IsTrue(daysSeenByTheSteps.Contains(day), $"No step on day {day}.");
            }
        }

        [Test]
        public void AStepListener_ThatChangesTheSpeed_ChangesItFromTheNextStep()
        {
            GameClock clock = NewClock();
            clock.SetSpeed(GameSpeed.Turbo);
            int steps = 0;
            clock.Stepped += _ =>
            {
                if (++steps == 2)
                {
                    clock.SetSpeed(GameSpeed.Normal);
                }
            };

            // At x1800, a hundredth of a second is 18 seconds, six steps of 3 seconds. Two
            // pass, then the four sixths of the real time left pass at normal speed.
            clock.Advance(0.01f);

            Assert.AreEqual(6f + 0.01f * 4f / 6f, clock.DeltaTime, 1e-4f);
            Assert.AreEqual(GameSpeed.Normal, clock.Speed);
        }

        [Test]
        public void AnAdvanceThatNeedsTooManySteps_DropsTheTimeTheyCannotHold()
        {
            var clock = new GameClock(30f, 60f, float.MaxValue, 932, 12, 30);
            clock.SetSpeed(GameSpeed.Turbo);
            int steps = 0;
            clock.Stepped += _ => steps++;

            clock.Advance(1f);

            Assert.AreEqual(GameClock.MaxStepsPerAdvance, steps);
            Assert.AreEqual(GameClock.MaxStepsPerAdvance * 3f, clock.DeltaTime, 1f);
        }

        [Test]
        public void SpeedChanged_IsRaisedOnEveryChange_FastForwardChangedWhenItStartsOrEnds()
        {
            GameClock clock = NewClock();
            var speeds = new List<GameSpeed>();
            var fastForwards = new List<bool>();
            clock.SpeedChanged += speeds.Add;
            clock.FastForwardChanged += fastForwards.Add;

            clock.SetSpeed(GameSpeed.FastForward);
            clock.SetSpeed(GameSpeed.Turbo);
            clock.SetSpeed(GameSpeed.Turbo);
            clock.SetSpeed(GameSpeed.Normal);

            CollectionAssert.AreEqual(new[] { GameSpeed.FastForward, GameSpeed.Turbo, GameSpeed.Normal }, speeds);
            CollectionAssert.AreEqual(new[] { true, false }, fastForwards);
        }

        [TestCase(0f, 60f, 1800f, 12, 30)]
        [TestCase(-30f, 60f, 1800f, 12, 30)]
        [TestCase(float.NaN, 60f, 1800f, 12, 30)]
        [TestCase(float.PositiveInfinity, 60f, 1800f, 12, 30)]
        [TestCase(30f, 0f, 1800f, 12, 30)]
        [TestCase(30f, float.NaN, 1800f, 12, 30)]
        [TestCase(30f, float.PositiveInfinity, 1800f, 12, 30)]
        [TestCase(30f, 60f, 0f, 12, 30)]
        [TestCase(30f, 60f, float.NaN, 12, 30)]
        [TestCase(30f, 60f, float.PositiveInfinity, 12, 30)]
        [TestCase(30f, 60f, 1800f, 0, 30)]
        [TestCase(30f, 60f, 1800f, 12, 0)]
        public void InvalidSettings_AreRejected(
            float secondsPerDay, float fastForwardMultiplier, float turboMultiplier, int monthsPerYear, int daysPerMonth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GameClock(
                    secondsPerDay, fastForwardMultiplier, turboMultiplier, 932, monthsPerYear, daysPerMonth));
        }
    }
}
