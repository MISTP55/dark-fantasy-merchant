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

        private readonly List<Object> created = new List<Object>();

        private GoodDefinition wheat;
        private GoodDefinition fish;
        private GoodDefinition salt;
        private EconomyDefinition economy;

        [SetUp]
        public void SetUp()
        {
            wheat = Track(TestEconomy.CreateGood("Blé", 10, 2.0));
            fish = Track(TestEconomy.CreateGood("Poisson", 12, 1.5));
            salt = Track(TestEconomy.CreateGood("Sel", 18, 0.5));
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

            Assert.AreEqual(30, fresh.TargetStockDays);
            Assert.AreEqual(1.5, fresh.EfficientProductionRate);
            Assert.AreEqual(60, fresh.EfficientCeilingDays);
            Assert.AreEqual(1.1, fresh.InefficientProductionRate);
            Assert.AreEqual(36, fresh.InefficientCeilingDays);
            Assert.AreEqual(2.5, fresh.EmptyStockPriceMultiplier);
            Assert.AreEqual(0.5, fresh.SurplusPriceMultiplier);
            Assert.AreEqual(0.05, fresh.BuyMargin);
            Assert.AreEqual(0.05, fresh.SellMargin);
            Assert.AreEqual(0, fresh.Goods.Count);
        }

        [Test]
        public void ANewGood_AndANewCity_HaveUsableDefaults()
        {
            GoodDefinition good = Track(ScriptableObject.CreateInstance<GoodDefinition>());
            CityDefinition city = Track(ScriptableObject.CreateInstance<CityDefinition>());

            Assert.GreaterOrEqual(good.BasePrice, 1);
            Assert.AreEqual(0, city.Population);
            Assert.AreEqual(0, city.EfficientGoods.Count);
            Assert.AreEqual(0, city.InefficientGoods.Count);
        }

        [Test]
        public void IndexOf_IsThePlaceInTheCatalogue()
        {
            GoodDefinition stranger = Track(TestEconomy.CreateGood("Vin", 60, 0.6));

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
            Assert.DoesNotThrow(() => economy.CreateMarket(city, economy.CreatePricing()));
        }

        [Test]
        public void CreateMarket_StocksEveryGoodOfTheCatalogue_FromThePopulation()
        {
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing());

            Assert.AreEqual(3, market.GoodCount);

            // Wheat: 12 barrels a day, efficient, 60 days.
            Assert.AreEqual(720, market.StockOf(0), Tolerance);

            // Fish: 9 barrels a day, inefficient, 36 days.
            Assert.AreEqual(324, market.StockOf(1), Tolerance);

            // Salt: 3 barrels a day, not produced, the target of 30 days.
            Assert.AreEqual(90, market.StockOf(2), Tolerance);
            Assert.AreEqual(90, market.TargetOf(2), Tolerance);
        }

        [Test]
        public void CreateMarket_ProducesAtTheDefinitionsRates()
        {
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing());
            market.Take(0, 100);
            market.Take(1, 100);

            market.StartDay();

            // Wheat: 620 + 18 - 12. Fish: 224 + 9.9 - 9.
            Assert.AreEqual(626, market.StockOf(0), Tolerance);
            Assert.AreEqual(224.9, market.StockOf(1), Tolerance);
        }

        [Test]
        public void CreateMarket_ForACityWithoutInhabitants_HasEmptyStocks()
        {
            CityDefinition city = City(0, new[] { wheat }, new GoodDefinition[0]);

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing());

            Assert.AreEqual(0, market.AvailableOf(0));
            Assert.AreEqual(0, market.AvailableOf(2));
        }

        [Test]
        public void CreatePricing_UsesTheDefinitionsMultipliersAndMargins()
        {
            MarketPricing pricing = economy.CreatePricing();

            Assert.AreEqual(2.5, pricing.Multiplier(0, 100), Tolerance);
            Assert.AreEqual(0.5, pricing.Multiplier(200, 100), Tolerance);
            Assert.AreEqual(21, pricing.BuyPrice(20, 101, 100));
            Assert.AreEqual(19, pricing.SellPrice(20, 100, 100));
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
