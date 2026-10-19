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
        public void AWorkshopThatWantsTwo_TakesThemFromItsOwnReserve_BeforeTheBeggars()
        {
            // A workshop of 3 workers at most, which starts with 3. Near 1 000 inhabitants:
            // T0 20, T1 60, T2 = 60 + 10 days × 0.3 = 63, T3 = 63 + 20 = 83.
            var good = new MarketGood(10, 2, 3, 0.1);
            var market = new CityMarket(new[] { good }, TestMarkets.Thresholds, Workforce, 100,
                TestMarkets.Pricing, 7, new CityPopulation(1000, 100, 2000), TestMarkets.Labour);

            // 200 barrels, above T3: 1 worker kept, 2 in the reserve (a leak of 2 × 0.02: none).
            market.StartDay();
            Assert.AreEqual(1, market.WorkersOf(0));
            Assert.AreEqual(2, market.ReserveOf(0));
            Assert.AreEqual(1000, market.Population.Housed);

            // An empty stock: 1 + 16, cut to the maximum of 3, so 2 wanted, with 100 beggars and
            // 1 000 free places that could give them.
            market.Take(0, market.AvailableOf(0));
            market.StartDay();

            Assert.AreEqual(3, market.WorkersOf(0));
            Assert.AreEqual(0, market.ReserveOf(0));
            Assert.AreEqual(1000, market.Population.Housed);
        }

        [Test]
        public void AWorkshopThatWantsTwo_WithoutAReserve_HiresBeggars_BeforeAnotherReserve()
        {
            // Good 0: a workshop of up to 60 that starts with 10 and lays 9 off on the first day
            // (200 barrels, above its T3 of 140; a leak of 9 × 0.02: none). Good 1: a workshop of
            // 12 at most that starts with 10; an empty stock wants 10 + 16, cut to 12: 2 more.
            var market = new CityMarket(new[] { Good, new MarketGood(10, 2, 12, 0.1) }, TestMarkets.Thresholds,
                TwoWorkshopsWorkforce, 100, TestMarkets.Pricing, 7, new CityPopulation(1000, 100, 2000), TestMarkets.Labour);
            market.Take(1, market.AvailableOf(1));

            market.StartDay();

            Assert.AreEqual(1, market.WorkersOf(0));
            Assert.AreEqual(9, market.ReserveOf(0));
            Assert.AreEqual(12, market.WorkersOf(1));

            // 2 from the beggars: 8 more housed.
            Assert.AreEqual(1008, market.Population.Housed);
        }

        [Test]
        public void OverManyDays_TheHousedAreFourPerWorkerOrReserve_WithinTheRoom()
        {
            // A workshop under 30 (it leaks), one of 60, and a good the city does not produce.
            // 60 workers spread over two workshops: 20 (cut) and 30. 200 housed (20 rich, 60 wealthy, 120 poor), 11 beggars, 200 places.
            MarketGood[] goods = { new MarketGood(10, 2, 20, 0.1), new MarketGood(10, 2, 60, 0.1), new MarketGood(10, 2, 0, 0) };
            var workforce = new WorkforceRule(16, 8, 4, 1, 1, 60);
            int startingWorkers = CityMarket.StartingWorkforceOf(goods, workforce);
            CityPopulation population = CityPopulation.AtStart(startingWorkers, 4, 4d / 17d, new PerClass<int>(20, 60, 120));
            var market = new CityMarket(goods, TestMarkets.Thresholds, workforce, 7, TestMarkets.Pricing, 7,
                population, TestMarkets.Labour);
            market.AddPlayerHouse(SocialClass.Poor, 100);
            market.AddPlayerWorkshop(0, 30);

            int leaks = 0;
            int previousCityWorkers = CityWorkersAndReserves(market);
            Assert.AreEqual(50, startingWorkers);
            AssertHousing(market, "at the start");

            for (int day = 0; day < 200; day++)
            {
                // Three days flooded (the workshops lay off), then three days emptied (they hire again).
                for (int good = 0; good < 2; good++)
                {
                    if (day % 6 == 0)
                    {
                        market.Put(good, 10000);
                    }
                    else if (day % 6 == 3 && market.AvailableOf(good) > 0)
                    {
                        market.Take(good, market.AvailableOf(good));
                    }
                }

                market.StartDay();
                AssertHousing(market, $"on day {day}");

                // Hiring from the reserves moves nobody out; only a leak lowers this sum.
                int cityWorkers = CityWorkersAndReserves(market);

                if (cityWorkers < previousCityWorkers)
                {
                    leaks++;
                }

                previousCityWorkers = cityWorkers;
            }

            Assert.Greater(leaks, 0);
            Assert.Greater(market.PlayerWorkersOf(0), 0);
            Assert.AreNotEqual(population.StartingInhabitants, market.Population.Inhabitants);
            // The beggars consume nothing.
            Assert.AreEqual(2d * market.Population.Housed / 1000d, market.DailyConsumptionOf(0), 1e-12);
        }

        private static int CityWorkersAndReserves(CityMarket market)
        {
            int total = 0;

            for (int good = 0; good < market.GoodCount; good++)
            {
                total += market.WorkersOf(good) + market.ReserveOf(good);
            }

            return total;
        }

        private static void AssertHousing(CityMarket market, string when)
        {
            int employed = CityWorkersAndReserves(market);

            for (int good = 0; good < market.GoodCount; good++)
            {
                employed += market.PlayerWorkersOf(good);
            }

            Assert.AreEqual(4 * employed, market.Population.Housed, when);
            Assert.LessOrEqual(market.Population.Housed, market.Population.Room, when);
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
        public void ThePlayersWorkshops_AreWorthTheirWorkers_TimesTheMerchantOutput_AndTheBonus()
        {
            // A merchant's worker makes a twentieth. Three workshops of 10 jobs: 3 % more.
            var good = new MarketGood(10, 2, 60, 0.1, 0.05);
            var market = new CityMarket(new[] { good }, TestMarkets.Thresholds, Workforce, 100,
                TestMarkets.Pricing, 7, new CityPopulation(1000, 200, 1200), TestMarkets.Labour);
            var rule = new MerchantOutputRule(3, 1.03, 6, 1.06, 9, 1.1);

            Assert.AreEqual(0, market.PlayerDailyOutputOf(0, rule));

            market.AddPlayerWorkshop(0, 10);
            market.AddPlayerWorkshop(0, 10);
            market.AddPlayerWorkshop(0, 10);
            market.StartDay();

            Assert.AreEqual(30, market.PlayerWorkersOf(0));
            Assert.AreEqual(30 * 0.05 * 1.03, market.PlayerDailyOutputOf(0, rule), 1e-9);
            Assert.Throws<ArgumentNullException>(() => market.PlayerDailyOutputOf(0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.PlayerDailyOutputOf(1, rule));
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

            market.AddPlayerHouse(SocialClass.Poor, 100);

            Assert.AreEqual(1100, market.Population.Room);
            Assert.AreEqual(1, market.PlayerHousesOf(SocialClass.Poor));
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
            market.AddPlayerHouse(SocialClass.Poor, 100);

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
