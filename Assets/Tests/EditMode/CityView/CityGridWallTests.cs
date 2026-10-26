using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityGridWallTests
    {
        private static readonly Footprint Cell = new Footprint(1, 1);
        private static readonly Footprint TownHall = new Footprint(3, 2);
        private static readonly Footprint House = new Footprint(2, 1);

        // The ring of the tests: the outline of x 1..8, y 1..8 on a 10 by 10 grid.
        private static readonly RectInt Ring = new RectInt(1, 1, 8, 8);

        private static GridPlace At(int x, int y)
        {
            return new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
        }

        private static IEnumerable<Vector2Int> Outline(RectInt rect)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                for (int y = rect.yMin; y < rect.yMax; y++)
                {
                    if (x == rect.xMin || y == rect.yMin || x == rect.xMax - 1 || y == rect.yMax - 1)
                    {
                        yield return new Vector2Int(x, y);
                    }
                }
            }
        }

        private static void PlaceWall(CityGrid grid, Vector2Int cell)
        {
            grid.Place(BuildingKind.Wall(), BuildingOwner.City, Cell, new GridPlace(cell, BuildingOrientation.Along));
        }

        private static void PlaceWalls(CityGrid grid, IEnumerable<Vector2Int> cells, params Vector2Int[] except)
        {
            var skipped = new HashSet<Vector2Int>(except);
            foreach (Vector2Int cell in cells)
            {
                if (!skipped.Contains(cell))
                {
                    PlaceWall(grid, cell);
                }
            }
        }

        // A 10 by 10 grid, its 3 by 2 town hall at (4, 4), walled by the ring.
        private static CityGrid Walled(IEnumerable<Vector2Int> water = null)
        {
            var grid = new CityGrid(10, 10, water ?? new Vector2Int[0], RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 4));
            PlaceWalls(grid, Outline(Ring));
            return grid;
        }

        [Test]
        public void AWall_IsPlacedAsAOneByOneCityBuilding()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0]);

            PlaceWall(grid, new Vector2Int(2, 3));

            Assert.IsTrue(grid.BuildingAt(new Vector2Int(2, 3)).Kind.IsWall);
            Assert.Throws<ArgumentException>(() =>
                grid.Place(BuildingKind.Wall(), BuildingOwner.City, House, At(5, 5)));
        }

        [Test]
        public void AWall_IsNeitherRoadNorConnected()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 4));
            var connected = new List<PlacedBuilding>();
            grid.BuildingConnected += connected.Add;

            PlaceWall(grid, new Vector2Int(3, 4));

            Assert.IsFalse(grid.IsNetworkRoad(new Vector2Int(3, 4)));
            Assert.IsFalse(grid.BuildingAt(new Vector2Int(3, 4)).IsConnected);
            Assert.IsEmpty(connected);
        }

        [Test]
        public void Enclosure_IsClosed_InsideARing()
        {
            CityGrid grid = Walled();

            Assert.IsTrue(grid.HasClosedWalls);
            Assert.IsTrue(grid.IsEnclosed(new Vector2Int(2, 2)));
            Assert.IsTrue(grid.IsEnclosed(new Vector2Int(5, 4)), "under the town hall");
            Assert.IsFalse(grid.IsEnclosed(new Vector2Int(0, 0)));
            Assert.IsFalse(grid.IsEnclosed(new Vector2Int(1, 1)), "a wall");
        }

        [Test]
        public void Enclosure_IsOpen_WithAGap()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 4));
            PlaceWalls(grid, Outline(Ring), new Vector2Int(8, 5));

            Assert.IsFalse(grid.HasClosedWalls);
            Assert.IsFalse(grid.IsEnclosed(new Vector2Int(5, 5)));
        }

        [Test]
        public void Enclosure_IsOpen_WithoutTownHall()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0]);
            PlaceWalls(grid, Outline(Ring));

            Assert.IsFalse(grid.HasClosedWalls);
            Assert.IsFalse(grid.IsEnclosed(new Vector2Int(5, 5)));
        }

        [Test]
        public void Enclosure_IsOpen_WhenTheTownHallTouchesTheEdge()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(0, 4));
            PlaceWalls(grid, Outline(new RectInt(3, 1, 6, 8)));

            Assert.IsFalse(grid.HasClosedWalls);
        }

        [Test]
        public void Enclosure_WaterCloses()
        {
            var water = new List<Vector2Int>();
            for (int x = 0; x < 10; x++)
            {
                water.Add(new Vector2Int(x, 0));
                water.Add(new Vector2Int(x, 1));
            }

            var grid = new CityGrid(10, 10, water, RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 4));
            for (int y = 2; y <= 8; y++)
            {
                PlaceWall(grid, new Vector2Int(1, y));
                PlaceWall(grid, new Vector2Int(8, y));
            }

            for (int x = 2; x <= 7; x++)
            {
                PlaceWall(grid, new Vector2Int(x, 8));
            }

            Assert.IsTrue(grid.HasClosedWalls);
            Assert.IsTrue(grid.IsEnclosed(new Vector2Int(2, 2)));
            Assert.IsFalse(grid.IsEnclosed(new Vector2Int(4, 1)), "water");
        }

        [Test]
        public void Enclosure_DiagonalWalls_Close()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 4));
            PlaceWalls(grid, Outline(Ring), new Vector2Int(1, 1));
            PlaceWall(grid, new Vector2Int(2, 2));

            // (1, 2) and (2, 1) touch only by a corner across (2, 2): the side-only flood does not pass.
            Assert.IsTrue(grid.HasClosedWalls);
        }

        [Test]
        public void Enclosure_CrossesOtherBuildings()
        {
            CityGrid grid = Walled();

            // A wall of houses between the town hall and the cells on its left.
            for (int y = 2; y <= 7; y++)
            {
                grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, new Footprint(1, 1), At(3, y));
            }

            Assert.IsTrue(grid.IsEnclosed(new Vector2Int(2, 5)));
        }

        [Test]
        public void Enclosure_FollowsWallsPlacedLater()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 4));
            PlaceWalls(grid, Outline(Ring), new Vector2Int(8, 5));

            Assert.IsFalse(grid.HasClosedWalls);

            PlaceWall(grid, new Vector2Int(8, 5));

            Assert.IsTrue(grid.HasClosedWalls);
        }

        [Test]
        public void KeepsInsideWalls_RefusesOutside()
        {
            CityGrid grid = Walled();
            grid.KeepsInsideWalls = true;

            Assert.IsFalse(grid.CanPlace(House, At(0, 0)), "outside");
            Assert.IsTrue(grid.CanPlace(House, At(2, 2)), "inside");
            Assert.IsFalse(grid.CanPlace(House, At(0, 5)), "across the wall");

            var places = new List<GridPlace>();
            grid.GetPlaces(House, places);
            foreach (GridPlace place in places)
            {
                Assert.IsTrue(grid.IsEnclosed(place.Origin), place.Origin.ToString());
            }

            Assert.DoesNotThrow(() => PlaceWall(grid, new Vector2Int(0, 0)), "walls are never refused for the enclosure");
        }

        [Test]
        public void KeepsInsideWalls_Off_PlacesAnywhere()
        {
            CityGrid grid = Walled();

            Assert.IsFalse(grid.KeepsInsideWalls);
            Assert.IsTrue(grid.CanPlace(House, At(0, 0)));
        }

        [Test]
        public void TryValidateWalls_Passes_WhenEverythingIsInside()
        {
            CityGrid grid = Walled();
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, House, At(2, 2));

            Assert.IsTrue(grid.TryValidateWalls(out string problem), problem);
            Assert.IsNull(problem);
        }

        [Test]
        public void TryValidateWalls_NamesTheBuildingOutside()
        {
            CityGrid grid = Walled();
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, new Footprint(1, 1), At(0, 3));

            Assert.IsFalse(grid.TryValidateWalls(out string problem));
            StringAssert.Contains("house", problem);
            StringAssert.Contains("(0, 3)", problem);
            StringAssert.Contains("outside the walls", problem);
        }

        [Test]
        public void TryValidateWalls_ChecksOnlyThePortsLandRows()
        {
            // Water on the two bottom rows; the port faces south on x 3..6, its land row y 2, inside the walls.
            var water = new List<Vector2Int>();
            for (int x = 0; x < 10; x++)
            {
                water.Add(new Vector2Int(x, 0));
                water.Add(new Vector2Int(x, 1));
            }

            var grid = new CityGrid(10, 10, water, RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 5));
            for (int y = 2; y <= 8; y++)
            {
                PlaceWall(grid, new Vector2Int(1, y));
                PlaceWall(grid, new Vector2Int(8, y));
            }

            for (int x = 2; x <= 7; x++)
            {
                PlaceWall(grid, new Vector2Int(x, 8));
            }

            grid.PlacePort(new PortFootprint(new Footprint(4, 3), 2), new PortSite(new Vector2Int(3, 0), PortFacing.South));

            Assert.IsTrue(grid.TryValidateWalls(out string problem), problem);
        }

        [Test]
        public void TryValidateWalls_Open()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 4));

            Assert.IsFalse(grid.TryValidateWalls(out string problem));
            StringAssert.Contains("do not close", problem);
        }

        [Test]
        public void TryFindRoads_StaysInsideTheWalls()
        {
            // Closed walls make every free cell inside a road's; open ones leave none to a grid kept inside them.
            CityGrid closed = Walled();
            closed.KeepsInsideWalls = true;
            var roads = new List<Vector2Int>();

            Assert.IsTrue(RoadPlanner.TryFindRoads(closed, new[] { new Vector2Int(2, 2) }, roads));
            foreach (Vector2Int road in roads)
            {
                Assert.IsTrue(closed.IsEnclosed(road), road.ToString());
            }

            var open = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
            open.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 4));
            PlaceWalls(open, Outline(Ring), new Vector2Int(8, 5));
            open.KeepsInsideWalls = true;

            Assert.IsFalse(RoadPlanner.TryFindRoads(open, new[] { new Vector2Int(2, 2) }, roads));
        }

        [Test]
        public void TryCollectNewRoads_RefusesACellOutsideTheWalls()
        {
            CityGrid grid = Walled();
            grid.KeepsInsideWalls = true;
            var newCells = new List<Vector2Int>();

            Assert.IsFalse(CityMarket.TryCollectNewRoads(grid, new[] { new Vector2Int(3, 4), new Vector2Int(0, 4) }, newCells));
            Assert.IsEmpty(newCells);
            Assert.IsTrue(CityMarket.TryCollectNewRoads(grid, new[] { new Vector2Int(3, 4) }, newCells));
        }
    }
}
