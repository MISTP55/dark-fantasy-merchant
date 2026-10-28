using System.Collections.Generic;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityInfoPanelControllerTests
    {
        private readonly List<Object> created = new List<Object>();

        private GoodDefinition grain;
        private GoodDefinition fish;
        private GoodDefinition salt;
        private GoodDefinition wine;

        [SetUp]
        public void SetUp()
        {
            grain = Track(TestEconomy.CreateGood("Céréales", 110, 7));
            fish = Track(TestEconomy.CreateGood("Poisson", 440, 7));
            salt = Track(TestEconomy.CreateGood("Sel", 28.5, 7));
            wine = Track(TestEconomy.CreateGood("Vin", 220, 7));
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

        [Test]
        public void ProducedGoodsText_NamesTheGoodsOfAProduction()
        {
            CityDefinition city = Track(TestEconomy.CreateCity(new[] { salt, grain }, new[] { wine }));
            var goods = new[] { grain, fish, salt, wine };

            Assert.AreEqual("Céréales, Sel", CityInfoPanelController.ProducedGoodsText(city, goods, GoodProduction.Efficient));
            Assert.AreEqual("Vin", CityInfoPanelController.ProducedGoodsText(city, goods, GoodProduction.Inefficient));
        }

        [Test]
        public void ProducedGoodsText_NamesTheGoodsInAlphabeticalOrder()
        {
            CityDefinition city = Track(TestEconomy.CreateCity(new[] { wine, salt, grain }, new GoodDefinition[0]));

            Assert.AreEqual(
                "Céréales, Sel, Vin",
                CityInfoPanelController.ProducedGoodsText(city, new[] { wine, salt, grain }, GoodProduction.Efficient));
        }

        [Test]
        public void ProducedGoodsText_LeavesOutTheGoodsThatAreNotGiven()
        {
            // An inactive good is not among the economy's goods.
            CityDefinition city = Track(TestEconomy.CreateCity(new[] { grain, salt }, new GoodDefinition[0]));

            Assert.AreEqual("Sel", CityInfoPanelController.ProducedGoodsText(city, new[] { fish, salt }, GoodProduction.Efficient));
        }

        [Test]
        public void ProducedGoodsText_SaysNone_WhenTheCityProducesNothingThatWay()
        {
            CityDefinition city = Track(TestEconomy.CreateCity(new[] { grain }, new GoodDefinition[0]));

            Assert.AreEqual(
                CityInfoPanelController.NoProducedGoods,
                CityInfoPanelController.ProducedGoodsText(city, new[] { grain, fish }, GoodProduction.Inefficient));
        }

        [Test]
        public void ShipsTabText_CountsTheShipsInTheCity()
        {
            Assert.AreEqual("Navires (3)", CityInfoPanelController.ShipsTabText(3));
            Assert.AreEqual("Navires (0)", CityInfoPanelController.ShipsTabText(0));
        }

        [Test]
        public void TheShipsTab_WithoutAShip_ReadsAucunNavire()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/WorldMap/CityInfoPanel.uxml");
            Assert.IsNotNull(uxml);

            VisualElement tree = uxml.CloneTree();

            Assert.AreEqual("Aucun navire", tree.Q<Label>("no-ship-label").text);
        }

        [Test]
        public void TradingShipLabels_NumbersTheShipsThatShareAName()
        {
            var labels = CityInfoPanelController.TradingShipLabels(
                new[] { "Navire marchand", "La Corneille", "Navire marchand", "Navire marchand" });

            Assert.That(labels, Is.EqualTo(new[]
            {
                "Navire marchand", "La Corneille", "Navire marchand (2)", "Navire marchand (3)",
            }));
        }

        [Test]
        public void TradingPartnerLabels_AppendsComptoirAfterTheShips()
        {
            var labels = CityInfoPanelController.TradingPartnerLabels(
                new[] { "La Corneille", "Navire marchand", "Navire marchand" }, true);

            Assert.That(labels, Is.EqualTo(new[]
            {
                "La Corneille", "Navire marchand", "Navire marchand (2)", "Comptoir",
            }));
        }

        [Test]
        public void TradingPartnerLabels_NoShipNoPost_IsAucunNavire()
        {
            var labels = CityInfoPanelController.TradingPartnerLabels(new string[0], false);

            Assert.That(labels, Is.EqualTo(new[] { "Aucun navire" }));
        }

        [Test]
        public void TradingPartnerLabels_NoShipWithPost_IsComptoirOnly()
        {
            var labels = CityInfoPanelController.TradingPartnerLabels(new string[0], true);

            Assert.That(labels, Is.EqualTo(new[] { "Comptoir" }));
        }

        [Test]
        public void TradingPartnerLabels_ShipsWithoutPost_AreTheShipLabels()
        {
            var labels = CityInfoPanelController.TradingPartnerLabels(new[] { "Aurore" }, false);

            Assert.That(labels, Is.EqualTo(new[] { "Aurore" }));
        }

        [Test]
        public void TheMarketLine_ReadsAvec()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/WorldMap/CityInfoPanel.uxml");
            VisualElement tree = uxml.CloneTree();

            Assert.AreEqual("Avec :", tree.Q<Label>(className: "market__ship-label").text);
            Assert.AreEqual("Cale", tree.Q<Label>("market-store-header").text);
        }
    }
}
