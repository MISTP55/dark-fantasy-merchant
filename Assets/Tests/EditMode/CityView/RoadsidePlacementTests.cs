using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class RoadsidePlacementTests
    {
        private static readonly Footprint House = new Footprint(2, 1);
        private readonly List<Vector2Int> roads = new List<Vector2Int>();

        private static GridPlace At(int x, int y)
        {
            return new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
        }

        private static CityGrid GridWithHall(int width, int height, int hallX, int hallY, params Vector2Int[] forbidden)
        {
            var grid = new CityGrid(width, height, forbidden);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2), At(hallX, hallY));
            return grid;
        }

        [Test]
        public void PrefersAPlaceTouchingTheNetwork_NoRoad()
        {
            var grid = GridWithHall(14, 14, 5, 6);
            grid.PlaceRoads(new[] { new Vector2Int(8, 6) }, BuildingOwner.City);
            for (int seed = 0; seed < 30; seed++)
            {
                Assert.IsTrue(new RoadsidePlacement(new System.Random(seed)).TryChoose(grid, House, out GridPlace place, roads));
                Assert.AreEqual(0, roads.Count, $"seed {seed}");
                Assert.IsTrue(grid.WouldConnect(House, place), $"seed {seed}");
            }
        }

        // A 3x2 town hall at (4, 5) ringed by forbidden cells, but for the pocket (3, 4): it touches the hall by
        // a corner and can be reached only diagonally, so no house covers it and no house touches the hall.
        private static CityGrid PocketGrid()
        {
            var forbidden = new List<Vector2Int>();
            for (int y = 4; y <= 7; y++)
            {
                for (int x = 3; x <= 7; x++)
                {
                    bool hall = x >= 4 && x <= 6 && y >= 5 && y <= 6;
                    if (!hall && !(x == 3 && y == 4))
                    {
                        forbidden.Add(new Vector2Int(x, y));
                    }
                }
            }

            forbidden.Add(new Vector2Int(2, 4));
            forbidden.Add(new Vector2Int(3, 3));
            return GridWithHall(12, 12, 4, 5, forbidden.ToArray());
        }

        [Test]
        public void WhenNoneTouches_ReturnsTheShortestRoads()
        {
            var grid = PocketGrid();
            for (int seed = 0; seed < 20; seed++)
            {
                Assert.IsTrue(new RoadsidePlacement(new System.Random(seed)).TryChoose(grid, House, out _, roads));
                CollectionAssert.AreEqual(new[] { new Vector2Int(3, 4) }, roads, $"seed {seed}");
            }
        }

        [Test]
        public void SameSeed_SamePlace()
        {
            var grid = GridWithHall(20, 20, 8, 9);
            new RoadsidePlacement(new System.Random(9)).TryChoose(grid, House, out GridPlace first, roads);
            var firstRoads = new List<Vector2Int>(roads);
            new RoadsidePlacement(new System.Random(9)).TryChoose(grid, House, out GridPlace second, roads);
            Assert.AreEqual(first.Origin, second.Origin);
            Assert.AreEqual(first.Orientation, second.Orientation);
            CollectionAssert.AreEqual(firstRoads, roads);
        }

        [Test]
        public void NoTownHall_ReturnsFalse()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0]);
            roads.Add(new Vector2Int(1, 1));
            Assert.IsFalse(new RoadsidePlacement(new System.Random(1)).TryChoose(grid, House, out _, roads));
            Assert.AreEqual(0, roads.Count);
        }

        [Test]
        public void NetworkWalledIn_ReturnsFalse()
        {
            // The hall is walled in by forbidden cells: no free cell touches it, and the rest of the grid is cut off.
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

            var grid = GridWithHall(14, 12, 4, 5, forbidden.ToArray());
            roads.Add(new Vector2Int(1, 1));
            Assert.IsFalse(new RoadsidePlacement(new System.Random(1)).TryChoose(grid, House, out _, roads));
            Assert.AreEqual(0, roads.Count);
        }

        [Test]
        public void WhenTheCheapestRouteCrossesTheHouse_ReturnsTheExactRoads()
        {
            // The only place is (3, 0)-(4, 0), touching the hall. Its nearest touching cell, (5, 1), is 3 roads away
            // through the house's own cells, but with those blocked the way round takes 4.
            var free = new HashSet<Vector2Int>
            {
                new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(2, 2),
                new Vector2Int(3, 3), new Vector2Int(4, 2), new Vector2Int(5, 1),
            };
            var forbidden = new List<Vector2Int>();
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!free.Contains(cell) && !(x <= 2 && y <= 1))
                    {
                        forbidden.Add(cell);
                    }
                }
            }

            CityGrid grid = GridWithHall(6, 4, 0, 0, forbidden.ToArray());

            Assert.IsTrue(new RoadsidePlacement(new System.Random(1)).TryChoose(grid, House, out GridPlace place, roads));
            Assert.AreEqual(new Vector2Int(3, 0), place.Origin);
            CollectionAssert.AreEqual(
                new[] { new Vector2Int(2, 2), new Vector2Int(3, 3), new Vector2Int(4, 2), new Vector2Int(5, 1) }, roads);
        }

        [Test]
        public void ReturnedRoadsConnectTheHouse()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                CityGrid grid = PocketGrid();
                Assert.IsTrue(new RoadsidePlacement(new System.Random(seed)).TryChoose(grid, House, out GridPlace place, roads));
                Assert.AreEqual(1, roads.Count, $"seed {seed}");
                PlacedBuilding house = grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, House, place);
                Assert.IsFalse(house.IsConnected);
                grid.PlaceRoads(roads, BuildingOwner.City);
                Assert.IsTrue(house.IsConnected, $"seed {seed}");
            }
        }
    }
}
