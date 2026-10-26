using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>The layout window's "Regenerate layout": a whole city made again, its water on the side of the sea.</summary>
    public class CityLayoutRegenerationTests
    {
        private readonly List<Object> created = new List<Object>();

        private BuildingsDefinition buildings;
        private EconomyDefinition economy;
        private CityDefinition city;
        private CityLayoutDefinition layout;

        [SetUp]
        public void SetUp()
        {
            buildings = Track(TestBuildings.CreateCatalogue());
            economy = AssetDatabase.LoadAssetAtPath<EconomyDefinition>("Assets/Data/Economy/Economy.asset");
            city = Track(ScriptableObject.CreateInstance<CityDefinition>());
            var serialized = new SerializedObject(city);
            serialized.FindProperty("mapPosition").vector2Value = new Vector2(0.5f, 0.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // A hand-made layout of the default size, room for the generated walls: a water cell in the middle and a house, no port.
            layout = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
            layout.SetContent(80, 80, new[] { new Vector2Int(20, 20) }, new[]
            {
                new CityLayoutDefinition.StartingHouse(SocialClass.Rich, new Vector2Int(30, 30), BuildingOrientation.Along),
            }, null, new Vector2Int[0], null, new Vector2Int[0]);
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

        // Water to the right of the city on a 100 by 100 map.
        private static NavigationGrid SeaToTheEast()
        {
            var mask = new NavigationGrid(100, 100);
            for (int y = 0; y < 100; y++)
            {
                for (int x = 55; x < 100; x++)
                {
                    mask.SetNavigable(x, y, true);
                }
            }

            return mask;
        }

        [Test]
        public void Regenerate_MakesAUsableCity_WithItsWaterOnTheSideOfTheSea()
        {
            Assert.IsTrue(CityLayoutEditing.Regenerate(layout, city, economy, buildings, SeaToTheEast(), 1, out string problem), problem);

            Assert.AreEqual(80, layout.Width);
            Assert.AreEqual(80, layout.Height);
            Assert.IsTrue(layout.HasPort);
            Assert.AreEqual(PortFacing.East, layout.PortSite.Facing);
            Assert.IsTrue(layout.HasTownHall);
            CollectionAssert.DoesNotContain(layout.ForbiddenCells, new Vector2Int(20, 20));
            Assert.AreEqual(CityLayoutEditing.GeneratedWaterRows * 80, layout.ForbiddenCells.Count);

            foreach (Vector2Int cell in layout.ForbiddenCells)
            {
                Assert.GreaterOrEqual(cell.x, 80 - CityLayoutEditing.GeneratedWaterRows, cell.ToString());
            }

            // Its nearest side the distance from the water.
            Assert.AreEqual(
                80 - CityLayoutEditing.GeneratedWaterRows - CityLayoutEditing.GeneratedTownHallDistance,
                layout.TownHallPlace.Origin.x + buildings.TownHall.Footprint.SizeFor(layout.TownHallPlace.Orientation).x);

            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out problem), problem);
            Assert.AreEqual(CityLayoutEditing.GeneratedBerths, layout.Berths.Count);
            PerClass<int> needed = economy.StartingRoomOf(city, buildings.HouseRooms);

            foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor })
            {
                Assert.GreaterOrEqual(grid.ConnectedHousesOf(housedClass) * buildings.HouseRooms[housedClass], needed[housedClass], housedClass.ToString());
                Assert.AreEqual(grid.HousesOf(housedClass), grid.ConnectedHousesOf(housedClass), housedClass.ToString());
            }
        }

        // The layouts are connected by sides, so that they work under either contact.
        [Test]
        public void TryGenerate_PlacesBySides_WhateverTheCataloguesContact()
        {
            TestBuildings.SetRoadContact(buildings, RoadContact.SidesAndCorners);

            Assert.IsTrue(CityLayoutEditing.TryGenerate(city, economy, buildings, SeaToTheEast(), 1, 80, 80, out CityGrid grid, out string problem), problem);
            Assert.AreEqual(RoadContact.SidesOnly, grid.Contact);
        }

        [Test]
        public void Regenerate_WithoutAMask_PutsTheWaterToTheSouth()
        {
            Assert.IsTrue(CityLayoutEditing.Regenerate(layout, city, economy, buildings, null, 1, out string problem), problem);

            Assert.AreEqual(PortFacing.South, layout.PortSite.Facing);
        }

        [Test]
        public void Regenerate_WhenNothingFits_ChangesNothing()
        {
            layout.SetContent(3, 3, new Vector2Int[0], new CityLayoutDefinition.StartingHouse[0], null, new Vector2Int[0], null, new Vector2Int[0]);

            Assert.IsFalse(CityLayoutEditing.Regenerate(layout, city, economy, buildings, null, 1, out string problem));
            StringAssert.Contains("port", problem);
            Assert.AreEqual(3, layout.Width);
            Assert.IsFalse(layout.HasPort);
        }
    }
}
