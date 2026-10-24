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

        [Test]
        public void TryCreateGrid_PlacesTheStartingHousesAsTheCitys()
        {
            layout.SetContent(10, 10, new[] { new Vector2Int(9, 9) }, new[]
            {
                House(SocialClass.Poor, 0, 0, BuildingOrientation.Along),
                House(SocialClass.Rich, 5, 5, BuildingOrientation.Across),
            });

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);

            Assert.AreEqual(10, grid.Width);
            Assert.AreEqual(10, grid.Height);
            Assert.IsTrue(grid.IsForbidden(new Vector2Int(9, 9)));
            Assert.AreEqual(2, grid.Buildings.Count);
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
            layout.SetContent(10, 10, new Vector2Int[0], new[]
            {
                House(SocialClass.Poor, 0, 0, BuildingOrientation.Along),
                House(SocialClass.Wealthy, 1, 0, BuildingOrientation.Along),
            });

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("house", problem);
            StringAssert.Contains("wealthy", problem);
        }

        [Test]
        public void TryCreateGrid_Fails_ForAHouseOnAForbiddenCell()
        {
            layout.SetContent(10, 10, new[] { new Vector2Int(1, 0) }, new[] { House(SocialClass.Poor, 0, 0, BuildingOrientation.Along) });

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("house", problem);
            StringAssert.Contains("poor", problem);
        }

        [Test]
        public void TryCreateGrid_Fails_ForASizeUnderOne()
        {
            layout.SetContent(0, 10, new Vector2Int[0], new CityLayoutDefinition.StartingHouse[0]);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem));
            Assert.IsNull(grid);
            StringAssert.Contains("size", problem);
        }

        [Test]
        public void TryCreateGrid_Fails_ForAForbiddenCellOutside()
        {
            layout.SetContent(4, 4, new[] { new Vector2Int(4, 0) }, new CityLayoutDefinition.StartingHouse[0]);

            Assert.IsFalse(layout.TryCreateGrid(buildings, out _, out string problem));
            StringAssert.Contains("forbidden", problem);
        }
    }
}
