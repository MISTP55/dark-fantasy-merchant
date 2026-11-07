using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class LeavingRuleTests
    {
        [Test]
        public void LeavingOf_TheExcess_AQuarterPlusOne()
        {
            Assert.AreEqual(26, new LeavingRule(30).LeavingOf(new CityPopulation(1000, 0, 900)));
        }

        [Test]
        public void LeavingOf_NobodyWithinTheRoom()
        {
            Assert.AreEqual(0, new LeavingRule(30).LeavingOf(new CityPopulation(900, 0, 900)));
            Assert.AreEqual(0, new LeavingRule(30).LeavingOf(new CityPopulation(1000, 0, 2000)));
        }

        [Test]
        public void LeavingOf_NeverTheLastOne()
        {
            Assert.AreEqual(2, new LeavingRule(30).LeavingOf(new CityPopulation(5, 0, 0)));
            Assert.AreEqual(0, new LeavingRule(30).LeavingOf(new CityPopulation(1, 0, 0)));
        }

        [Test]
        public void LeavingOf_NeedsAPopulation()
        {
            Assert.Throws<ArgumentNullException>(() => new LeavingRule(30).LeavingOf(null));
        }

        [Test]
        public void Constructor_KeepsTheProtectedWorkers_AndRefusesNegativeOnes()
        {
            Assert.AreEqual(30, new LeavingRule(30).ProtectedWorkers);
            Assert.Throws<ArgumentOutOfRangeException>(() => new LeavingRule(-1));
        }
    }
}
