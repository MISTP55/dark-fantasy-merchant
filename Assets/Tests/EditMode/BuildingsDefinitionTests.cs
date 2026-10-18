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

            SetBuilding("house", 5000, (timber, 10), (metal, 10));
            SetBuilding("workshop", 10000, (timber, 10), (metal, 30));
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
            Assert.AreEqual(100, fresh.HouseRoom);
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
        public void HouseMaterials_MapsTheHousesMaterialsToTheirIndices()
        {
            IReadOnlyList<MaterialCost> materials = buildings.HouseMaterials(economy);

            Assert.AreEqual(2, materials.Count);
            Assert.AreEqual(1, materials[0].Good);
            Assert.AreEqual(10, materials[0].Units);
            Assert.AreEqual(2, materials[1].Good);
            Assert.AreEqual(10, materials[1].Units);
        }

        [Test]
        public void HouseMaterials_LeaveOutAnInactiveMaterial_AndAnEmptyOne()
        {
            SetBuilding("house", 5000, (timber, 10), (null, 5), (metal, 10));
            TestEconomy.SetActive(economy, metal, false);

            IReadOnlyList<MaterialCost> materials = buildings.HouseMaterials(economy);

            Assert.AreEqual(1, materials.Count);
            Assert.AreEqual(economy.IndexOf(timber), materials[0].Good);
            Assert.AreEqual(10, materials[0].Units);
        }

        [Test]
        public void HouseMaterials_AddUpAGoodListedTwice()
        {
            SetBuilding("house", 5000, (timber, 10), (timber, 4));

            IReadOnlyList<MaterialCost> materials = buildings.HouseMaterials(economy);

            Assert.AreEqual(1, materials.Count);
            Assert.AreEqual(14, materials[0].Units);
        }

        [Test]
        public void HouseMaterials_WithEveryMaterialInactive_AreNone()
        {
            TestEconomy.SetActive(economy, timber, false);
            TestEconomy.SetActive(economy, metal, false);

            Assert.AreEqual(0, buildings.HouseMaterials(economy).Count);
        }

        [Test]
        public void TryCreateCost_AnInactiveMaterialOfTheWorkshop_IsAProblemNamingIt()
        {
            TestEconomy.SetActive(economy, metal, false);

            Assert.IsFalse(buildings.TryCreateCost(buildings.Workshop, economy, out _, out string problem));
            StringAssert.Contains("Métal", problem);
            StringAssert.Contains("workshop", problem);
        }

        [Test]
        public void TryCreateCost_TheHouse_LeavesOutAnInactiveMaterial()
        {
            TestEconomy.SetActive(economy, metal, false);

            Assert.IsTrue(buildings.TryCreateCost(buildings.House, economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(5000, cost.Gold);
            Assert.AreEqual(1, cost.Materials.Count);
            Assert.AreEqual(economy.IndexOf(timber), cost.Materials[0].Good);
        }

        [Test]
        public void TryCreateCost_TheHouseWithEveryMaterialInactive_CostsOnlyItsGold()
        {
            TestEconomy.SetActive(economy, timber, false);
            TestEconomy.SetActive(economy, metal, false);

            Assert.IsTrue(buildings.TryCreateCost(buildings.House, economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(5000, cost.Gold);
            Assert.AreEqual(0, cost.Materials.Count);
        }

        [Test]
        public void TryCreateCost_ANullMaterial_IsAProblem()
        {
            SetBuilding("workshop", 10000, (null, 10));

            Assert.IsFalse(buildings.TryCreateCost(buildings.Workshop, economy, out _, out string problem));
            StringAssert.Contains("workshop", problem);
        }

        [Test]
        public void TryCreateCost_NoUnits_IsAProblemNamingTheMaterial()
        {
            SetBuilding("workshop", 10000, (timber, 0));

            Assert.IsFalse(buildings.TryCreateCost(buildings.Workshop, economy, out _, out string problem));
            StringAssert.Contains("Bois", problem);
        }

        [Test]
        public void TryCreateCost_ANegativeGold_IsAProblem()
        {
            SetBuilding("house", -1, (timber, 10));

            Assert.IsFalse(buildings.TryCreateCost(buildings.House, economy, out _, out string problem));
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
            serialized.FindProperty("houseRoom").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(buildings.TryValidate(out problem));
            StringAssert.Contains("room", problem);
        }

        [Test]
        public void TryValidate_IgnoresTheCosts_AMaterialSwitchedOffOnlyMakesItsBuildingUnavailable()
        {
            TestEconomy.SetActive(economy, metal, false);
            SetBuilding("house", -1, (timber, 10));

            Assert.IsTrue(buildings.TryValidate(out string problem), problem);
            Assert.IsFalse(buildings.TryCreateCost(buildings.House, economy, out _, out _));
            Assert.IsFalse(buildings.TryCreateCost(buildings.Workshop, economy, out _, out _));
        }

        [Test]
        public void CostsText_WritesTheHouseThenTheWorkshop()
        {
            Assert.AreEqual(
                "Logement : 5 000 or, 10 bois, 10 métal · Atelier : 10 000 or, 10 bois, 30 métal",
                buildings.CostsText(economy));
        }

        [Test]
        public void CostsText_NamesTheMaterialsInAlphabeticalOrder()
        {
            SetBuilding("house", 5000, (metal, 10), (timber, 20));

            StringAssert.StartsWith("Logement : 5 000 or, 20 bois, 10 métal ·", buildings.CostsText(economy));
        }

        [Test]
        public void CostsText_WritesABuildingWhoseCostCannotBeCreatedAsUnavailable()
        {
            SetBuilding("house", -1, (timber, 10));

            Assert.AreEqual(
                "Logement : indisponible · Atelier : 10 000 or, 10 bois, 30 métal",
                buildings.CostsText(economy));

            TestEconomy.SetActive(economy, metal, false);

            Assert.AreEqual("Logement : indisponible · Atelier : indisponible", buildings.CostsText(economy));
        }

        [Test]
        public void CostsText_NamesOnlyTheActiveMaterialsOfTheHouse()
        {
            string gold = TreasuryHudController.FormatGold(5000);
            TestEconomy.SetActive(economy, metal, false);

            StringAssert.StartsWith($"Logement : {gold}, 10 bois ·", buildings.CostsText(economy));

            TestEconomy.SetActive(economy, timber, false);

            StringAssert.StartsWith($"Logement : {gold} ·", buildings.CostsText(economy));
        }
    }
}
