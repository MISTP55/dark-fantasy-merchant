using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class RoadPlannerTests
    {
        private static readonly Footprint One = new Footprint(1, 1);

        private static GridPlace At(int x, int y)
        {
            return new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
        }

        private static CityGrid Grid(int width, int height, params Vector2Int[] forbidden)
        {
            return new CityGrid(width, height, forbidden);
        }

        private static void Hall(CityGrid grid, int x, int y)
        {
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(2, 2), At(x, y));
        }

        private static Vector2Int[] Cells(params (int x, int y)[] cells)
        {
            var result = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                result[i] = new Vector2Int(cells[i].x, cells[i].y);
            }

            return result;
        }

        [Test]
        public void AlreadyTouchingNetworkRoad_NeedsNoRoad()
        {
            var grid = Grid(12, 12);
            Hall(grid, 4, 4);
            grid.Place(BuildingKind.Road(), BuildingOwner.Player, One, At(6, 4));
            var roads = new List<Vector2Int> { new Vector2Int(9, 9) };

            bool found = RoadPlanner.TryFindRoads(grid, Cells((7, 4), (8, 4)), roads);

            Assert.IsTrue(found);
            Assert.AreEqual(0, roads.Count);
        }

        [Test]
        public void TouchingOnlyTownHall_NeedsOneRoad()
        {
            var grid = Grid(12, 12);
            Hall(grid, 4, 4);
            var roads = new List<Vector2Int>();

            bool found = RoadPlanner.TryFindRoads(grid, Cells((6, 4), (7, 4)), roads);

            Assert.IsTrue(found);
            Assert.AreEqual(1, roads.Count);
            Assert.IsTrue(grid.JoinsNetwork(roads[0]));
        }

        [Test]
        public void ShortestPathAroundAWall()
        {
            var forbidden = new List<Vector2Int>();
            for (int y = 0; y <= 6; y++)
            {
                forbidden.Add(new Vector2Int(4, y));
            }

            var grid = Grid(8, 8, forbidden.ToArray());
            Hall(grid, 0, 0);
            var roads = new List<Vector2Int>();

            bool found = RoadPlanner.TryFindRoads(grid, Cells((6, 0)), roads);

            Assert.IsTrue(found);
            Assert.AreEqual(12, roads.Count);
            Assert.IsTrue(grid.JoinsNetwork(roads[0]));
            for (int i = 0; i < roads.Count; i++)
            {
                Assert.IsFalse(grid.IsForbidden(roads[i]));
                if (i > 0)
                {
                    Vector2Int step = roads[i] - roads[i - 1];
                    Assert.LessOrEqual(Mathf.Max(Mathf.Abs(step.x), Mathf.Abs(step.y)), 1);
                }
            }
        }

        [Test]
        public void UsesDiagonalSteps()
        {
            var grid = Grid(8, 8);
            Hall(grid, 0, 0);
            var roads = new List<Vector2Int>();

            bool found = RoadPlanner.TryFindRoads(grid, Cells((4, 4)), roads);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { new Vector2Int(2, 2), new Vector2Int(3, 3) }, roads);
        }

        [Test]
        public void PlannedCellsAreBlocked()
        {
            var grid = Grid(7, 2);
            Hall(grid, 0, 0);
            var roads = new List<Vector2Int>();

            bool found = RoadPlanner.TryFindRoads(grid, Cells((2, 0), (2, 1)), roads);

            Assert.IsFalse(found);
        }

        [Test]
        public void WalledIn_ReturnsFalse()
        {
            var forbidden = new List<Vector2Int>();
            for (int y = 5; y <= 7; y++)
            {
                for (int x = 5; x <= 7; x++)
                {
                    if (x != 6 || y != 6)
                    {
                        forbidden.Add(new Vector2Int(x, y));
                    }
                }
            }

            var grid = Grid(8, 8, forbidden.ToArray());
            Hall(grid, 0, 0);
            var roads = new List<Vector2Int> { new Vector2Int(1, 1) };

            bool found = RoadPlanner.TryFindRoads(grid, Cells((6, 6)), roads);

            Assert.IsFalse(found);
            Assert.AreEqual(0, roads.Count);
        }

        [Test]
        public void NoTownHall_ReturnsFalse()
        {
            var grid = Grid(8, 8);
            var roads = new List<Vector2Int>();

            Assert.IsFalse(RoadPlanner.TryFindRoads(grid, Cells((4, 4)), roads));
            Assert.AreEqual(0, roads.Count);
        }

        [Test]
        public void DistancesFromNetwork_OneNextToTownHall_MaxValueOnBuildings()
        {
            var grid = Grid(8, 8);
            Hall(grid, 0, 0);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, new Footprint(2, 1), At(4, 0));
            var distances = new int[64];

            RoadPlanner.DistancesFromNetwork(grid, distances);

            Assert.AreEqual(1, distances[2]);
            Assert.AreEqual(2, distances[3]);
            Assert.AreEqual(6, distances[7 * 8 + 7]);
            Assert.AreEqual(int.MaxValue, distances[0]);
            Assert.AreEqual(int.MaxValue, distances[4]);
        }

        // The hall covers (0, 0)-(1, 1); the building is the cell (5, 0).
        [Test]
        public void Connects_RoadsFromTheHallToTheBuilding()
        {
            var grid = Grid(8, 8);
            Hall(grid, 0, 0);

            Assert.IsTrue(RoadPlanner.Connects(grid, Cells((5, 0)), Cells((2, 0), (3, 1), (4, 1))));
        }

        [Test]
        public void Connects_NoRoads_IsWhetherTheBuildingTouchesTheNetwork()
        {
            var grid = Grid(8, 8);
            Hall(grid, 0, 0);

            Assert.IsFalse(RoadPlanner.Connects(grid, Cells((2, 0)), Cells()));

            grid.PlaceRoads(Cells((2, 0)), BuildingOwner.City);

            Assert.IsTrue(RoadPlanner.Connects(grid, Cells((3, 0)), Cells()));
        }

        [Test]
        public void Connects_ARoadThatJoinsNothing_IsFalse()
        {
            var grid = Grid(8, 8);
            Hall(grid, 0, 0);

            // (4, 1) is listed before the road it would touch.
            Assert.IsFalse(RoadPlanner.Connects(grid, Cells((5, 0)), Cells((2, 0), (4, 1), (3, 1))));
            Assert.IsFalse(RoadPlanner.Connects(grid, Cells((5, 0)), Cells((4, 0))));
        }

        [Test]
        public void Connects_RoadsThatStopShortOfTheBuilding_IsFalse()
        {
            var grid = Grid(8, 8);
            Hall(grid, 0, 0);

            Assert.IsFalse(RoadPlanner.Connects(grid, Cells((6, 0)), Cells((2, 0), (3, 0))));
        }

        [Test]
        public void Connects_ARoadThatCannotBeLaid_IsFalse()
        {
            var grid = Grid(8, 8, new Vector2Int(3, 0));
            Hall(grid, 0, 0);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, One, At(3, 1));

            // Forbidden, under a building, under the building itself, twice, off the grid.
            Assert.IsFalse(RoadPlanner.Connects(grid, Cells((4, 0)), Cells((2, 0), (3, 0))));
            Assert.IsFalse(RoadPlanner.Connects(grid, Cells((4, 1)), Cells((2, 1), (3, 1))));
            Assert.IsFalse(RoadPlanner.Connects(grid, Cells((2, 0)), Cells((2, 0))));
            Assert.IsFalse(RoadPlanner.Connects(grid, Cells((3, 2)), Cells((2, 2), (2, 2))));
            Assert.IsFalse(RoadPlanner.Connects(grid, Cells((0, 2)), Cells((-1, 2))));
        }
    }
}
