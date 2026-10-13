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
            // Per week for 1 000 inhabitants: 2, 1.5 and 0.5 barrels a day.
            wheat = Track(TestEconomy.CreateGood("Blé", 10, 14));
            fish = Track(TestEconomy.CreateGood("Poisson", 12, 10.5));
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

            Assert.AreEqual(10, fresh.TargetStockDays);
            Assert.AreEqual(7, fresh.StartingStockDays);
            Assert.AreEqual(2, fresh.EfficientProductionRate);
            Assert.AreEqual(60, fresh.EfficientCeilingDays);
            Assert.AreEqual(1.5, fresh.InefficientProductionRate);
            Assert.AreEqual(36, fresh.InefficientCeilingDays);
            Assert.AreEqual(0.05, fresh.BuyMargin);
            Assert.AreEqual(0.05, fresh.SellMargin);
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

            // The target is 10 days.
            Assert.AreEqual(30, market.TargetOf(2), Tolerance);
        }

        [Test]
        public void CreateMarket_SpreadsTheWeeklyConsumptionOverTheDaysOfTheWeek()
        {
            CityDefinition city = City(6000, new GoodDefinition[0], new GoodDefinition[0]);

            // Wheat: 84 barrels a week, over a week of 10 days.
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), 10);

            Assert.AreEqual(8.4 * 10, market.TargetOf(0), Tolerance);
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
        public void CreateMarket_ProducesAtTheDefinitionsRates()
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
        public void CreatePricing_UsesTheDefinitionsMargins()
        {
            MarketPricing pricing = economy.CreatePricing();

            Assert.AreEqual(21, pricing.BuyPrice(10, 20, 50, 101, 100));
            Assert.AreEqual(19, pricing.SellPrice(10, 20, 50, 100, 100));
        }

        [Test]
        public void CreateMarket_PricesAGoodBetweenItsOwnMinimumAndMaximum()
        {
            // Salt: minimum 9, base 18, maximum 45; 3 barrels a day, 21 in stock, a target of 30.
            CityDefinition city = City(6000, new GoodDefinition[0], new GoodDefinition[0]);
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            // The last barrel is bought at the maximum plus 5 %: 47.25.
            Assert.AreEqual(48, market.BuyPriceOf(2, 20));

            // A barrel sold into twice the target or more is paid the minimum minus 5 %: 8.55.
            Assert.AreEqual(8, market.SellPriceOf(2, 159));
        }

        [Test]
        public void ANewGood_HasBoundsAroundItsBasePrice()
        {
            GoodDefinition good = Track(ScriptableObject.CreateInstance<GoodDefinition>());

            Assert.GreaterOrEqual(good.MinPrice, 1);
            Assert.LessOrEqual(good.MinPrice, good.BasePrice);
            Assert.GreaterOrEqual(good.MaxPrice, good.BasePrice);
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
