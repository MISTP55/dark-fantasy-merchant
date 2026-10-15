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

        private CityDefinition City(int population, GoodDefinition[] efficient, GoodDefinition[] inefficient)
        {
            return Track(TestEconomy.CreateCity(population, efficient, inefficient));
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
            Assert.AreEqual(7, fresh.StableFoodDays);
            Assert.AreEqual(14, fresh.AbundantFoodDays);
            Assert.AreEqual(0.05, fresh.MaxMonthlyGrowth);
            Assert.AreEqual(0.1, fresh.MaxMonthlyDecline);
            Assert.AreEqual(0, fresh.Goods.Count);
        }

        [Test]
        public void ANewGood_AndANewCity_HaveUsableDefaults()
        {
            GoodDefinition good = Track(ScriptableObject.CreateInstance<GoodDefinition>());
            CityDefinition city = Track(ScriptableObject.CreateInstance<CityDefinition>());

            Assert.GreaterOrEqual(good.BasePrice, 1);
            Assert.Greater(good.MaxWorkers, 0);
            Assert.Greater(good.DailyOutputPerWorker, 0d);
            Assert.AreEqual(GoodCategory.RawMaterial, good.Category);
            Assert.AreEqual(0, city.Population);
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
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });

            Assert.AreEqual(GoodProduction.Efficient, city.ProductionOf(wheat));
            Assert.AreEqual(GoodProduction.Inefficient, city.ProductionOf(fish));
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(salt));
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(null));
        }

        [Test]
        public void AGoodInBothLists_IsEfficient()
        {
            CityDefinition city = City(6000, new[] { wheat }, new[] { wheat });

            Assert.AreEqual(GoodProduction.Efficient, city.ProductionOf(wheat));
        }

        [Test]
        public void AnEmptyEntry_IsIgnored()
        {
            CityDefinition city = City(6000, new GoodDefinition[] { null, wheat }, new GoodDefinition[] { null });

            Assert.AreEqual(GoodProduction.Efficient, city.ProductionOf(wheat));
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(null));
            Assert.DoesNotThrow(() => economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek));
        }

        [Test]
        public void CreateMarket_StocksEveryGoodOfTheCatalogue_FromThePopulation()
        {
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(3, market.GoodCount);

            // A week of what the city consumes, whatever it produces: wheat 12 barrels a day,
            // fish 9, salt 3.
            Assert.AreEqual(84, market.StockOf(0), Tolerance);
            Assert.AreEqual(63, market.StockOf(1), Tolerance);
            Assert.AreEqual(21, market.StockOf(2), Tolerance);

            // Salt is not produced: T0 is 7 days, T1 14 more.
            Assert.AreEqual(21, market.ThresholdsOf(2).T0, Tolerance);
            Assert.AreEqual(63, market.ThresholdsOf(2).T1, Tolerance);
        }

        [Test]
        public void CreateMarket_SpreadsTheWeeklyConsumptionOverTheDaysOfTheWeek()
        {
            CityDefinition city = City(6000, new GoodDefinition[0], new GoodDefinition[0]);

            // Wheat: 84 barrels a week, over a week of 10 days; T0 is 7 days of it.
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), 10);

            Assert.AreEqual(8.4 * 7, market.ThresholdsOf(0).T0, Tolerance);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void CreateMarket_RejectsAWeekWithoutDays(int daysPerWeek)
        {
            CityDefinition city = City(6000, new GoodDefinition[0], new GoodDefinition[0]);

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => economy.CreateMarket(city, economy.CreatePricing(), daysPerWeek));
        }

        [Test]
        public void CreateMarket_ProducesAtTheDefinitionsOutputs()
        {
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            market.StartDay();

            // Wheat: 84 + 24 - 12. Fish: 63 + 13.5 - 9.
            Assert.AreEqual(96, market.StockOf(0), Tolerance);
            Assert.AreEqual(67.5, market.StockOf(1), Tolerance);
        }

        [Test]
        public void CreateMarket_ForACityWithoutInhabitants_HasEmptyStocks()
        {
            CityDefinition city = City(0, new[] { wheat }, new GoodDefinition[0]);

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(0, market.AvailableOf(0));
            Assert.AreEqual(0, market.AvailableOf(2));
        }

        [Test]
        public void CreateMarket_StartsWithThePopulationOfTheCity()
        {
            CityDefinition city = City(6000, new GoodDefinition[0], new GoodDefinition[0]);

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(6000, market.Population.Inhabitants);
        }

        [Test]
        public void CreateFoodGrowth_UsesTheDefinitionsSettings()
        {
            FoodGrowth growth = economy.CreateFoodGrowth(30);

            Assert.AreEqual(14, growth.AbundantDays);
            Assert.AreEqual(30, growth.DaysPerMonth);
            Assert.AreEqual(-0.1, growth.MonthlyRateAt(0), Tolerance);
            Assert.AreEqual(0, growth.MonthlyRateAt(7), Tolerance);
            Assert.AreEqual(0.05, growth.MonthlyRateAt(14), Tolerance);
        }

        [Test]
        public void CreateMarket_FeedsThePopulationWithFoodOnly()
        {
            // The city starts with a week of everything; only the iron is then piled up.
            GoodDefinition iron = Track(TestEconomy.CreateGood("Fer", 50, 7, GoodCategory.RawMaterial));
            EconomyDefinition mixed = Track(TestEconomy.CreateEconomy(wheat, iron));
            CityDefinition city = City(1000, new GoodDefinition[0], new GoodDefinition[0]);

            CityMarket market = mixed.CreateMarket(city, mixed.CreatePricing(), DaysPerWeek, mixed.CreateFoodGrowth(30));
            market.Put(1, 1000);

            Assert.AreEqual(0, market.MonthlyGrowthRate, Tolerance);

            // Two weeks of wheat: 14 more barrels.
            market.Put(0, 14);

            Assert.AreEqual(0.05, market.MonthlyGrowthRate, Tolerance);
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
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(25, market.WorkersOf(0));
            Assert.AreEqual(25, market.WorkersOf(1));
            Assert.AreEqual(0, market.WorkersOf(2));
            Assert.AreEqual(60, market.MaxWorkersOf(1));
        }

        [Test]
        public void CreateWorkforceRule_UsesTheDefinitionsSettings()
        {
            WorkforceRule rule = economy.CreateWorkforceRule();

            Assert.AreEqual(16, rule.HiredUpToT0);
            Assert.AreEqual(1, rule.WorkersAboveT3);
            Assert.AreEqual(250, rule.StartingWorkforce);
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
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(492, market.ThresholdsOf(0).T2, Tolerance);
            Assert.AreEqual(576, market.ThresholdsOf(0).T3, Tolerance);
            Assert.AreEqual(324, market.ThresholdsOf(1).T2, Tolerance);
            Assert.AreEqual(387, market.ThresholdsOf(1).T3, Tolerance);
            Assert.AreEqual(63, market.ThresholdsOf(2).T2, Tolerance);
            Assert.AreEqual(84, market.ThresholdsOf(2).T3, Tolerance);
        }

        [Test]
        public void CreateMarket_PricesAGoodByTheCurvesAndItsThresholds()
        {
            // Salt: base price 18, 3 barrels a day, 21 in stock: T0 exactly.
            CityDefinition city = City(6000, new GoodDefinition[0], new GoodDefinition[0]);
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            // Bought: 18 x 1.5595, the average over [20, 21] from 1.619 to 1.5.
            Assert.AreEqual(29, market.BuyPriceOf(2));

            // Sold: 18 x 1.3952, the average over [21, 22] from 1.4 to 1.3905.
            Assert.AreEqual(25, market.SellPriceOf(2));
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
        public void TryValidate_RejectsAnEmptyEntry()
        {
            EconomyDefinition holed = Track(TestEconomy.CreateEconomy(wheat, null));

            Assert.IsFalse(holed.TryValidate(out string problem));
            Assert.IsNotEmpty(problem);
        }
    }
}
