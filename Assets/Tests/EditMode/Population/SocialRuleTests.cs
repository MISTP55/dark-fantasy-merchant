using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class SocialRuleTests
    {
        private static SocialRule Rule => TestMarkets.Social;

        private static CityPopulation Population(int rich, int wealthy, int poor, int richRoom, int wealthyRoom, int poorRoom, int satisfaction)
        {
            var population = new CityPopulation(new PerClass<int>(rich, wealthy, poor), 0, new PerClass<int>(richRoom, wealthyRoom, poorRoom));
            population.SetSatisfaction(SocialClass.Rich, satisfaction);
            population.SetSatisfaction(SocialClass.Wealthy, satisfaction);
            population.SetSatisfaction(SocialClass.Poor, satisfaction);
            return population;
        }

        [Test]
        public void GoodTerm_FollowsTheStockAgainstT0()
        {
            Assert.AreEqual(-8, Rule.GoodTerm(8, 0, 10));
            Assert.AreEqual(-4, Rule.GoodTerm(8, 5, 10));
            Assert.AreEqual(0, Rule.GoodTerm(8, 10, 10));
            Assert.AreEqual(2, Rule.GoodTerm(8, 13, 10)); // 2.4 truncated
            Assert.AreEqual(4, Rule.GoodTerm(8, 15, 10));
            Assert.AreEqual(8, Rule.GoodTerm(8, 20, 10));
            Assert.AreEqual(8, Rule.GoodTerm(8, 25, 10));
        }

        [Test]
        public void TargetOf_AddsTheBaseAndTheCommonTerm()
        {
            Assert.AreEqual(-9, Rule.TargetOf(SocialClass.Rich, -5));
            Assert.AreEqual(32, Rule.TargetOf(SocialClass.Poor, 50));
            Assert.AreEqual(-9, Rule.TargetOf(SocialClass.Wealthy, 0));
        }

        [Test]
        public void NextSatisfaction_MovesTowardsTwiceTheTarget()
        {
            Assert.AreEqual(49, Rule.NextSatisfaction(50, -9));
            Assert.AreEqual(52, Rule.NextSatisfaction(50, 30));
            Assert.AreEqual(60, Rule.NextSatisfaction(60, 30));
            Assert.AreEqual(80, Rule.NextSatisfaction(79, 45));
            Assert.AreEqual(-40, Rule.NextSatisfaction(-40, -30));
        }

        [Test]
        public void ClassTargetOf_IsPatricianIIIs()
        {
            Assert.AreEqual(168, Rule.ClassTargetOf(SocialClass.Rich, 50, 600, 200));
            Assert.AreEqual(337, Rule.ClassTargetOf(SocialClass.Wealthy, 50, 600, 400));
            Assert.AreEqual(40, Rule.ClassTargetOf(SocialClass.Rich, 50, 600, 40));
            Assert.AreEqual(10, Rule.ClassTargetOf(SocialClass.Rich, 50, 600, 0));
            Assert.AreEqual(1, Rule.ClassTargetOf(SocialClass.Rich, -40, 600, 200));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.ClassTargetOf(SocialClass.Poor, 50, 600, 700));
        }

        [Test]
        public void RiseAndFall_MovesPoorUpTowardsTheTargets()
        {
            CityPopulation population = Population(60, 180, 600, 200, 400, 700, 50);

            Rule.RiseAndFall(population);

            Assert.AreEqual(66, population.CountOf(SocialClass.Rich));
            Assert.AreEqual(184, population.CountOf(SocialClass.Wealthy));
            Assert.AreEqual(590, population.CountOf(SocialClass.Poor));
        }

        [Test]
        public void RiseAndFall_SendsAClassAboveItsRoomBackToThePoor()
        {
            CityPopulation population = Population(50, 300, 600, 40, 400, 700, 50);

            Rule.RiseAndFall(population);

            Assert.AreEqual(47, population.CountOf(SocialClass.Rich));
            Assert.AreEqual(950, population.Housed);
        }

        [Test]
        public void RiseAndFall_FillsAnEmptyClassOneAtATime()
        {
            CityPopulation population = Population(0, 300, 600, 200, 400, 700, 50);

            Rule.RiseAndFall(population);

            Assert.AreEqual(1, population.CountOf(SocialClass.Rich));
            Assert.AreEqual(900, population.Housed);
        }

        [Test]
        public void RiseAndFall_WithoutAFloor_EmptiesAClassWithoutRoom_ToOne()
        {
            var rule = new SocialRule(50, new PerClass<int>(-13, -18, -27), 2, 1, 9, -40, 80, 40, 320, 160, 0, 100, 30);
            CityPopulation population = Population(20, 300, 600, 0, 400, 700, 50);

            rule.RiseAndFall(population);

            Assert.AreEqual(1, population.CountOf(SocialClass.Rich));
        }

        [Test]
        public void LeavingPoorOf_IsTheExcessOverTheRoom() =>
            Assert.AreEqual(6, Rule.LeavingPoorOf(Population(0, 0, 1000, 0, 0, 980, 10)));

        [Test]
        public void LeavingPoorOf_IsOnePercentOfUnhappyPoor() =>
            Assert.AreEqual(10, Rule.LeavingPoorOf(Population(0, 0, 1000, 0, 0, 1400, -5)));

        [Test]
        public void LeavingPoorOf_NeverTakesTheLastPoor() =>
            Assert.AreEqual(0, Rule.LeavingPoorOf(Population(0, 0, 1, 0, 0, 0, 10)));

        [Test]
        public void LeavingPoorOf_IsZeroForHousedHappyPoor() =>
            Assert.AreEqual(0, Rule.LeavingPoorOf(Population(0, 0, 600, 0, 0, 700, 10)));

        [Test]
        public void Constructor_RefusesAStartOutsideTheBounds()
        {
            var bases = new PerClass<int>(-13, -18, -27);

            Assert.Throws<ArgumentOutOfRangeException>(() => new SocialRule(81, bases, 2, 1, 9, -40, 80, 40, 320, 160, 10, 100, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SocialRule(50, bases, 2, 1, 9, 80, -40, 40, 320, 160, 10, 100, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SocialRule(50, bases, -2, 1, 9, -40, 80, 40, 320, 160, 10, 100, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SocialRule(50, bases, 2, 1, 9, -40, 80, 40, 0, 160, 10, 100, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SocialRule(50, bases, 2, 1, 9, -40, 80, 40, 320, 160, -1, 100, 30));
            Assert.DoesNotThrow(() => new SocialRule(50, bases, 2, 1, 9, -40, 80, 40, 320, 160, 10, 100, 30));
        }
    }
}
