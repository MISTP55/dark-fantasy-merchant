using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityGridNetworkTests
    {
        private static readonly Footprint One = new Footprint(1, 1);
        private static readonly Footprint House = new Footprint(2, 1);

        private static GridPlace At(int x, int y, BuildingOrientation orientation = BuildingOrientation.Along)
        {
            return new GridPlace(new Vector2Int(x, y), orientation);
        }

        private static CityGrid Empty(int size = 12)
        {
            return new CityGrid(size, size, new Vector2Int[0]);
        }

        private static PlacedBuilding Hall(CityGrid grid, int x, int y)
        {
            return grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(2, 2), At(x, y));
        }

        private static PlacedBuilding Road(CityGrid grid, int x, int y)
        {
            return grid.Place(BuildingKind.Road(), BuildingOwner.Player, One, At(x, y));
        }

        private static PlacedBuilding NewHouse(CityGrid grid, int x, int y)
        {
            return grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, House, At(x, y));
        }

        [Test]
        public void RoadNextToTownHall_IsNetworkRoad()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            Road(grid, 6, 4);
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(6, 4)));
        }

        [Test]
        public void RoadTouchingTownHallOnlyByCorner_IsNetworkRoad()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            Road(grid, 6, 6);
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(6, 6)));
        }

        [Test]
        public void RoadChainByCorners_IsNetworkRoad()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            Road(grid, 6, 6);
            Road(grid, 7, 7);
            Road(grid, 8, 8);
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(8, 8)));
        }

        [Test]
        public void RoadWithoutTownHall_IsNotNetworkRoad()
        {
            var grid = Empty();
            Road(grid, 3, 3);
            Assert.IsFalse(grid.IsNetworkRoad(new Vector2Int(3, 3)));
            Assert.IsFalse(grid.IsNetworkRoad(new Vector2Int(5, 5)));
        }

        [Test]
        public void HouseTouchingOnlyTownHall_IsNotConnected()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            PlacedBuilding house = NewHouse(grid, 6, 4);
            Assert.IsFalse(house.IsConnected);
            Assert.AreEqual(0, grid.ConnectedHousesOf(SocialClass.Poor));
        }

        [Test]
        public void HouseTouchingNetworkRoadByCorner_IsConnectedWhenPlaced()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            Road(grid, 6, 4);
            var log = new List<string>();
            grid.BuildingPlaced += b => log.Add("placed");
            grid.BuildingConnected += b => log.Add("connected");
            PlacedBuilding house = NewHouse(grid, 7, 5);
            Assert.IsTrue(house.IsConnected);
            CollectionAssert.AreEqual(new[] { "placed", "connected" }, log);
        }

        [Test]
        public void HousePlacedFirst_GetsConnectedByTheLastRoadOfAChain()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            PlacedBuilding house = NewHouse(grid, 9, 7);
            var connected = new List<PlacedBuilding>();
            grid.BuildingConnected += connected.Add;
            Road(grid, 6, 4);
            Road(grid, 7, 5);
            Assert.AreEqual(0, connected.Count);
            Assert.IsFalse(house.IsConnected);
            PlacedBuilding last = Road(grid, 8, 6);
            Assert.IsFalse(last.IsConnected);
            CollectionAssert.AreEqual(new[] { house }, connected);
            Assert.IsTrue(house.IsConnected);
        }

        [Test]
        public void TownHallPlacedAfterRoads_ConnectsEverythingAlreadyTouchingThem()
        {
            var grid = Empty();
            Road(grid, 6, 4);
            Road(grid, 7, 4);
            PlacedBuilding first = NewHouse(grid, 7, 5);
            PlacedBuilding second = NewHouse(grid, 8, 3);
            var connected = new List<PlacedBuilding>();
            grid.BuildingConnected += connected.Add;
            Hall(grid, 4, 4);
            CollectionAssert.AreEqual(new[] { first, second }, connected);
            Assert.IsTrue(first.IsConnected);
            Assert.IsTrue(second.IsConnected);
        }

        [Test]
        public void SecondTownHall_Throws()
        {
            var grid = Empty();
            Hall(grid, 0, 0);
            Assert.AreSame(grid.Buildings[0], grid.TownHall);
            Assert.Throws<InvalidOperationException>(() => Hall(grid, 5, 5));
        }

        [Test]
        public void RoadOfTwoCells_Throws()
        {
            var grid = Empty();
            Assert.Throws<ArgumentException>(() =>
                grid.Place(BuildingKind.Road(), BuildingOwner.Player, new Footprint(2, 1), At(0, 0)));
        }

        [Test]
        public void ConnectedHousesOf_CountsOnlyConnectedHouses()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            Road(grid, 6, 4);
            NewHouse(grid, 7, 4);
            NewHouse(grid, 0, 0);
            Assert.AreEqual(2, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(1, grid.ConnectedHousesOf(SocialClass.Poor));
            Assert.AreEqual(0, grid.ConnectedHousesOf(SocialClass.Rich));
        }

        [Test]
        public void WouldConnect_TrueOnlyWhenTheFootprintTouchesANetworkRoad()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            Road(grid, 6, 4);
            Road(grid, 0, 0);
            Assert.IsTrue(grid.WouldConnect(House, At(7, 5)));
            Assert.IsTrue(grid.WouldConnect(House, At(6, 3)));
            Assert.IsFalse(grid.WouldConnect(House, At(8, 4)));
            Assert.IsFalse(grid.WouldConnect(House, At(1, 1)));
            Assert.IsFalse(grid.WouldConnect(House, At(2, 4)));
        }

        [Test]
        public void JoinsNetwork_TrueNextToTownHallOrNetworkRoad()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            Road(grid, 6, 4);
            Assert.IsTrue(grid.JoinsNetwork(new Vector2Int(3, 3)));
            Assert.IsTrue(grid.JoinsNetwork(new Vector2Int(7, 5)));
            Assert.IsFalse(grid.JoinsNetwork(new Vector2Int(9, 9)));
        }

        [Test]
        public void PlaceRoads_FloodsOnceAndConnectsInPlacementOrder()
        {
            var grid = Empty();
            Hall(grid, 4, 4);
            PlacedBuilding house = NewHouse(grid, 8, 6);
            var connected = new List<PlacedBuilding>();
            grid.BuildingConnected += connected.Add;
            grid.PlaceRoads(new[] { new Vector2Int(7, 6), new Vector2Int(6, 5), new Vector2Int(6, 4) }, BuildingOwner.Player);
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(7, 6)));
            CollectionAssert.AreEqual(new[] { house }, connected);
        }
    }
}
