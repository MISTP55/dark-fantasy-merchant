using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class BuildingKindTests
    {
        private static readonly BuildingKind[] Every =
        {
            BuildingKind.House(SocialClass.Poor),
            BuildingKind.Workshop(2),
            BuildingKind.Road(),
            BuildingKind.TownHall(),
            BuildingKind.Port(),
            BuildingKind.Wall(),
            BuildingKind.TradingPost(),
        };

        [Test]
        public void EveryKind_HasItsName()
        {
            CollectionAssert.AreEqual(
                new[] { "house", "workshop", "road", "town hall", "port", "wall", "trading post" },
                System.Array.ConvertAll(Every, kind => kind.Name));
        }

        [Test]
        public void TheRoadAndTheTownHall_CarryTheNetwork()
        {
            CollectionAssert.AreEqual(
                new[] { false, false, true, true, false, false, false },
                System.Array.ConvertAll(Every, kind => kind.CarriesNetwork));
        }

        [Test]
        public void TheHouseAndTheWorkshop_AloneNeedAConnection()
        {
            CollectionAssert.AreEqual(
                new[] { true, true, false, false, false, false, false },
                System.Array.ConvertAll(Every, kind => kind.NeedsConnection));
        }

        [Test]
        public void TheRoadAndTheWall_CoverOneCell()
        {
            CollectionAssert.AreEqual(
                new[] { false, false, true, false, false, true, false },
                System.Array.ConvertAll(Every, kind => kind.CoversOneCell));
        }

        [Test]
        public void TheWall_AloneClosesTheWalls()
        {
            CollectionAssert.AreEqual(
                new[] { false, false, false, false, false, true, false },
                System.Array.ConvertAll(Every, kind => kind.ClosesWalls));
        }

        [Test]
        public void AWallBesideANetworkRoad_IsNeverConnected()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(8, 4);
            grid.PlaceRoads(new[] { new Vector2Int(3, 0) }, BuildingOwner.City);
            var connected = new List<PlacedBuilding>();
            grid.BuildingConnected += connected.Add;

            PlacedBuilding wall = grid.Place(BuildingKind.Wall(), BuildingOwner.City, new Footprint(1, 1),
                new GridPlace(new Vector2Int(4, 0), BuildingOrientation.Along));
            grid.PlaceRoads(new[] { new Vector2Int(3, 1) }, BuildingOwner.City);

            Assert.IsFalse(wall.IsConnected);
            CollectionAssert.DoesNotContain(connected, wall);
        }
    }
}
