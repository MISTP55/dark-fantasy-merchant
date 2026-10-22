using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>In-memory building definitions. The caller destroys what it creates.</summary>
    public static class TestBuildings
    {
        public static WorkshopDefinition CreateWorkshop(
            string name, GoodDefinition good, long gold, int jobs, params (GoodDefinition good, int units)[] materials)
        {
            var workshop = ScriptableObject.CreateInstance<WorkshopDefinition>();
            workshop.name = name;

            var serialized = new SerializedObject(workshop);
            serialized.FindProperty("good").objectReferenceValue = good;
            serialized.FindProperty("jobs").intValue = jobs;
            SetBuilding(serialized, name, gold, materials);
            return workshop;
        }

        public static HouseDefinition CreateHouse(
            string name, SocialClass housedClass, long gold, int room, params (GoodDefinition good, int units)[] materials)
        {
            var house = ScriptableObject.CreateInstance<HouseDefinition>();
            house.name = name;

            var serialized = new SerializedObject(house);
            serialized.FindProperty("socialClass").enumValueIndex = (int)housedClass;
            serialized.FindProperty("room").intValue = room;
            SetBuilding(serialized, name, gold, materials);
            return house;
        }

        /// <summary>Gives a definition a new gold and new materials.</summary>
        public static void SetCost(BuildingDefinition building, long gold, params (GoodDefinition good, int units)[] materials)
        {
            var serialized = new SerializedObject(building);
            SetBuilding(serialized, building.DisplayName, gold, materials);
        }

        private static void SetBuilding(
            SerializedObject serialized, string name, long gold, (GoodDefinition good, int units)[] materials)
        {
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("gold").longValue = gold;
            SerializedProperty list = serialized.FindProperty("materials");
            list.arraySize = materials.Length;

            for (int i = 0; i < materials.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("good").objectReferenceValue = materials[i].good;
                entry.FindPropertyRelative("units").intValue = materials[i].units;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
