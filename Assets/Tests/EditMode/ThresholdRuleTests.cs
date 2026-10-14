using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ThresholdRuleTests
    {
        private const double Tolerance = 1e-9;

        // Patrician III's: 7 days, 14 more, 10 days of production more; floors of 5 to 20 barrels.
        private static ThresholdRule Rule => new ThresholdRule(7, 14, 10, 5, 10, 15, 20);

        private static void AssertThresholds(StockThresholds actual, double t0, double t1, double t2, double t3)
        {
            Assert.AreEqual(t0, actual.T0, Tolerance, "T0");
            Assert.AreEqual(t1, actual.T1, Tolerance, "T1");
            Assert.AreEqual(t2, actual.T2, Tolerance, "T2");
            Assert.AreEqual(t3, actual.T3, Tolerance, "T3");
        }

        [Test]
        public void AGoodTheCityDoesNotProduce_HasT2AtT1()
        {
            // Two barrels a day: 14, then 28 more, then nothing produced, then T0 again.
            AssertThresholds(Rule.ThresholdsFor(2, 0), 14, 42, 42, 56);
        }

        [Test]
        public void T2_AddsDaysOfProduction()
        {
            // Four barrels produced a day: 40 more barrels from T1 to T2.
            AssertThresholds(Rule.ThresholdsFor(2, 4), 14, 42, 82, 96);
        }

        [Test]
        public void TheThresholds_FollowTheConsumption()
        {
            AssertThresholds(Rule.ThresholdsFor(20, 0), 140, 420, 420, 560);
        }

        [Test]
        public void AThreshold_IsAtLeastItsFloor()
        {
            // Half a barrel a day: 3.5 for T0, raised to 5; T1 is 5 + 7, above its floor of 10;
            // T2 is raised to 15, T3 is 15 + 5, which is its floor.
            AssertThresholds(Rule.ThresholdsFor(0.5, 0), 5, 12, 15, 20);
        }

        [Test]
        public void WithoutConsumption_TheThresholdsAreTheFloors()
        {
            AssertThresholds(Rule.ThresholdsFor(0, 0), 5, 10, 15, 20);
        }

        [Test]
        public void WithoutDays_TheThresholdsAreTheFloors()
        {
            var rule = new ThresholdRule(0, 0, 0, 5, 10, 15, 20);

            AssertThresholds(rule.ThresholdsFor(100, 100), 5, 10, 15, 20);
        }

        [Test]
        public void ARule_KeepsItsSettings()
        {
            ThresholdRule rule = Rule;

            Assert.AreEqual(7, rule.T0Days);
            Assert.AreEqual(14, rule.T1AddedDays);
            Assert.AreEqual(10, rule.T2ProductionDays);
            Assert.AreEqual(5, rule.T0Floor);
            Assert.AreEqual(10, rule.T1Floor);
            Assert.AreEqual(15, rule.T2Floor);
            Assert.AreEqual(20, rule.T3Floor);
        }

        [TestCase(-1, 14, 10, 5, 10, 15, 20)]
        [TestCase(7, -1, 10, 5, 10, 15, 20)]
        [TestCase(7, 14, double.NaN, 5, 10, 15, 20)]
        [TestCase(double.PositiveInfinity, 14, 10, 5, 10, 15, 20)]
        [TestCase(7, 14, 10, 0, 10, 15, 20)]
        [TestCase(7, 14, 10, 5, 10, 8, 20)]
        [TestCase(7, 14, 10, 5, 10, 15, double.PositiveInfinity)]
        [TestCase(7, 14, 10, double.NaN, 10, 15, 20)]
        public void RejectsSettingsThatMakeNoSense(
            double t0Days, double t1AddedDays, double t2ProductionDays,
            double t0Floor, double t1Floor, double t2Floor, double t3Floor)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ThresholdRule(t0Days, t1AddedDays, t2ProductionDays, t0Floor, t1Floor, t2Floor, t3Floor));
        }

        [TestCase(-1, 0)]
        [TestCase(2, -1)]
        [TestCase(double.NaN, 0)]
        [TestCase(2, double.PositiveInfinity)]
        public void ThresholdsFor_RejectsFiguresThatMakeNoSense(double dailyConsumption, double dailyProduction)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.ThresholdsFor(dailyConsumption, dailyProduction));
        }
    }
}
