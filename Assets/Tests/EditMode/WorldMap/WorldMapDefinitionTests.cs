using System.Collections.Generic;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WorldMapDefinitionTests
    {
        private readonly List<Object> created = new List<Object>();

        private CityDefinition north;
        private CityDefinition east;
        private CityDefinition south;
        private WorldMapDefinition map;

        [SetUp]
        public void SetUp()
        {
            north = Track(ScriptableObject.CreateInstance<CityDefinition>());
            east = Track(ScriptableObject.CreateInstance<CityDefinition>());
            south = Track(ScriptableObject.CreateInstance<CityDefinition>());
            map = Track(CreateMap(north, east, south));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        [Test]
        public void ANewMap_HasNoCity()
        {
            var empty = Track(ScriptableObject.CreateInstance<WorldMapDefinition>());

            Assert.IsEmpty(empty.Cities);
            Assert.IsEmpty(empty.MapCities);
        }

        [Test]
        public void TheCities_AreTheActiveOnes_InTheMapsOrder()
        {
            SetActive(map, east, false);

            CollectionAssert.AreEqual(new[] { north, south }, map.Cities);
            Assert.AreEqual(3, map.MapCities.Count);
            Assert.AreSame(east, map.MapCities[1].City);
            Assert.IsFalse(map.MapCities[1].Active);
        }

        [Test]
        public void ACitySwitchedBackOn_ReturnsToItsPlace()
        {
            SetActive(map, north, false);
            CollectionAssert.AreEqual(new[] { east, south }, map.Cities);

            SetActive(map, north, true);

            CollectionAssert.AreEqual(new[] { north, east, south }, map.Cities);
        }

        [Test]
        public void AMapWithNoActiveCity_HasNoCity()
        {
            SetActive(map, north, false);
            SetActive(map, east, false);
            SetActive(map, south, false);

            Assert.IsEmpty(map.Cities);
            Assert.AreEqual(3, map.MapCities.Count);
        }

        [Test]
        public void AnActiveEntryWithNoCity_StaysIn_ForTheViewToReport()
        {
            WorldMapDefinition withHole = Track(CreateMap(north, null));

            CollectionAssert.AreEqual(new[] { north, null }, withHole.Cities);
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }

        // Every city active, as the map lists them in a new game.
        private static WorldMapDefinition CreateMap(params CityDefinition[] cities)
        {
            var map = ScriptableObject.CreateInstance<WorldMapDefinition>();
            var serialized = new SerializedObject(map);
            SerializedProperty entries = serialized.FindProperty("cities");
            entries.arraySize = cities.Length;

            for (int i = 0; i < cities.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("city").objectReferenceValue = cities[i];
                entry.FindPropertyRelative("active").boolValue = true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return map;
        }

        private static void SetActive(WorldMapDefinition map, CityDefinition city, bool active)
        {
            var serialized = new SerializedObject(map);
            SerializedProperty entries = serialized.FindProperty("cities");

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);

                if (entry.FindPropertyRelative("city").objectReferenceValue == city)
                {
                    entry.FindPropertyRelative("active").boolValue = active;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
