using System;
using System.Linq;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketLabourTests
    {
        // Two barrels a day for 1 000 inhabitants; a workshop of up to 60 workers making a tenth each.
        // Near 1 000 inhabitants: T0 20, T1 60, T2 120, T3 140.
        private static MarketGood Good => new MarketGood(10, 2, 60, 0.1);

        // The workshop of a one-good market starts with 10 workers.
        private static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 10);

        // Each workshop of a two-goods market starts with 10 workers.
        private static WorkforceRule TwoWorkshopsWorkforce => new WorkforceRule(16, 8, 4, 1, 1, 20);

        private static CityMarket Market(
            double startingDays,
            CityPopulation population,
            LabourRule labour = null,
            int goods = 1,
            WorkforceRule workforce = null)
        {
            return new CityMarket(Enumerable.Repeat(Good, goods).ToArray(), TestMarkets.Thresholds, workforce ?? Workforce, startingDays,
                TestMarkets.Pricing, 7, population, labour ?? TestMarkets.Labour);
        }

        private static CityMarket TwoGoodsMarket(double startingDays, CityPopulation population)
        {
            return Market(startingDays, population, goods: 2, workforce: TwoWorkshopsWorkforce);
        }

        private static CityMarket MarketWithoutLabour(double startingDays, CityPopulation population)
        {
            return new CityMarket(new[] { Good }, TestMarkets.Thresholds, Workforce, startingDays,
                TestMarkets.Pricing, 7, population, null);
        }

        [Test]
        public void AWorkshop_HiresBeggars_FourPerWorker_WithinTheFreeRoom()
        {
            // An empty stock wants 26 workers; 24 places take 6 of the 40 beggars.
            CityMarket market = Market(0, new CityPopulation(1000, 40, 1024));

            market.StartDay();

            Assert.AreEqual(16, market.WorkersOf(0));
            Assert.AreEqual(1024, market.Population.Housed);

            // 16 left, then 7 come: the target is 61 at 1 040 inhabitants.
            Assert.AreEqual(23, market.Population.Beggars);
        }

        [Test]
        public void WithoutFreeRoom_NoBeggarIsHired()
        {
            CityMarket market = Market(0, new CityPopulation(1000, 100, 1000));

            market.StartDay();

            Assert.AreEqual(10, market.WorkersOf(0));
            Assert.AreEqual(1000, market.Population.Housed);
            Assert.AreEqual(96, market.Population.Beggars);
        }

        [Test]
        public void ALayoff_GoesToTheReserve_AndStaysHoused()
        {
            // 200 barrels, above T3: the workshop keeps one worker.
            CityMarket market = Market(100, new CityPopulation(1000));

            market.StartDay();

            Assert.AreEqual(1, market.WorkersOf(0));
            Assert.AreEqual(9, market.ReserveOf(0));
            Assert.AreEqual(1000, market.Population.Housed);
        }

        [Test]
        public void AWorkshop_HiresFromItsReserveFirst()
        {
            CityMarket market = Market(100, new CityPopulation(1000));
            market.StartDay();
            market.Take(0, market.AvailableOf(0));

            market.StartDay();

            Assert.AreEqual(10, market.WorkersOf(0));
            Assert.AreEqual(0, market.ReserveOf(0));
            Assert.AreEqual(1000, market.Population.Housed);
        }

        [Test]
        public void AWorkshop_TakesOtherReserves_AfterTheBeggars()
        {
            CityMarket market = TwoGoodsMarket(100, new CityPopulation(1000, 8, 1008));
            market.Take(1, market.AvailableOf(1));

            market.StartDay();

            Assert.AreEqual(1, market.WorkersOf(0));
            Assert.AreEqual(0, market.ReserveOf(0));

            // 2 from the beggars, then the 9 of good 0's reserve.
            Assert.AreEqual(21, market.WorkersOf(1));
            Assert.AreEqual(1008, market.Population.Housed);
        }

        [Test]
        public void ALeak_SendsFourInhabitantsPerWorkerToTheBeggars()
        {
            CityMarket market = Market(100, new CityPopulation(1000), new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 0.5, 30));

            market.StartDay();

            // 9 × 0.5 = 4.5: 4 leave.
            Assert.AreEqual(5, market.ReserveOf(0));
            Assert.AreEqual(984, market.Population.Housed);

            // 16, then 7 come.
            Assert.AreEqual(23, market.Population.Beggars);
        }

        [Test]
        public void TheCity_HiresBeforeThePlayer()
        {
            CityMarket market = Market(0, new CityPopulation(1000, 40, 1100));
            market.AddPlayerWorkshop(0, 30);

            market.StartDay();

            Assert.AreEqual(20, market.WorkersOf(0));
            Assert.AreEqual(0, market.PlayerWorkersOf(0));
        }

        [Test]
        public void Vacancies_AreFilledFromTheBeggars()
        {
            CityMarket market = Market(100, new CityPopulation(1000, 50, 1100));
            market.AddPlayerWorkshop(0, 30);

            market.StartDay();

            Assert.AreEqual(12, market.PlayerWorkersOf(0));
            Assert.AreEqual(1048, market.Population.Housed);
            Assert.AreEqual(9, market.Population.Beggars);

            market.StartDay();

            Assert.AreEqual(14, market.PlayerWorkersOf(0));
        }

        [Test]
        public void Vacancies_FillThePlayersWorkshopsInTheOrderTheyWereBuilt()
        {
            CityMarket market = TwoGoodsMarket(100, new CityPopulation(1000, 200, 1200));
            market.AddPlayerWorkshop(1, 30);
            market.AddPlayerWorkshop(0, 30);

            market.StartDay();

            Assert.AreEqual(30, market.PlayerWorkersOf(1));
            Assert.AreEqual(19, market.PlayerWorkersOf(0));
            Assert.AreEqual(30, market.PlayerJobsOf(0));
            Assert.AreEqual(1, market.PlayerWorkshopsOf(0));
        }

        [Test]
        public void Vacancies_NeedFiveBeggars()
        {
            CityMarket market = Market(100, new CityPopulation(1000, 4, 1100));
            market.AddPlayerWorkshop(0, 30);

            market.StartDay();

            Assert.AreEqual(0, market.PlayerWorkersOf(0));
        }

        [Test]
        public void AddPlayerHouse_AddsItsRoom_AndRaisesChangedOnce()
        {
            CityMarket market = Market(30, new CityPopulation(1000));
            int changes = 0;
            market.Changed += () => changes++;

            market.AddPlayerHouse(100);

            Assert.AreEqual(1100, market.Population.Room);
            Assert.AreEqual(1, market.PlayerHouses);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void AddPlayerWorkshop_AddsItsJobs_AndRaisesChangedOnce()
        {
            CityMarket market = Market(30, new CityPopulation(1000));
            int changes = 0;
            market.Changed += () => changes++;

            market.AddPlayerWorkshop(0, 30);

            Assert.AreEqual(1, market.PlayerWorkshopsOf(0));
            Assert.AreEqual(30, market.PlayerJobsOf(0));
            Assert.AreEqual(0, market.PlayerWorkersOf(0));
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void AddPlayerWorkshop_RefusesAGoodNotProduced()
        {
            var market = new CityMarket(new[] { new MarketGood(10, 2, 0, 0), Good }, TestMarkets.Thresholds, Workforce, 30,
                TestMarkets.Pricing, 7, new CityPopulation(1000), TestMarkets.Labour);

            Assert.Throws<ArgumentOutOfRangeException>(() => market.AddPlayerWorkshop(0, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.AddPlayerWorkshop(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.AddPlayerWorkshop(2, 30));
            Assert.AreEqual(0, market.PlayerWorkshopsOf(1));
        }

        [Test]
        public void WithoutLabour_HiringIsImmediate_AndThePopulationStays()
        {
            CityMarket market = MarketWithoutLabour(0, new CityPopulation(1000, 40, 1000));

            market.StartDay();

            Assert.AreEqual(26, market.WorkersOf(0));
            Assert.AreEqual(0, market.ReserveOf(0));
            Assert.AreEqual(40, market.Population.Beggars);
            Assert.AreEqual(0, market.BeggarTarget);
        }

        [Test]
        public void WithoutLabour_ThePlayersWorkshopsHireNobody()
        {
            CityMarket market = MarketWithoutLabour(100, new CityPopulation(1000, 50, 1100));
            market.AddPlayerWorkshop(0, 30);
            market.AddPlayerHouse(100);

            market.StartDay();

            Assert.AreEqual(1, market.PlayerWorkshopsOf(0));
            Assert.AreEqual(0, market.PlayerWorkersOf(0));
            Assert.AreEqual(1200, market.Population.Room);
            Assert.AreEqual(50, market.Population.Beggars);
        }

        [Test]
        public void BeggarTarget_FollowsTheInhabitants()
        {
            // √(1 040 × 50 ÷ 2) = 161, ÷ 3 = 53, + 8.
            CityMarket market = Market(30, new CityPopulation(1000, 40, 1024));

            Assert.AreEqual(61, market.BeggarTarget);
        }

        [Test]
        public void StartingWorkforceOf_SumsTheStartingWorkers()
        {
            Assert.AreEqual(26, CityMarket.StartingWorkforceOf(
                new[] { Good, Good, new MarketGood(10, 2, 0, 0) }, new WorkforceRule(16, 8, 4, 1, 1, 25)));
        }
    }
}
