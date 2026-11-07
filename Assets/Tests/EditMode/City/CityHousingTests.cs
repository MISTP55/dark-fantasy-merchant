using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityHousingTests
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

        private static City NewCity(CityPopulation population, CityHousingRule housing, LabourRule labour)
        {
            return new City(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, population, labour, housing == null ? null : new[] { housing });
        }

        private static City NewCity(CityPopulation population, CityHousingRule housing = null)
        {
            return NewCity(population, housing ?? Housing, TestMarkets.Labour);
        }

        private static void Stock(City city, int timber, int metal)
        {
            if (timber > 0)
            {
                city.Market.Put(Timber, timber);
            }

            if (metal > 0)
            {
                city.Market.Put(Metal, metal);
            }
        }

        [Test]
        public void AboveTheOccupancy_TheCityBuildsAHouse_FromItsStock()
        {
            City city = NewCity(new CityPopulation(960, 0, 1000));
            Stock(city, 25, 12);

            city.StartDay();

            Assert.AreEqual(1100, city.Population.Room);
            Assert.AreEqual(1, city.CityHouses);
            Assert.AreEqual(15, city.Market.AvailableOf(Timber));
            Assert.AreEqual(2, city.Market.AvailableOf(Metal));
        }

        [Test]
        public void AtTheOccupancyExactly_NothingIsBuilt()
        {
            City city = NewCity(new CityPopulation(950, 0, 1000));
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(1000, city.Population.Room);
            Assert.AreEqual(0, city.CityHouses);
            Assert.AreEqual(10, city.Market.AvailableOf(Timber));
        }

        [Test]
        public void TheBeggars_DoNotCount()
        {
            City city = NewCity(new CityPopulation(900, 100, 1000));
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(0, city.CityHouses);
        }

        [Test]
        public void TheCity_EmptiesItsStock_ToBuild()
        {
            City city = NewCity(new CityPopulation(1000, 0, 1000));
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(1, city.CityHouses);
            Assert.AreEqual(0, city.Market.StockOf(Timber));
            Assert.AreEqual(0, city.Market.StockOf(Metal));
        }

        [Test]
        public void WithoutEnoughOfOneMaterial_NothingIsBuilt_NorTaken()
        {
            City city = NewCity(new CityPopulation(1000, 0, 1000));
            Stock(city, 30, 9);

            city.StartDay();

            Assert.AreEqual(0, city.CityHouses);
            Assert.AreEqual(1000, city.Population.Room);
            Assert.AreEqual(30, city.Market.AvailableOf(Timber));
            Assert.AreEqual(9, city.Market.AvailableOf(Metal));
        }

        [Test]
        public void OneHouseADay_AtMost()
        {
            // Still above 95 % after one house: 1 100 room for 1 100 housed.
            City city = NewCity(new CityPopulation(1100, 0, 1000));
            Stock(city, 100, 100);

            city.StartDay();
            Assert.AreEqual(1, city.CityHouses);

            city.StartDay();
            Assert.AreEqual(2, city.CityHouses);
            Assert.AreEqual(1200, city.Population.Room);
        }

        [Test]
        public void AHouseWithoutMaterials_IsBuiltWhateverTheStock()
        {
            var housing = new CityHousingRule(SocialClass.Poor, 0.95, 100, Array.Empty<MaterialCost>());
            City city = NewCity(new CityPopulation(1000, 0, 1000), housing);

            city.StartDay();

            Assert.AreEqual(1, city.CityHouses);
            Assert.AreEqual(1100, city.Population.Room);
        }

        [Test]
        public void TheMaterialsTaken_AreNotCountedAsConsumption()
        {
            City city = NewCity(new CityPopulation(1000, 0, 1000));
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(0, city.RecentConsumptionOf(Timber));
        }

        [Test]
        public void TheCityHouses_AreNotThePlayers()
        {
            City city = NewCity(new CityPopulation(1000, 0, 1000));
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(0, city.PlayerHousesOf(SocialClass.Poor));
        }

        [Test]
        public void AMarketWithoutLabour_NeverBuilds()
        {
            City city = new City(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(1000), null, new[] { Housing });
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(0, city.CityHouses);
            Assert.AreEqual(1000, city.Population.Room);
        }

        [Test]
        public void AMarketWithoutHousing_NeverBuilds()
        {
            City city = new City(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(1000, 0, 1000), TestMarkets.Labour);
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(0, city.CityHouses);
        }

        [Test]
        public void AMaterialOutsideTheCatalogue_IsRefused()
        {
            var housing = new CityHousingRule(SocialClass.Poor, 0.95, 100, new[] { new MaterialCost(2, 10) });

            Assert.Throws<ArgumentOutOfRangeException>(() => NewCity(new CityPopulation(1000), housing));
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

        private static City GridCity(CityPopulation population, CityGrid grid, IPlacementStrategy placement)
        {
            return new City(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, population, TestMarkets.Labour, new[] { GridHousing }, null, grid, placement);
        }

        private static CityGrid Grid(int width, int height) => new CityGrid(width, height, new Vector2Int[0]);

        [Test]
        public void BuildCityHouses_PlacesTheHouseOnTheGrid()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(5, 5);
            City city = GridCity(new CityPopulation(960, 0, 1000), grid, new RoadsidePlacement(new System.Random(1)));
            Stock(city, 25, 12);

            city.StartDay();

            Assert.AreEqual(1, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(1, grid.ConnectedHousesOf(SocialClass.Poor));
            Assert.AreEqual(BuildingOwner.City, grid.Buildings[1].Owner);
            Assert.AreEqual(1, city.CityHouses);
            Assert.AreEqual(1140, city.Population.Room);
        }

        [Test]
        public void BuildCityHouses_BuildsNothing_WhenTheGridHasNoPlace()
        {
            CityGrid grid = Grid(1, 1);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint,
                new GridPlace(Vector2Int.zero, BuildingOrientation.Along));
            City city = GridCity(new CityPopulation(960, 0, 1000), grid, new RandomPlacement(new System.Random(1)));
            Stock(city, 25, 12);

            for (int day = 0; day < 5; day++)
            {
                city.StartDay();
            }

            Assert.AreEqual(1000, city.Population.Room);
            Assert.AreEqual(0, city.CityHouses);
            Assert.AreEqual(25, city.Market.AvailableOf(Timber));
            Assert.AreEqual(12, city.Market.AvailableOf(Metal));
        }

        [Test]
        public void BuildCityHouses_TakesNoMaterial_WhenThereIsNoPlace()
        {
            CityGrid grid = Grid(1, 1);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint,
                new GridPlace(Vector2Int.zero, BuildingOrientation.Along));
            City city = GridCity(new CityPopulation(960, 0, 1000), grid, new RandomPlacement(new System.Random(1)));
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(10, city.Market.AvailableOf(Timber));
            Assert.AreEqual(10, city.Market.AvailableOf(Metal));
        }

        [Test]
        public void Room_IsAlwaysTheConnectedHousesOnTheGrid()
        {
            // A road along x = 3 joins the town hall; the two starting houses touch it, a third does not yet.
            CityGrid grid = TestMarkets.GridWithTownHall(8, 8);
            grid.PlaceRoads(new[] { new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(3, 2) }, BuildingOwner.City);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint,
                new GridPlace(new Vector2Int(4, 0), BuildingOrientation.Along));
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint,
                new GridPlace(new Vector2Int(4, 1), BuildingOrientation.Along));
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint,
                new GridPlace(new Vector2Int(7, 7), BuildingOrientation.Along));
            var population = new CityPopulation(new PerClass<int>(0, 0, 270), 0, new PerClass<int>(0, 0, 280));
            City city = GridCity(population, grid, new RoadsidePlacement(new System.Random(1)));
            Stock(city, 10, 10);

            city.StartDay();
            city.AddPlayerHouse(SocialClass.Poor, 140, HouseFootprint, new GridPlace(new Vector2Int(4, 3), BuildingOrientation.Along));

            Assert.AreEqual(1, city.CityHouses);
            Assert.AreEqual(5, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(4, grid.ConnectedHousesOf(SocialClass.Poor));
            Assert.AreEqual(grid.ConnectedHousesOf(SocialClass.Poor) * 140, city.Population.RoomOf(SocialClass.Poor));
        }

        [Test]
        public void Constructor_Throws_ForAGridWithoutStrategy()
        {
            var population = new CityPopulation(960, 0, 1000);

            Assert.Throws<ArgumentException>(() => GridCity(population, Grid(2, 2), null));
            Assert.Throws<ArgumentException>(() => new City(new[] { Material, Material }, TestMarkets.Thresholds,
                TestMarkets.Workforce, 0, TestMarkets.Pricing, 7, population, TestMarkets.Labour, new[] { GridHousing }, null,
                null, new RandomPlacement(new System.Random(1))));
        }

        [Test]
        public void Constructor_Throws_ForAGridWithAHousingRuleWithoutFootprint()
        {
            Assert.Throws<ArgumentException>(() => new City(new[] { Material, Material }, TestMarkets.Thresholds,
                TestMarkets.Workforce, 0, TestMarkets.Pricing, 7, new CityPopulation(960, 0, 1000), TestMarkets.Labour,
                new[] { Housing }, null, Grid(2, 2), new RandomPlacement(new System.Random(1))));
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
