using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WorldMapViewUsableCityTests
    {
        private readonly List<Object> created = new List<Object>();

        private BuildingsDefinition buildings;
        private CityDefinition city;
        private CityLayoutDefinition layout;

        [SetUp]
        public void SetUp()
        {
            buildings = Track(TestBuildings.CreateCatalogue());
            city = Track(ScriptableObject.CreateInstance<CityDefinition>());
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

        // A 12 by 12 grid: water on its three top rows, walls on the rest of its edge, the town hall and the port inside.
        private void GiveLayout(params Vector2Int[] berths)
        {
            var water = new List<Vector2Int>();
            var walls = new List<Vector2Int>();
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    if (y >= 9)
                    {
                        water.Add(new Vector2Int(x, y));
                    }
                    else if (x == 0 || y == 0 || x == 11)
                    {
                        walls.Add(new Vector2Int(x, y));
                    }
                }
            }

            layout.SetContent(
                12, 12, water, new CityLayoutDefinition.StartingHouse[0],
                new GridPlace(new Vector2Int(8, 1), BuildingOrientation.Along), new Vector2Int[0],
                new PortSite(new Vector2Int(1, 8), PortFacing.North), berths, walls);

            var serialized = new SerializedObject(city);
            serialized.FindProperty("layout").objectReferenceValue = layout;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void IsUsable_NoLayout_IsFalse()
        {
            Assert.IsFalse(WorldMapView.IsUsable(city, buildings, out string problem));
            StringAssert.Contains("layout", problem);
        }

        [Test]
        public void IsUsable_LayoutWithoutBerth_IsFalse()
        {
            GiveLayout();

            Assert.IsFalse(WorldMapView.IsUsable(city, buildings, out string problem));
            StringAssert.Contains("berth", problem);
        }

        [Test]
        public void IsUsable_UsableLayout_IsTrue()
        {
            GiveLayout(new Vector2Int(6, 10));

            Assert.IsTrue(WorldMapView.IsUsable(city, buildings, out string problem), problem);
            Assert.IsNull(problem);
        }
    }
}
