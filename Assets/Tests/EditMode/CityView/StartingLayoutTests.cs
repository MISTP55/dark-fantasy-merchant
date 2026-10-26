using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class StartingLayoutTests
    {
        private static readonly PortFootprint Port = new PortFootprint(new Footprint(4, 3), 2);
        private static readonly Footprint TownHall = new Footprint(3, 2);
        private static readonly PerClass<Footprint> Houses = new PerClass<Footprint>(new Footprint(2, 1), new Footprint(2, 1), new Footprint(2, 1));

        private static bool TryBuild(
            PortFacing shore, PerClass<int> houses, out CityGrid grid, out string problem, int size = 40, int wallSide = 25)
        {
            return StartingLayout.TryBuild(
                size, size, shore, 4, 6, 20, wallSide, Port, 10, TownHall, houses, Houses, RoadContact.SidesOnly,
                new System.Random(3), out grid, out problem);
        }

        // The outline of the square of `side` cells centred on the town hall, a cell more on the far side.
        private static HashSet<Vector2Int> Outline(PlacedBuilding townHall, int side)
        {
            int x0 = townHall.Place.Origin.x + (townHall.Size.x - side) / 2;
            int y0 = townHall.Place.Origin.y + (townHall.Size.y - side) / 2;
            var cells = new HashSet<Vector2Int>();
            for (int i = 0; i < side; i++)
            {
                cells.Add(new Vector2Int(x0 + i, y0));
                cells.Add(new Vector2Int(x0 + i, y0 + side - 1));
                cells.Add(new Vector2Int(x0, y0 + i));
                cells.Add(new Vector2Int(x0 + side - 1, y0 + i));
            }

            return cells;
        }

        private static bool InBand(PortFacing shore, Vector2Int cell, int size)
        {
            switch (shore)
            {
                case PortFacing.North:
                    return cell.y >= size - 4;
                case PortFacing.South:
                    return cell.y < 4;
                case PortFacing.East:
                    return cell.x >= size - 4;
                default:
                    return cell.x < 4;
            }
        }

        [TestCase(PortFacing.North)]
        [TestCase(PortFacing.East)]
        [TestCase(PortFacing.South)]
        [TestCase(PortFacing.West)]
        public void TryBuild_PutsTheWaterThePortTheTownHallAndConnectedHouses(PortFacing shore)
        {
            Assert.IsTrue(TryBuild(shore, new PerClass<int>(2, 3, 5), out CityGrid grid, out string problem), problem);

            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Assert.AreEqual(InBand(shore, cell, 40), grid.IsForbidden(cell), cell.ToString());
                }
            }

            Assert.IsNotNull(grid.Port);
            Assert.AreEqual(shore, grid.Port.PortFacing);
            Assert.IsNotNull(grid.TownHall);
            Assert.AreEqual(2, grid.ConnectedHousesOf(SocialClass.Rich));
            Assert.AreEqual(3, grid.ConnectedHousesOf(SocialClass.Wealthy));
            Assert.AreEqual(5, grid.ConnectedHousesOf(SocialClass.Poor));
            Assert.IsNull(problem);
        }

        [TestCase(PortFacing.North)]
        [TestCase(PortFacing.East)]
        [TestCase(PortFacing.South)]
        [TestCase(PortFacing.West)]
        public void TryBuild_PlacesTenBerthsAndPasses(PortFacing shore)
        {
            Assert.IsTrue(TryBuild(shore, new PerClass<int>(2, 3, 5), out CityGrid grid, out string problem), problem);

            Assert.AreEqual(10, grid.Berths.Count);
            Assert.IsTrue(HarbourPaths.TryValidate(grid, out string berthProblem), berthProblem);
        }

        [Test]
        public void TryBuild_PutsThePortInTheMiddleOfTheShore()
        {
            Assert.IsTrue(TryBuild(PortFacing.South, new PerClass<int>(0, 0, 0), out CityGrid grid, out _));

            // Its land row on the first row of land, its centre on the middle of the edge.
            Assert.AreEqual(2, grid.Port.Place.Origin.y);
            Assert.AreEqual(18, grid.Port.Place.Origin.x);
        }

        // 4 rows of water then 6 of land: the town hall's near side 10 cells from the edge, its long side along the
        // shore, in the middle of it.
        [TestCase(PortFacing.North, 18, 28)]
        [TestCase(PortFacing.South, 18, 10)]
        [TestCase(PortFacing.East, 28, 18)]
        [TestCase(PortFacing.West, 10, 18)]
        public void TryBuild_PutsTheTownHallInlandOfTheShore(PortFacing shore, int x, int y)
        {
            Assert.IsTrue(TryBuild(shore, new PerClass<int>(0, 0, 0), out CityGrid grid, out string problem), problem);

            Assert.AreEqual(new Vector2Int(x, y), grid.TownHall.Place.Origin);
            Assert.AreEqual(PortFacings.OrientationOf(shore), grid.TownHall.Place.Orientation);
        }

        [TestCase(PortFacing.North)]
        [TestCase(PortFacing.East)]
        [TestCase(PortFacing.South)]
        [TestCase(PortFacing.West)]
        public void TryBuild_WallsTheOutlineOfTheSquare(PortFacing shore)
        {
            Assert.IsTrue(TryBuild(shore, new PerClass<int>(2, 3, 5), out CityGrid grid, out string problem), problem);

            HashSet<Vector2Int> outline = Outline(grid.TownHall, 25);
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    var cell = new Vector2Int(x, y);
                    PlacedBuilding there = grid.BuildingAt(cell);
                    bool wall = there != null && there.Kind.IsWall;
                    Assert.AreEqual(outline.Contains(cell) && !grid.IsForbidden(cell), wall, cell.ToString());
                }
            }

            Assert.IsTrue(grid.HasClosedWalls);
        }

        [TestCase(PortFacing.North)]
        [TestCase(PortFacing.East)]
        [TestCase(PortFacing.South)]
        [TestCase(PortFacing.West)]
        public void TryBuild_PutsEveryBuildingInsideTheWalls(PortFacing shore)
        {
            Assert.IsTrue(TryBuild(shore, new PerClass<int>(2, 3, 5), out CityGrid grid, out string problem), problem);

            Assert.IsTrue(grid.TryValidateWalls(out string wallProblem), wallProblem);
            Assert.IsTrue(grid.KeepsInsideWalls);
        }

        [Test]
        public void TryBuild_Fails_WhenTheWallsDoNotClose()
        {
            // The square leaves the 40 by 40 grid on its land sides.
            Assert.IsFalse(TryBuild(PortFacing.South, new PerClass<int>(0, 0, 0), out CityGrid grid, out string problem, wallSide: 45));
            Assert.IsNull(grid);
            StringAssert.Contains("walls", problem);
        }

        [Test]
        public void TryBuild_Fails_WhenTheHousesDoNotFit()
        {
            Assert.IsFalse(TryBuild(PortFacing.South, new PerClass<int>(0, 0, 500), out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("houses", problem);
        }

        [Test]
        public void TryBuild_Fails_WhenTheHousesDoNotFitInsideTheWalls()
        {
            // 5 by 5 cells inside, 6 of them under the town hall: no room for 10 houses and their roads.
            Assert.IsFalse(TryBuild(PortFacing.South, new PerClass<int>(0, 0, 10), out CityGrid grid, out string problem, wallSide: 7));
            Assert.IsNull(grid);
            StringAssert.Contains("houses", problem);
        }

        [Test]
        public void TryBuild_Fails_WhenThePortHasNoRoom()
        {
            // A grid narrower than the port.
            Assert.IsFalse(TryBuild(PortFacing.South, new PerClass<int>(0, 0, 0), out CityGrid grid, out string problem, 3));
            Assert.IsNull(grid);
            StringAssert.Contains("port", problem);
        }
    }
}
