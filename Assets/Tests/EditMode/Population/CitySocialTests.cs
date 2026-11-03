using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CitySocialTests
    {
        private static readonly SocialClass[] Classes = { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };

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
            SocialRule social = null,
            IReadOnlyList<CityHousingRule> housing = null,
            bool withSocial = true,
            bool onGrid = false)
        {
            return new City(goods ?? new[] { Good }, TestMarkets.Thresholds, workforce ?? Workforce, startingDays,
                TestMarkets.Pricing, 7, population, TestMarkets.Labour, housing, withSocial ? social ?? TestMarkets.Social : null,
                onGrid ? TestMarkets.GridWithRoad() : null, onGrid ? TestMarkets.Placement : null);
        }

        private static void AssertSatisfactions(City city, int rich, int wealthy, int poor)
        {
            Assert.AreEqual(rich, city.Population.SatisfactionOf(SocialClass.Rich), "rich");
            Assert.AreEqual(wealthy, city.Population.SatisfactionOf(SocialClass.Wealthy), "wealthy");
            Assert.AreEqual(poor, city.Population.SatisfactionOf(SocialClass.Poor), "poor");
        }

        [Test]
        public void ASocialRule_SetsTheStartingSatisfactions()
        {
            City city = NewCity(Population(100, 300, 600, 0, 120, 350, 700), 7);

            AssertSatisfactions(city, 50, 50, 50);
        }

        [Test]
        public void ASocialRule_NeedsLabour()
        {
            Assert.Throws<ArgumentException>(() => new City(new[] { Good }, TestMarkets.Thresholds, Workforce, 7,
                TestMarkets.Pricing, 7, new CityPopulation(1000), null, null, TestMarkets.Social));
        }

        [Test]
        public void StartDay_LowersTheSatisfactionOfACityWithoutTheGood()
        {
            // Targets -13 - 5 + 9, -18 - 4 + 9, -27 - 8 + 9.
            var notProduced = new MarketGood(10, PerClass<double>.All(2), 0, 0, 0, new PerClass<int>(5, 4, 8));
            City city = NewCity(Population(100, 300, 600, 0, 120, 350, 700), 0, new[] { notProduced });

            city.StartDay();

            AssertSatisfactions(city, 49, 49, 49);
        }

        [Test]
        public void AGoodNobodyConsumes_DoesNotWeighInTheSatisfaction_EvenAsAnInput()
        {
            // Pitch: weighs much, consumed by no class. Ships: consumed by nobody either, made out of pitch.
            // 100 days of the workshop's need in stock: far above twice T0, so pitch counted by mistake
            // would raise the satisfactions while the others fall.
            var pitch = new MarketGood(10, PerClass<double>.All(0), 0, 0, 0, PerClass<int>.All(40));
            var ships = new MarketGood(10, PerClass<double>.All(0), 60, 0.1, 0, PerClass<int>.All(0));
            City withInputs = NewCity(
                Population(100, 300, 600, 0, 120, 350, 700), 100, new[] { pitch, ships.WithInputs(new[] { new MarketInput(0, 0.5) }) });
            City without = NewCity(Population(100, 300, 600, 0, 120, 350, 700), 100, new[] { pitch, ships });

            withInputs.StartDay();
            without.StartDay();

            Assert.Greater(withInputs.DailyInputNeedOf(0), 0d);

            foreach (SocialClass housedClass in Classes)
            {
                Assert.AreEqual(without.Population.SatisfactionOf(housedClass), withInputs.Population.SatisfactionOf(housedClass), housedClass.ToString());
            }
        }

        [Test]
        public void StartDay_RaisesTheSatisfactionOfAWellSuppliedCity()
        {
            // Above twice T0: targets 36, 31, 22.
            var notProduced = new MarketGood(10, PerClass<double>.All(2), 0, 0, 0, PerClass<int>.All(40));
            City city = NewCity(Population(100, 300, 600, 0, 120, 350, 700), 100, new[] { notProduced });

            city.StartDay();

            AssertSatisfactions(city, 52, 52, 49);
        }

        [Test]
        public void StartDay_IgnoresAGoodTheCityDoesNotConsume()
        {
            var notProduced = new MarketGood(10, PerClass<double>.All(2), 0, 0, 0, new PerClass<int>(5, 4, 8));
            var unwanted = new MarketGood(10, PerClass<double>.All(0), 0, 0, 0, PerClass<int>.All(9));
            City city = NewCity(Population(100, 300, 600, 0, 120, 350, 700), 0, new[] { notProduced, unwanted });

            city.StartDay();

            AssertSatisfactions(city, 49, 49, 49);
        }

        [Test]
        public void StartDay_DoesNotHireBeggarsWhileThePoorAreUnhappy()
        {
            City city = NewCity(Population(0, 0, 1000, 100, 0, 0, 1100), 0);
            city.Population.SetSatisfaction(SocialClass.Poor, 0);

            city.StartDay();

            Assert.AreEqual(10, city.WorkersOf(0));
        }

        [Test]
        public void StartDay_HiresBeggarsWhileThePoorAreHappy()
        {
            City city = NewCity(Population(0, 0, 1000, 100, 0, 0, 1100), 0);

            city.StartDay();

            Assert.AreEqual(26, city.WorkersOf(0));
        }

        [Test]
        public void StartDay_MovesPoorUpAfterTheSatisfaction()
        {
            City city = NewCity(Population(60, 180, 600, 0, 200, 400, 700), 7);

            city.StartDay();

            Assert.AreEqual(66, city.Population.CountOf(SocialClass.Rich));
            Assert.AreEqual(184, city.Population.CountOf(SocialClass.Wealthy));
        }

        [Test]
        public void WithoutASocialRule_TheClassesNeverMove()
        {
            City city = NewCity(Population(60, 180, 600, 0, 200, 400, 700), 7, withSocial: false);

            city.StartDay();

            Assert.AreEqual(60, city.Population.CountOf(SocialClass.Rich));
            Assert.AreEqual(180, city.Population.CountOf(SocialClass.Wealthy));
            AssertSatisfactions(city, 0, 0, 0);
        }

        [Test]
        public void StartDay_ThePoorWithoutRoomLeaveWithTheirJobs()
        {
            // Above T3, the workshop keeps 1 worker and 9 go to its reserve. One rich and one
            // wealthy rise, then (998 - 980) / 4 + 1 = 5 poor want to leave: one worker's family.
            City city = NewCity(Population(0, 0, 1000, 0, 0, 0, 980), 100);

            city.StartDay();

            Assert.AreEqual(8, city.ReserveOf(0));
            Assert.AreEqual(994, city.Population.CountOf(SocialClass.Poor));
            Assert.AreEqual(996, city.Population.Housed);
        }

        [Test]
        public void LeavingPoor_TakeTheWorkersAbove30_WhenThereIsNoReserve()
        {
            City city = NewCity(Population(0, 0, 1000, 0, 0, 0, 980), 0, workforce: new WorkforceRule(16, 8, 4, 1, 1, 50));

            city.StartDay();

            Assert.AreEqual(49, city.WorkersOf(0));
            Assert.AreEqual(994, city.Population.CountOf(SocialClass.Poor));
        }

        [Test]
        public void LeavingPoor_TakeThePlayersWorkersOnceTheCitysAreUnder30()
        {
            // The poor are unhappy: the city's workshop (stock between T0 and T1) hires no beggar
            // and stays at 10, the player's fills its 10 jobs. Their satisfaction falls below 0:
            // a hundredth of the 1 038 poor want to leave, two workers' families, both from the player.
            City city = NewCity(Population(0, 0, 1000, 60, 0, 0, 1100), 15, onGrid: true);
            TestMarkets.AddPlayerWorkshop(city, 0, 10);
            city.Population.SetSatisfaction(SocialClass.Poor, 0);

            city.StartDay();

            Assert.AreEqual(10, city.WorkersOf(0));
            Assert.AreEqual(0, city.ReserveOf(0));
            Assert.AreEqual(8, city.PlayerWorkersOf(0));
            Assert.AreEqual(1030, city.Population.CountOf(SocialClass.Poor));
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

            Assert.AreEqual(120, city.Population.CountOf(SocialClass.Rich));
            Assert.AreEqual(160, city.Population.RoomOf(SocialClass.Rich));
            Assert.AreEqual(350, city.Population.RoomOf(SocialClass.Wealthy));
            Assert.AreEqual(700, city.Population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(1, city.CityHouses);
            Assert.AreEqual(20, city.Market.AvailableOf(Timber));
        }

        [Test]
        public void OverManyDays_TheHousedStayFourPerWorker_WhateverTheClassesDo()
        {
            // A workshop under 30 (it leaks), one of 60, a player's workshop, and a social rule
            // whose poor end unhappy: hiring, layoffs, leaks, rises, falls and departures all run.
            MarketGood[] goods = { new MarketGood(10, 2, 20, 0.1), new MarketGood(10, 2, 60, 0.1), new MarketGood(10, 2, 0, 0) };
            var workforce = new WorkforceRule(16, 8, 4, 1, 1, 60);
            CityPopulation population = CityPopulation.AtStart(
                City.StartingWorkforceOf(goods, workforce), 4, 4d / 17d, new PerClass<int>(40, 70, 140));
            var city = new City(goods, TestMarkets.Thresholds, workforce, 7, TestMarkets.Pricing, 7,
                population, TestMarkets.Labour, null, TestMarkets.Social, grid: TestMarkets.GridWithRoad(), placement: TestMarkets.Placement);
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

            Assert.Less(city.Population.SatisfactionOf(SocialClass.Poor), 0);
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
