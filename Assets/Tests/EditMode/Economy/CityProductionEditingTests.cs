using System.Collections.Generic;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityProductionEditingTests
    {
        private readonly List<Object> created = new List<Object>();

        private GoodDefinition grain;
        private GoodDefinition fish;
        private GoodDefinition salt;

        [SetUp]
        public void SetUp()
        {
            grain = Track(TestEconomy.CreateGood("Céréales", 110, 7));
            fish = Track(TestEconomy.CreateGood("Poisson", 440, 7));
            salt = Track(TestEconomy.CreateGood("Sel", 28.5, 7));
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

        private static void Set(CityDefinition city, GoodDefinition good, GoodProduction production)
        {
            var serialized = new SerializedObject(city);
            CityProductionEditing.SetProduction(serialized, good, production);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void SetProduction_MovesAGood_FromOneListToTheOther()
        {
            CityDefinition city = Track(TestEconomy.CreateCity(new[] { grain, fish }, new[] { salt }));

            Set(city, fish, GoodProduction.Inefficient);

            CollectionAssert.AreEqual(new[] { grain }, city.EfficientGoods);
            CollectionAssert.AreEqual(new[] { salt, fish }, city.InefficientGoods);
        }

        [Test]
        public void SetProduction_ToNone_TakesTheGoodOutOfBothLists()
        {
            CityDefinition city = Track(TestEconomy.CreateCity(new[] { grain, salt }, new[] { salt }));

            Set(city, salt, GoodProduction.None);

            CollectionAssert.AreEqual(new[] { grain }, city.EfficientGoods);
            Assert.AreEqual(0, city.InefficientGoods.Count);
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(salt));
        }

        [Test]
        public void SetProduction_AddsAGoodTheCityDidNotProduce_Once()
        {
            CityDefinition city = Track(TestEconomy.CreateCity(new[] { grain }, new GoodDefinition[0]));

            Set(city, fish, GoodProduction.Efficient);
            Set(city, fish, GoodProduction.Efficient);

            CollectionAssert.AreEqual(new[] { grain, fish }, city.EfficientGoods);
        }

        [Test]
        public void SetProduction_DropsTheEmptyEntries()
        {
            CityDefinition city = Track(TestEconomy.CreateCity(new[] { grain, null }, new GoodDefinition[] { null }));

            Set(city, fish, GoodProduction.Inefficient);

            CollectionAssert.AreEqual(new[] { grain }, city.EfficientGoods);
            CollectionAssert.AreEqual(new[] { fish }, city.InefficientGoods);
        }
    }
}
