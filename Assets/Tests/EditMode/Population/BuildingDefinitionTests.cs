using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class BuildingDefinitionTests
    {
        private readonly List<Object> created = new List<Object>();

        private GoodDefinition wheat;
        private GoodDefinition timber;
        private GoodDefinition metal;
        private EconomyDefinition economy;
        private WorkshopDefinition workshop;
        private HouseDefinition house;

        [SetUp]
        public void SetUp()
        {
            wheat = Track(TestEconomy.CreateGood("Blé", 10, 14));
            timber = Track(TestEconomy.CreateGood("Bois", 55, 3, unit: GoodUnit.Bushel));
            metal = Track(TestEconomy.CreateGood("Métal", 255, 3));
            economy = Track(TestEconomy.CreateEconomy(wheat, timber, metal));
            workshop = Track(TestBuildings.CreateWorkshop("Bûcheron", timber, 10000, 30, (timber, 10), (metal, 30)));
            house = Track(TestBuildings.CreateHouse("Maison de pauvres", SocialClass.Poor, 5000, 140, (timber, 10), (metal, 10)));
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

        [Test]
        public void ANewWorkshopOrHouse_HasTheGamesJobsAndRoom()
        {
            Assert.AreEqual(30, Track(ScriptableObject.CreateInstance<WorkshopDefinition>()).Jobs);

            HouseDefinition fresh = Track(ScriptableObject.CreateInstance<HouseDefinition>());
            Assert.AreEqual(SocialClass.Poor, fresh.SocialClass);
            Assert.AreEqual(140, fresh.Room);
        }

        [Test]
        public void TheDefinitions_GiveTheirAttributes()
        {
            Assert.AreEqual("Bûcheron", workshop.DisplayName);
            Assert.AreEqual(10000, workshop.Gold);
            Assert.AreSame(timber, workshop.Good);
            Assert.AreEqual(30, workshop.Jobs);
            Assert.AreEqual(2, workshop.Materials.Count);
            Assert.AreEqual(SocialClass.Poor, house.SocialClass);
            Assert.AreEqual(140, house.Room);
        }

        [Test]
        public void TryCreateCost_MapsTheMaterialsToTheirIndicesAmongTheActiveGoods()
        {
            Assert.IsTrue(workshop.TryCreateCost(economy, out BuildingCost cost, out string problem), problem);

            Assert.AreEqual(10000, cost.Gold);
            Assert.AreEqual(2, cost.Materials.Count);
            Assert.AreEqual(1, cost.Materials[0].Good);
            Assert.AreEqual(10, cost.Materials[0].Units);
            Assert.AreEqual(2, cost.Materials[1].Good);
            Assert.AreEqual(30, cost.Materials[1].Units);
        }

        [Test]
        public void MaterialsOf_LeaveOutAnInactiveMaterial_AndAnEmptyOne()
        {
            TestBuildings.SetCost(house, 5000, (timber, 10), (null, 5), (metal, 10));
            TestEconomy.SetActive(economy, metal, false);

            IReadOnlyList<MaterialCost> materials = house.MaterialsOf(economy);

            Assert.AreEqual(1, materials.Count);
            Assert.AreEqual(economy.IndexOf(timber), materials[0].Good);
            Assert.AreEqual(10, materials[0].Units);
        }

        [Test]
        public void MaterialsOf_AddUpAGoodListedTwice()
        {
            TestBuildings.SetCost(house, 5000, (timber, 10), (timber, 4));

            IReadOnlyList<MaterialCost> materials = house.MaterialsOf(economy);

            Assert.AreEqual(1, materials.Count);
            Assert.AreEqual(14, materials[0].Units);
        }

        [Test]
        public void MaterialsOf_LeaveOutNoUnits()
        {
            TestBuildings.SetCost(workshop, 10000, (timber, 0));

            Assert.AreEqual(0, workshop.MaterialsOf(economy).Count);
        }

        [Test]
        public void TryCreateCost_WithEveryMaterialInactive_CostsOnlyItsGold()
        {
            TestEconomy.SetActive(economy, timber, false);
            TestEconomy.SetActive(economy, metal, false);

            Assert.IsTrue(house.TryCreateCost(economy, out BuildingCost cost, out string problem), problem);
            Assert.AreEqual(5000, cost.Gold);
            Assert.AreEqual(0, cost.Materials.Count);
        }

        [Test]
        public void TryCreateCost_ANegativeGold_IsAProblemNamingTheBuilding()
        {
            TestBuildings.SetCost(house, -1, (timber, 10));

            Assert.IsFalse(house.TryCreateCost(economy, out BuildingCost cost, out string problem));
            Assert.IsNull(cost);
            StringAssert.Contains("Maison de pauvres", problem);
            StringAssert.Contains("gold", problem);
        }

        [Test]
        public void TryCreateCost_WithoutAnEconomy_IsAProblem()
        {
            Assert.IsFalse(workshop.TryCreateCost(null, out _, out string problem));
            Assert.IsNotNull(problem);
        }

        [Test]
        public void CostText_WritesTheGoldThenTheActiveMaterialsInAlphabeticalOrder()
        {
            TestBuildings.SetCost(house, 5000, (metal, 10), (timber, 20));
            Assert.AreEqual($"{Gold(5000)}, 20 bois, 10 métal", house.CostText(economy));

            TestEconomy.SetActive(economy, metal, false);
            Assert.AreEqual($"{Gold(5000)}, 20 bois", house.CostText(economy));

            TestEconomy.SetActive(economy, timber, false);
            Assert.AreEqual(Gold(5000), house.CostText(economy));
        }

        [Test]
        public void CostText_OfANegativeGold_IsUnavailable()
        {
            TestBuildings.SetCost(workshop, -1);

            Assert.AreEqual("indisponible", workshop.CostText(economy));
        }

        [Test]
        public void TryValidate_RefusesNoName_NoGood_NoJobs_NoRoom()
        {
            Assert.IsTrue(workshop.TryValidate(out string problem), problem);
            Assert.IsTrue(house.TryValidate(out problem), problem);

            Assert.IsFalse(Track(TestBuildings.CreateWorkshop(" ", timber, 10000, 30)).TryValidate(out problem));
            StringAssert.Contains("no name", problem);
            Assert.IsFalse(Track(TestBuildings.CreateWorkshop("Bûcheron", null, 10000, 30)).TryValidate(out problem));
            StringAssert.Contains("no good", problem);
            Assert.IsFalse(Track(TestBuildings.CreateWorkshop("Bûcheron", timber, 10000, 0)).TryValidate(out problem));
            StringAssert.Contains("jobs", problem);
            Assert.IsFalse(Track(TestBuildings.CreateHouse("Maison", SocialClass.Rich, 1, 0)).TryValidate(out problem));
            StringAssert.Contains("room", problem);
        }
    }
}
