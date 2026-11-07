using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WorkforceRuleTests
    {
        // 16, 8, 4 and 1 workers a day, one above T3; 250 to start with.
        private static WorkforceRule Rule => new WorkforceRule(16, 8, 4, 1, 1, 250);

        private static readonly StockThresholds Thresholds = new StockThresholds(20, 60, 90, 110);

        [TestCase(0, 26)]
        [TestCase(20, 26)]
        [TestCase(20.5, 18)]
        [TestCase(60, 18)]
        [TestCase(61, 14)]
        [TestCase(90, 14)]
        [TestCase(91, 11)]
        [TestCase(110, 11)]
        public void TheWorkersHiredInADay_FollowTheStock(double stock, int expected)
        {
            // Ten workers to start with; a stock equal to a threshold counts as under it.
            Assert.AreEqual(expected, Rule.NextWorkers(10, 60, stock, Thresholds));
        }

        [Test]
        public void Hiring_StopsAtTheMaximum()
        {
            Assert.AreEqual(60, Rule.NextWorkers(55, 60, 0, Thresholds));
            Assert.AreEqual(60, Rule.NextWorkers(60, 60, 100, Thresholds));
        }

        [TestCase(110.01)]
        [TestCase(500)]
        public void AboveT3_AWorkshopKeepsOneWorker_AndDoesNotProduce(double stock)
        {
            Assert.AreEqual(1, Rule.NextWorkers(60, 60, stock, Thresholds));
            Assert.IsFalse(WorkforceRule.ProducesAt(stock, Thresholds));
        }

        [Test]
        public void AtT3_AWorkshopStillProduces()
        {
            Assert.IsTrue(WorkforceRule.ProducesAt(110, Thresholds));
            Assert.IsTrue(WorkforceRule.ProducesAt(0, Thresholds));
        }

        [Test]
        public void AboveT3_TheWorkersKept_AreNeverAboveTheMaximum()
        {
            var rule = new WorkforceRule(16, 8, 4, 1, 5, 250);

            Assert.AreEqual(3, rule.NextWorkers(3, 3, 500, Thresholds));
        }

        [Test]
        public void AWorkshopWithoutRoomForWorkers_StaysEmpty()
        {
            Assert.AreEqual(0, Rule.NextWorkers(0, 0, 0, Thresholds));
            Assert.AreEqual(0, Rule.NextWorkers(0, 0, 500, Thresholds));
        }

        [TestCase(1, 300, 250)]
        [TestCase(2, 200, 125)]
        [TestCase(3, 60, 60)]
        [TestCase(4, 78, 63)]
        [TestCase(5, 65, 50)]
        public void TheStartingWorkforce_IsSpreadEvenly_RoundedUp_AndCutToTheMaximum(int workshops, int maxWorkers, int expected)
        {
            // 250 over 3 is 84, cut to 60; 250 over 4 is 62.5, rounded up.
            Assert.AreEqual(expected, Rule.StartingWorkersOf(maxWorkers, workshops));
        }

        [Test]
        public void WithoutWorkshops_NoOneStarts()
        {
            Assert.AreEqual(0, Rule.StartingWorkersOf(60, 0));
            Assert.AreEqual(0, Rule.StartingWorkersOf(0, 3));
        }

        [Test]
        public void AHugeStartingWorkforce_DoesNotOverflow()
        {
            var rule = new WorkforceRule(16, 8, 4, 1, 1, int.MaxValue);

            Assert.AreEqual(60, rule.StartingWorkersOf(60, 2));
            Assert.AreEqual(60, new WorkforceRule(int.MaxValue, 8, 4, 1, 1, 250).NextWorkers(50, 60, 0, Thresholds));
        }

        [Test]
        public void ARule_KeepsItsSettings()
        {
            WorkforceRule rule = Rule;

            Assert.AreEqual(16, rule.HiredUpToT0);
            Assert.AreEqual(8, rule.HiredUpToT1);
            Assert.AreEqual(4, rule.HiredUpToT2);
            Assert.AreEqual(1, rule.HiredUpToT3);
            Assert.AreEqual(1, rule.WorkersAboveT3);
            Assert.AreEqual(250, rule.StartingWorkforce);
        }

        [TestCase(-1, 8, 4, 1, 1, 250, "hiredUpToT0")]
        [TestCase(16, -1, 4, 1, 1, 250, "hiredUpToT1")]
        [TestCase(16, 8, -1, 1, 1, 250, "hiredUpToT2")]
        [TestCase(16, 8, 4, -1, 1, 250, "hiredUpToT3")]
        [TestCase(16, 8, 4, 1, -1, 250, "workersAboveT3")]
        [TestCase(16, 8, 4, 1, 1, -1, "startingWorkforce")]
        public void RejectsANegativeSetting_AndNamesIt(
            int hiredUpToT0, int hiredUpToT1, int hiredUpToT2, int hiredUpToT3, int workersAboveT3, int startingWorkforce, string setting)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new WorkforceRule(hiredUpToT0, hiredUpToT1, hiredUpToT2, hiredUpToT3, workersAboveT3, startingWorkforce));

            Assert.AreEqual(setting, exception.ParamName);
        }

        [Test]
        public void RejectsNegativeWorkers()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.NextWorkers(-1, 60, 0, Thresholds));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.NextWorkers(10, -1, 0, Thresholds));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.StartingWorkersOf(-1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.StartingWorkersOf(60, -1));
        }
    }
}
