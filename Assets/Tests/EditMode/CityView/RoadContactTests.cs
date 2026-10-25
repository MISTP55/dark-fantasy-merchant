using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class RoadContactTests
    {
        private static readonly Footprint One = new Footprint(1, 1);
        private static readonly Footprint House = new Footprint(2, 1);

        private static GridPlace At(int x, int y)
        {
            return new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
        }

        private static CityGrid SidesOnly(int size = 12)
        {
            return new CityGrid(size, size, new Vector2Int[0], RoadContact.SidesOnly);
        }

        private static void Hall(CityGrid grid, int x, int y)
        {
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2), At(x, y));
        }

        private static void Road(CityGrid grid, int x, int y)
        {
            grid.Place(BuildingKind.Road(), BuildingOwner.Player, One, At(x, y));
        }

        private static PlacedBuilding NewHouse(CityGrid grid, int x, int y)
        {
            return grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, House, At(x, y));
        }

        private static Vector2Int[] CellsOf(int x, int y)
        {
            return new[] { new Vector2Int(x, y), new Vector2Int(x + 1, y) };
        }

        private static bool ShareASide(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        }

        [Test]
        public void NewGrid_TouchesBySidesAndCorners()
        {
            Assert.AreEqual(RoadContact.SidesAndCorners, new CityGrid(4, 4, new Vector2Int[0]).Contact);
        }

        [Test]
        public void SidesOnly_RoadTouchingTownHallByCorner_IsNotNetworkRoad()
        {
            var grid = SidesOnly();
            Hall(grid, 4, 4);
            Road(grid, 7, 6);
            Assert.IsFalse(grid.IsNetworkRoad(new Vector2Int(7, 6)));
            Assert.IsFalse(grid.JoinsNetwork(new Vector2Int(3, 3)));
        }

        [Test]
        public void SidesOnly_RoadBesideTownHall_IsNetworkRoad()
        {
            var grid = SidesOnly();
            Hall(grid, 4, 4);
            Road(grid, 7, 5);
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(7, 5)));
        }

        [Test]
        public void SidesOnly_RoadChainByCorner_StopsAtTheCorner()
        {
            var grid = SidesOnly();
            Hall(grid, 4, 4);
            Road(grid, 7, 5);
            Road(grid, 8, 6);
            Assert.IsFalse(grid.IsNetworkRoad(new Vector2Int(8, 6)));
        }

        [Test]
        public void SidesOnly_HouseTouchingNetworkRoadByCorner_IsNotConnected()
        {
            var grid = SidesOnly();
            Hall(grid, 4, 4);
            Road(grid, 7, 5);
            PlacedBuilding house = NewHouse(grid, 8, 6);
            Assert.IsFalse(house.IsConnected);
        }

        [Test]
        public void SidesOnly_HouseBesideNetworkRoad_IsConnected()
        {
            var grid = SidesOnly();
            Hall(grid, 4, 4);
            Road(grid, 7, 5);
            PlacedBuilding house = NewHouse(grid, 7, 6);
            Assert.IsTrue(house.IsConnected);
        }

        [Test]
        public void SidesOnly_TryFindRoads_StepsBySidesAndConnects()
        {
            var grid = SidesOnly();
            Hall(grid, 1, 1);
            Vector2Int[] house = CellsOf(7, 7);
            var roads = new List<Vector2Int>();

            Assert.IsTrue(RoadPlanner.TryFindRoads(grid, house, roads));
            for (int i = 1; i < roads.Count; i++)
            {
                Assert.IsTrue(ShareASide(roads[i - 1], roads[i]), $"{roads[i - 1]} and {roads[i]} share no side");
            }

            Assert.IsTrue(RoadPlanner.Connects(grid, house, roads));
            foreach (Vector2Int road in roads)
            {
                Road(grid, road.x, road.y);
            }

            Assert.IsTrue(NewHouse(grid, 7, 7).IsConnected);
        }

        [Test]
        public void SidesOnly_DistancesFromNetwork_CountsStepsBySides()
        {
            var grid = SidesOnly(8);
            Hall(grid, 0, 0);
            var distances = new int[64];
            RoadPlanner.DistancesFromNetwork(grid, distances);

            // (3, 2) is diagonal to the hall's corner (2, 1): two side steps away.
            Assert.AreEqual(1, distances[2 * 8 + 2]);
            Assert.AreEqual(2, distances[2 * 8 + 3]);
        }

        [Test]
        public void SidesOnly_Connects_DiagonalRoads_IsFalse()
        {
            var grid = SidesOnly();
            Hall(grid, 4, 4);
            var roads = new List<Vector2Int> { new Vector2Int(7, 5), new Vector2Int(8, 6) };
            Assert.IsFalse(RoadPlanner.Connects(grid, CellsOf(9, 7), roads));
        }

        [Test]
        public void SidesOnly_RoadsidePlacement_ReturnsTheFewestRoadsThatConnect([NUnit.Framework.Range(0, 9)] int seed)
        {
            // Houses around the hall leave only places that need roads.
            var grid = SidesOnly();
            Hall(grid, 4, 4);
            NewHouse(grid, 4, 3);
            NewHouse(grid, 6, 3);
            NewHouse(grid, 4, 6);
            NewHouse(grid, 6, 6);

            var places = new List<GridPlace>();
            var exact = new List<Vector2Int>();
            int fewest = int.MaxValue;
            grid.GetPlaces(House, places);
            foreach (GridPlace candidate in places)
            {
                if (RoadPlanner.TryFindRoads(grid, CellsOf(House, candidate), exact))
                {
                    fewest = Mathf.Min(fewest, exact.Count);
                }
            }

            var roads = new List<Vector2Int>();
            Assert.IsTrue(new RoadsidePlacement(new System.Random(seed)).TryChoose(grid, House, out GridPlace place, roads));
            Assert.AreEqual(fewest, roads.Count);

            foreach (Vector2Int road in roads)
            {
                Road(grid, road.x, road.y);
            }

            PlacedBuilding house = grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, House, place);
            Assert.IsTrue(house.IsConnected);
        }

        private static List<Vector2Int> CellsOf(Footprint footprint, GridPlace place)
        {
            Vector2Int size = footprint.SizeFor(place.Orientation);
            var cells = new List<Vector2Int>();
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    cells.Add(new Vector2Int(place.Origin.x + x, place.Origin.y + y));
                }
            }

            return cells;
        }
    }
}
