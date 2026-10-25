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

        // The 3 by 2 town hall covers x 7..9, y 0..1 in the corner of the grid, away from the houses of the tests.
        private static readonly GridPlace TownHall = new GridPlace(new Vector2Int(7, 0), BuildingOrientation.Along);

        // Water on the two top rows of the 10 by 10 grids; the port faces north on x 0..3, its land row y 7.
        private static readonly PortSite Port = new PortSite(new Vector2Int(0, 7), PortFacing.North);

        private static IEnumerable<Vector2Int> WithWater(IEnumerable<Vector2Int> cells)
        {
            var all = new List<Vector2Int>(cells);
            for (int y = 8; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    all.Add(new Vector2Int(x, y));
                }
            }

            return all;
        }

        [Test]
        public void NoPort_IsUnusable()
        {
            layout.SetContent(10, 10, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0], null);

            Assert.IsFalse(layout.HasPort);
            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("no port", problem);
        }

        [Test]
        public void APortThatCannotStand_IsUnusable()
        {
            layout.SetContent(10, 10, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0],
                new PortSite(new Vector2Int(0, 7), PortFacing.South));

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("port cannot stand", problem);
            StringAssert.Contains("back rows land", problem);
        }

        [Test]
        public void ThePort_IsOnTheGrid()
        {
            layout.SetContent(10, 10, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0], Port);

            Assert.IsTrue(layout.HasPort);
            Assert.AreEqual(Port.Origin, layout.PortSite.Origin);
            Assert.AreEqual(PortFacing.North, layout.PortSite.Facing);
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);
            Assert.IsNotNull(grid.Port);
            Assert.AreEqual(PortFacing.North, grid.Port.PortFacing);
            Assert.AreSame(grid.Port, grid.BuildingAt(new Vector2Int(3, 9)));
            Assert.AreSame(grid.Port, grid.BuildingAt(new Vector2Int(3, 7)));
        }

        [Test]
        public void NoTownHall_IsUnusable()
        {
            layout.SetContent(10, 10, new Vector2Int[0], new CityLayoutDefinition.StartingHouse[0], null, new Vector2Int[0], null);

            Assert.IsFalse(layout.HasTownHall);
            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("no town hall", problem);
        }

        [Test]
        public void TheGrid_TakesTheCataloguesRoadContact()
        {
            // The road at (6, 2) touches the town hall's corner (7, 1) only.
            layout.SetContent(10, 10, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall,
                new[] { new Vector2Int(6, 2) }, Port);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid corners, out string problem), problem);
            Assert.AreEqual(RoadContact.SidesAndCorners, corners.Contact);
            Assert.IsTrue(corners.IsNetworkRoad(new Vector2Int(6, 2)));

            TestBuildings.SetRoadContact(buildings, RoadContact.SidesOnly);
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid sides, out problem), problem);
            Assert.AreEqual(RoadContact.SidesOnly, sides.Contact);
            Assert.IsFalse(sides.IsNetworkRoad(new Vector2Int(6, 2)));
        }

        [Test]
        public void TownHallAndRoads_AreOnTheGrid()
        {
            layout.SetContent(10, 10, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall,
                new[] { new Vector2Int(6, 0), new Vector2Int(5, 0) }, Port);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            Assert.IsTrue(layout.HasTownHall);
            Assert.AreSame(grid.TownHall, grid.BuildingAt(new Vector2Int(9, 1)));
            Assert.AreEqual(new Vector2Int(3, 2), grid.TownHall.Size);
            Assert.IsTrue(grid.BuildingAt(new Vector2Int(6, 0)).Kind.IsRoad);
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(6, 0)));
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(5, 0)));
            Assert.AreEqual(BuildingOwner.City, grid.BuildingAt(new Vector2Int(5, 0)).Owner);
        }

        [Test]
        public void ARoadOnTheTownHall_IsUnusable()
        {
            layout.SetContent(10, 10, WithWater(new Vector2Int[0]), new CityLayoutDefinition.StartingHouse[0], TownHall,
                new[] { new Vector2Int(8, 0) }, Port);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("road at (8, 0)", problem);
        }

        [Test]
        public void StartingHouseOnARoad_IsUnusable()
        {
            layout.SetContent(10, 10, WithWater(new Vector2Int[0]), new[] { House(SocialClass.Poor, 0, 0, BuildingOrientation.Along) }, TownHall,
                new[] { new Vector2Int(1, 0) }, Port);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("poor", problem);
        }

        [Test]
        public void StartingHouseTouchingRoads_IsConnected()
        {
            layout.SetContent(10, 10, WithWater(new Vector2Int[0]), new[] { House(SocialClass.Poor, 0, 0, BuildingOrientation.Along) }, TownHall,
                new[] { new Vector2Int(6, 0), new Vector2Int(5, 0), new Vector2Int(4, 0), new Vector2Int(3, 0), new Vector2Int(2, 0) }, Port);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            Assert.AreEqual(1, grid.ConnectedHousesOf(SocialClass.Poor));
        }

        [Test]
        public void TryCreateGrid_PlacesTheStartingHousesAsTheCitys()
        {
            layout.SetContent(10, 10, WithWater(new[] { new Vector2Int(9, 9) }), new[]
            {
                House(SocialClass.Poor, 0, 0, BuildingOrientation.Along),
                House(SocialClass.Rich, 5, 5, BuildingOrientation.Across),
            }, TownHall, new Vector2Int[0], Port);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            Assert.AreEqual(10, grid.Width);
            Assert.AreEqual(10, grid.Height);
            Assert.IsTrue(grid.IsForbidden(new Vector2Int(9, 9)));
            Assert.AreEqual(4, grid.Buildings.Count);
            Assert.AreEqual(1, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(1, grid.HousesOf(SocialClass.Rich));
            Assert.AreEqual(0, grid.HousesOf(SocialClass.Wealthy));

            PlacedBuilding poor = grid.BuildingAt(new Vector2Int(1, 0));
            Assert.AreEqual(BuildingOwner.City, poor.Owner);
            Assert.AreEqual(new Vector2Int(2, 1), poor.Size);

            // The rich house lies across: 1 wide, 2 deep.
            Assert.IsNotNull(grid.BuildingAt(new Vector2Int(5, 6)));
            Assert.IsNull(grid.BuildingAt(new Vector2Int(6, 5)));
        }

        [Test]
        public void TryCreateGrid_Fails_ForOverlappingHouses()
        {
            layout.SetContent(10, 10, WithWater(new Vector2Int[0]), new[]
            {
                House(SocialClass.Poor, 0, 0, BuildingOrientation.Along),
                House(SocialClass.Wealthy, 1, 0, BuildingOrientation.Along),
            }, TownHall, new Vector2Int[0], Port);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("house", problem);
            StringAssert.Contains("wealthy", problem);
        }

        [Test]
        public void TryCreateGrid_Fails_ForAHouseOnAForbiddenCell()
        {
            layout.SetContent(10, 10, WithWater(new[] { new Vector2Int(1, 0) }), new[] { House(SocialClass.Poor, 0, 0, BuildingOrientation.Along) }, TownHall, new Vector2Int[0], Port);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("house", problem);
            StringAssert.Contains("poor", problem);
        }

        [Test]
        public void TryCreateGrid_Fails_ForASizeUnderOne()
        {
            layout.SetContent(0, 10, new Vector2Int[0], new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0], null);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("size", problem);
        }

        [Test]
        public void TryCreateGrid_Fails_ForAForbiddenCellOutside()
        {
            layout.SetContent(4, 4, new[] { new Vector2Int(4, 0) }, new CityLayoutDefinition.StartingHouse[0], TownHall, new Vector2Int[0], null);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("forbidden", problem);
        }
    }
}
