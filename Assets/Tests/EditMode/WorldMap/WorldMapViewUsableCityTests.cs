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

        private void GiveLayout(params Vector2Int[] berths)
        {
            var water = new List<Vector2Int>();
            for (int y = 8; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    water.Add(new Vector2Int(x, y));
                }
            }

            layout.SetContent(
                10, 10, water, new CityLayoutDefinition.StartingHouse[0],
                new GridPlace(new Vector2Int(7, 0), BuildingOrientation.Along), new Vector2Int[0],
                new PortSite(new Vector2Int(0, 7), PortFacing.North), berths);

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
            GiveLayout(new Vector2Int(5, 9));

            Assert.IsTrue(WorldMapView.IsUsable(city, buildings, out string problem), problem);
            Assert.IsNull(problem);
        }
    }
}
