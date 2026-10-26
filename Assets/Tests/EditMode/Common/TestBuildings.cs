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
            serialized.FindProperty("footprint").vector2IntValue = new Vector2Int(2, 2);
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
            serialized.FindProperty("footprint").vector2IntValue = new Vector2Int(2, 1);
            SetBuilding(serialized, name, gold, materials);
            return house;
        }

        /// <summary>A road costing 50 gold, one cell.</summary>
        public static RoadDefinition CreateRoad(string name = "Route", long gold = 50)
        {
            var road = ScriptableObject.CreateInstance<RoadDefinition>();
            road.name = name;
            var serialized = new SerializedObject(road);
            serialized.FindProperty("footprint").vector2IntValue = new Vector2Int(1, 1);
            SetBuilding(serialized, name, gold, new (GoodDefinition good, int units)[0]);
            return road;
        }

        /// <summary>A free town hall of 3 by 2 cells.</summary>
        public static TownHallDefinition CreateTownHall(string name = "Hôtel de ville")
        {
            var townHall = ScriptableObject.CreateInstance<TownHallDefinition>();
            townHall.name = name;
            var serialized = new SerializedObject(townHall);
            serialized.FindProperty("footprint").vector2IntValue = new Vector2Int(3, 2);
            SetBuilding(serialized, name, 0, new (GoodDefinition good, int units)[0]);
            return townHall;
        }

        /// <summary>A free port of 4 by 3 cells, its 2 front rows on water.</summary>
        public static PortDefinition CreatePort(string name = "Port", int waterRows = 2)
        {
            var port = ScriptableObject.CreateInstance<PortDefinition>();
            port.name = name;
            var serialized = new SerializedObject(port);
            serialized.FindProperty("footprint").vector2IntValue = new Vector2Int(4, 3);
            serialized.FindProperty("waterRows").intValue = waterRows;
            SetBuilding(serialized, name, 0, new (GoodDefinition good, int units)[0]);
            return port;
        }

        /// <summary>A free wall of one cell.</summary>
        public static WallDefinition CreateWall(string name = "Mur")
        {
            var wall = ScriptableObject.CreateInstance<WallDefinition>();
            wall.name = name;
            var serialized = new SerializedObject(wall);
            serialized.FindProperty("footprint").vector2IntValue = new Vector2Int(1, 1);
            SetBuilding(serialized, name, 0, new (GoodDefinition good, int units)[0]);
            return wall;
        }

        /// <summary>Gives a catalogue its wall; null takes it away.</summary>
        public static void SetWall(BuildingsDefinition buildings, WallDefinition wall)
        {
            var serialized = new SerializedObject(buildings);
            serialized.FindProperty("wall").objectReferenceValue = wall;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Gives a catalogue its port; null takes it away.</summary>
        public static void SetPort(BuildingsDefinition buildings, PortDefinition port)
        {
            var serialized = new SerializedObject(buildings);
            serialized.FindProperty("port").objectReferenceValue = port;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Gives a catalogue its road and its town hall; null takes one away.</summary>
        public static void SetRoadAndTownHall(BuildingsDefinition buildings, RoadDefinition road, TownHallDefinition townHall)
        {
            var serialized = new SerializedObject(buildings);
            serialized.FindProperty("road").objectReferenceValue = road;
            serialized.FindProperty("townHall").objectReferenceValue = townHall;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A valid catalogue: a house per class (40, 70 and 140 places, 2 by 1 cells), a road, a town hall, a port, a wall and a random placement strategy.</summary>
        public static BuildingsDefinition CreateCatalogue()
        {
            var buildings = ScriptableObject.CreateInstance<BuildingsDefinition>();
            HouseDefinition rich = CreateHouse("Maison de riches", SocialClass.Rich, 0, 40);
            HouseDefinition wealthy = CreateHouse("Maison d'aisés", SocialClass.Wealthy, 0, 70);
            HouseDefinition poor = CreateHouse("Maison de pauvres", SocialClass.Poor, 0, 140);

            var serialized = new SerializedObject(buildings);
            SerializedProperty houses = serialized.FindProperty("houses");
            houses.arraySize = 3;
            houses.GetArrayElementAtIndex(0).objectReferenceValue = rich;
            houses.GetArrayElementAtIndex(1).objectReferenceValue = wealthy;
            houses.GetArrayElementAtIndex(2).objectReferenceValue = poor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetRoadAndTownHall(buildings, CreateRoad(), CreateTownHall());
            SetPort(buildings, CreatePort());
            SetWall(buildings, CreateWall());
            SetPlacement(buildings, ScriptableObject.CreateInstance<RandomPlacementDefinition>());
            return buildings;
        }

        /// <summary>Sets when a road touches a building or another road in the catalogue's cities.</summary>
        public static void SetRoadContact(BuildingsDefinition buildings, RoadContact contact)
        {
            var serialized = new SerializedObject(buildings);
            serialized.FindProperty("roadContact").enumValueIndex = (int)contact;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Gives a catalogue its placement strategy; null takes it away.</summary>
        public static void SetPlacement(BuildingsDefinition buildings, PlacementStrategyDefinition placement)
        {
            var serialized = new SerializedObject(buildings);
            serialized.FindProperty("placementStrategy").objectReferenceValue = placement;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFootprint(BuildingDefinition building, Vector2Int footprint)
        {
            var serialized = new SerializedObject(building);
            serialized.FindProperty("footprint").vector2IntValue = footprint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
