using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class LabourRuleTests
    {
        [TestCase(1067, 62)]   // √26 675 = 163 → 54 + 8
        [TestCase(10000, 174)] // √250 000 = 500 → 166 + 8
        [TestCase(0, 8)]
        public void BeggarTarget_FollowsTheRootOfThePopulation(int population, int target) =>
            Assert.AreEqual(target, TestMarkets.Labour.BeggarTargetFor(population));

        [Test]
        public void WithoutSatisfaction_TheTargetIsTheMinimum() =>
            Assert.AreEqual(8, new LabourRule(4, 0, 3, 8, 5, 10, 5, 3, 0.02, 30).BeggarTargetFor(1067));

        [TestCase(0, 1067, 7)]    // √1 067 = 32: (32 + 4) / 5 = 7 come
        [TestCase(59, 1067, 62)]  // never above the target
        [TestCase(100, 1067, 96)] // (32 + 9) / 10 = 4 go
        [TestCase(63, 1067, 62)]  // never under the target
        [TestCase(62, 1067, 62)]
        public void NextBeggars_ComeOrGo_TowardsTheTarget(int beggars, int population, int next) =>
            Assert.AreEqual(next, TestMarkets.Labour.NextBeggars(beggars, population));

        [Test]
        public void AnEmptyCity_GetsNoBeggar() => Assert.AreEqual(0, TestMarkets.Labour.NextBeggars(0, 0));

        [TestCase(40, 24, 6)]  // the room holds 6 workers' families
        [TestCase(10, 100, 2)] // 10 beggars make 2 workers
        [TestCase(3, 100, 0)]
        public void WorkersHireableFrom_TheBeggars_AndTheFreeRoom(int beggars, int freeRoom, int workers) =>
            Assert.AreEqual(workers, TestMarkets.Labour.WorkersHireableFrom(beggars, freeRoom));

        [Test]
        public void WorkersHireableFrom_ANegativeFreeRoom_HiresNobody() =>
            Assert.AreEqual(0, TestMarkets.Labour.WorkersHireableFrom(100, -50));

        [TestCase(50, 100, 12)] // min(49, 100) / 4
        [TestCase(200, 20, 5)]  // min(199, 20) / 4
        [TestCase(4, 100, 0)]   // under 5 beggars
        [TestCase(50, 3, 0)]    // 3 free room is not more than 3
        [TestCase(5, 4, 1)]     // min(4, 4) / 4
        public void VacanciesFillable_FollowTheBeggarsAndTheFreeRoom(int beggars, int freeRoom, int workers) =>
            Assert.AreEqual(workers, TestMarkets.Labour.VacanciesFillable(beggars, freeRoom));

        [TestCase(0, 5, 10, 0, 0.2)]    // 10 × 0.02: nothing whole yet
        [TestCase(0.9, 5, 10, 1, -0.9)] // 1.1 → 1 leaves; the counter loses it twice
        [TestCase(0.9, 20, 10, 0, 0.1)] // 30 workers and reserve: nothing leaves, it loses it once
        [TestCase(2.5, 19, 10, 1, -0.3)]// 2.7 → 2, cut to 30 − 29 = 1; 0.7 − 1
        [TestCase(-2, 5, 10, 0, -1.8)]  // a negative whole part counts as 0
        public void LeakAfterLayoff_LeaksTheWholePart_UpToTheCeiling(double counter, int workers, int reserve, int leaving, double next)
        {
            ReserveLeak leak = TestMarkets.Labour.LeakAfterLayoff(counter, workers, reserve);
            Assert.AreEqual(leaving, leak.Leaving);
            Assert.AreEqual(next, leak.Counter, 1e-9);
        }

        [Test]
        public void TheConstructor_RejectsUnusableSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(0, 50, 3, 8, 5, 10, 5, 3, 0.02, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, double.NaN, 3, 8, 5, 10, 5, 3, 0.02, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 0, 8, 5, 10, 5, 3, 0.02, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, -1, 5, 10, 5, 3, 0.02, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 0, 10, 5, 3, 0.02, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 0, 5, 3, 0.02, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 10, -1, 3, 0.02, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 10, 5, -1, 0.02, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 1.5, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 0.02, -1));
        }
    }
}
