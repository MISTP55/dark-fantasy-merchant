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
        private static City NewCity(double basePrice, double merchantOutput)
        {
            var made = new MarketGood(basePrice, 0, 1, 0.1, merchantOutput).WithInputs(new[] { new MarketInput(Input, 1) });
            var input = new MarketGood(10, 0, 1, 0.1, merchantOutput);
            return new City(new[] { made, input }, TestMarkets.Thresholds, Workforce, 100, TestMarkets.Pricing, 7,
                new CityPopulation(1000, 200, 2000), TestMarkets.Labour, grid: TestMarkets.GridWithRoad(), placement: TestMarkets.Placement, merchantOutput: Bonus);
        }

        // The workshops, an empty trading post, and a day for them to hire.
        private static City Hired(City city, params int[] jobsOfEachWorkshop)
        {
            foreach (int jobs in jobsOfEachWorkshop)
            {
                TestMarkets.AddPlayerWorkshop(city, Made, jobs);
            }

            city.SetPlayerTradingPost(new Warehouse(2000));
            city.StartDay();
            return city;
        }

        [Test]
        public void Wages_AreEightyPercentOfTheWeeksBaseValue()
        {
            // 10 workers making a seventieth each: one unit a week of a good worth 100.
            City city = Hired(NewCity(100, 1d / 70), 10);

            Assert.AreEqual(10, city.PlayerWorkersOf(Made));
            Assert.AreEqual(80, PlayerWorkshopCosts.WeeklyWagesOf(new[] { city }, 0.8));
        }

        [Test]
        public void Wages_FollowTheWorkersPresent()
        {
            // More jobs than a day fills.
            City city = Hired(NewCity(100, 0.1), 1000);
            int workers = city.PlayerWorkersOf(Made);

            Assert.That(workers, Is.InRange(1, 999));
            Assert.AreEqual(workers * 0.1 * 7 * 100 * 0.8, city.PlayerWeeklyWagesOf(0.8), 1e-6);
        }

        [Test]
        public void Wages_ArePaidInFull_WhenShortOfInputs()
        {
            // The trading post holds no Input: the workshop makes nothing and pays its workers all the same.
            City city = Hired(NewCity(100, 1d / 70), 10);

            Assert.AreEqual(0, city.PlayerTradingPost.UnitsOf(Made));
            Assert.AreEqual(80, PlayerWorkshopCosts.WeeklyWagesOf(new[] { city }, 0.8));
        }

        [Test]
        public void Wages_CountTheBonus()
        {
            City city = Hired(NewCity(100, 1d / 70), 10, 10, 10);

            Assert.AreEqual(30, city.PlayerWorkersOf(Made));
            Assert.AreEqual(247, PlayerWorkshopCosts.WeeklyWagesOf(new[] { city }, 0.8));
        }

        [Test]
        public void Wages_AreRoundedOnce_AfterSumming()
        {
            // 0.4 gold in each city.
            City first = Hired(NewCity(1, 1d / 140), 10);
            City second = Hired(NewCity(1, 1d / 140), 10);

            Assert.AreEqual(0.4, first.PlayerWeeklyWagesOf(0.8), 1e-9);
            Assert.AreEqual(1, PlayerWorkshopCosts.WeeklyWagesOf(new[] { first, second }, 0.8));
        }

        [Test]
        public void Wages_RefuseANegativeShare()
        {
            City city = NewCity(100, 0.1);

            Assert.Throws<ArgumentOutOfRangeException>(() => city.PlayerWeeklyWagesOf(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => city.PlayerWeeklyWagesOf(double.NaN));
        }

        [Test]
        public void RunningCost_IsOwedForEveryWorkshop_WithOrWithoutWorkers()
        {
            // Without labour, nobody is ever hired.
            var city = new City(new[] { new MarketGood(100, 0, 1, 0.1, 0.1) }, TestMarkets.Thresholds, Workforce, 100,
                TestMarkets.Pricing, 7, new CityPopulation(1000), grid: TestMarkets.GridWithRoad(), placement: TestMarkets.Placement);
            TestMarkets.AddPlayerWorkshop(city, Made, 10);
            TestMarkets.AddPlayerWorkshop(city, Made, 10);
            city.StartDay();

            Assert.AreEqual(0, city.PlayerWorkersOf(Made));
            Assert.AreEqual(400, PlayerWorkshopCosts.WeeklyRunningCostOf(new[] { city }, good => good == Made ? 200 : 0));
        }
    }
}
