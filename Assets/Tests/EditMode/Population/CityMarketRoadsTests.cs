using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketRoadsTests
    {
        private const int Timber = 0;
        private const int Metal = 1;
        private const int Produced = 2;

        private static readonly Footprint HouseFootprint = new Footprint(1, 1);
        private static readonly Footprint WorkshopFootprint = new Footprint(2, 2);

        // Consumed by no one and produced nowhere: only the houses take them.
        private static MarketGood Material => new MarketGood(10, 0, 0, 0);

        // A house of 140 places for the poor that takes 10 timber and 10 metal, built above 95 % of the room.
        private static CityHousingRule PoorHousing => new CityHousingRule(
            SocialClass.Poor,
            0.95,
            140,
            new[] { new MaterialCost(Timber, 10), new MaterialCost(Metal, 10) },
            HouseFootprint);

        private static GridPlace At(int x, int y)
        {
            return new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
        }

        private static IReadOnlyList<Vector2Int> Cells(params (int x, int y)[] cells)
        {
            var result = new List<Vector2Int>();
            foreach ((int x, int y) in cells)
            {
                result.Add(new Vector2Int(x, y));
            }

            return result;
        }

        private static CityMarket Market(CityGrid grid, CityPopulation population, IPlacementStrategy placement = null)
        {
            return new CityMarket(
                new[] { Material, Material, new MarketGood(10, 2, 30, 0.1) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                0,
                TestMarkets.Pricing,
                7,
                population,
                TestMarkets.Labour,
                new[] { PoorHousing },
                null,
                grid,
                placement ?? new RoadsidePlacement(new System.Random(1)));
        }

        // 100 poor in room for 1 000, and 400 beggars ready to be hired.
        private static CityPopulation Roomy => new CityPopulation(100, 400, 1000);

        [Test]
        public void PlayerHouseUnconnected_GivesNoRoom()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, Roomy);

            market.AddPlayerHouse(SocialClass.Poor, 77, HouseFootprint, At(8, 8));

            Assert.AreEqual(1000, market.Population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(1, market.PlayerHousesOf(SocialClass.Poor));
            Assert.IsFalse(grid.BuildingAt(new Vector2Int(8, 8)).IsConnected);
        }

        [Test]
        public void PlayerHouseConnectedLaterByPlayerRoads_GivesItsRoomOnce()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, Roomy);
            market.AddPlayerHouse(SocialClass.Poor, 77, HouseFootprint, At(5, 0));
            int changes = 0;
            market.Changed += () => changes++;

            market.AddPlayerRoads(Cells((3, 0), (4, 0)));

            Assert.AreEqual(1077, market.Population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(1, changes);
            Assert.AreEqual(BuildingOwner.Player, grid.BuildingAt(new Vector2Int(4, 0)).Owner);

            market.AddPlayerRoads(Cells((4, 1), (5, 1)));

            Assert.AreEqual(1077, market.Population.RoomOf(SocialClass.Poor));
        }

        [Test]
        public void PlayerHouseTouchingTheNetwork_GivesItsRoomAtOnce()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, Roomy);
            market.AddPlayerRoads(Cells((3, 0)));

            market.AddPlayerHouse(SocialClass.Poor, 77, HouseFootprint, At(4, 0));

            Assert.AreEqual(1077, market.Population.RoomOf(SocialClass.Poor));
        }

        [Test]
        public void StartingHouseUnconnected_GetsTheRoomOfItsClassRuleWhenConnected()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint, At(5, 0));
            CityMarket market = Market(grid, Roomy);

            Assert.AreEqual(1000, market.Population.RoomOf(SocialClass.Poor));

            market.AddPlayerRoads(Cells((3, 0), (4, 0)));

            Assert.AreEqual(1140, market.Population.RoomOf(SocialClass.Poor));
        }

        [Test]
        public void StartingHouseConnected_AddsNothing()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            grid.PlaceRoads(Cells((3, 0)), BuildingOwner.City);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, HouseFootprint, At(4, 0));
            CityMarket market = Market(grid, Roomy);

            market.AddPlayerRoads(Cells((5, 0)));

            Assert.AreEqual(1000, market.Population.RoomOf(SocialClass.Poor));
        }

        [Test]
        public void StartingHouseUnconnected_OfAClassWithoutHousingRule_Throws()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            grid.Place(BuildingKind.House(SocialClass.Rich), BuildingOwner.City, HouseFootprint, At(5, 0));

            Assert.Throws<ArgumentException>(() => Market(grid, Roomy));
        }

        [Test]
        public void PlayerWorkshopUnconnected_DoesNotHire()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, Roomy);
            market.AddPlayerWorkshop(Produced, 5, WorkshopFootprint, At(5, 0));

            market.StartDay();

            Assert.AreEqual(0, market.PlayerWorkersOf(Produced));
        }

        [Test]
        public void PlayerWorkshopConnected_HiresOnTheNextDay()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, Roomy);
            market.AddPlayerWorkshop(Produced, 5, WorkshopFootprint, At(5, 0));
            market.StartDay();

            market.AddPlayerRoads(Cells((3, 0), (4, 0)));

            Assert.AreEqual(0, market.PlayerWorkersOf(Produced));

            market.StartDay();

            Assert.AreEqual(5, market.PlayerWorkersOf(Produced));
        }

        [Test]
        public void CityHouse_IsBuiltWithItsRoadsAndIsConnected()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, new CityPopulation(960, 0, 1000));
            market.Put(Timber, 25);
            market.Put(Metal, 12);

            market.StartDay();

            Assert.AreEqual(1, market.CityHouses);
            Assert.AreEqual(1140, market.Population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(15, market.AvailableOf(Timber));
            Assert.AreEqual(2, market.AvailableOf(Metal));

            int roads = 0;
            foreach (PlacedBuilding building in grid.Buildings)
            {
                if (building.Kind.IsHouse)
                {
                    Assert.IsTrue(building.IsConnected);
                    Assert.AreEqual(BuildingOwner.City, building.Owner);
                }
                else if (building.Kind.IsRoad)
                {
                    roads++;
                    Assert.AreEqual(BuildingOwner.City, building.Owner);
                    Assert.IsTrue(grid.IsNetworkRoad(building.Place.Origin));
                }
            }

            // The grid has no road at first: the hall alone connects nothing.
            Assert.GreaterOrEqual(roads, 1);
        }

        [Test]
        public void CityHouse_NetworkWalledIn_NothingTakenNoHouseNoRoad()
        {
            // The hall is walled in by forbidden cells: no road can join it.
            var forbidden = new List<Vector2Int>();
            for (int y = 3; y <= 8; y++)
            {
                for (int x = 3; x <= 9; x++)
                {
                    bool hall = x >= 4 && x <= 6 && y >= 5 && y <= 6;
                    if (!hall)
                    {
                        forbidden.Add(new Vector2Int(x, y));
                    }
                }
            }

            var grid = new CityGrid(14, 12, forbidden);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2), At(4, 5));
            CityMarket market = Market(grid, new CityPopulation(960, 0, 1000));
            market.Put(Timber, 25);
            market.Put(Metal, 12);

            market.StartDay();

            AssertNothingBuilt(market, grid);
        }

        [Test]
        public void CityHouse_PlacedWhereItWouldNotConnect_NothingTakenNoHouseNoRoad()
        {
            // A random place and no road: the house would stand off the network.
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, new CityPopulation(960, 0, 1000), new RandomPlacement(new System.Random(1)));
            market.Put(Timber, 25);
            market.Put(Metal, 12);

            market.StartDay();

            AssertNothingBuilt(market, grid);
        }

        private static void AssertNothingBuilt(CityMarket market, CityGrid grid)
        {
            Assert.AreEqual(0, market.CityHouses);
            Assert.AreEqual(1000, market.Population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(25, market.AvailableOf(Timber));
            Assert.AreEqual(12, market.AvailableOf(Metal));
            Assert.AreEqual(1, grid.Buildings.Count);
        }

        [Test]
        public void AddPlayerRoads_OnlyExistingRoads_Throws()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, Roomy);
            market.AddPlayerRoads(Cells((3, 0), (4, 0)));
            int changes = 0;
            market.Changed += () => changes++;

            Assert.Throws<InvalidOperationException>(() => market.AddPlayerRoads(Cells((4, 0), (3, 0))));

            Assert.AreEqual(3, grid.Buildings.Count);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void AddPlayerRoads_KeepsTheExistingRoadsAndPlacesTheNewOnes()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, Roomy);
            grid.PlaceRoads(Cells((3, 0)), BuildingOwner.City);

            market.AddPlayerRoads(Cells((3, 0), (4, 0), (4, 0)));

            Assert.AreEqual(3, grid.Buildings.Count);
            Assert.AreEqual(BuildingOwner.City, grid.BuildingAt(new Vector2Int(3, 0)).Owner);
            Assert.AreEqual(BuildingOwner.Player, grid.BuildingAt(new Vector2Int(4, 0)).Owner);
        }

        [Test]
        public void AddPlayerRoads_ACellUnderAHouse_ThrowsAndPlacesNothing()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            CityMarket market = Market(grid, Roomy);
            market.AddPlayerHouse(SocialClass.Poor, 77, HouseFootprint, At(5, 0));

            Assert.Throws<InvalidOperationException>(() => market.AddPlayerRoads(Cells((3, 0), (4, 0), (5, 0))));

            Assert.IsNull(grid.BuildingAt(new Vector2Int(3, 0)));
            Assert.IsNull(grid.BuildingAt(new Vector2Int(4, 0)));
            Assert.AreEqual(2, grid.Buildings.Count);
        }

        [Test]
        public void AddPlayerRoads_ACellOffTheGridOrForbidden_Throws()
        {
            var grid = new CityGrid(12, 12, new[] { new Vector2Int(6, 6) });
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2), At(0, 0));
            CityMarket market = Market(grid, Roomy);

            Assert.Throws<InvalidOperationException>(() => market.AddPlayerRoads(Cells((3, 0), (-1, 0))));
            Assert.Throws<InvalidOperationException>(() => market.AddPlayerRoads(Cells((3, 0), (6, 6))));
            Assert.AreEqual(1, grid.Buildings.Count);
        }
    }
}
