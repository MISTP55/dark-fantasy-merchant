using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketHousingTests
    {
        private const int Timber = 0;
        private const int Metal = 1;

        // Consumed by no one and produced nowhere: only the houses take them.
        private static MarketGood Material => new MarketGood(10, 0, 0, 0);

        // A house of 100 places that takes 10 timber and 10 metal, built above 95 % of the room.
        private static CityHousingRule Housing => new CityHousingRule(
            0.95,
            100,
            new[] { new MaterialCost(Timber, 10), new MaterialCost(Metal, 10) });

        private static CityMarket Market(CityPopulation population, CityHousingRule housing, LabourRule labour)
        {
            return new CityMarket(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, population, labour, housing);
        }

        private static CityMarket Market(CityPopulation population, CityHousingRule housing = null)
        {
            return Market(population, housing ?? Housing, TestMarkets.Labour);
        }

        private static void Stock(CityMarket market, int timber, int metal)
        {
            if (timber > 0)
            {
                market.Put(Timber, timber);
            }

            if (metal > 0)
            {
                market.Put(Metal, metal);
            }
        }

        [Test]
        public void AboveTheOccupancy_TheCityBuildsAHouse_FromItsStock()
        {
            CityMarket market = Market(new CityPopulation(960, 0, 1000));
            Stock(market, 25, 12);

            market.StartDay();

            Assert.AreEqual(1100, market.Population.Room);
            Assert.AreEqual(1, market.CityHouses);
            Assert.AreEqual(15, market.AvailableOf(Timber));
            Assert.AreEqual(2, market.AvailableOf(Metal));
        }

        [Test]
        public void AtTheOccupancyExactly_NothingIsBuilt()
        {
            CityMarket market = Market(new CityPopulation(950, 0, 1000));
            Stock(market, 10, 10);

            market.StartDay();

            Assert.AreEqual(1000, market.Population.Room);
            Assert.AreEqual(0, market.CityHouses);
            Assert.AreEqual(10, market.AvailableOf(Timber));
        }

        [Test]
        public void TheBeggars_DoNotCount()
        {
            CityMarket market = Market(new CityPopulation(900, 100, 1000));
            Stock(market, 10, 10);

            market.StartDay();

            Assert.AreEqual(0, market.CityHouses);
        }

        [Test]
        public void TheCity_EmptiesItsStock_ToBuild()
        {
            CityMarket market = Market(new CityPopulation(1000, 0, 1000));
            Stock(market, 10, 10);

            market.StartDay();

            Assert.AreEqual(1, market.CityHouses);
            Assert.AreEqual(0, market.StockOf(Timber));
            Assert.AreEqual(0, market.StockOf(Metal));
        }

        [Test]
        public void WithoutEnoughOfOneMaterial_NothingIsBuilt_NorTaken()
        {
            CityMarket market = Market(new CityPopulation(1000, 0, 1000));
            Stock(market, 30, 9);

            market.StartDay();

            Assert.AreEqual(0, market.CityHouses);
            Assert.AreEqual(1000, market.Population.Room);
            Assert.AreEqual(30, market.AvailableOf(Timber));
            Assert.AreEqual(9, market.AvailableOf(Metal));
        }

        [Test]
        public void OneHouseADay_AtMost()
        {
            // Still above 95 % after one house: 1 100 room for 1 100 housed.
            CityMarket market = Market(new CityPopulation(1100, 0, 1000));
            Stock(market, 100, 100);

            market.StartDay();
            Assert.AreEqual(1, market.CityHouses);

            market.StartDay();
            Assert.AreEqual(2, market.CityHouses);
            Assert.AreEqual(1200, market.Population.Room);
        }

        [Test]
        public void AHouseWithoutMaterials_IsBuiltWhateverTheStock()
        {
            var housing = new CityHousingRule(0.95, 100, Array.Empty<MaterialCost>());
            CityMarket market = Market(new CityPopulation(1000, 0, 1000), housing);

            market.StartDay();

            Assert.AreEqual(1, market.CityHouses);
            Assert.AreEqual(1100, market.Population.Room);
        }

        [Test]
        public void TheMaterialsTaken_AreNotCountedAsConsumption()
        {
            CityMarket market = Market(new CityPopulation(1000, 0, 1000));
            Stock(market, 10, 10);

            market.StartDay();

            Assert.AreEqual(0, market.RecentConsumptionOf(Timber));
        }

        [Test]
        public void TheCityHouses_AreNotThePlayers()
        {
            CityMarket market = Market(new CityPopulation(1000, 0, 1000));
            Stock(market, 10, 10);

            market.StartDay();

            Assert.AreEqual(0, market.PlayerHouses);
        }

        [Test]
        public void AMarketWithoutLabour_NeverBuilds()
        {
            CityMarket market = new CityMarket(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(1000), null, Housing);
            Stock(market, 10, 10);

            market.StartDay();

            Assert.AreEqual(0, market.CityHouses);
            Assert.AreEqual(1000, market.Population.Room);
        }

        [Test]
        public void AMarketWithoutHousing_NeverBuilds()
        {
            CityMarket market = new CityMarket(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(1000, 0, 1000), TestMarkets.Labour);
            Stock(market, 10, 10);

            market.StartDay();

            Assert.AreEqual(0, market.CityHouses);
        }

        [Test]
        public void AMaterialOutsideTheCatalogue_IsRefused()
        {
            var housing = new CityHousingRule(0.95, 100, new[] { new MaterialCost(2, 10) });

            Assert.Throws<ArgumentOutOfRangeException>(() => Market(new CityPopulation(1000), housing));
        }

        [TestCase(-0.01)]
        [TestCase(1.01)]
        [TestCase(double.NaN)]
        public void AnOccupancyOutsideZeroToOne_IsRefused(double occupancy)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CityHousingRule(occupancy, 100, Array.Empty<MaterialCost>()));
        }

        [Test]
        public void ARoomUnderOne_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CityHousingRule(0.95, 0, Array.Empty<MaterialCost>()));
        }

        [Test]
        public void AGoodTwiceAmongTheMaterials_IsRefused()
        {
            Assert.Throws<ArgumentException>(
                () => new CityHousingRule(0.95, 100, new[] { new MaterialCost(Timber, 1), new MaterialCost(Timber, 2) }));
        }

        [Test]
        public void IsCrowded_IsAboveTheOccupancy_StrictlyOverTheRoom()
        {
            CityHousingRule housing = Housing;

            Assert.IsFalse(housing.IsCrowded(new CityPopulation(950, 0, 1000)));
            Assert.IsTrue(housing.IsCrowded(new CityPopulation(951, 0, 1000)));
            Assert.IsTrue(housing.IsCrowded(new CityPopulation(1, 0, 0)));
            Assert.IsFalse(housing.IsCrowded(new CityPopulation(0, 0, 0)));
        }
    }
}
