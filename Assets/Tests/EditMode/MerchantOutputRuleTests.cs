using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MerchantOutputRuleTests
    {
        private const double Tolerance = 1e-9;

        // Patrician III's: 3 % from 3 workshops, 6 % from 6, 10 % from 9.
        private static MerchantOutputRule Rule => new MerchantOutputRule(3, 1.03, 6, 1.06, 9, 1.1);

        [TestCase(0, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 1.03)]
        [TestCase(5, 1.03)]
        [TestCase(6, 1.06)]
        [TestCase(8, 1.06)]
        [TestCase(9, 1.1)]
        [TestCase(40, 1.1)]
        public void BonusFor_FollowsTheSteps(int workshops, double bonus)
        {
            Assert.AreEqual(bonus, Rule.BonusFor(workshops), Tolerance);
        }

        [Test]
        public void DailyOutputOf_IsTheWorkersTimesTheirOutput_TimesTheBonus()
        {
            // 90 workers of 3 workshops making a fifteenth each: 6 units, plus 3 %.
            Assert.AreEqual(6.18, Rule.DailyOutputOf(90, 1d / 15d, 3), Tolerance);
            Assert.AreEqual(2, Rule.DailyOutputOf(30, 1d / 15d, 1), Tolerance);
            Assert.AreEqual(0, Rule.DailyOutputOf(0, 0.1, 1));
        }

        [Test]
        public void ABadArgument_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.BonusFor(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.DailyOutputOf(-1, 0.1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.DailyOutputOf(1, -0.1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.DailyOutputOf(1, double.NaN, 1));
        }

        [Test]
        public void BadSettings_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MerchantOutputRule(0, 1.03, 6, 1.06, 9, 1.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MerchantOutputRule(3, 1.03, 3, 1.06, 9, 1.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MerchantOutputRule(3, 1.03, 6, 1.06, 6, 1.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MerchantOutputRule(3, -1, 6, 1.06, 9, 1.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MerchantOutputRule(3, 1.03, 6, double.NaN, 9, 1.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MerchantOutputRule(3, 1.03, 6, 1.06, 9, double.PositiveInfinity));
        }
    }
}
