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
            0.95,
            100,
            new[] { new MaterialCost(Timber, 10), new MaterialCost(Metal, 10) });

        private static City NewCity(CityPopulation population, CityHousingRule housing, LabourRule labour)
        {
            return new City(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, population, new CityLabourRules(labour), housing);
        }

        private static City NewCity(CityPopulation population, CityHousingRule housing = null)
        {
            return NewCity(population, housing ?? Housing, TestMarkets.Labour);
        }

        private static void Stock(City city, int timber, int metal)
        {
            if (timber > 0)
            {
                city.Market.TryAdd(Timber, timber);
            }

            if (metal > 0)
            {
                city.Market.TryAdd(Metal, metal);
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
            Assert.AreEqual(15, city.Market.UnitsOf(Timber));
            Assert.AreEqual(2, city.Market.UnitsOf(Metal));
        }

        [Test]
        public void AtTheOccupancyExactly_NothingIsBuilt()
        {
            City city = NewCity(new CityPopulation(950, 0, 1000));
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(1000, city.Population.Room);
            Assert.AreEqual(0, city.CityHouses);
            Assert.AreEqual(10, city.Market.UnitsOf(Timber));
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
            Assert.AreEqual(0, city.Market.AmountOf(Timber));
            Assert.AreEqual(0, city.Market.AmountOf(Metal));
        }

        [Test]
        public void WithoutEnoughOfOneMaterial_NothingIsBuilt_NorTaken()
        {
            City city = NewCity(new CityPopulation(1000, 0, 1000));
            Stock(city, 30, 9);

            city.StartDay();

            Assert.AreEqual(0, city.CityHouses);
            Assert.AreEqual(1000, city.Population.Room);
            Assert.AreEqual(30, city.Market.UnitsOf(Timber));
            Assert.AreEqual(9, city.Market.UnitsOf(Metal));
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
            var housing = new CityHousingRule(0.95, 100, Array.Empty<MaterialCost>());
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

            Assert.AreEqual(0, city.PlayerHouses);
        }

        [Test]
        public void AMarketWithoutLabour_NeverBuilds()
        {
            City city = new City(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(1000), null, Housing);
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(0, city.CityHouses);
            Assert.AreEqual(1000, city.Population.Room);
        }

        [Test]
        public void AMarketWithoutHousing_NeverBuilds()
        {
            City city = new City(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(1000, 0, 1000), TestMarkets.LabourRules);
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(0, city.CityHouses);
        }

        [Test]
        public void AMaterialOutsideTheCatalogue_IsRefused()
        {
            var housing = new CityHousingRule(0.95, 100, new[] { new MaterialCost(2, 10) });

            Assert.Throws<ArgumentOutOfRangeException>(() => NewCity(new CityPopulation(1000), housing));
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

        private static readonly Footprint HouseFootprint = new Footprint(1, 1);

        private static CityHousingRule GridHousing => new CityHousingRule(
            0.95,
            140,
            new[] { new MaterialCost(Timber, 10), new MaterialCost(Metal, 10) },
            HouseFootprint);

        private static City GridCity(CityPopulation population, CityGrid grid, IPlacementStrategy placement)
        {
            return new City(new[] { Material, Material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, population, TestMarkets.LabourRules, GridHousing,
                new CityGridSetup(grid, placement));
        }

        private static CityGrid Grid(int width, int height) => new CityGrid(width, height, new Vector2Int[0]);

        [Test]
        public void BuildCityHouses_PlacesTheHouseOnTheGrid()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(5, 5);
            City city = GridCity(new CityPopulation(960, 0, 1000), grid, new RoadsidePlacement(new System.Random(1)));
            Stock(city, 25, 12);

            city.StartDay();

            Assert.AreEqual(1, grid.CountHouses());
            Assert.AreEqual(1, grid.CountConnectedHouses());
            Assert.AreEqual(BuildingOwner.City, grid.Buildings[1].Owner);
            Assert.AreEqual(1, city.CityHouses);
            Assert.AreEqual(1140, city.Population.Room);
        }

        [Test]
        public void BuildCityHouses_BuildsNothing_WhenTheGridHasNoPlace()
        {
            CityGrid grid = Grid(1, 1);
            grid.Place(BuildingKind.House(), BuildingOwner.City, HouseFootprint,
                new GridPlace(Vector2Int.zero, BuildingOrientation.Along));
            City city = GridCity(new CityPopulation(960, 0, 1000), grid, new RandomPlacement(new System.Random(1)));
            Stock(city, 25, 12);

            for (int day = 0; day < 5; day++)
            {
                city.StartDay();
            }

            Assert.AreEqual(1000, city.Population.Room);
            Assert.AreEqual(0, city.CityHouses);
            Assert.AreEqual(25, city.Market.UnitsOf(Timber));
            Assert.AreEqual(12, city.Market.UnitsOf(Metal));
        }

        [Test]
        public void BuildCityHouses_TakesNoMaterial_WhenThereIsNoPlace()
        {
            CityGrid grid = Grid(1, 1);
            grid.Place(BuildingKind.House(), BuildingOwner.City, HouseFootprint,
                new GridPlace(Vector2Int.zero, BuildingOrientation.Along));
            City city = GridCity(new CityPopulation(960, 0, 1000), grid, new RandomPlacement(new System.Random(1)));
            Stock(city, 10, 10);

            city.StartDay();

            Assert.AreEqual(10, city.Market.UnitsOf(Timber));
            Assert.AreEqual(10, city.Market.UnitsOf(Metal));
        }

        [Test]
        public void Room_IsAlwaysTheConnectedHousesOnTheGrid()
        {
            // A road along x = 3 joins the town hall; the two starting houses touch it, a third does not yet.
            CityGrid grid = TestMarkets.GridWithTownHall(8, 8);
            grid.PlaceRoads(new[] { new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(3, 2) }, BuildingOwner.City);
            grid.Place(BuildingKind.House(), BuildingOwner.City, HouseFootprint,
                new GridPlace(new Vector2Int(4, 0), BuildingOrientation.Along));
            grid.Place(BuildingKind.House(), BuildingOwner.City, HouseFootprint,
                new GridPlace(new Vector2Int(4, 1), BuildingOrientation.Along));
            grid.Place(BuildingKind.House(), BuildingOwner.City, HouseFootprint,
                new GridPlace(new Vector2Int(7, 7), BuildingOrientation.Along));
            var population = new CityPopulation(270, 0, 280);
            City city = GridCity(population, grid, new RoadsidePlacement(new System.Random(1)));
            Stock(city, 10, 10);

            city.StartDay();
            city.AddPlayerHouse(140, HouseFootprint, new GridPlace(new Vector2Int(4, 3), BuildingOrientation.Along));

            Assert.AreEqual(1, city.CityHouses);
            Assert.AreEqual(5, grid.CountHouses());
            Assert.AreEqual(4, grid.CountConnectedHouses());
            Assert.AreEqual(grid.CountConnectedHouses() * 140, city.Population.Room);
        }

        [Test]
        public void GridSetup_Throws_WithoutAGridOrAStrategy()
        {
            Assert.Throws<ArgumentNullException>(() => new CityGridSetup(Grid(2, 2), null));
            Assert.Throws<ArgumentNullException>(() => new CityGridSetup(null, new RandomPlacement(new System.Random(1))));
        }

        [Test]
        public void Constructor_Throws_ForAGridWithAHousingRuleWithoutFootprint()
        {
            Assert.Throws<ArgumentException>(() => new City(new[] { Material, Material }, TestMarkets.Thresholds,
                TestMarkets.Workforce, 0, TestMarkets.Pricing, 7, new CityPopulation(960, 0, 1000), TestMarkets.LabourRules,
                Housing, new CityGridSetup(Grid(2, 2), new RandomPlacement(new System.Random(1)))));
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
