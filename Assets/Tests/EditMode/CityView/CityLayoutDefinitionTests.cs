using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityLayoutDefinitionTests
    {
        private readonly List<Object> created = new List<Object>();

        private BuildingsDefinition buildings;
        private CityLayoutDefinition layout;

        [SetUp]
        public void SetUp()
        {
            buildings = Track(TestBuildings.CreateCatalogue());
            layout = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object instance in created)
            {
                Object.DestroyImmediate(instance);
            }

            created.Clear();
        }

        private T Track<T>(T instance) where T : Object
        {
            created.Add(instance);
            return instance;
        }

        private static CityLayoutDefinition.StartingHouse House(SocialClass socialClass, int x, int y, BuildingOrientation orientation)
        {
            return new CityLayoutDefinition.StartingHouse(socialClass, new Vector2Int(x, y), orientation);
        }

        // The 3 by 2 town hall covers x 8..10, y 1..2 in the corner inside the walls, away from the houses of the tests.
        private static readonly GridPlace TownHall = new GridPlace(new Vector2Int(8, 1), BuildingOrientation.Along);

        // Water on the three top rows of the 12 by 12 grids; the port faces north on x 1..4, its land row y 8.
        private static readonly PortSite Port = new PortSite(new Vector2Int(1, 8), PortFacing.North);

        // A water cell on the top edge, beside the port, which ships reach from the edge.
        private static readonly Vector2Int[] Berths = { new Vector2Int(6, 10) };

        // The 2 by 3 trading post site covers x 8..9, y 6..8, inside the walls, away from the houses and water of the tests.
        private static readonly GridPlace Site = new GridPlace(new Vector2Int(8, 6), BuildingOrientation.Along);

        private static IEnumerable<Vector2Int> WithWater(IEnumerable<Vector2Int> cells)
        {
            var all = new List<Vector2Int>(cells);
            for (int y = 9; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    all.Add(new Vector2Int(x, y));
                }
            }

            return all;
        }


        // The 12 by 12 grids are walled on their edge below the water: the land cells of x 0 and 11, and of y 0.
        private const int WallCount = 9 + 9 + 10;

        private static List<Vector2Int> EdgeWalls(int width, int height, IEnumerable<Vector2Int> water)
        {
            var wet = new HashSet<Vector2Int>(water);
            var walls = new List<Vector2Int>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if ((x == 0 || y == 0 || x == width - 1 || y == height - 1) && !wet.Contains(cell))
                    {
                        walls.Add(cell);
                    }
                }
            }

            return walls;
        }

        // The layout's content, its edge walled, its trading post site at Site.
        private void Set(
            int width, int height, IEnumerable<Vector2Int> water, IEnumerable<CityLayoutDefinition.StartingHouse> houses,
            GridPlace? townHall, IEnumerable<Vector2Int> roads, PortSite? port, IEnumerable<Vector2Int> berths)
        {
            SetWithSite(width, height, water, houses, townHall, roads, port, berths, Site);
        }

        private void SetWithSite(
            int width, int height, IEnumerable<Vector2Int> water, IEnumerable<CityLayoutDefinition.StartingHouse> houses,
            GridPlace? townHall, IEnumerable<Vector2Int> roads, PortSite? port, IEnumerable<Vector2Int> berths,
            GridPlace? site)
        {
            var waterCells = new List<Vector2Int>(water);
            layout.SetContent(
                width, height, waterCells, houses, townHall, roads, port, berths, EdgeWalls(width, height, waterCells), site);
        }

        [Test]
        public void NoPort_IsUnusable()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0], null, Berths);

            Assert.IsFalse(layout.HasPort);
            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("no port", problem);
        }

        [Test]
        public void APortThatCannotStand_IsUnusable()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0],
                new PortSite(new Vector2Int(1, 8), PortFacing.South), Berths);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("port cannot stand", problem);
            StringAssert.Contains("back rows land", problem);
        }

        [Test]
        public void ThePort_IsOnTheGrid()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0], Port, Berths);

            Assert.IsTrue(layout.HasPort);
            Assert.AreEqual(Port.Origin, layout.PortSite.Origin);
            Assert.AreEqual(PortFacing.North, layout.PortSite.Facing);
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);
            Assert.IsNotNull(grid.Port);
            Assert.AreEqual(PortFacing.North, grid.Port.PortFacing);
            Assert.AreSame(grid.Port, grid.BuildingAt(new Vector2Int(4, 10)));
            Assert.AreSame(grid.Port, grid.BuildingAt(new Vector2Int(4, 8)));
        }

        [Test]
        public void NoTownHall_IsUnusable()
        {
            Set(12, 12, new Vector2Int[0], new CityLayoutDefinition.StartingHouse[0], null, new Vector2Int[0], null, Berths);

            Assert.IsFalse(layout.HasTownHall);
            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("no town hall", problem);
        }

        [Test]
        public void TheGrid_TakesTheCataloguesRoadContact()
        {
            // The road at (7, 3) touches the town hall's corner (8, 2) only.
            Set(12, 12, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall,
                new[] { new Vector2Int(7, 3) }, Port, Berths);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid corners, out string problem), problem);
            Assert.AreEqual(RoadContact.SidesAndCorners, corners.Contact);
            Assert.IsTrue(corners.IsNetworkRoad(new Vector2Int(7, 3)));

            TestBuildings.SetRoadContact(buildings, RoadContact.SidesOnly);
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid sides, out problem), problem);
            Assert.AreEqual(RoadContact.SidesOnly, sides.Contact);
            Assert.IsFalse(sides.IsNetworkRoad(new Vector2Int(7, 3)));
        }

        [Test]
        public void TownHallAndRoads_AreOnTheGrid()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall,
                new[] { new Vector2Int(7, 1), new Vector2Int(6, 1) }, Port, Berths);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            Assert.IsTrue(layout.HasTownHall);
            Assert.AreSame(grid.TownHall, grid.BuildingAt(new Vector2Int(10, 2)));
            Assert.AreEqual(new Vector2Int(3, 2), grid.TownHall.Size);
            Assert.IsTrue(grid.BuildingAt(new Vector2Int(7, 1)).Kind.IsRoad);
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(7, 1)));
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(6, 1)));
            Assert.AreEqual(BuildingOwner.City, grid.BuildingAt(new Vector2Int(6, 1)).Owner);
        }

        [Test]
        public void ARoadOnTheTownHall_IsUnusable()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall,
                new[] { new Vector2Int(9, 1) }, Port, Berths);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("road at (9, 1)", problem);
        }

        [Test]
        public void StartingHouseOnARoad_IsUnusable()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), new[] { House(SocialClass.Poor, 1, 1, BuildingOrientation.Along) }, TownHall,
                new[] { new Vector2Int(2, 1) }, Port, Berths);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("poor", problem);
        }

        [Test]
        public void StartingHouseTouchingRoads_IsConnected()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), new[] { House(SocialClass.Poor, 1, 1, BuildingOrientation.Along) }, TownHall,
                new[] { new Vector2Int(7, 1), new Vector2Int(6, 1), new Vector2Int(5, 1), new Vector2Int(4, 1), new Vector2Int(3, 1) }, Port, Berths);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            Assert.AreEqual(1, grid.ConnectedHousesOf(SocialClass.Poor));
        }

        [Test]
        public void TryCreateGrid_PlacesTheStartingHousesAsTheCitys()
        {
            Set(12, 12, WithWater(new[] { new Vector2Int(10, 10) }), new[]
            {
                House(SocialClass.Poor, 1, 1, BuildingOrientation.Along),
                House(SocialClass.Rich, 6, 6, BuildingOrientation.Across),
            }, TownHall, new Vector2Int[0], Port, Berths);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            Assert.AreEqual(12, grid.Width);
            Assert.AreEqual(12, grid.Height);
            Assert.IsTrue(grid.IsForbidden(new Vector2Int(10, 10)));
            Assert.AreEqual(4 + WallCount, grid.Buildings.Count);
            Assert.AreEqual(1, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(1, grid.HousesOf(SocialClass.Rich));
            Assert.AreEqual(0, grid.HousesOf(SocialClass.Wealthy));

            PlacedBuilding poor = grid.BuildingAt(new Vector2Int(2, 1));
            Assert.AreEqual(BuildingOwner.City, poor.Owner);
            Assert.AreEqual(new Vector2Int(2, 1), poor.Size);

            // The rich house lies across: 1 wide, 2 deep.
            Assert.IsNotNull(grid.BuildingAt(new Vector2Int(6, 7)));
            Assert.IsNull(grid.BuildingAt(new Vector2Int(7, 6)));
        }

        [Test]
        public void TryCreateGrid_Fails_ForOverlappingHouses()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), new[]
            {
                House(SocialClass.Poor, 1, 1, BuildingOrientation.Along),
                House(SocialClass.Wealthy, 2, 1, BuildingOrientation.Along),
            }, TownHall, new Vector2Int[0], Port, Berths);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("house", problem);
            StringAssert.Contains("wealthy", problem);
        }

        [Test]
        public void TryCreateGrid_Fails_ForAHouseOnAForbiddenCell()
        {
            Set(12, 12, WithWater(new[] { new Vector2Int(2, 1) }), new[] { House(SocialClass.Poor, 1, 1, BuildingOrientation.Along) }, TownHall, new Vector2Int[0], Port, Berths);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("house", problem);
            StringAssert.Contains("poor", problem);
        }

        [Test]
        public void TryCreateGrid_Fails_ForASizeUnderOne()
        {
            Set(0, 12, new Vector2Int[0], new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0], null, Berths);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("size", problem);
        }

        [Test]
        public void TryCreateGrid_Fails_ForAForbiddenCellOutside()
        {
            Set(4, 4, new[] { new Vector2Int(5, 1) }, new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0], null, Berths);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("forbidden", problem);
        }

        private static CityLayoutDefinition.StartingHouse[] NoHouses => new CityLayoutDefinition.StartingHouse[0];

        [Test]
        public void TryCreateGrid_WithoutBerth_FailsNamingIt()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, new Vector2Int[0]);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("berth", problem);
        }

        [Test]
        public void TryCreateGrid_BerthOnLand_Fails()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, new[] { new Vector2Int(6, 4) });

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("berth at (6, 4) cannot stand", problem);
        }

        [Test]
        public void TryCreateGrid_UnreachableBerth_Fails()
        {
            // A water cell closed in by land: no way from the edge.
            Set(
                12, 12, WithWater(new[] { new Vector2Int(9, 5) }), NoHouses, TownHall, new Vector2Int[0], Port,
                new[] { new Vector2Int(6, 10), new Vector2Int(9, 5) });

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("berth at (9, 5) cannot be reached", problem);
        }

        [Test]
        public void TheWalls_AreOnTheGrid()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths);

            CollectionAssert.AreEquivalent(EdgeWalls(12, 12, WithWater(new Vector2Int[0])), layout.Walls);
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);
            foreach (Vector2Int cell in layout.Walls)
            {
                Assert.IsTrue(grid.BuildingAt(cell).Kind.IsWall, cell.ToString());
                Assert.AreEqual(BuildingOwner.City, grid.BuildingAt(cell).Owner);
            }
        }

        [Test]
        public void NoWalls_IsUnusable()
        {
            layout.SetContent(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths, new Vector2Int[0], Site);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("do not close", problem);
        }

        [Test]
        public void ABuildingOutsideTheWalls_IsUnusable()
        {
            // Walls on x 3 and on y 0 between it and the edge leave the column of x 1..2 outside.
            var water = new List<Vector2Int>(WithWater(new Vector2Int[0]));
            var walls = new List<Vector2Int>();
            for (int y = 0; y < 9; y++)
            {
                walls.Add(new Vector2Int(3, y));
                walls.Add(new Vector2Int(11, y));
            }

            for (int x = 4; x < 11; x++)
            {
                walls.Add(new Vector2Int(x, 0));
            }

            // The port on x 4..7, inside, a berth beside it.
            layout.SetContent(12, 12, water, new[] { House(SocialClass.Poor, 1, 4, BuildingOrientation.Across) }, TownHall,
                new Vector2Int[0], new PortSite(new Vector2Int(4, 8), PortFacing.North), new[] { new Vector2Int(9, 10) }, walls, Site);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("house at (1, 4) stands outside the walls", problem);
        }

        [Test]
        public void AWallThatCannotStand_IsUnusable()
        {
            var walls = EdgeWalls(12, 12, WithWater(new Vector2Int[0]));
            walls.Add(new Vector2Int(9, 2));
            layout.SetContent(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths, walls, Site);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("wall at (9, 2) cannot stand", problem);

            walls[walls.Count - 1] = new Vector2Int(12, 3);
            layout.SetContent(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths, walls, Site);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out problem));
            StringAssert.Contains("wall at (12, 3) cannot stand", problem);
        }

        [Test]
        public void TheGrid_KeepsInsideTheWalls()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);
            Assert.IsTrue(grid.KeepsInsideWalls);
        }

        [Test]
        public void SetContent_KeepsTheGridsWalls()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths);
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            var copy = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
            copy.SetContent(grid);

            CollectionAssert.AreEquivalent(layout.Walls, copy.Walls);
            Assert.IsTrue(copy.TryCreateGrid(buildings, out _, out problem), problem);
        }

        [Test]
        public void SetContent_KeepsTheGridsBerths()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, new[] { new Vector2Int(6, 10), new Vector2Int(7, 9) });
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            var copy = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
            copy.SetContent(grid);

            CollectionAssert.AreEqual(new[] { new Vector2Int(6, 10), new Vector2Int(7, 9) }, copy.Berths);
            Assert.IsTrue(copy.TryCreateGrid(buildings, out _, out problem), problem);
        }

        [Test]
        public void TryCreateGrid_FailsWithoutATradingPostSite()
        {
            SetWithSite(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths, null);

            Assert.IsFalse(layout.HasTradingPostSite);
            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("no trading post site", problem);
        }

        [Test]
        public void TryCreateGrid_FailsWhenTheSiteIsOnWaterOrABuilding()
        {
            // On the water's first row, then on the town hall, then on a wall.
            foreach (Vector2Int origin in new[] { new Vector2Int(6, 7), new Vector2Int(8, 1), new Vector2Int(0, 3) })
            {
                SetWithSite(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths,
                    new GridPlace(origin, BuildingOrientation.Along));

                Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), origin.ToString());
                Assert.IsNull(grid);
                StringAssert.Contains($"the trading post site at ({origin.x}, {origin.y}) cannot stand", problem);
            }
        }

        [Test]
        public void TryCreateGrid_FailsWhenTheSiteIsOutsideTheWalls()
        {
            // Walls on x 3 and on y 0 between it and the edge leave the column of x 1..2 outside.
            var walls = new List<Vector2Int>();
            for (int y = 0; y < 9; y++)
            {
                walls.Add(new Vector2Int(3, y));
                walls.Add(new Vector2Int(11, y));
            }

            for (int x = 4; x < 11; x++)
            {
                walls.Add(new Vector2Int(x, 0));
            }

            layout.SetContent(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0],
                new PortSite(new Vector2Int(4, 8), PortFacing.North), new[] { new Vector2Int(9, 10) }, walls,
                new GridPlace(new Vector2Int(1, 2), BuildingOrientation.Along));

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("the trading post site at (1, 2) is outside its walls", problem);
        }

        [Test]
        public void TryCreateGrid_ReservesTheSite()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths);

            Assert.IsTrue(layout.HasTradingPostSite);
            Assert.AreEqual(Site, layout.TradingPostSite);
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);
            Assert.AreEqual(Site, grid.TradingPostSite);
            Assert.AreEqual(buildings.TradingPost.Footprint, grid.TradingPostFootprint);
            for (int y = 6; y < 9; y++)
            {
                for (int x = 8; x < 10; x++)
                {
                    Assert.IsTrue(grid.IsReserved(new Vector2Int(x, y)), $"({x}, {y})");
                }
            }

            Assert.IsFalse(grid.IsReserved(new Vector2Int(10, 6)));
            Assert.IsNull(grid.TradingPost);
        }

        [Test]
        public void SetContent_FromAGrid_KeepsItsTradingPostSite()
        {
            Set(12, 12, WithWater(new Vector2Int[0]), NoHouses, TownHall, new Vector2Int[0], Port, Berths);
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            var copy = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
            copy.SetContent(grid);

            Assert.IsTrue(copy.HasTradingPostSite);
            Assert.AreEqual(Site, copy.TradingPostSite);
            Assert.IsTrue(copy.TryCreateGrid(buildings, out _, out problem), problem);
        }
    }
}
