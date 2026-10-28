using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PlayerWorkshopCostsTests
    {
        private const int Made = 0;
        private const int Input = 1;

        private static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 30);

        private static MerchantOutputRule Bonus => new MerchantOutputRule(3, 1.03, 6, 1.06, 9, 1.1);

        // The city's workshops have one worker, so that the beggars are left to the player's; Made needs one Input a unit.
        private static CityMarket Market(double basePrice, double merchantOutput)
        {
            var made = new MarketGood(basePrice, 0, 1, 0.1, merchantOutput).WithInputs(new[] { new MarketInput(Input, 1) });
            var input = new MarketGood(10, 0, 1, 0.1, merchantOutput);
            return new CityMarket(new[] { made, input }, TestMarkets.Thresholds, Workforce, 100, TestMarkets.Pricing, 7,
                new CityPopulation(1000, 200, 2000), TestMarkets.Labour, merchantOutput: Bonus);
        }

        // The workshops, an empty trading post, and a day for them to hire.
        private static CityMarket Hired(CityMarket market, params int[] jobsOfEachWorkshop)
        {
            foreach (int jobs in jobsOfEachWorkshop)
            {
                market.AddPlayerWorkshop(Made, jobs);
            }

            market.SetPlayerTradingPost(new Warehouse(2000));
            market.StartDay();
            return market;
        }

        [Test]
        public void Wages_AreEightyPercentOfTheWeeksBaseValue()
        {
            // 10 workers making a seventieth each: one unit a week of a good worth 100.
            CityMarket market = Hired(Market(100, 1d / 70), 10);

            Assert.AreEqual(10, market.PlayerWorkersOf(Made));
            Assert.AreEqual(80, PlayerWorkshopCosts.WeeklyWagesOf(new[] { market }, 0.8));
        }

        [Test]
        public void Wages_FollowTheWorkersPresent()
        {
            // More jobs than a day fills.
            CityMarket market = Hired(Market(100, 0.1), 1000);
            int workers = market.PlayerWorkersOf(Made);

            Assert.That(workers, Is.InRange(1, 999));
            Assert.AreEqual(workers * 0.1 * 7 * 100 * 0.8, market.PlayerWeeklyWagesOf(0.8), 1e-6);
        }

        [Test]
        public void Wages_ArePaidInFull_WhenShortOfInputs()
        {
            // The trading post holds no Input: the workshop makes nothing and pays its workers all the same.
            CityMarket market = Hired(Market(100, 1d / 70), 10);

            Assert.AreEqual(0, market.PlayerTradingPost.UnitsOf(Made));
            Assert.AreEqual(80, PlayerWorkshopCosts.WeeklyWagesOf(new[] { market }, 0.8));
        }

        [Test]
        public void Wages_CountTheBonus()
        {
            CityMarket market = Hired(Market(100, 1d / 70), 10, 10, 10);

            Assert.AreEqual(30, market.PlayerWorkersOf(Made));
            Assert.AreEqual(247, PlayerWorkshopCosts.WeeklyWagesOf(new[] { market }, 0.8));
        }

        [Test]
        public void Wages_AreRoundedOnce_AfterSumming()
        {
            // 0.4 gold in each city.
            CityMarket first = Hired(Market(1, 1d / 140), 10);
            CityMarket second = Hired(Market(1, 1d / 140), 10);

            Assert.AreEqual(0.4, first.PlayerWeeklyWagesOf(0.8), 1e-9);
            Assert.AreEqual(1, PlayerWorkshopCosts.WeeklyWagesOf(new[] { first, second }, 0.8));
        }

        [Test]
        public void Wages_RefuseANegativeShare()
        {
            CityMarket market = Market(100, 0.1);

            Assert.Throws<ArgumentOutOfRangeException>(() => market.PlayerWeeklyWagesOf(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.PlayerWeeklyWagesOf(double.NaN));
        }

        [Test]
        public void RunningCost_IsOwedForEveryWorkshop_WithOrWithoutWorkers()
        {
            // Without labour, nobody is ever hired.
            var market = new CityMarket(new[] { new MarketGood(100, 0, 1, 0.1, 0.1) }, TestMarkets.Thresholds, Workforce, 100,
                TestMarkets.Pricing, 7, new CityPopulation(1000));
            market.AddPlayerWorkshop(Made, 10);
            market.AddPlayerWorkshop(Made, 10);
            market.StartDay();

            Assert.AreEqual(0, market.PlayerWorkersOf(Made));
            Assert.AreEqual(400, PlayerWorkshopCosts.WeeklyRunningCostOf(new[] { market }, good => good == Made ? 200 : 0));
        }
    }
}
