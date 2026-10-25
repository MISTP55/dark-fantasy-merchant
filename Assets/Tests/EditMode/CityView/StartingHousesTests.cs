using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class StartingHousesTests
    {
        private static PerClass<Footprint> Footprints() =>
            new PerClass<Footprint>(new Footprint(2, 1), new Footprint(2, 1), new Footprint(2, 1));

        private static CityGrid GridWithTownHall(int size, RoadContact contact = RoadContact.SidesAndCorners)
        {
            var grid = new CityGrid(size, size, new Vector2Int[0], contact);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2),
                new GridPlace(new Vector2Int(size / 2 - 2, size / 2 - 1), BuildingOrientation.Along));
            return grid;
        }

        private static void AssertEveryHouseIsConnected(CityGrid grid)
        {
            foreach (PlacedBuilding building in grid.Buildings)
            {
                if (building.Kind.IsHouse)
                {
                    Assert.IsTrue(building.IsConnected, building.Place.Origin.ToString());
                }
            }
        }

        [Test]
        public void TryPlace_PlacesTheHousesOfEachClass_AsTheCitys()
        {
            CityGrid grid = GridWithTownHall(20);
            bool placed = StartingHouses.TryPlace(grid, new PerClass<int>(1, 2, 3), Footprints(), new System.Random(3));

            Assert.IsTrue(placed);
            Assert.AreEqual(1, grid.ConnectedHousesOf(SocialClass.Rich));
            Assert.AreEqual(2, grid.ConnectedHousesOf(SocialClass.Wealthy));
            Assert.AreEqual(3, grid.ConnectedHousesOf(SocialClass.Poor));
            foreach (PlacedBuilding building in grid.Buildings)
            {
                Assert.AreEqual(BuildingOwner.City, building.Owner);
            }
        }

        [Test]
        public void TryPlace_IsFalse_WhenTheGridIsTooSmall()
        {
            var grid = new CityGrid(2, 2, new Vector2Int[0]);
            bool placed = StartingHouses.TryPlace(grid, new PerClass<int>(0, 0, 3), Footprints(), new System.Random(3));

            Assert.IsFalse(placed);
            Assert.AreEqual(2, grid.Buildings.Count);
        }

        [Test]
        public void TryPlace_IsFalse_WithoutATownHall()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0]);

            Assert.IsFalse(StartingHouses.TryPlace(grid, new PerClass<int>(0, 0, 1), Footprints(), new System.Random(3)));
        }

        // Houses packed along the roads used to wall the town hall in long before 30 were placed.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TryPlace_ConnectsManyHousesBySides(int seed)
        {
            CityGrid grid = GridWithTownHall(40, RoadContact.SidesOnly);

            Assert.IsTrue(StartingHouses.TryPlace(grid, new PerClass<int>(30, 0, 0), Footprints(), new System.Random(seed)));
            Assert.AreEqual(30, grid.ConnectedHousesOf(SocialClass.Rich));
        }

        [Test]
        public void TryPlace_SpreadsTheHousesOverTheGrid()
        {
            CityGrid grid = GridWithTownHall(40);
            Vector2Int hall = grid.TownHall.Place.Origin;

            Assert.IsTrue(StartingHouses.TryPlace(grid, new PerClass<int>(5, 5, 5), Footprints(), new System.Random(1)));

            float farthest = 0f;
            foreach (PlacedBuilding building in grid.Buildings)
            {
                if (building.Kind.IsHouse)
                {
                    farthest = Mathf.Max(farthest, Vector2Int.Distance(building.Place.Origin, hall));
                }
            }

            Assert.Greater(farthest, 10f);
        }

        [Test]
        public void TryPlace_MovesAHouseNoRoadCanReach_AlongTheRoads()
        {
            // A walled-in pocket on the left, much larger than the town hall's side: a house drawn there gets no road.
            var water = new List<Vector2Int>();
            for (int y = 0; y < 5; y++)
            {
                water.Add(new Vector2Int(5, y));
            }

            for (int seed = 0; seed < 10; seed++)
            {
                var grid = new CityGrid(12, 5, water);
                grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2),
                    new GridPlace(new Vector2Int(6, 0), BuildingOrientation.Along));

                Assert.IsTrue(StartingHouses.TryPlace(grid, new PerClass<int>(0, 0, 1), Footprints(), new System.Random(seed)), seed.ToString());
                AssertEveryHouseIsConnected(grid);
                Assert.AreEqual(1, grid.HousesOf(SocialClass.Poor));
                foreach (PlacedBuilding building in grid.Buildings)
                {
                    Assert.GreaterOrEqual(building.Place.Origin.x, 6, seed.ToString());
                }
            }
        }
    }
}
