using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class StartingHousesTests
    {
        private static PerClass<Footprint> Footprints() =>
            new PerClass<Footprint>(new Footprint(2, 1), new Footprint(2, 1), new Footprint(2, 1));

        [Test]
        public void TryPlace_PlacesTheHousesOfEachClass()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0]);
            bool placed = StartingHouses.TryPlace(
                grid, new PerClass<int>(1, 2, 3), Footprints(), new RandomPlacement(new System.Random(3)));

            Assert.IsTrue(placed);
            Assert.AreEqual(1, grid.HousesOf(SocialClass.Rich));
            Assert.AreEqual(2, grid.HousesOf(SocialClass.Wealthy));
            Assert.AreEqual(3, grid.HousesOf(SocialClass.Poor));
            foreach (PlacedBuilding building in grid.Buildings)
            {
                Assert.AreEqual(BuildingOwner.City, building.Owner);
            }
        }

        [Test]
        public void TryPlace_IsFalse_WhenTheGridIsTooSmall()
        {
            var grid = new CityGrid(2, 2, new Vector2Int[0]);
            bool placed = StartingHouses.TryPlace(
                grid, new PerClass<int>(0, 0, 3), Footprints(), new RandomPlacement(new System.Random(3)));

            Assert.IsFalse(placed);
            Assert.AreEqual(2, grid.Buildings.Count);
        }

        [Test]
        public void WithRoadsidePlacement_EveryHouseIsConnected()
        {
            var grid = new CityGrid(30, 30, new Vector2Int[0]);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2),
                new GridPlace(new Vector2Int(13, 14), BuildingOrientation.Along));

            bool placed = StartingHouses.TryPlace(
                grid, new PerClass<int>(2, 4, 8), Footprints(), new RoadsidePlacement(new System.Random(5)));

            Assert.IsTrue(placed);
            Assert.AreEqual(14, grid.ConnectedHousesOf(SocialClass.Rich) + grid.ConnectedHousesOf(SocialClass.Wealthy)
                + grid.ConnectedHousesOf(SocialClass.Poor));
            foreach (PlacedBuilding building in grid.Buildings)
            {
                Assert.AreEqual(BuildingOwner.City, building.Owner);
            }
        }
    }
}
