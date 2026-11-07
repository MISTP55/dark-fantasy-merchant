using System;
using System.Linq;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityLabourTests
    {
        // Two barrels a day for 1 000 inhabitants; a workshop of up to 60 workers making a tenth each.
        // Near 1 000 inhabitants: T0 20, T1 60, T2 120, T3 140.
        private static MarketGood Good => new MarketGood(10, 2, 60, 0.1);

        // The workshop of a one-good market starts with 10 workers.
        private static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 10);

        // Each workshop of a two-goods market starts with 10 workers.
        private static WorkforceRule TwoWorkshopsWorkforce => new WorkforceRule(16, 8, 4, 1, 1, 20);

        private static City NewCity(
            double startingDays,
            CityPopulation population,
            LabourRule labour = null,
            int goods = 1,
            WorkforceRule workforce = null)
        {
            return new City(Enumerable.Repeat(Good, goods).ToArray(), TestMarkets.Thresholds, workforce ?? Workforce, startingDays,
                TestMarkets.Pricing, 7, population, new CityLabourRules(labour ?? TestMarkets.Labour),
                grid: TestMarkets.GridSetupWithRoad());
        }

        private static City TwoGoodsCity(double startingDays, CityPopulation population)
        {
            return NewCity(startingDays, population, goods: 2, workforce: TwoWorkshopsWorkforce);
        }

        private static City CityWithoutLabour(double startingDays, CityPopulation population)
        {
            return new City(new[] { Good }, TestMarkets.Thresholds, Workforce, startingDays,
                TestMarkets.Pricing, 7, population, grid: TestMarkets.GridSetupWithRoad());
        }

        [Test]
        public void AWorkshop_HiresBeggars_FourPerWorker_WithinTheFreeRoom()
        {
            // An empty stock wants 26 workers; 24 places take 6 of the 40 beggars.
            City cityState = NewCity(0, new CityPopulation(1000, 40, 1024));

            cityState.StartDay();

            Assert.AreEqual(16, cityState.WorkersOf(0));
            Assert.AreEqual(1024, cityState.Population.Housed);

            // 16 left, then 7 come: the target is 61 at 1 040 inhabitants.
            Assert.AreEqual(23, cityState.Population.Beggars);
        }

        [Test]
        public void EveryGood_IsConsumedBeforeAnyWorkshopHires()
        {
            // The first good's workshop hires 16 workers from the beggars: 64 more housed. The
            // second good, after it in the catalogue, is still consumed by the 1 000 of the morning.
            var goods = new[] { Good, new MarketGood(10, 2, 0, 0) };
            var cityState = new City(goods, TestMarkets.Thresholds, Workforce, 0, TestMarkets.Pricing, 7,
                new CityPopulation(1000, 100, 1100), TestMarkets.LabourRules);
            cityState.Market.TryAdd(1, 100);

            cityState.StartDay();

            Assert.AreEqual(26, cityState.WorkersOf(0));
            Assert.AreEqual(2d, cityState.RecentConsumptionOf(1), 1e-9);
        }

        [Test]
        public void EveryWorkshop_DecidesOnTheThresholdsOfTheMorning()
        {
            // T0 is 10 days of 2 barrels for 1 000: 20. The first workshop hires 16 from the
            // beggars (64 more housed), which would raise the second good's T0 to 21.28; its stock
            // of 21 stays above the T0 of the morning, so it hires 8 (up to T1), not 16.
            City cityState = TwoGoodsCity(0, new CityPopulation(1000, 100, 1100));
            cityState.Market.TryAdd(1, 23);

            cityState.StartDay();

            Assert.AreEqual(26, cityState.WorkersOf(0));
            Assert.AreEqual(18, cityState.WorkersOf(1));
        }

        [Test]
        public void TheThresholds_FollowThePopulation_OnlyAtTheEndOfTheDay()
        {
            var population = new CityPopulation(1000, 100, 1100);
            City cityState = CityWithoutLabour(0, population);
            Assert.AreEqual(20, cityState.Market.ThresholdsOf(0).T0, 1e-9);

            population.Hire(10, 4);
            Assert.AreEqual(20, cityState.Market.ThresholdsOf(0).T0, 1e-9);

            cityState.StartDay();
            Assert.AreEqual(20.8, cityState.Market.ThresholdsOf(0).T0, 1e-9);
        }

        [Test]
        public void WithoutFreeRoom_NoBeggarIsHired()
        {
            City cityState = NewCity(0, new CityPopulation(1000, 100, 1000));

            cityState.StartDay();

            Assert.AreEqual(10, cityState.WorkersOf(0));
            Assert.AreEqual(1000, cityState.Population.Housed);
            Assert.AreEqual(96, cityState.Population.Beggars);
        }

        [Test]
        public void ALayoff_GoesToTheReserve_AndStaysHoused()
        {
            // 200 barrels, above T3: the workshop keeps one worker.
            City cityState = NewCity(100, new CityPopulation(1000));

            cityState.StartDay();

            Assert.AreEqual(1, cityState.WorkersOf(0));
            Assert.AreEqual(9, cityState.ReserveOf(0));
            Assert.AreEqual(1000, cityState.Population.Housed);
        }

        [Test]
        public void AWorkshop_HiresFromItsReserveFirst()
        {
            City cityState = NewCity(100, new CityPopulation(1000));
            cityState.StartDay();
            cityState.Market.TryRemove(0, cityState.Market.UnitsOf(0));

            cityState.StartDay();

            Assert.AreEqual(10, cityState.WorkersOf(0));
            Assert.AreEqual(0, cityState.ReserveOf(0));
            Assert.AreEqual(1000, cityState.Population.Housed);
        }

        [Test]
        public void AWorkshop_TakesOtherReserves_AfterTheBeggars()
        {
            City cityState = TwoGoodsCity(100, new CityPopulation(1000, 8, 1008));
            cityState.Market.TryRemove(1, cityState.Market.UnitsOf(1));

            cityState.StartDay();

            Assert.AreEqual(1, cityState.WorkersOf(0));
            Assert.AreEqual(0, cityState.ReserveOf(0));

            // 2 from the beggars, then the 9 of good 0's reserve.
            Assert.AreEqual(21, cityState.WorkersOf(1));
            Assert.AreEqual(1008, cityState.Population.Housed);
        }

        [Test]
        public void AWorkshopThatWantsTwo_TakesThemFromItsOwnReserve_BeforeTheBeggars()
        {
            // A workshop of 3 workers at most, which starts with 3. Near 1 000 inhabitants:
            // T0 20, T1 60, T2 = 60 + 10 days × 0.3 = 63, T3 = 63 + 20 = 83.
            var good = new MarketGood(10, 2, 3, 0.1);
            var cityState = new City(new[] { good }, TestMarkets.Thresholds, Workforce, 100,
                TestMarkets.Pricing, 7, new CityPopulation(1000, 100, 2000), TestMarkets.LabourRules);

            // 200 barrels, above T3: 1 worker kept, 2 in the reserve (a leak of 2 × 0.02: none).
            cityState.StartDay();
            Assert.AreEqual(1, cityState.WorkersOf(0));
            Assert.AreEqual(2, cityState.ReserveOf(0));
            Assert.AreEqual(1000, cityState.Population.Housed);

            // An empty stock: 1 + 16, cut to the maximum of 3, so 2 wanted, with 100 beggars and
            // 1 000 free places that could give them.
            cityState.Market.TryRemove(0, cityState.Market.UnitsOf(0));
            cityState.StartDay();

            Assert.AreEqual(3, cityState.WorkersOf(0));
            Assert.AreEqual(0, cityState.ReserveOf(0));
            Assert.AreEqual(1000, cityState.Population.Housed);
        }

        [Test]
        public void AWorkshopThatWantsTwo_WithoutAReserve_HiresBeggars_BeforeAnotherReserve()
        {
            // Good 0: a workshop of up to 60 that starts with 10 and lays 9 off on the first day
            // (200 barrels, above its T3 of 140; a leak of 9 × 0.02: none). Good 1: a workshop of
            // 12 at most that starts with 10; an empty stock wants 10 + 16, cut to 12: 2 more.
            var cityState = new City(new[] { Good, new MarketGood(10, 2, 12, 0.1) }, TestMarkets.Thresholds,
                TwoWorkshopsWorkforce, 100, TestMarkets.Pricing, 7, new CityPopulation(1000, 100, 2000),
                TestMarkets.LabourRules);
            cityState.Market.TryRemove(1, cityState.Market.UnitsOf(1));

            cityState.StartDay();

            Assert.AreEqual(1, cityState.WorkersOf(0));
            Assert.AreEqual(9, cityState.ReserveOf(0));
            Assert.AreEqual(12, cityState.WorkersOf(1));

            // 2 from the beggars: 8 more housed.
            Assert.AreEqual(1008, cityState.Population.Housed);
        }

        [Test]
        public void OverManyDays_TheHousedAreFourPerWorkerOrReserve_WithinTheRoom()
        {
            // A workshop under 30 (it leaks), one of 60, and a good the city does not produce.
            // 60 workers spread over two workshops: 20 (cut) and 30. 200 housed, 11 beggars, 200 places.
            MarketGood[] goods = { new MarketGood(10, 2, 20, 0.1), new MarketGood(10, 2, 60, 0.1), new MarketGood(10, 2, 0, 0) };
            var workforce = new WorkforceRule(16, 8, 4, 1, 1, 60);
            int startingWorkers = City.StartingWorkforceOf(goods, workforce);
            CityPopulation population = CityPopulation.AtStart(startingWorkers, 4, 4d / 17d, 100);
            var cityState = new City(goods, TestMarkets.Thresholds, workforce, 7, TestMarkets.Pricing, 7,
                population, TestMarkets.LabourRules, grid: TestMarkets.GridSetupWithRoad());
            TestMarkets.AddPlayerHouse(cityState, 100);
            TestMarkets.AddPlayerWorkshop(cityState, 0, 30);

            int leaks = 0;
            int previousCityWorkers = CityWorkersAndReserves(cityState);
            Assert.AreEqual(50, startingWorkers);
            AssertHousing(cityState, "at the start");

            for (int day = 0; day < 200; day++)
            {
                // Three days flooded (the workshops lay off), then three days emptied (they hire again).
                for (int good = 0; good < 2; good++)
                {
                    if (day % 6 == 0)
                    {
                        cityState.Market.TryAdd(good, 10000);
                    }
                    else if (day % 6 == 3 && cityState.Market.UnitsOf(good) > 0)
                    {
                        cityState.Market.TryRemove(good, cityState.Market.UnitsOf(good));
                    }
                }

                cityState.StartDay();
                AssertHousing(cityState, $"on day {day}");

                // Hiring from the reserves moves nobody out; only a leak lowers this sum.
                int cityWorkers = CityWorkersAndReserves(cityState);

                if (cityWorkers < previousCityWorkers)
                {
                    leaks++;
                }

                previousCityWorkers = cityWorkers;
            }

            Assert.Greater(leaks, 0);
            Assert.Greater(cityState.PlayerWorkersOf(0), 0);
            Assert.AreNotEqual(population.StartingInhabitants, cityState.Population.Inhabitants);
            // The beggars consume nothing.
            Assert.AreEqual(2d * cityState.Population.Housed / 1000d, cityState.DailyConsumptionOf(0), 1e-12);
        }

        private static int CityWorkersAndReserves(City cityState)
        {
            int total = 0;

            for (int good = 0; good < cityState.Market.GoodCount; good++)
            {
                total += cityState.WorkersOf(good) + cityState.ReserveOf(good);
            }

            return total;
        }

        private static void AssertHousing(City cityState, string when)
        {
            int employed = CityWorkersAndReserves(cityState);

            for (int good = 0; good < cityState.Market.GoodCount; good++)
            {
                employed += cityState.PlayerWorkersOf(good);
            }

            Assert.AreEqual(4 * employed, cityState.Population.Housed, when);
            Assert.LessOrEqual(cityState.Population.Housed, cityState.Population.Room, when);
        }

        [Test]
        public void ALeak_SendsFourInhabitantsPerWorkerToTheBeggars()
        {
            City cityState = NewCity(100, new CityPopulation(1000), new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 0.5, 30));

            cityState.StartDay();

            // 9 × 0.5 = 4.5: 4 leave.
            Assert.AreEqual(5, cityState.ReserveOf(0));
            Assert.AreEqual(984, cityState.Population.Housed);

            // 16, then 7 come.
            Assert.AreEqual(23, cityState.Population.Beggars);
        }

        [Test]
        public void TheCity_HiresBeforeThePlayer()
        {
            City cityState = NewCity(0, new CityPopulation(1000, 40, 1100));
            TestMarkets.AddPlayerWorkshop(cityState, 0, 30);

            cityState.StartDay();

            Assert.AreEqual(20, cityState.WorkersOf(0));
            Assert.AreEqual(0, cityState.PlayerWorkersOf(0));
        }

        [Test]
        public void Vacancies_AreFilledFromTheBeggars()
        {
            City cityState = NewCity(100, new CityPopulation(1000, 50, 1100));
            TestMarkets.AddPlayerWorkshop(cityState, 0, 30);

            cityState.StartDay();

            Assert.AreEqual(12, cityState.PlayerWorkersOf(0));
            Assert.AreEqual(1048, cityState.Population.Housed);
            Assert.AreEqual(9, cityState.Population.Beggars);

            cityState.StartDay();

            Assert.AreEqual(14, cityState.PlayerWorkersOf(0));
        }

        [Test]
        public void Vacancies_FillThePlayersWorkshopsInTheOrderTheyWereBuilt()
        {
            City cityState = TwoGoodsCity(100, new CityPopulation(1000, 200, 1200));
            TestMarkets.AddPlayerWorkshop(cityState, 1, 30);
            TestMarkets.AddPlayerWorkshop(cityState, 0, 30);

            cityState.StartDay();

            Assert.AreEqual(30, cityState.PlayerWorkersOf(1));
            Assert.AreEqual(19, cityState.PlayerWorkersOf(0));
            Assert.AreEqual(30, cityState.PlayerJobsOf(0));
            Assert.AreEqual(1, cityState.PlayerWorkshopsOf(0));
        }

        [Test]
        public void ThePlayersWorkshops_AreWorthTheirWorkers_TimesTheMerchantOutput_AndTheBonus()
        {
            // A merchant's worker makes a twentieth. Three workshops of 10 jobs: 3 % more.
            var good = new MarketGood(10, 2, 60, 0.1, 0.05);
            var cityState = new City(new[] { good }, TestMarkets.Thresholds, Workforce, 100,
                TestMarkets.Pricing, 7, new CityPopulation(1000, 200, 1200), TestMarkets.LabourRules,
                grid: TestMarkets.GridSetupWithRoad());
            var rule = new MerchantOutputRule(3, 1.03, 6, 1.06, 9, 1.1);

            Assert.AreEqual(0, cityState.PlayerDailyOutputOf(0, rule));

            TestMarkets.AddPlayerWorkshop(cityState, 0, 10);
            TestMarkets.AddPlayerWorkshop(cityState, 0, 10);
            TestMarkets.AddPlayerWorkshop(cityState, 0, 10);
            cityState.StartDay();

            Assert.AreEqual(30, cityState.PlayerWorkersOf(0));
            Assert.AreEqual(30 * 0.05 * 1.03, cityState.PlayerDailyOutputOf(0, rule), 1e-9);
            Assert.Throws<ArgumentNullException>(() => cityState.PlayerDailyOutputOf(0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => cityState.PlayerDailyOutputOf(1, rule));
        }

        [Test]
        public void Vacancies_NeedFiveBeggars()
        {
            City cityState = NewCity(100, new CityPopulation(1000, 4, 1100));
            TestMarkets.AddPlayerWorkshop(cityState, 0, 30);

            cityState.StartDay();

            Assert.AreEqual(0, cityState.PlayerWorkersOf(0));
        }

        [Test]
        public void AddPlayerHouse_AddsItsRoom_AndRaisesChangedOnce()
        {
            City cityState = NewCity(30, new CityPopulation(1000));
            int changes = 0;
            cityState.Changed += () => changes++;

            TestMarkets.AddPlayerHouse(cityState, 100);

            Assert.AreEqual(1100, cityState.Population.Room);
            Assert.AreEqual(1, cityState.PlayerHouses);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void AddPlayerWorkshop_AddsItsJobs_AndRaisesChangedOnce()
        {
            City cityState = NewCity(30, new CityPopulation(1000));
            int changes = 0;
            cityState.Changed += () => changes++;

            TestMarkets.AddPlayerWorkshop(cityState, 0, 30);

            Assert.AreEqual(1, cityState.PlayerWorkshopsOf(0));
            Assert.AreEqual(30, cityState.PlayerJobsOf(0));
            Assert.AreEqual(0, cityState.PlayerWorkersOf(0));
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void AddPlayerWorkshop_RefusesAGoodNotProduced()
        {
            var cityState = new City(new[] { new MarketGood(10, 2, 0, 0), Good }, TestMarkets.Thresholds, Workforce, 30,
                TestMarkets.Pricing, 7, new CityPopulation(1000), TestMarkets.LabourRules,
                grid: TestMarkets.GridSetupWithRoad());

            Assert.Throws<ArgumentOutOfRangeException>(() => TestMarkets.AddPlayerWorkshop(cityState, 0, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestMarkets.AddPlayerWorkshop(cityState, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestMarkets.AddPlayerWorkshop(cityState, 2, 30));
            Assert.AreEqual(0, cityState.PlayerWorkshopsOf(1));
        }

        [Test]
        public void WithoutLabour_HiringIsImmediate_AndThePopulationStays()
        {
            City cityState = CityWithoutLabour(0, new CityPopulation(1000, 40, 1000));

            cityState.StartDay();

            Assert.AreEqual(26, cityState.WorkersOf(0));
            Assert.AreEqual(0, cityState.ReserveOf(0));
            Assert.AreEqual(40, cityState.Population.Beggars);
            Assert.AreEqual(0, cityState.BeggarTarget);
        }

        [Test]
        public void WithoutLabour_ThePlayersWorkshopsHireNobody()
        {
            City cityState = CityWithoutLabour(100, new CityPopulation(1000, 50, 1100));
            TestMarkets.AddPlayerWorkshop(cityState, 0, 30);
            TestMarkets.AddPlayerHouse(cityState, 100);

            cityState.StartDay();

            Assert.AreEqual(1, cityState.PlayerWorkshopsOf(0));
            Assert.AreEqual(0, cityState.PlayerWorkersOf(0));
            Assert.AreEqual(1200, cityState.Population.Room);
            Assert.AreEqual(50, cityState.Population.Beggars);
        }

        [Test]
        public void BeggarTarget_FollowsTheInhabitants()
        {
            // √(1 040 × 50 ÷ 2) = 161, ÷ 3 = 53, + 8.
            City cityState = NewCity(30, new CityPopulation(1000, 40, 1024));

            Assert.AreEqual(61, cityState.BeggarTarget);
        }

        [Test]
        public void StartingWorkforceOf_SumsTheStartingWorkers()
        {
            Assert.AreEqual(26, City.StartingWorkforceOf(
                new[] { Good, Good, new MarketGood(10, 2, 0, 0) }, new WorkforceRule(16, 8, 4, 1, 1, 25)));
        }
    }
}
