using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityLayoutEditingTradingPostTests
    {
        // A 12 by 12 layout with water on the two top rows and the town hall at (4, 0); the site, 2 by 3 Along,
        // is put at (8, 4), covering x 8..9 and y 4..6.
        private static readonly GridPlace Site = new GridPlace(new Vector2Int(8, 4), BuildingOrientation.Along);

        private readonly List<Object> created = new List<Object>();

        private BuildingsDefinition buildings;
        private CityLayoutDefinition layout;

        [SetUp]
        public void SetUp()
        {
            buildings = Track(TestBuildings.CreateCatalogue());
            layout = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
            layout.SetContent(12, 12, Water(), new CityLayoutDefinition.StartingHouse[0], Along(4, 0), new Vector2Int[0], null, new Vector2Int[0]);
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

        private static List<Vector2Int> Water()
        {
            var water = new List<Vector2Int>();
            for (int y = 10; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    water.Add(new Vector2Int(x, y));
                }
            }

            return water;
        }

        private static GridPlace Along(int x, int y)
        {
            return new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
        }

        private void AssertSite(GridPlace expected)
        {
            Assert.IsTrue(layout.HasTradingPostSite);
            Assert.AreEqual(expected.Origin, layout.TradingPostSite.Origin);
            Assert.AreEqual(expected.Orientation, layout.TradingPostSite.Orientation);
        }

        [Test]
        public void SetTradingPostSite_PlacesAndMovesTheSite()
        {
            Assert.IsTrue(CityLayoutEditing.SetTradingPostSite(layout, buildings, Site));
            AssertSite(Site);

            var moved = new GridPlace(new Vector2Int(0, 4), BuildingOrientation.Across);
            Assert.IsTrue(CityLayoutEditing.SetTradingPostSite(layout, buildings, moved));
            AssertSite(moved);
        }

        [Test]
        public void SetTradingPostSite_RefusesWaterAndBuildings()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            Assert.IsFalse(CityLayoutEditing.SetTradingPostSite(layout, buildings, Along(0, 9)), "on the water");
            Assert.IsFalse(CityLayoutEditing.SetTradingPostSite(layout, buildings, Along(4, 0)), "on the town hall");
            Assert.IsFalse(CityLayoutEditing.SetTradingPostSite(layout, buildings, Along(11, 4)), "outside the grid");
            AssertSite(Site);

            CityLayoutEditing.SetRoads(layout, buildings, new[] { new Vector2Int(0, 5) }, true);
            Assert.IsFalse(CityLayoutEditing.SetTradingPostSite(layout, buildings, Along(0, 4)), "on a road");
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(2, 2) }, true);
            Assert.IsFalse(CityLayoutEditing.SetTradingPostSite(layout, buildings, Along(1, 1)), "on a wall");
            Assert.IsTrue(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(0, 8)));
            Assert.IsFalse(CityLayoutEditing.SetTradingPostSite(layout, buildings, Along(0, 8)), "on a house");
            AssertSite(Site);
        }

        [Test]
        public void CanPlaceTradingPostSite_IgnoresTheCurrentSite()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            Assert.IsTrue(CityLayoutEditing.CanPlaceTradingPostSite(layout, buildings, Along(9, 4)));
        }

        [Test]
        public void RemoveTradingPostSite_ClearsIt()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            CityLayoutEditing.RemoveTradingPostSite(layout);

            Assert.IsFalse(layout.HasTradingPostSite);
        }

        [Test]
        public void IsUnderTradingPostSite_CoversItsSixCells()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            Assert.IsTrue(CityLayoutEditing.IsUnderTradingPostSite(layout, buildings, new Vector2Int(8, 4)));
            Assert.IsTrue(CityLayoutEditing.IsUnderTradingPostSite(layout, buildings, new Vector2Int(9, 6)));
            Assert.IsFalse(CityLayoutEditing.IsUnderTradingPostSite(layout, buildings, new Vector2Int(10, 4)));
            Assert.IsFalse(CityLayoutEditing.IsUnderTradingPostSite(layout, buildings, new Vector2Int(8, 7)));
        }

        [Test]
        public void SetRoads_SkipsCellsUnderTheSite()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            CityLayoutEditing.SetRoads(layout, buildings, new[] { new Vector2Int(8, 4), new Vector2Int(7, 4) }, true);

            CollectionAssert.AreEqual(new[] { new Vector2Int(7, 4) }, layout.Roads);
            AssertSite(Site);
        }

        [Test]
        public void SetWalls_SkipsCellsUnderTheSite()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(9, 6), new Vector2Int(10, 6) }, true);

            CollectionAssert.AreEqual(new[] { new Vector2Int(10, 6) }, layout.Walls);
            AssertSite(Site);
        }

        [Test]
        public void SetForbidden_SkipsCellsUnderTheSite()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            CityLayoutEditing.SetForbidden(layout, buildings, new[] { new Vector2Int(8, 5), new Vector2Int(7, 5) }, true);

            CollectionAssert.Contains(layout.ForbiddenCells, new Vector2Int(7, 5));
            CollectionAssert.DoesNotContain(layout.ForbiddenCells, new Vector2Int(8, 5));
            AssertSite(Site);
        }

        [Test]
        public void TryAddHouse_RefusesAPlaceOnTheSite()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            Assert.IsFalse(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(8, 4)));
            Assert.IsFalse(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(9, 6)));
            Assert.AreEqual(0, layout.StartingHouses.Count);
        }

        [Test]
        public void SetTownHall_RefusesAPlaceOnTheSite()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            Assert.IsFalse(CityLayoutEditing.SetTownHall(layout, buildings, Along(8, 4)));
            Assert.AreEqual(new Vector2Int(4, 0), layout.TownHallPlace.Origin);
        }

        [Test]
        public void SetRoads_KeepsTheSiteElsewhere()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            CityLayoutEditing.SetRoads(layout, buildings, new[] { new Vector2Int(0, 0) }, true);

            AssertSite(Site);
        }

        [Test]
        public void Resize_DropsASiteThatFallsOutside()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            CityLayoutEditing.Resize(layout, buildings, 9, 12);

            Assert.IsFalse(layout.HasTradingPostSite);
        }

        [Test]
        public void Resize_KeepsASiteStillInside()
        {
            CityLayoutEditing.SetTradingPostSite(layout, buildings, Site);

            CityLayoutEditing.Resize(layout, buildings, 10, 10);

            AssertSite(Site);
        }
    }
}
