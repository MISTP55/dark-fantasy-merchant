using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class EconomyDefinitionTests
    {
        private const double Tolerance = 1e-9;
        private const int DaysPerWeek = 7;

        private readonly List<Object> created = new List<Object>();

        private GoodDefinition wheat;
        private GoodDefinition fish;
        private GoodDefinition salt;
        private EconomyDefinition economy;

        [SetUp]
        public void SetUp()
        {
            // Per week for 1 000 inhabitants: 2, 1.5 and 0.5 barrels a day. Workshops of 60 workers
            // making 0.4, 0.3 and 0.1 barrels each.
            wheat = Track(TestEconomy.CreateGood("Blé", 10, 14, dailyOutputPerWorker: 0.4));
            fish = Track(TestEconomy.CreateGood("Poisson", 12, 10.5, dailyOutputPerWorker: 0.3));
            salt = Track(TestEconomy.CreateGood("Sel", 18, 3.5));
            economy = Track(TestEconomy.CreateEconomy(wheat, fish, salt));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object instance in created)
            {
                Object.DestroyImmediate(instance);
            }

            created.Clear();
        }

        private T Track<T>(T instance) where T : Object
        {
            created.Add(instance);
            return instance;
        }

        private CityDefinition City(GoodDefinition[] efficient, GoodDefinition[] inefficient)
        {
            return Track(TestEconomy.CreateCity(efficient, inefficient));
        }

        [Test]
        public void ANewDefinition_HasTheGamesSettings()
        {
            EconomyDefinition fresh = Track(ScriptableObject.CreateInstance<EconomyDefinition>());

            Assert.AreEqual(7, fresh.StartingStockDays);
            Assert.AreEqual(0.75, fresh.InefficientEfficiency);
            Assert.AreEqual(16, fresh.HiredUpToT0);
            Assert.AreEqual(8, fresh.HiredUpToT1);
            Assert.AreEqual(4, fresh.HiredUpToT2);
            Assert.AreEqual(1, fresh.HiredUpToT3);
            Assert.AreEqual(1, fresh.WorkersAboveT3);
            Assert.AreEqual(250, fresh.StartingWorkforce);
            Assert.AreEqual(3, fresh.MerchantFirstStep);
            Assert.AreEqual(1.03, fresh.MerchantFirstBonus);
            Assert.AreEqual(6, fresh.MerchantSecondStep);
            Assert.AreEqual(1.06, fresh.MerchantSecondBonus);
            Assert.AreEqual(9, fresh.MerchantThirdStep);
            Assert.AreEqual(1.1, fresh.MerchantThirdBonus);
            Assert.AreEqual(10, fresh.BarrelsPerBushel);
            Assert.AreEqual(4, fresh.InhabitantsPerWorker);
            Assert.AreEqual(7, fresh.T0Days);
            Assert.AreEqual(14, fresh.T1AddedDays);
            Assert.AreEqual(10, fresh.T2ProductionDays);
            Assert.AreEqual(5, fresh.T0Floor);
            Assert.AreEqual(10, fresh.T1Floor);
            Assert.AreEqual(15, fresh.T2Floor);
            Assert.AreEqual(20, fresh.T3Floor);
            Assert.AreEqual(4, fresh.BuyFactors.Empty);
            Assert.AreEqual(1.5, fresh.BuyFactors.AtT0);
            Assert.AreEqual(1, fresh.BuyFactors.AtT1);
            Assert.AreEqual(0.8, fresh.BuyFactors.AtT2);
            Assert.AreEqual(0.6, fresh.BuyFactors.AtT3);
            Assert.AreEqual(2, fresh.SellFactors.Empty);
            Assert.AreEqual(1.4, fresh.SellFactors.AtT0);
            Assert.AreEqual(1, fresh.SellFactors.AtT1);
            Assert.AreEqual(0.7, fresh.SellFactors.AtT2);
            Assert.AreEqual(0.5, fresh.SellFactors.AtT3);
            Assert.AreEqual(0, fresh.Goods.Count);

            Assert.AreEqual(30, fresh.CreateLeavingRule().ProtectedWorkers);
            Assert.AreEqual(0.97, fresh.CreateHousingRule(SocialClass.Rich, 40, System.Array.Empty<MaterialCost>()).Occupancy, Tolerance);
            Assert.AreEqual(0.96, fresh.CreateHousingRule(SocialClass.Wealthy, 70, System.Array.Empty<MaterialCost>()).Occupancy, Tolerance);
            Assert.AreEqual(0.95, fresh.CreateHousingRule(SocialClass.Poor, 140, System.Array.Empty<MaterialCost>()).Occupancy, Tolerance);
        }

        [Test]
        public void CreateMarket_ConvertsTheWeeklyConsumption()
        {
            GoodDefinition wine = Track(TestEconomy.CreateGood("Vin", 220, 0));
            TestEconomy.SetConsumption(wine, 9.24);
            EconomyDefinition definition = Track(TestEconomy.CreateEconomy(wine));
            City cityState = definition.CreateCity(City(new GoodDefinition[0], new GoodDefinition[0]), definition.CreatePricing(), 7);

            // 6 000 housed, 9.24 a week per thousand.
            Assert.AreEqual(9.24 / 7 * 6, cityState.DailyConsumptionOf(0), Tolerance);
            Assert.AreEqual(9.24, wine.WeeklyConsumptionPerThousand, Tolerance);
        }

        [Test]
        public void TryValidateSettings_RefusesNegativeProtectedWorkers()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("protectedWorkers").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("departures", problem);
        }

        [Test]
        public void ANewGood_AndANewCity_HaveUsableDefaults()
        {
            GoodDefinition good = Track(ScriptableObject.CreateInstance<GoodDefinition>());
            CityDefinition city = Track(ScriptableObject.CreateInstance<CityDefinition>());

            Assert.GreaterOrEqual(good.BasePrice, 1);
            Assert.Greater(good.MaxWorkers, 0);
            Assert.AreEqual(GoodUnit.Barrel, good.Unit);
            Assert.Greater(good.DailyOutputPerWorker, 0d);
            Assert.Greater(good.MerchantDailyOutputPerWorker, 0d);
            Assert.AreEqual(GoodCategory.RawMaterial, good.Category);
            Assert.AreEqual(0, city.EfficientGoods.Count);
            Assert.AreEqual(0, city.InefficientGoods.Count);
        }

        [Test]
        public void IndexOf_IsThePlaceInTheCatalogue()
        {
            GoodDefinition stranger = Track(TestEconomy.CreateGood("Vin", 60, 4.2));

            Assert.AreEqual(0, economy.IndexOf(wheat));
            Assert.AreEqual(2, economy.IndexOf(salt));
            Assert.AreEqual(-1, economy.IndexOf(stranger));
            Assert.AreEqual(-1, economy.IndexOf(null));
        }

        [Test]
        public void ProductionOf_ReadsTheCitysLists()
        {
            CityDefinition city = City(new[] { wheat }, new[] { fish });

            Assert.AreEqual(GoodProduction.Efficient, city.ProductionOf(wheat));
            Assert.AreEqual(GoodProduction.Inefficient, city.ProductionOf(fish));
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(salt));
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(null));
        }

        [Test]
        public void AGoodInBothLists_IsEfficient()
        {
            CityDefinition city = City(new[] { wheat }, new[] { wheat });

            Assert.AreEqual(GoodProduction.Efficient, city.ProductionOf(wheat));
        }

        [Test]
        public void AnEmptyEntry_IsIgnored()
        {
            CityDefinition city = City(new GoodDefinition[] { null, wheat }, new GoodDefinition[] { null });

            Assert.AreEqual(GoodProduction.Efficient, city.ProductionOf(wheat));
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(null));
            Assert.DoesNotThrow(() => economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek));
        }

        [Test]
        public void CreateMarket_StocksEveryGoodOfTheCatalogue_FromThePopulation()
        {
            CityDefinition city = City(new[] { wheat }, new[] { fish });

            City cityState = economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(3, cityState.Market.GoodCount);

            // A week of what the city consumes, whatever it produces: wheat 12 barrels a day,
            // fish 9, salt 3.
            Assert.AreEqual(84, cityState.Market.StockOf(0), Tolerance);
            Assert.AreEqual(63, cityState.Market.StockOf(1), Tolerance);
            Assert.AreEqual(21, cityState.Market.StockOf(2), Tolerance);

            // Salt is not produced: T0 is 7 days, T1 14 more.
            Assert.AreEqual(21, cityState.Market.ThresholdsOf(2).T0, Tolerance);
            Assert.AreEqual(63, cityState.Market.ThresholdsOf(2).T1, Tolerance);
        }

        [Test]
        public void CreateMarket_SpreadsTheWeeklyConsumptionOverTheDaysOfTheWeek()
        {
            CityDefinition city = City(new GoodDefinition[0], new GoodDefinition[0]);

            // Wheat: 84 barrels a week, over a week of 10 days; T0 is 7 days of it.
            City cityState = economy.CreateCity(city, economy.CreatePricing(), 10);

            Assert.AreEqual(8.4 * 7, cityState.Market.ThresholdsOf(0).T0, Tolerance);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void CreateMarket_RejectsAWeekWithoutDays(int daysPerWeek)
        {
            CityDefinition city = City(new GoodDefinition[0], new GoodDefinition[0]);

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => economy.CreateCity(city, economy.CreatePricing(), daysPerWeek));
        }

        [Test]
        public void CreateMarket_ProducesAtTheDefinitionsOutputs()
        {
            CityDefinition city = City(new[] { wheat }, new[] { fish });
            City cityState = economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek);

            cityState.StartDay();

            // Wheat: 84 + 24 - 12. Fish: 63 + 13.5 - 9.
            Assert.AreEqual(96, cityState.Market.StockOf(0), Tolerance);
            Assert.AreEqual(67.5, cityState.Market.StockOf(1), Tolerance);
        }

        [Test]
        public void CreateMarket_GivesThePlayersWorkshopsTheMerchantOutput_TimesTheEfficiency()
        {
            GoodDefinition wine = Track(TestEconomy.CreateGood("Vin", 60, 4.2, merchantDailyOutputPerWorker: 0.067));
            GoodDefinition oil = Track(TestEconomy.CreateGood("Huile", 20, 4.2, merchantDailyOutputPerWorker: 0.2));
            EconomyDefinition both = Track(TestEconomy.CreateEconomy(wine, oil, salt));
            CityDefinition city = City(new[] { wine }, new[] { oil });

            Assert.AreEqual(0.067, MerchantOutputOf(both, city, 0), Tolerance);
            Assert.AreEqual(0.15, MerchantOutputOf(both, city, 1), Tolerance);
        }

        // What a worker of the player's workshop for the good makes in a day: one workshop, no bonus.
        private double MerchantOutputOf(EconomyDefinition definition, CityDefinition city, int good)
        {
            // On a grid without houses, the city starts without room: the player's house gives it to every poor and more.
            City cityState = definition.CreateCity(city, definition.CreatePricing(), DaysPerWeek, definition.CreateLabourRule(), PerClass<int>.All(100),
                grid: TestMarkets.GridWithRoad(), placement: TestMarkets.Placement);
            TestMarkets.AddPlayerHouse(cityState, SocialClass.Poor, 100000);
            TestMarkets.AddPlayerWorkshop(cityState, good, 30);

            // Full stocks: the city's workshops hire nobody and leave the beggars to the player.
            for (int i = 0; i < cityState.Market.GoodCount; i++)
            {
                cityState.Market.Put(i, 100000);
            }

            cityState.StartDay();
            Assert.Greater(cityState.PlayerWorkersOf(good), 0);

            return cityState.PlayerDailyOutputOf(good, definition.CreateMerchantOutputRule()) / cityState.PlayerWorkersOf(good);
        }

        [Test]
        public void CreateMarket_WithoutStartingWorkforce_StartsEmpty()
        {
            TestEconomy.SetStartingPopulation(economy, 0);
            CityDefinition city = City(new[] { wheat }, new GoodDefinition[0]);

            City cityState = economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(0, cityState.Population.Inhabitants);
            Assert.AreEqual(0, cityState.WorkersOf(0));
            Assert.AreEqual(0, cityState.Market.AvailableOf(0));
            Assert.AreEqual(0, cityState.Market.AvailableOf(2));
        }

        [Test]
        public void CreateMarket_StartsWithThePopulationOfTheWorkforce()
        {
            // 4 inhabitants for each of 250 workers.
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("startingWorkforce").intValue = 250;
            serialized.FindProperty("inhabitantsPerWorker").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            City cityState = economy.CreateCity(City(new[] { wheat }, new[] { fish }), economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(1000, economy.StartingPopulation);
            Assert.AreEqual(1000, cityState.Population.Inhabitants);
        }

        [Test]
        public void CreateMarket_ForACityThatProducesNothing_HasNoWorkers_AndTheSamePopulation()
        {
            City producer = economy.CreateCity(City(new[] { wheat }, new GoodDefinition[0]), economy.CreatePricing(), DaysPerWeek);
            City idle = economy.CreateCity(City(new GoodDefinition[0], new GoodDefinition[0]), economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(0, idle.WorkersOf(0));
            Assert.AreEqual(producer.Population.Inhabitants, idle.Population.Inhabitants);
        }

        [Test]
        public void TryValidateSettings_SaysWhenTheMerchantStepsAreOutOfOrder()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("merchantThirdStep").intValue = 6;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("merchants' workshops", problem);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => economy.CreateMerchantOutputRule());
        }

        [Test]
        public void TryValidateSettings_SaysWhenThePopulationPerWorkerIsWrong()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("inhabitantsPerWorker").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("starting population", problem);
        }

        [Test]
        public void CreateMarket_WithLabour_SplitsThePopulationIntoClasses()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("startingWorkforce").intValue = 250;
            serialized.FindProperty("inhabitantsPerWorker").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CityDefinition city = City(new[] { wheat }, new[] { fish });

            // 125 workers a workshop, cut to the 60 of each: 120 workers.
            City cityState = economy.CreateCity(
                city, economy.CreatePricing(), DaysPerWeek, economy.CreateLabourRule(), new PerClass<int>(40, 70, 140));

            Assert.AreEqual(480, cityState.Population.Housed);
            Assert.AreEqual(48, cityState.Population.CountOf(SocialClass.Rich));
            Assert.AreEqual(144, cityState.Population.CountOf(SocialClass.Wealthy));
            Assert.AreEqual(288, cityState.Population.CountOf(SocialClass.Poor));
            Assert.AreEqual(28, cityState.Population.Beggars);
            Assert.AreEqual(80, cityState.Population.RoomOf(SocialClass.Rich));
            Assert.AreEqual(210, cityState.Population.RoomOf(SocialClass.Wealthy));
            Assert.AreEqual(420, cityState.Population.RoomOf(SocialClass.Poor));
            Assert.IsNotNull(cityState.Labour);
        }

        private void UseStartingWorkforce()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("startingWorkforce").intValue = 250;
            serialized.FindProperty("inhabitantsPerWorker").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void StartingRoomOf_IsTheRoomOfAMarketWithoutGrid()
        {
            UseStartingWorkforce();
            CityDefinition city = City(new[] { wheat }, new[] { fish });
            var roomPerHouse = new PerClass<int>(40, 70, 140);

            City cityState = economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek, economy.CreateLabourRule(), roomPerHouse);
            PerClass<int> room = economy.StartingRoomOf(city, roomPerHouse);

            Assert.AreEqual(cityState.Population.RoomOf(SocialClass.Rich), room.Rich);
            Assert.AreEqual(cityState.Population.RoomOf(SocialClass.Wealthy), room.Wealthy);
            Assert.AreEqual(cityState.Population.RoomOf(SocialClass.Poor), room.Poor);
            Assert.AreEqual(420, room.Poor);
        }

        [Test]
        public void CreateMarket_TakesTheRoomOfTheGrid()
        {
            UseStartingWorkforce();
            CityDefinition city = City(new[] { wheat }, new[] { fish });
            var roomPerHouse = new PerClass<int>(40, 70, 140);
            var footprints = new PerClass<Footprint>(new Footprint(2, 1), new Footprint(2, 1), new Footprint(2, 1));
            var grid = new CityGrid(20, 20, new Vector2Int[0]);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2),
                new GridPlace(new Vector2Int(8, 9), BuildingOrientation.Along));
            var placement = new RoadsidePlacement(new System.Random(1));
            Assert.IsTrue(StartingHouses.TryPlace(grid, new PerClass<int>(2, 3, 5), footprints, new System.Random(1)));

            // A house off the network: its room comes only when it connects.
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, footprints.Poor,
                new GridPlace(new Vector2Int(0, 0), BuildingOrientation.Along));
            var housing = new List<CityHousingRule>();
            foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor })
            {
                housing.Add(economy.CreateHousingRule(housedClass, roomPerHouse[housedClass], System.Array.Empty<MaterialCost>(), footprints[housedClass]));
            }

            City without = economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek, economy.CreateLabourRule(), roomPerHouse);
            City cityState = economy.CreateCity(
                city, economy.CreatePricing(), DaysPerWeek, economy.CreateLabourRule(), roomPerHouse, housing, economy.CreateLeavingRule(), grid, placement);

            Assert.AreEqual(2 * 40, cityState.Population.RoomOf(SocialClass.Rich));
            Assert.AreEqual(3 * 70, cityState.Population.RoomOf(SocialClass.Wealthy));
            Assert.AreEqual(5 * 140, cityState.Population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(without.Population.Housed, cityState.Population.Housed);
            Assert.AreEqual(without.Population.Beggars, cityState.Population.Beggars);
            Assert.AreSame(grid, cityState.Grid);
        }

        [Test]
        public void CreateMarket_WithLabour_NeedsTheRoomOfAHouse()
        {
            CityDefinition city = City(new[] { wheat }, new[] { fish });

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek, economy.CreateLabourRule(), new PerClass<int>(40, 0, 140)));
        }

        [Test]
        public void TryValidateSettings_SaysWhenTheCityHouseOccupancyIsWrong()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("cityWealthyHouseOccupancy").doubleValue = 1.5;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("cities' houses", problem);
        }

        [Test]
        public void CreateHousingRule_BuildsAboveTheDefinitionsOccupancy()
        {
            EconomyDefinition fresh = Track(ScriptableObject.CreateInstance<EconomyDefinition>());

            CityHousingRule rule = fresh.CreateHousingRule(SocialClass.Poor, 100, new[] { new MaterialCost(0, 10) });

            Assert.AreEqual(SocialClass.Poor, rule.HousedClass);
            Assert.AreEqual(0.95, rule.Occupancy, Tolerance);
            Assert.AreEqual(100, rule.Room);
            Assert.AreEqual(1, rule.Materials.Count);
        }

        [Test]
        public void CreateMarket_GivesTheMarketItsHousing()
        {
            CityDefinition city = City(new[] { wheat }, new[] { fish });
            CityHousingRule housing = economy.CreateHousingRule(SocialClass.Rich, 40, System.Array.Empty<MaterialCost>());
            LeavingRule leaving = economy.CreateLeavingRule();

            City cityState = economy.CreateCity(
                city, economy.CreatePricing(), DaysPerWeek, economy.CreateLabourRule(), PerClass<int>.All(100), new[] { housing }, leaving);

            Assert.AreSame(housing, cityState.Housing[0]);
            Assert.AreSame(leaving, cityState.Leaving);
        }

        [Test]
        public void TryValidateSettings_SaysWhenTheLabourIsWrong()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("beggarArrivalDivisor").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("labour", problem);
        }

        [Test]
        public void CreateLabourRule_UsesTheDefinitionsSettings()
        {
            EconomyDefinition fresh = Track(ScriptableObject.CreateInstance<EconomyDefinition>());

            Assert.AreEqual(4d / 17d, fresh.StartingBeggarsPerWorker, Tolerance);

            LabourRule rule = fresh.CreateLabourRule();

            Assert.AreEqual(4, rule.InhabitantsPerWorker);
            Assert.AreEqual(50d, rule.BeggarSatisfaction);
            Assert.AreEqual(3, rule.BeggarTargetDivisor);
            Assert.AreEqual(8, rule.BeggarTargetMinimum);
            Assert.AreEqual(5, rule.BeggarArrivalDivisor);
            Assert.AreEqual(10, rule.BeggarDepartureDivisor);
            Assert.AreEqual(5, rule.VacancyMinimumBeggars);
            Assert.AreEqual(3, rule.VacancyMinimumFreeRoom);
            Assert.AreEqual(0.02, rule.ReserveLeakShare);
            Assert.AreEqual(30, rule.ReserveLeakCeiling);
        }

        [Test]
        public void CreatePricing_UsesTheDefinitionsFactors()
        {
            MarketPricing pricing = economy.CreatePricing();

            Assert.AreEqual(4, pricing.BuyCurve.Empty);
            Assert.AreEqual(0.6, pricing.BuyCurve.AtT3);
            Assert.AreEqual(2, pricing.SellCurve.Empty);
            Assert.AreEqual(0.5, pricing.SellCurve.AtT3);
        }

        [Test]
        public void CreatePricing_RejectsSellFactorsAboveTheBuyFactors()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("sellFactors.atT1").doubleValue = 1.2;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => economy.CreatePricing());
        }

        [Test]
        public void TryValidateSettings_AcceptsTheGamesSettings()
        {
            Assert.IsTrue(economy.TryValidateSettings(out string problem), problem);
            Assert.IsNull(problem);
        }

        [TestCase("buyFactors.atT3", 0.9, "buy factors", "the factor at T3")]
        [TestCase("sellFactors.empty", 0, "sell factors", "above 0")]
        [TestCase("sellFactors.atT1", 1.2, "sell factors", "At T1")]
        [TestCase("t2Floor", 8, "stock thresholds", "t2Floor")]
        [TestCase("merchantSecondBonus", -1, "merchants' workshops", "bonus")]
        public void TryValidateSettings_SaysWhichSettingIsWrong_AndWhy(string property, double value, string setting, string detail)
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty(property).doubleValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains(setting, problem);
            StringAssert.Contains(detail, problem);
        }

        [Test]
        public void CreateMarket_SpreadsTheStartingWorkforce_OverTheCitysWorkshops()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("startingWorkforce").intValue = 50;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CityDefinition city = City(new[] { wheat }, new[] { fish });

            City cityState = economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(25, cityState.WorkersOf(0));
            Assert.AreEqual(25, cityState.WorkersOf(1));
            Assert.AreEqual(0, cityState.WorkersOf(2));
            Assert.AreEqual(60, cityState.MaxWorkersOf(1));
        }

        [Test]
        public void CreateMarket_SpreadsTheStartingWorkforce_OverTheWorkshopsOfActiveGoodsOnly()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("startingWorkforce").intValue = 50;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            TestEconomy.SetActive(economy, fish, false);
            CityDefinition city = City(new[] { wheat }, new[] { fish });

            City cityState = economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(2, cityState.Market.GoodCount);
            Assert.AreEqual(50, cityState.WorkersOf(0));
            Assert.AreEqual(0, cityState.WorkersOf(1));
        }

        [Test]
        public void AnInactiveGood_IsLeftOutOfTheGoods_AndOfTheirIndices()
        {
            TestEconomy.SetActive(economy, fish, false);

            CollectionAssert.AreEqual(new[] { wheat, salt }, economy.Goods);
            Assert.AreEqual(1, economy.IndexOf(salt));
            Assert.AreEqual(-1, economy.IndexOf(fish));
            Assert.IsTrue(economy.IsInCatalogue(fish));
            Assert.IsFalse(economy.IsInCatalogue(null));
        }

        [Test]
        public void AGoodSwitchedBackOn_ReturnsToItsPlace()
        {
            TestEconomy.SetActive(economy, fish, false);
            Assert.AreEqual(2, economy.Goods.Count);

            TestEconomy.SetActive(economy, fish, true);

            CollectionAssert.AreEqual(new[] { wheat, fish, salt }, economy.Goods);
        }

        [Test]
        public void TheUnitsAndTheAiWeights_AreThoseOfTheActiveGoods()
        {
            GoodDefinition timber = Track(TestEconomy.CreateGood("Bois", 50, 7, unit: GoodUnit.Bushel, aiWeight: 11));
            EconomyDefinition withTimber = Track(TestEconomy.CreateEconomy(wheat, fish, timber));
            TestEconomy.SetActive(withTimber, fish, false);

            GoodUnits units = withTimber.CreateGoodUnits();

            Assert.AreEqual(2, units.Count);
            Assert.AreEqual(10, units.RoomOf(1));
            CollectionAssert.AreEqual(new[] { 9, 11 }, withTimber.CreateAiWeights());
        }

        [Test]
        public void CreateWorkforceRule_UsesTheDefinitionsSettings()
        {
            WorkforceRule rule = economy.CreateWorkforceRule();

            Assert.AreEqual(16, rule.HiredUpToT0);
            Assert.AreEqual(1, rule.WorkersAboveT3);
            // The test economy starts its cities with 6 000 workers (TestEconomy.CreateEconomy).
            Assert.AreEqual(6000, rule.StartingWorkforce);
        }

        [Test]
        public void TryValidateSettings_SaysWhenTheWorkforceIsWrong()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("workersAboveT3").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("the workforce", problem);
            StringAssert.Contains("workersAboveT3", problem);
        }

        [Test]
        public void ANewGood_HasAnAiWeightOfNine()
        {
            var good = ScriptableObject.CreateInstance<GoodDefinition>();
            Assert.AreEqual(9, good.AiWeight);
            Object.DestroyImmediate(good);
        }

        [Test]
        public void CreateAiWeights_GivesTheWeightOfEveryGood_InTheCataloguesOrder()
        {
            GoodDefinition grain = Track(TestEconomy.CreateGood("Céréales", 110, 4.725, aiWeight: 16));
            GoodDefinition tiles = Track(TestEconomy.CreateGood("Tuiles", 79.8, 0.016, aiWeight: 8));
            EconomyDefinition mixed = Track(TestEconomy.CreateEconomy(grain, tiles));

            CollectionAssert.AreEqual(new[] { 16, 8 }, mixed.CreateAiWeights());
        }

        [Test]
        public void TryValidateSettings_SaysWhichGoodHasANegativeAiWeight()
        {
            GoodDefinition salt = Track(TestEconomy.CreateGood("Sel", 28.5, 0.35, aiWeight: -1));
            EconomyDefinition broken = Track(TestEconomy.CreateEconomy(salt));

            Assert.IsFalse(broken.TryValidateSettings(out string problem));
            StringAssert.Contains("AI weight", problem);
            StringAssert.Contains("Sel", problem);
        }

        [Test]
        public void CreateGoodUnits_GivesABushelTheBarrelsPerBushel()
        {
            GoodDefinition grain = Track(TestEconomy.CreateGood("Céréales", 110, 4.725, unit: GoodUnit.Bushel));
            EconomyDefinition mixed = Track(TestEconomy.CreateEconomy(grain, salt));

            GoodUnits units = mixed.CreateGoodUnits();

            Assert.AreEqual(2, units.Count);
            Assert.AreEqual(10, units.RoomOf(0));
            Assert.AreEqual(1, units.RoomOf(1));
            Assert.AreEqual(10, mixed.RoomOf(grain));
            Assert.AreEqual(1, mixed.RoomOf(salt));
        }

        [Test]
        public void TryValidateSettings_SaysWhenABushelHasNoRoom()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("barrelsPerBushel").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("barrels per bushel", problem);
        }

        [Test]
        public void CreateThresholdRule_UsesTheDefinitionsDaysAndFloors()
        {
            // Two barrels a day consumed and four produced.
            StockThresholds thresholds = economy.CreateThresholdRule().ThresholdsFor(2, 4);

            Assert.AreEqual(14, thresholds.T0, Tolerance);
            Assert.AreEqual(42, thresholds.T1, Tolerance);
            Assert.AreEqual(82, thresholds.T2, Tolerance);
            Assert.AreEqual(96, thresholds.T3, Tolerance);
        }

        [Test]
        public void CreateMarket_ThresholdsFollowTheProductionOfTheCity()
        {
            // Wheat efficient (12 a day, 24 produced), fish inefficient (9, 13.5), salt not produced (3).
            CityDefinition city = City(new[] { wheat }, new[] { fish });
            City cityState = economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(492, cityState.Market.ThresholdsOf(0).T2, Tolerance);
            Assert.AreEqual(576, cityState.Market.ThresholdsOf(0).T3, Tolerance);
            Assert.AreEqual(324, cityState.Market.ThresholdsOf(1).T2, Tolerance);
            Assert.AreEqual(387, cityState.Market.ThresholdsOf(1).T3, Tolerance);
            Assert.AreEqual(63, cityState.Market.ThresholdsOf(2).T2, Tolerance);
            Assert.AreEqual(84, cityState.Market.ThresholdsOf(2).T3, Tolerance);
        }

        [Test]
        public void CreateMarket_PricesAGoodByTheCurvesAndItsThresholds()
        {
            // Salt: base price 18, 3 barrels a day, 21 in stock: T0 exactly.
            CityDefinition city = City(new GoodDefinition[0], new GoodDefinition[0]);
            City cityState = economy.CreateCity(city, economy.CreatePricing(), DaysPerWeek);

            // Bought: 18 x 1.5595, the average over [20, 21] from 1.619 to 1.5.
            Assert.AreEqual(29, cityState.Market.BuyPriceOf(2));

            // Sold: 18 x 1.3952, the average over [21, 22] from 1.4 to 1.3905.
            Assert.AreEqual(25, cityState.Market.SellPriceOf(2));
        }

        [Test]
        public void TryValidate_AcceptsACatalogueOfDistinctGoods()
        {
            Assert.IsTrue(economy.TryValidate(out string problem), problem);
        }

        [Test]
        public void TryValidate_RejectsAnEmptyCatalogue()
        {
            EconomyDefinition empty = Track(TestEconomy.CreateEconomy());

            Assert.IsFalse(empty.TryValidate(out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void TryValidate_RejectsAGoodListedTwice()
        {
            EconomyDefinition doubled = Track(TestEconomy.CreateEconomy(wheat, fish, wheat));

            Assert.IsFalse(doubled.TryValidate(out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void TryValidate_RejectsACatalogueWithNoActiveGood()
        {
            TestEconomy.SetActive(economy, wheat, false);
            TestEconomy.SetActive(economy, fish, false);
            TestEconomy.SetActive(economy, salt, false);

            Assert.IsFalse(economy.TryValidate(out string problem));
            StringAssert.Contains("active", problem);
        }

        [Test]
        public void TryValidate_RejectsAnInactiveGoodListedTwice()
        {
            EconomyDefinition doubled = Track(TestEconomy.CreateEconomy(wheat, fish, fish));
            TestEconomy.SetActive(doubled, fish, false);

            Assert.IsFalse(doubled.TryValidate(out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void ARecipe_ReachesTheMarket_ByTheIndicesOfTheActiveGoods()
        {
            TestEconomy.SetInputs(fish, (salt, 0.25));
            TestEconomy.SetActive(economy, wheat, false);

            City cityState = economy.CreateCity(City(new[] { fish }, new GoodDefinition[0]), economy.CreatePricing(), DaysPerWeek);

            // Without wheat, fish is the good 0 and salt the good 1.
            Assert.Greater(cityState.WorkersOf(0), 0);
            Assert.AreEqual(cityState.WorkersOf(0) * 0.3 * 0.25, cityState.DailyInputNeedOf(1), Tolerance);
        }

        [Test]
        public void AnInactiveInput_IsLeftOut_TheWorkshopNeedsOnlyTheOthers()
        {
            TestEconomy.SetInputs(fish, (salt, 0.25), (wheat, 0.1));
            TestEconomy.SetActive(economy, wheat, false);

            City cityState = economy.CreateCity(City(new[] { fish }, new GoodDefinition[0]), economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(2, cityState.Market.GoodCount);
            Assert.AreEqual(cityState.WorkersOf(0) * 0.3 * 0.25, cityState.DailyInputNeedOf(1), Tolerance);
        }

        [Test]
        public void AnInefficientWorkshop_NeedsInputsForWhatItMakes()
        {
            TestEconomy.SetInputs(fish, (salt, 0.25));

            City cityState = economy.CreateCity(City(new GoodDefinition[0], new[] { fish }), economy.CreatePricing(), DaysPerWeek);

            Assert.Greater(cityState.WorkersOf(1), 0);
            Assert.AreEqual(cityState.WorkersOf(1) * 0.3 * 0.75 * 0.25, cityState.DailyInputNeedOf(2), Tolerance);
        }

        [TestCase("no good")]
        [TestCase("itself")]
        [TestCase("not in the catalogue")]
        [TestCase("twice")]
        [TestCase("zero")]
        public void TryValidate_RefusesABrokenRecipe(string broken)
        {
            GoodDefinition outsider = Track(TestEconomy.CreateGood("Ambre", 50, 1));

            switch (broken)
            {
                case "no good":
                    TestEconomy.SetInputs(fish, (null, 0.25));
                    break;
                case "itself":
                    TestEconomy.SetInputs(fish, (fish, 0.25));
                    break;
                case "not in the catalogue":
                    TestEconomy.SetInputs(fish, (outsider, 0.25));
                    break;
                case "twice":
                    TestEconomy.SetInputs(fish, (salt, 0.25), (salt, 0.1));
                    break;
                case "zero":
                    TestEconomy.SetInputs(fish, (salt, 0d));
                    break;
            }

            // An inactive good's recipe is checked too.
            TestEconomy.SetActive(economy, fish, false);

            Assert.IsFalse(economy.TryValidate(out string problem));
            StringAssert.Contains(fish.name, problem);
        }

        [Test]
        public void TryValidate_AcceptsARecipeWhoseInputIsInactive()
        {
            TestEconomy.SetInputs(fish, (salt, 0.25));
            TestEconomy.SetActive(economy, salt, false);

            Assert.IsTrue(economy.TryValidate(out string problem), problem);
        }

        [Test]
        public void TryValidate_RejectsAnEmptyEntry()
        {
            EconomyDefinition holed = Track(TestEconomy.CreateEconomy(wheat, null));

            Assert.IsFalse(holed.TryValidate(out string problem));
            Assert.IsNotEmpty(problem);
        }
    }
}
