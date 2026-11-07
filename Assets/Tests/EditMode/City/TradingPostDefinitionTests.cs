using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TradingPostDefinitionTests
    {
        private TradingPostDefinition post;

        [SetUp]
        public void SetUp()
        {
            post = TestBuildings.CreateTradingPost();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(post);
        }

        [Test]
        public void TryValidate_AcceptsTheDefaults()
        {
            Assert.IsTrue(post.TryValidate(out string problem), problem);
            Assert.AreEqual(2000, post.Capacity);
            Assert.AreEqual(1, post.RentPerBarrel);
        }

        [Test]
        public void TryValidate_RefusesNegativeCapacityOrRent()
        {
            var serialized = new SerializedObject(post);
            serialized.FindProperty("capacity").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(post.TryValidate(out string problem));
            StringAssert.Contains("capacity", problem);

            serialized.FindProperty("capacity").intValue = 2000;
            serialized.FindProperty("rentPerBarrel").longValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(post.TryValidate(out problem));
            StringAssert.Contains("rent", problem);
        }
    }
}
