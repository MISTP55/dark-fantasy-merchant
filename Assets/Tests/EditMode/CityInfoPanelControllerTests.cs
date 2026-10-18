using System.Collections.Generic;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

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
        public void ShipsTabText_CountsTheShipsInPort()
        {
            Assert.AreEqual("Navires au port (3)", CityInfoPanelController.ShipsTabText(3));
            Assert.AreEqual("Navires au port (0)", CityInfoPanelController.ShipsTabText(0));
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
    }
}
