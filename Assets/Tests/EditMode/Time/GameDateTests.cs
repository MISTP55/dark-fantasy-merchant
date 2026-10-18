using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GameDateTests
    {
        private static GameDate After(int elapsedDays)
        {
            return GameDate.FromElapsedDays(elapsedDays, 932, 12, 30);
        }

        [Test]
        public void NoElapsedDay_IsTheFirstDayOfTheStartYear()
        {
            Assert.AreEqual(new GameDate(932, 1, 1), After(0));
        }

        [Test]
        public void TheLastDayOfAMonth_IsFollowedByTheFirstOfTheNext()
        {
            Assert.AreEqual(new GameDate(932, 1, 30), After(29));
            Assert.AreEqual(new GameDate(932, 2, 1), After(30));
        }

        [Test]
        public void TheLastDayOfAYear_IsFollowedByTheFirstOfTheNextYear()
        {
            Assert.AreEqual(new GameDate(932, 12, 30), After(359));
            Assert.AreEqual(new GameDate(933, 1, 1), After(360));
        }

        [Test]
        public void ManyYears_AreCounted()
        {
            Assert.AreEqual(new GameDate(942, 6, 15), After(10 * 360 + 5 * 30 + 14));
        }

        [Test]
        public void AnotherCalendarShape_IsFollowed()
        {
            // 4 months of 10 days: day 45 is the sixth day of the first month of the next year.
            Assert.AreEqual(new GameDate(2, 1, 6), GameDate.FromElapsedDays(45, 1, 4, 10));
        }

        [Test]
        public void Dates_AreEqualByValue()
        {
            Assert.IsTrue(new GameDate(932, 3, 4) == new GameDate(932, 3, 4));
            Assert.IsTrue(new GameDate(932, 3, 4) != new GameDate(932, 3, 5));
            Assert.AreEqual(new GameDate(932, 3, 4).GetHashCode(), new GameDate(932, 3, 4).GetHashCode());
        }

        [TestCase(-1, 12, 30)]
        [TestCase(0, 0, 30)]
        [TestCase(0, 12, 0)]
        public void InvalidArguments_AreRejected(int elapsedDays, int monthsPerYear, int daysPerMonth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GameDate.FromElapsedDays(elapsedDays, 932, monthsPerYear, daysPerMonth));
        }
    }
}
