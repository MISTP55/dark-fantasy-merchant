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
        private BuildingsDefinition buildings;

        [SetUp]
        public void SetUp()
        {
            wheat = Track(TestEconomy.CreateGood("Blé", 10, 14));
            timber = Track(TestEconomy.CreateGood("Bois", 55, 3, unit: GoodUnit.Bushel));
            metal = Track(TestEconomy.CreateGood("Métal", 255, 3));
            economy = Track(TestEconomy.CreateEconomy(wheat, timber, metal));
            buildings = Track(ScriptableObject.CreateInstance<BuildingsDefinition>());

            SetBuilding("poorHouse", 5000, (timber, 10), (metal, 10));
            SetBuilding("wealthyHouse", 10000, (timber, 15), (metal, 20));
            SetBuilding("richHouse", 25000, (timber, 25), (metal, 30));
            SetBuilding("workshop", 10000, (timber, 10), (metal, 30));
        }

        private static string Gold(long gold) => TreasuryHudController.FormatGold(gold);

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

        private void SetBuilding(string field, long gold, params (GoodDefinition good, int units)[] materials)
        {
            var serialized = new SerializedObject(buildings);
            SerializedProperty building = serialized.FindProperty(field);
            building.FindPropertyRelative("gold").longValue = gold;
            SerializedProperty list = building.FindPropertyRelative("materials");
            list.arraySize = materials.Length;

            for (int i = 0; i < materials.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("good").objectReferenceValue = materials[i].good;
                entry.FindPropertyRelative("units").intValue = materials[i].units;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void ANewDefinition_HasTheGamesBuildings()
        {
            BuildingsDefinition fresh = Track(ScriptableObject.CreateInstance<BuildingsDefinition>());

            Assert.AreEqual(30, fresh.WorkshopJobs);
            Assert.AreEqual(40, fresh.HouseRooms.Rich);
            Assert.AreEqual(70, fresh.HouseRooms.Wealthy);
            Assert.AreEqual(140, fresh.HouseRooms.Poor);
        }

        [Test]
        public void TryCreateCost_MapsTheMaterialsToTheirIndicesAmongTheActiveGoods()
        {
            Assert.IsTrue(buildings.TryCreateCost(buildings.Workshop, economy, out BuildingCost cost, out string problem), problem);

            Assert.AreEqual(10000, cost.Gold);
            Assert.AreEqual(2, cost.Materials.Count);
            Assert.AreEqual(1, cost.Materials[0].Good);
            Assert.AreEqual(10, cost.Materials[0].Units);
            Assert.AreEqual(2, cost.Materials[1].Good);
            Assert.AreEqual(30, cost.Materials[1].Units);
        }

        [Test]
        public void MaterialsOf_MapsTheHousesMaterialsToTheirIndices()
        {
            IReadOnlyList<MaterialCost> materials = buildings.MaterialsOf(buildings.HouseOf(SocialClass.Poor), economy);

            Assert.AreEqual(2, materials.Count);
            Assert.AreEqual(1, materials[0].Good);
            Assert.AreEqual(10, materials[0].Units);
            Assert.AreEqual(2, materials[1].Good);
            Assert.AreEqual(10, materials[1].Units);
        }

        [Test]
        public void MaterialsOf_LeaveOutAnInactiveMaterial_AndAnEmptyOne()
        {
            SetBuilding("poorHouse", 5000, (timber, 10), (null, 5), (metal, 10));
            TestEconomy.SetActive(economy, metal, false);

            IReadOnlyList<MaterialCost> materials = buildings.MaterialsOf(buildings.HouseOf(SocialClass.Poor), economy);

            Assert.AreEqual(1, materials.Count);
            Assert.AreEqual(economy.IndexOf(timber), materials[0].Good);
            Assert.AreEqual(10, materials[0].Units);
        }

        [Test]
        public void MaterialsOf_AddUpAGoodListedTwice()
        {
            SetBuilding("poorHouse", 5000, (timber, 10), (timber, 4));

            IReadOnlyList<MaterialCost> materials = buildings.MaterialsOf(buildings.HouseOf(SocialClass.Poor), economy);

            Assert.AreEqual(1, materials.Count);
            Assert.AreEqual(14, materials[0].Units);
        }

        [Test]
        public void MaterialsOf_WithEveryMaterialInactive_AreNone()
        {
            TestEconomy.SetActive(economy, timber, false);
            TestEconomy.SetActive(economy, metal, false);

            Assert.AreEqual(0, buildings.MaterialsOf(buildings.HouseOf(SocialClass.Poor), economy).Count);
        }

        [Test]
        public void TryCreateCost_TheWorkshop_LeavesOutAnInactiveMaterial()
        {
            TestEconomy.SetActive(economy, metal, false);

            Assert.IsTrue(buildings.TryCreateCost(buildings.Workshop, economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(10000, cost.Gold);
            Assert.AreEqual(1, cost.Materials.Count);
            Assert.AreEqual(economy.IndexOf(timber), cost.Materials[0].Good);
            Assert.AreEqual(10, cost.Materials[0].Units);
        }

        [Test]
        public void TryCreateCost_TheWorkshopWithEveryMaterialInactive_CostsOnlyItsGold()
        {
            TestEconomy.SetActive(economy, timber, false);
            TestEconomy.SetActive(economy, metal, false);

            Assert.IsTrue(buildings.TryCreateCost(buildings.Workshop, economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(10000, cost.Gold);
            Assert.AreEqual(0, cost.Materials.Count);
        }

        [Test]
        public void TryCreateCost_TheHouse_LeavesOutAnInactiveMaterial()
        {
            TestEconomy.SetActive(economy, metal, false);

            Assert.IsTrue(buildings.TryCreateCost(buildings.HouseOf(SocialClass.Poor), economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(5000, cost.Gold);
            Assert.AreEqual(1, cost.Materials.Count);
            Assert.AreEqual(economy.IndexOf(timber), cost.Materials[0].Good);
        }

        [Test]
        public void TryCreateCost_TheHouseWithEveryMaterialInactive_CostsOnlyItsGold()
        {
            TestEconomy.SetActive(economy, timber, false);
            TestEconomy.SetActive(economy, metal, false);

            Assert.IsTrue(buildings.TryCreateCost(buildings.HouseOf(SocialClass.Poor), economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(5000, cost.Gold);
            Assert.AreEqual(0, cost.Materials.Count);
        }

        [Test]
        public void TryCreateCost_ANullMaterial_IsLeftOut()
        {
            SetBuilding("workshop", 10000, (null, 10), (metal, 30));

            Assert.IsTrue(buildings.TryCreateCost(buildings.Workshop, economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(1, cost.Materials.Count);
            Assert.AreEqual(economy.IndexOf(metal), cost.Materials[0].Good);
        }

        [Test]
        public void TryCreateCost_NoUnits_IsLeftOut()
        {
            SetBuilding("workshop", 10000, (timber, 0));

            Assert.IsTrue(buildings.TryCreateCost(buildings.Workshop, economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(0, cost.Materials.Count);
        }

        [Test]
        public void TryCreateCost_AGoodListedTwice_IsAddedUp()
        {
            SetBuilding("workshop", 10000, (metal, 30), (metal, 5));

            Assert.IsTrue(buildings.TryCreateCost(buildings.Workshop, economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(1, cost.Materials.Count);
            Assert.AreEqual(35, cost.Materials[0].Units);
        }

        [Test]
        public void TryCreateCost_ANegativeGold_IsAProblem()
        {
            SetBuilding("poorHouse", -1, (timber, 10));

            Assert.IsFalse(buildings.TryCreateCost(buildings.HouseOf(SocialClass.Poor), economy, out _, out string problem));
            StringAssert.Contains("house", problem);
            StringAssert.Contains("gold", problem);
        }

        [Test]
        public void TryValidate_AcceptsTheGamesBuildings_AndRefusesNoJobsOrNoRoom()
        {
            Assert.IsTrue(buildings.TryValidate(out string problem), problem);

            var serialized = new SerializedObject(buildings);
            serialized.FindProperty("workshopJobs").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(buildings.TryValidate(out problem));
            StringAssert.Contains("jobs", problem);

            serialized.FindProperty("workshopJobs").intValue = 30;
            serialized.FindProperty("wealthyHouseRoom").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(buildings.TryValidate(out problem));
            StringAssert.Contains("room", problem);
        }

        [Test]
        public void TryValidate_IgnoresTheCosts_ANegativeGoldOnlyMakesItsBuildingUnavailable()
        {
            SetBuilding("poorHouse", -1, (timber, 10));
            SetBuilding("workshop", -1, (timber, 10));

            Assert.IsTrue(buildings.TryValidate(out string problem), problem);
            Assert.IsFalse(buildings.TryCreateCost(buildings.HouseOf(SocialClass.Poor), economy, out _, out _));
            Assert.IsFalse(buildings.TryCreateCost(buildings.Workshop, economy, out _, out _));
        }

        [Test]
        public void CostsText_WritesTheThreeHousesInAlphabeticalOrder_ThenTheWorkshop()
        {
            Assert.AreEqual(
                $"Maison d'aisés : {Gold(10000)}, 15 bois, 20 métal · Maison de pauvres : {Gold(5000)}, 10 bois, 10 métal · "
                + $"Maison de riches : {Gold(25000)}, 25 bois, 30 métal · Atelier : {Gold(10000)}, 10 bois, 30 métal",
                buildings.CostsText(economy));
        }

        [Test]
        public void CostsText_NamesTheMaterialsInAlphabeticalOrder()
        {
            SetBuilding("poorHouse", 5000, (metal, 10), (timber, 20));

            StringAssert.Contains($"Maison de pauvres : {Gold(5000)}, 20 bois, 10 métal ·", buildings.CostsText(economy));
        }

        [Test]
        public void CostsText_WritesABuildingWhoseCostCannotBeCreatedAsUnavailable()
        {
            SetBuilding("poorHouse", -1, (timber, 10));

            StringAssert.Contains("Maison de pauvres : indisponible · Maison de riches", buildings.CostsText(economy));
            StringAssert.EndsWith($"Atelier : {Gold(10000)}, 10 bois, 30 métal", buildings.CostsText(economy));

            SetBuilding("workshop", -1, (timber, 10));

            StringAssert.EndsWith("Atelier : indisponible", buildings.CostsText(economy));
        }

        [Test]
        public void CostsText_NamesOnlyTheActiveMaterialsOfTheHouse()
        {
            string gold = TreasuryHudController.FormatGold(5000);
            TestEconomy.SetActive(economy, metal, false);

            StringAssert.Contains($"Maison de pauvres : {gold}, 10 bois ·", buildings.CostsText(economy));

            TestEconomy.SetActive(economy, timber, false);

            StringAssert.Contains($"Maison de pauvres : {gold} ·", buildings.CostsText(economy));
        }

        [Test]
        public void CostsText_NamesOnlyTheActiveMaterialsOfTheWorkshop()
        {
            string gold = TreasuryHudController.FormatGold(10000);
            TestEconomy.SetActive(economy, metal, false);

            StringAssert.EndsWith($"Atelier : {gold}, 10 bois", buildings.CostsText(economy));

            TestEconomy.SetActive(economy, timber, false);

            StringAssert.EndsWith($"Atelier : {gold}", buildings.CostsText(economy));
        }
    }
}
