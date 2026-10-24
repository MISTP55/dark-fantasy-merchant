using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

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
            SocialClass.Poor,
            0.95,
            100,
            new[] { new MaterialCost(Timber, 10), new MaterialCost(Metal, 10) });

        private static CityMarket Market(CityPopulation population, CityHousingRule housing, LabourRule labour)
        {
            return new CityMarket(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, population, labour, housing == null ? null : new[] { housing });
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
            var housing = new CityHousingRule(SocialClass.Poor, 0.95, 100, Array.Empty<MaterialCost>());
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

            Assert.AreEqual(0, market.PlayerHousesOf(SocialClass.Poor));
        }

        [Test]
        public void AMarketWithoutLabour_NeverBuilds()
        {
            CityMarket market = new CityMarket(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(1000), null, new[] { Housing });
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
            var housing = new CityHousingRule(SocialClass.Poor, 0.95, 100, new[] { new MaterialCost(2, 10) });

            Assert.Throws<ArgumentOutOfRangeException>(() => Market(new CityPopulation(1000), housing));
        }

        [TestCase(-0.01)]
        [TestCase(1.01)]
        [TestCase(double.NaN)]
        public void AnOccupancyOutsideZeroToOne_IsRefused(double occupancy)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CityHousingRule(SocialClass.Poor, occupancy, 100, Array.Empty<MaterialCost>()));
        }

        [Test]
        public void ARoomUnderOne_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CityHousingRule(SocialClass.Poor, 0.95, 0, Array.Empty<MaterialCost>()));
        }

        [Test]
        public void AGoodTwiceAmongTheMaterials_IsRefused()
        {
            Assert.Throws<ArgumentException>(
                () => new CityHousingRule(SocialClass.Poor, 0.95, 100, new[] { new MaterialCost(Timber, 1), new MaterialCost(Timber, 2) }));
        }

        private static readonly Footprint HouseFootprint = new Footprint(1, 1);

        private static CityHousingRule GridHousing => new CityHousingRule(
            SocialClass.Poor,
            0.95,
            140,
            new[] { new MaterialCost(Timber, 10), new MaterialCost(Metal, 10) },
            HouseFootprint);

        private static CityMarket GridMarket(CityPopulation population, CityGrid grid, IPlacementStrategy placement)
        {
            return new CityMarket(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, population, TestMarkets.Labour, new[] { GridHousing }, null, grid, placement);
        }

        private static CityGrid Grid(int width, int height) => new CityGrid(width, height, new Vector2Int[0]);

        [Test]
        public void BuildCityHouses_PlacesTheHouseOnTheGrid()
        {
            CityGrid grid = Grid(5, 5);
            CityMarket market = GridMarket(new CityPopulation(960, 0, 1000), grid, new RandomPlacement(new System.Random(1)));
            Stock(market, 25, 12);

            market.StartDay();

            Assert.AreEqual(1, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(BuildingOwner.City, grid.Buildings[0].Owner);
            Assert.AreEqual(1, market.CityHouses);
            Assert.AreEqual(1140, market.Population.Room);
        }

        [Test]
        public void BuildCityHouses_BuildsNothing_WhenTheGridHasNoPlace()
        {
            CityGrid grid = Grid(1, 1);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint,
                new GridPlace(Vector2Int.zero, BuildingOrientation.Along));
            CityMarket market = GridMarket(new CityPopulation(960, 0, 1000), grid, new RandomPlacement(new System.Random(1)));
            Stock(market, 25, 12);

            for (int day = 0; day < 5; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(1000, market.Population.Room);
            Assert.AreEqual(0, market.CityHouses);
            Assert.AreEqual(25, market.AvailableOf(Timber));
            Assert.AreEqual(12, market.AvailableOf(Metal));
        }

        [Test]
        public void BuildCityHouses_TakesNoMaterial_WhenThereIsNoPlace()
        {
            CityGrid grid = Grid(1, 1);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint,
                new GridPlace(Vector2Int.zero, BuildingOrientation.Along));
            CityMarket market = GridMarket(new CityPopulation(960, 0, 1000), grid, new RandomPlacement(new System.Random(1)));
            Stock(market, 10, 10);

            market.StartDay();

            Assert.AreEqual(10, market.AvailableOf(Timber));
            Assert.AreEqual(10, market.AvailableOf(Metal));
        }

        [Test]
        public void Room_IsAlwaysTheHousesOnTheGrid()
        {
            CityGrid grid = Grid(6, 6);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint,
                new GridPlace(Vector2Int.zero, BuildingOrientation.Along));
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint,
                new GridPlace(new Vector2Int(1, 0), BuildingOrientation.Along));
            var population = new CityPopulation(new PerClass<int>(0, 0, 270), 0, new PerClass<int>(0, 0, 280));
            CityMarket market = GridMarket(population, grid, new RandomPlacement(new System.Random(1)));
            Stock(market, 10, 10);

            market.StartDay();
            market.AddPlayerHouse(SocialClass.Poor, 140, HouseFootprint, new GridPlace(new Vector2Int(5, 5), BuildingOrientation.Along));

            Assert.AreEqual(1, market.CityHouses);
            Assert.AreEqual(4, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(grid.HousesOf(SocialClass.Poor) * 140, market.Population.RoomOf(SocialClass.Poor));
        }

        [Test]
        public void Constructor_Throws_ForAGridWithoutStrategy()
        {
            var population = new CityPopulation(960, 0, 1000);

            Assert.Throws<ArgumentException>(() => GridMarket(population, Grid(2, 2), null));
            Assert.Throws<ArgumentException>(() => new CityMarket(new[] { Material, Material }, TestMarkets.Thresholds,
                TestMarkets.Workforce, 0, TestMarkets.Pricing, 7, population, TestMarkets.Labour, new[] { GridHousing }, null,
                null, new RandomPlacement(new System.Random(1))));
        }

        [Test]
        public void Constructor_Throws_ForAGridWithAHousingRuleWithoutFootprint()
        {
            Assert.Throws<ArgumentException>(() => new CityMarket(new[] { Material, Material }, TestMarkets.Thresholds,
                TestMarkets.Workforce, 0, TestMarkets.Pricing, 7, new CityPopulation(960, 0, 1000), TestMarkets.Labour,
                new[] { Housing }, null, Grid(2, 2), new RandomPlacement(new System.Random(1))));
        }

        [Test]
        public void AddPlayerHouse_WithoutPlace_Throws_WhenTheMarketHasAGrid()
        {
            CityMarket market = GridMarket(new CityPopulation(960, 0, 1000), Grid(3, 3), new RandomPlacement(new System.Random(1)));

            Assert.Throws<InvalidOperationException>(() => market.AddPlayerHouse(SocialClass.Poor, 100));
            Assert.Throws<InvalidOperationException>(() => market.AddPlayerWorkshop(0, 5));
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
