using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class BuildingsSetupTests
    {
        private readonly List<Object> created = new List<Object>();

        private GoodDefinition wheat;
        private GoodDefinition timber;
        private Dictionary<SocialClass, HouseDefinition> houses;
        private Dictionary<GoodDefinition, WorkshopDefinition> workshops;
        private BuildingsDefinition buildings;
        private RoadDefinition road;
        private TownHallDefinition townHall;
        private PortDefinition port;
        private int loads;

        [SetUp]
        public void SetUp()
        {
            wheat = Track(TestEconomy.CreateGood("Blé", 10, 14));
            timber = Track(TestEconomy.CreateGood("Bois", 55, 3));
            houses = new Dictionary<SocialClass, HouseDefinition>
            {
                { SocialClass.Rich, Track(TestBuildings.CreateHouse("Maison de riches", SocialClass.Rich, 25000, 40)) },
                { SocialClass.Wealthy, Track(TestBuildings.CreateHouse("Maison d'aisés", SocialClass.Wealthy, 10000, 70)) },
                { SocialClass.Poor, Track(TestBuildings.CreateHouse("Maison de pauvres", SocialClass.Poor, 5000, 140)) },
            };
            workshops = new Dictionary<GoodDefinition, WorkshopDefinition>
            {
                { wheat, Track(TestBuildings.CreateWorkshop("Ferme", wheat, 10000, 30)) },
                { timber, Track(TestBuildings.CreateWorkshop("Bûcheron", timber, 10000, 30)) },
            };
            buildings = Track(ScriptableObject.CreateInstance<BuildingsDefinition>());
            TestBuildings.SetPlacement(buildings, Track(ScriptableObject.CreateInstance<RandomPlacementDefinition>()));
            road = Track(TestBuildings.CreateRoad());
            townHall = Track(TestBuildings.CreateTownHall());
            port = Track(TestBuildings.CreatePort());
            loads = 0;
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

        private void SetCatalogue(Object[] houseEntries, Object[] workshopEntries)
        {
            var serialized = new SerializedObject(buildings);
            SetList(serialized.FindProperty("houses"), houseEntries);
            SetList(serialized.FindProperty("workshops"), workshopEntries);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetList(SerializedProperty list, Object[] entries)
        {
            list.arraySize = entries.Length;

            for (int i = 0; i < entries.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            }
        }

        // Every definition exists already: none is created.
        private void Complete(bool isNew)
        {
            WorldMapSetup.CompleteBuildings(
                buildings, isNew, new[] { wheat, timber },
                housedClass =>
                {
                    loads++;
                    return (houses[housedClass], false);
                },
                good =>
                {
                    loads++;
                    return (workshops[good], false);
                },
                () => road,
                () => townHall,
                () => port);
        }

        [Test]
        public void CompleteBuildings_AddsRoadTownHallAndPort()
        {
            Complete(false);

            Assert.AreSame(road, buildings.Road);
            Assert.AreSame(townHall, buildings.TownHall);
            Assert.AreSame(port, buildings.Port);
        }

        [Test]
        public void CompleteBuildings_KeepsTheRoadTownHallAndPortItHolds()
        {
            RoadDefinition other = Track(TestBuildings.CreateRoad("Chemin"));
            PortDefinition otherPort = Track(TestBuildings.CreatePort("Quai"));
            TestBuildings.SetRoadAndTownHall(buildings, other, null);
            TestBuildings.SetPort(buildings, otherPort);

            Complete(false);

            Assert.AreSame(other, buildings.Road);
            Assert.AreSame(townHall, buildings.TownHall);
            Assert.AreSame(otherPort, buildings.Port);
        }

        [Test]
        public void CompletePlacement_ReplacesRandomByRoadside()
        {
            WorldMapSetup.CompletePlacement(buildings);

            Assert.IsInstanceOf<RoadsidePlacementDefinition>(buildings.PlacementStrategy);
            var roadside = buildings.PlacementStrategy;

            WorldMapSetup.CompletePlacement(buildings);

            Assert.AreSame(roadside, buildings.PlacementStrategy);
        }

        [Test]
        public void ANewCatalogue_TakesTheDefinitionsThatExist()
        {
            Complete(true);

            Assert.AreEqual(3, buildings.Houses.Count);
            Assert.AreEqual(2, buildings.Workshops.Count);
            Assert.IsTrue(buildings.TryValidate(out string problem), problem);
        }

        [Test]
        public void AnExistingCatalogue_LeavesOutAnExistingDefinitionTakenOutByHand()
        {
            SetCatalogue(new Object[] { houses[SocialClass.Rich], houses[SocialClass.Wealthy], houses[SocialClass.Poor] }, new Object[] { workshops[timber] });

            Complete(false);

            Assert.AreEqual(1, buildings.Workshops.Count);
            Assert.IsNull(buildings.WorkshopOf(wheat));
        }

        [Test]
        public void ADefinitionTheCatalogueHolds_IsNeitherLoadedNorAddedAgain_WhateverItsAssetIsCalled()
        {
            WorkshopDefinition renamed = Track(TestBuildings.CreateWorkshop("Scierie", timber, 10000, 30));
            SetCatalogue(new Object[] { houses[SocialClass.Rich], houses[SocialClass.Wealthy], houses[SocialClass.Poor] }, new Object[] { workshops[wheat], renamed });

            Complete(true);

            Assert.AreEqual(0, loads);
            Assert.AreEqual(2, buildings.Workshops.Count);
            Assert.AreSame(renamed, buildings.WorkshopOf(timber));
        }

        [Test]
        public void ADeletedDefinition_LeavesNoEmptyEntry_AndIsAddedWhenCreatedAgain()
        {
            SetCatalogue(new Object[] { houses[SocialClass.Rich], null, houses[SocialClass.Poor] }, new Object[] { null, workshops[wheat] });
            HouseDefinition rebuilt = Track(TestBuildings.CreateHouse("Maison d'aisés", SocialClass.Wealthy, 10000, 70));
            WorkshopDefinition rebuiltWorkshop = Track(TestBuildings.CreateWorkshop("Bûcheron", timber, 10000, 30));

            WorldMapSetup.CompleteBuildings(
                buildings, false, new[] { wheat, timber },
                housedClass => (rebuilt, true),
                good => (rebuiltWorkshop, true),
                () => road,
                () => townHall,
                () => port);

            Assert.AreEqual(3, buildings.Houses.Count);
            Assert.AreSame(rebuilt, buildings.HouseOf(SocialClass.Wealthy));
            Assert.AreEqual(2, buildings.Workshops.Count);
            Assert.AreSame(rebuiltWorkshop, buildings.WorkshopOf(timber));
            Assert.IsTrue(buildings.TryValidate(out string problem), problem);
        }
    }
}
