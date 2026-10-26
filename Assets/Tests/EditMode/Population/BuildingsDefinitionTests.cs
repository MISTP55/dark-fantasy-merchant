using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class BuildingsDefinitionTests
    {
        private readonly List<Object> created = new List<Object>();

        private GoodDefinition wheat;
        private GoodDefinition timber;
        private GoodDefinition metal;
        private EconomyDefinition economy;
        private HouseDefinition richHouse;
        private HouseDefinition wealthyHouse;
        private HouseDefinition poorHouse;
        private WorkshopDefinition farm;
        private WorkshopDefinition lumberjack;
        private BuildingsDefinition buildings;

        [SetUp]
        public void SetUp()
        {
            wheat = Track(TestEconomy.CreateGood("Blé", 10, 14));
            timber = Track(TestEconomy.CreateGood("Bois", 55, 3, unit: GoodUnit.Bushel));
            metal = Track(TestEconomy.CreateGood("Métal", 255, 3));
            economy = Track(TestEconomy.CreateEconomy(wheat, timber, metal));

            richHouse = Track(TestBuildings.CreateHouse("Maison de riches", SocialClass.Rich, 25000, 40, (timber, 25), (metal, 30)));
            wealthyHouse = Track(TestBuildings.CreateHouse("Maison d'aisés", SocialClass.Wealthy, 10000, 70, (timber, 15), (metal, 20)));
            poorHouse = Track(TestBuildings.CreateHouse("Maison de pauvres", SocialClass.Poor, 5000, 140, (timber, 10), (metal, 10)));
            farm = Track(TestBuildings.CreateWorkshop("Ferme", wheat, 10000, 30, (timber, 10), (metal, 30)));
            lumberjack = Track(TestBuildings.CreateWorkshop("Bûcheron", timber, 10000, 30, (timber, 10), (metal, 30)));

            buildings = Track(ScriptableObject.CreateInstance<BuildingsDefinition>());
            buildings.name = "Buildings";
            TestBuildings.SetPlacement(buildings, Track(ScriptableObject.CreateInstance<RandomPlacementDefinition>()));
            TestBuildings.SetRoadAndTownHall(buildings, Track(TestBuildings.CreateRoad()), Track(TestBuildings.CreateTownHall()));
            TestBuildings.SetPort(buildings, Track(TestBuildings.CreatePort()));
            TestBuildings.SetWall(buildings, Track(TestBuildings.CreateWall()));
            SetCatalogue(new[] { richHouse, wealthyHouse, poorHouse }, new[] { farm, lumberjack });
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

        private static string Gold(long gold) => TreasuryHudController.FormatGold(gold);

        private void SetCatalogue(HouseDefinition[] houses, WorkshopDefinition[] workshops)
        {
            var serialized = new SerializedObject(buildings);
            SetList(serialized.FindProperty("houses"), houses);
            SetList(serialized.FindProperty("workshops"), workshops);
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

        [Test]
        public void TryValidate_AcceptsOneHousePerClass()
        {
            Assert.IsTrue(buildings.TryValidate(out string problem), problem);
        }

        [Test]
        public void NoRoad_IsInvalid()
        {
            TestBuildings.SetRoadAndTownHall(buildings, null, Track(TestBuildings.CreateTownHall()));

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("no road", problem);
        }

        [Test]
        public void NoTownHall_IsInvalid()
        {
            TestBuildings.SetRoadAndTownHall(buildings, Track(TestBuildings.CreateRoad()), null);

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("no town hall", problem);
        }

        [Test]
        public void NoPort_IsInvalid()
        {
            TestBuildings.SetPort(buildings, null);

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("no port", problem);
        }

        [Test]
        public void APortWithWaterRowsOutOfRange_IsInvalid()
        {
            TestBuildings.SetPort(buildings, Track(TestBuildings.CreatePort(waterRows: 3)));

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("water rows", problem);
        }

        [Test]
        public void DefinitionOf_ThePort_IsThePort()
        {
            PortDefinition port = Track(TestBuildings.CreatePort());
            TestBuildings.SetPort(buildings, port);

            Assert.AreSame(port, buildings.DefinitionOf(BuildingKind.Port(), null));
        }

        [Test]
        public void NoWall_IsInvalid()
        {
            TestBuildings.SetWall(buildings, null);

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("no wall", problem);
        }

        [Test]
        public void WallOfTwoCells_IsInvalid()
        {
            WallDefinition wide = Track(TestBuildings.CreateWall());
            TestBuildings.SetFootprint(wide, new Vector2Int(1, 2));
            TestBuildings.SetWall(buildings, wide);

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("one cell", problem);
        }

        [Test]
        public void DefinitionOf_TheWall_IsTheWall()
        {
            WallDefinition wall = Track(TestBuildings.CreateWall());
            TestBuildings.SetWall(buildings, wall);

            Assert.AreSame(wall, buildings.DefinitionOf(BuildingKind.Wall(), null));
        }

        [Test]
        public void RoadOfTwoCells_IsInvalid()
        {
            RoadDefinition wide = Track(TestBuildings.CreateRoad());
            TestBuildings.SetFootprint(wide, new Vector2Int(2, 1));
            TestBuildings.SetRoadAndTownHall(buildings, wide, Track(TestBuildings.CreateTownHall()));

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("one cell", problem);
        }

        [Test]
        public void CreateRoadOffer_CostIsGoldOnly()
        {
            TestBuildings.SetCost(buildings.Road, 50, (timber, 5));

            RoadOffer offer = buildings.CreateRoadOffer(economy, null);

            Assert.AreEqual("Route", offer.Name);
            Assert.AreEqual(50, offer.CostPerCell.Gold);
            Assert.AreEqual(1, offer.CostPerCell.Materials.Count);
            Assert.AreEqual(buildings.Road.CostText(economy), offer.CostText);

            TestBuildings.SetCost(buildings.Road, 50);
            offer = buildings.CreateRoadOffer(economy, null);
            Assert.AreEqual(0, offer.CostPerCell.Materials.Count);
            Assert.AreEqual("50 or", offer.CostText);
        }

        [Test]
        public void TryValidate_Fails_WithoutPlacementStrategy()
        {
            TestBuildings.SetPlacement(buildings, null);

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("placement", problem);
        }

        [Test]
        public void CreateOffers_CarryTheFootprintOfTheirBuilding()
        {
            HouseOffer house = buildings.CreateHouseOffers(economy, null)[0];
            WorkshopOffer workshop = buildings.CreateWorkshopOffers(economy, null)[0];

            Assert.AreEqual(2, house.Footprint.Width);
            Assert.AreEqual(1, house.Footprint.Depth);
            Assert.AreEqual(2, workshop.Footprint.Width);
            Assert.AreEqual(2, workshop.Footprint.Depth);
        }

        [Test]
        public void TryValidate_RefusesAClassWithoutHouse()
        {
            SetCatalogue(new[] { wealthyHouse, poorHouse }, new[] { farm });

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("no house", problem);
            StringAssert.Contains("rich", problem);
        }

        [Test]
        public void TryValidate_RefusesTwoHousesOfAClass()
        {
            HouseDefinition other = Track(TestBuildings.CreateHouse("Taudis", SocialClass.Poor, 1000, 100));
            SetCatalogue(new[] { richHouse, wealthyHouse, poorHouse, other }, new[] { farm });

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("houses", problem);
        }

        [Test]
        public void TryValidate_RefusesTwoWorkshopsOfAGood_EvenInactive()
        {
            WorkshopDefinition sawmill = Track(TestBuildings.CreateWorkshop("Scierie", timber, 10000, 30));
            SetCatalogue(new[] { richHouse, wealthyHouse, poorHouse }, new[] { farm, lumberjack, sawmill });
            TestEconomy.SetActive(economy, timber, false);

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("workshops", problem);
            StringAssert.Contains("Bois", problem);
        }

        [Test]
        public void TryValidate_RefusesAnEmptyEntry_AndAnInvalidDefinition()
        {
            SetCatalogue(new[] { richHouse, wealthyHouse, poorHouse }, new[] { farm, null });

            Assert.IsFalse(buildings.TryValidate(out string problem));
            StringAssert.Contains("empty entry", problem);

            WorkshopDefinition idle = Track(TestBuildings.CreateWorkshop("Atelier vide", metal, 10000, 0));
            SetCatalogue(new[] { richHouse, wealthyHouse, poorHouse }, new[] { farm, idle });

            Assert.IsFalse(buildings.TryValidate(out problem));
            StringAssert.Contains("Atelier vide", problem);
            StringAssert.Contains("jobs", problem);
        }

        [Test]
        public void TryValidate_IgnoresTheCosts()
        {
            TestBuildings.SetCost(poorHouse, -1, (timber, 10));

            Assert.IsTrue(buildings.TryValidate(out string problem), problem);
        }

        [Test]
        public void HouseOf_AndWorkshopOf_FindTheDefinition()
        {
            Assert.AreSame(richHouse, buildings.HouseOf(SocialClass.Rich));
            Assert.AreSame(poorHouse, buildings.HouseOf(SocialClass.Poor));
            Assert.AreSame(lumberjack, buildings.WorkshopOf(timber));
            Assert.IsNull(buildings.WorkshopOf(metal));
            Assert.IsNull(buildings.WorkshopOf(null));
            Assert.AreEqual(3, buildings.Houses.Count);
            Assert.AreEqual(2, buildings.Workshops.Count);
        }

        [Test]
        public void HouseRooms_AreTheRoomsOfTheHouses()
        {
            PerClass<int> rooms = buildings.HouseRooms;

            Assert.AreEqual(40, rooms.Rich);
            Assert.AreEqual(70, rooms.Wealthy);
            Assert.AreEqual(140, rooms.Poor);
        }

        [Test]
        public void CreateHouseOffers_GivesTheClassNameRoomCostAndCostText()
        {
            IReadOnlyList<HouseOffer> offers = buildings.CreateHouseOffers(economy, null);

            Assert.AreEqual(3, offers.Count);
            Assert.AreEqual(SocialClass.Poor, offers[2].HousedClass);
            Assert.AreEqual("Maison de pauvres", offers[2].Name);
            Assert.AreEqual(140, offers[2].Room);
            Assert.AreEqual(5000, offers[2].Cost.Gold);
            Assert.AreEqual($"{Gold(5000)}, 10 bois, 10 métal", offers[2].CostText);
        }

        [Test]
        public void CreateWorkshopOffers_GivesTheIndexNameJobsCostAndCostText()
        {
            IReadOnlyList<WorkshopOffer> offers = buildings.CreateWorkshopOffers(economy, null);

            Assert.AreEqual(2, offers.Count);
            Assert.AreEqual(0, offers[0].Good);
            Assert.AreEqual("Ferme", offers[0].Name);
            Assert.AreEqual(30, offers[0].Jobs);
            Assert.AreEqual(10000, offers[0].Cost.Gold);
            Assert.AreEqual(2, offers[0].Cost.Materials.Count);
            Assert.AreEqual($"{Gold(10000)}, 10 bois, 30 métal", offers[0].CostText);
        }

        [Test]
        public void CreateWorkshopOffers_SkipsAWorkshopOfAnInactiveGood()
        {
            TestEconomy.SetActive(economy, wheat, false);

            IReadOnlyList<WorkshopOffer> offers = buildings.CreateWorkshopOffers(economy, null);

            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual("Bûcheron", offers[0].Name);
            Assert.AreEqual(economy.IndexOf(timber), offers[0].Good);
        }

        [Test]
        public void CreateOffers_ANegativeGold_GivesANullCost_AndReportsIt()
        {
            TestBuildings.SetCost(poorHouse, -1, (timber, 10));
            TestBuildings.SetCost(farm, -1);
            var reported = new List<BuildingDefinition>();

            IReadOnlyList<HouseOffer> houses = buildings.CreateHouseOffers(economy, (building, problem) => reported.Add(building));
            IReadOnlyList<WorkshopOffer> workshops = buildings.CreateWorkshopOffers(economy, (building, problem) => reported.Add(building));

            Assert.IsNull(houses[2].Cost);
            Assert.AreEqual("indisponible", houses[2].CostText);
            Assert.IsNotNull(houses[0].Cost);
            Assert.IsNull(workshops[0].Cost);
            Assert.IsNotNull(workshops[1].Cost);
            CollectionAssert.AreEqual(new BuildingDefinition[] { poorHouse, farm }, reported);
        }
    }
}
