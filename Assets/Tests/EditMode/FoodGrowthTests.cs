using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class FoodGrowthTests
    {
        private const double Tolerance = 1e-9;

        // A week of food keeps the population, two make it grow by 5 % a month, none loses 10 %.
        private static FoodGrowth Growth => new FoodGrowth(7, 14, 0.05, 0.1, 30);

        [TestCase(0, -0.1)]
        [TestCase(1.75, -0.075)]
        [TestCase(3.5, -0.05)]
        [TestCase(7, 0)]
        [TestCase(10.5, 0.025)]
        [TestCase(14, 0.05)]
        public void TheRate_FollowsTheDaysOfFood_InStraightLines(double foodDays, double rate)
        {
            Assert.AreEqual(rate, Growth.MonthlyRateAt(foodDays), Tolerance);
        }

        [Test]
        public void MoreThanTheAbundantDays_GrowsNoFaster()
        {
            Assert.AreEqual(0.05, Growth.MonthlyRateAt(15), Tolerance);
            Assert.AreEqual(0.05, Growth.MonthlyRateAt(1000), Tolerance);
        }

        [Test]
        public void LessThanNothing_DeclinesNoFaster()
        {
            Assert.AreEqual(-0.1, Growth.MonthlyRateAt(-1), Tolerance);
        }

        [Test]
        public void WithTheSameStableAndAbundantDays_TheRateJumpsToTheGrowth()
        {
            var growth = new FoodGrowth(7, 7, 0.05, 0.1, 30);

            Assert.AreEqual(-0.05, growth.MonthlyRateAt(3.5), Tolerance);
            Assert.AreEqual(0.05, growth.MonthlyRateAt(7), Tolerance);
        }

        [Test]
        public void WithoutStableDays_APopulationNeverDeclines()
        {
            var growth = new FoodGrowth(0, 14, 0.05, 0.1, 30);

            Assert.AreEqual(0, growth.MonthlyRateAt(0), Tolerance);
            Assert.AreEqual(0.025, growth.MonthlyRateAt(7), Tolerance);
        }

        [Test]
        public void KeepsTheAbundantDays_AndTheDaysOfTheMonth()
        {
            Assert.AreEqual(14, Growth.AbundantDays);
            Assert.AreEqual(30, Growth.DaysPerMonth);
        }

        [Test]
        public void RejectsSettingsThatMakeNoSense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoodGrowth(-1, 14, 0.05, 0.1, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoodGrowth(double.NaN, 14, 0.05, 0.1, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoodGrowth(7, 6, 0.05, 0.1, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoodGrowth(7, double.PositiveInfinity, 0.05, 0.1, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoodGrowth(7, 14, -0.05, 0.1, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoodGrowth(7, 14, 0.05, -0.1, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoodGrowth(7, 14, 0.05, 1.1, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoodGrowth(7, 14, 0.05, 0.1, 0));
        }
    }
}
