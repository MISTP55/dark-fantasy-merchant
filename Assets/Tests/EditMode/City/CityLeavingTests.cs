using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityLeavingTests
    {
        // Two barrels a day for 1 000 inhabitants; a workshop of up to 60 workers making a tenth each.
        private static MarketGood Good => new MarketGood(10, 2, 60, 0.1);

        // The workshop of a one-good market starts with 10 workers.
        private static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 10);

        private static CityPopulation Population(int rich, int wealthy, int poor, int beggars, int richRoom, int wealthyRoom, int poorRoom)
        {
            return new CityPopulation(new PerClass<int>(rich, wealthy, poor), beggars, new PerClass<int>(richRoom, wealthyRoom, poorRoom));
        }

        private static City NewCity(
            CityPopulation population,
            double startingDays,
            MarketGood[] goods = null,
            WorkforceRule workforce = null,
            IReadOnlyList<CityHousingRule> housing = null,
            bool withLeaving = true,
            bool onGrid = false)
        {
            return new City(goods ?? new[] { Good }, TestMarkets.Thresholds, workforce ?? Workforce, startingDays,
                TestMarkets.Pricing, 7, population, TestMarkets.Labour, housing, withLeaving ? TestMarkets.Leaving : null,
                onGrid ? TestMarkets.GridWithRoad() : null, onGrid ? TestMarkets.Placement : null);
        }

        [Test]
        public void ALeavingRule_NeedsLabour()
        {
            Assert.Throws<ArgumentException>(() => new City(new[] { Good }, TestMarkets.Thresholds, Workforce, 7,
                TestMarkets.Pricing, 7, new CityPopulation(1000), null, null, TestMarkets.Leaving));
        }

        [Test]
        public void StartDay_HiresBeggars_WithoutAnyCondition()
        {
            City city = NewCity(Population(0, 0, 1000, 100, 0, 0, 1100), 0);

            city.StartDay();

            Assert.AreEqual(26, city.WorkersOf(0));
        }

        [Test]
        public void EmptyStocks_NeitherStopTheHiringNorMakeTheHousedLeave()
        {
            // A consumed good nobody makes, always empty, and a workshop whose stock is emptied every day:
            // it always wants workers, and the room is ample.
            var notProduced = new MarketGood(10, 2, 0, 0);
            City city = NewCity(Population(0, 0, 1000, 100, 0, 0, 3000), 0, new[] { Good, notProduced });
            int starting = city.Population.Housed;

            for (int day = 0; day < 120; day++)
            {
                city.StartDay();
                Assert.GreaterOrEqual(city.Population.Housed, starting, $"on day {day}");

                if (city.Market.AvailableOf(0) > 0)
                {
                    city.Market.Take(0, city.Market.AvailableOf(0));
                }
            }

            Assert.AreEqual(60, city.WorkersOf(0));
        }

        [Test]
        public void TheClassesNeverMove()
        {
            City city = NewCity(Population(60, 180, 600, 0, 200, 400, 700), 7);

            for (int day = 0; day < 60; day++)
            {
                city.StartDay();
            }

            Assert.AreEqual(60, city.Population.CountOf(SocialClass.Rich));
            Assert.AreEqual(180, city.Population.CountOf(SocialClass.Wealthy));
        }

        [Test]
        public void StartDay_TheHousedWithoutRoomLeaveWithTheirJobs()
        {
            // Above T3, the workshop keeps 1 worker and 9 go to its reserve. (1000 - 980) / 4 + 1 = 6 want
            // to leave: one worker's family, from the reserve.
            City city = NewCity(Population(0, 0, 1000, 0, 0, 0, 980), 100);

            city.StartDay();

            Assert.AreEqual(8, city.ReserveOf(0));
            Assert.AreEqual(996, city.Population.CountOf(SocialClass.Poor));
            Assert.AreEqual(996, city.Population.Housed);
        }

        [Test]
        public void WithoutALeavingRule_NobodyLeaves()
        {
            City city = NewCity(Population(0, 0, 1000, 0, 0, 0, 980), 7, withLeaving: false);

            city.StartDay();

            Assert.AreEqual(1000, city.Population.Housed);
        }

        [Test]
        public void LeavingHoused_TakeTheWorkersAbove30_WhenThereIsNoReserve()
        {
            City city = NewCity(Population(0, 0, 1000, 0, 0, 0, 980), 0, workforce: new WorkforceRule(16, 8, 4, 1, 1, 50));

            city.StartDay();

            Assert.AreEqual(49, city.WorkersOf(0));
            Assert.AreEqual(996, city.Population.CountOf(SocialClass.Poor));
        }

        [Test]
        public void LeavingHoused_TakeThePlayersWorkersOnceTheCitysAreUnder30()
        {
            // On the first day the city's workshop hires (under 30, it stays protected) and the player's fills its
            // 10 jobs. Then families crowd in until 40 are beyond the room: 40 / 4 + 1 = 11 want to leave, two
            // workers' families, both from the player.
            City city = NewCity(Population(0, 0, 1000, 200, 0, 0, 1100), 25, onGrid: true);
            TestMarkets.AddPlayerWorkshop(city, 0, 10);
            city.StartDay();
            int cityWorkers = city.WorkersOf(0);
            Assert.LessOrEqual(cityWorkers, 30);
            Assert.AreEqual(10, city.PlayerWorkersOf(0));
            city.Population.Hire((1100 - city.Population.Housed) / 4 + 10, 4);
            Assert.AreEqual(1140, city.Population.Housed);

            city.StartDay();

            Assert.AreEqual(cityWorkers, city.WorkersOf(0));
            Assert.AreEqual(0, city.ReserveOf(0));
            Assert.AreEqual(8, city.PlayerWorkersOf(0));
            Assert.AreEqual(1132, city.Population.CountOf(SocialClass.Poor));
        }

        [Test]
        public void StartDay_BuildsAHouseForEachCrowdedClass()
        {
            const int Timber = 1;
            var material = new MarketGood(10, 0, 0, 0);
            var materials = new[] { new MaterialCost(Timber, 10) };
            var housing = new[]
            {
                new CityHousingRule(SocialClass.Rich, 0.97, 40, materials),
                new CityHousingRule(SocialClass.Wealthy, 0.96, 70, materials),
                new CityHousingRule(SocialClass.Poor, 0.95, 140, materials),
            };
            City city = NewCity(Population(117, 100, 600, 0, 120, 350, 700), 7, new[] { Good, material }, housing: housing);
            city.Market.Put(Timber, 30);

            city.StartDay();

            Assert.AreEqual(117, city.Population.CountOf(SocialClass.Rich));
            Assert.AreEqual(160, city.Population.RoomOf(SocialClass.Rich));
            Assert.AreEqual(350, city.Population.RoomOf(SocialClass.Wealthy));
            Assert.AreEqual(700, city.Population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(1, city.CityHouses);
            Assert.AreEqual(20, city.Market.AvailableOf(Timber));
        }

        [Test]
        public void OverManyDays_TheHousedStayFourPerWorker()
        {
            // A workshop under 30 (it leaks), one of 60, a player's workshop: hiring, layoffs, leaks and
            // departures all run.
            MarketGood[] goods = { new MarketGood(10, 2, 20, 0.1), new MarketGood(10, 2, 60, 0.1), new MarketGood(10, 2, 0, 0) };
            var workforce = new WorkforceRule(16, 8, 4, 1, 1, 60);
            CityPopulation population = CityPopulation.AtStart(
                City.StartingWorkforceOf(goods, workforce), 4, 4d / 17d, new PerClass<int>(40, 70, 140));
            var city = new City(goods, TestMarkets.Thresholds, workforce, 7, TestMarkets.Pricing, 7,
                population, TestMarkets.Labour, null, TestMarkets.Leaving, grid: TestMarkets.GridWithRoad(), placement: TestMarkets.Placement);
            TestMarkets.AddPlayerHouse(city, SocialClass.Poor, 140);
            TestMarkets.AddPlayerWorkshop(city, 0, 30);
            AssertFourPerWorker(city, "at the start");

            for (int day = 0; day < 200; day++)
            {
                // Three days flooded (the workshops lay off), then three days emptied (they hire again).
                for (int good = 0; good < 2; good++)
                {
                    if (day % 6 == 0)
                    {
                        city.Market.Put(good, 10000);
                    }
                    else if (day % 6 == 3 && city.Market.AvailableOf(good) > 0)
                    {
                        city.Market.Take(good, city.Market.AvailableOf(good));
                    }
                }

                city.StartDay();
                AssertFourPerWorker(city, $"on day {day}");
            }
        }

        private static void AssertFourPerWorker(City city, string when)
        {
            int employed = 0;

            for (int good = 0; good < city.Market.GoodCount; good++)
            {
                employed += city.WorkersOf(good) + city.ReserveOf(good) + city.PlayerWorkersOf(good);
            }

            Assert.AreEqual(4 * employed, city.Population.Housed, when);
        }

        [Test]
        public void TheHousingRules_HaveAClassOnceAtMost()
        {
            var twice = new[]
            {
                new CityHousingRule(SocialClass.Poor, 0.95, 140, Array.Empty<MaterialCost>()),
                new CityHousingRule(SocialClass.Poor, 0.95, 140, Array.Empty<MaterialCost>()),
            };

            Assert.Throws<ArgumentException>(() => NewCity(new CityPopulation(1000), 7, housing: twice));
        }

        [Test]
        public void APlayersHouse_AddsRoomToItsClass()
        {
            City city = NewCity(new CityPopulation(1000), 7, onGrid: true);

            TestMarkets.AddPlayerHouse(city, SocialClass.Wealthy, 70);

            Assert.AreEqual(70, city.Population.RoomOf(SocialClass.Wealthy));
            Assert.AreEqual(1, city.PlayerHousesOf(SocialClass.Wealthy));
            Assert.AreEqual(0, city.PlayerHousesOf(SocialClass.Poor));
        }
    }
}
