using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class AiMerchantsDefinitionTests
    {
        private AiMerchantsDefinition definition;
        private ShipDefinition shipDefinition;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<AiMerchantsDefinition>();
            shipDefinition = ScriptableObject.CreateInstance<ShipDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(shipDefinition);
        }

        private void Set(string property, System.Action<SerializedProperty> write)
        {
            var serialized = new SerializedObject(definition);
            write(serialized.FindProperty(property));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void TheDefaults_AreTheGames()
        {
            Assert.AreEqual(1, definition.ShipsPerCity);
            Assert.AreEqual(14, definition.Crew);
            Assert.AreEqual(1f, definition.StopoverDays);
            Assert.Greater(definition.Tint.b, definition.Tint.r);
        }

        [Test]
        public void ShipNameFor_NumbersTheShips()
        {
            Assert.AreEqual("Marchand indépendant 3", definition.ShipNameFor(3));
        }

        [TestCase("Rival", "Rival 2")]
        [TestCase("", "Marchand indépendant 2")]
        [TestCase("Rival {1}", "Rival {1} 2")]
        [TestCase("Rival {0", "Rival {0 2")]
        public void ShipNameFor_SurvivesAFormatWithoutAUsableNumber(string format, string expected)
        {
            Set("shipNameFormat", property => property.stringValue = format);

            Assert.AreEqual(expected, definition.ShipNameFor(2));
        }

        [Test]
        public void CreateCrew_MansTheShipOfTheDefinition()
        {
            Set("shipDefinition", property => property.objectReferenceValue = shipDefinition);
            var serialized = new SerializedObject(shipDefinition);
            serialized.FindProperty("crewCapacity").intValue = 28;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ShipCrew crew = definition.CreateCrew();

            Assert.AreEqual(28, crew.Capacity);
            Assert.AreEqual(14, crew.Count);
            Assert.AreEqual(1f, crew.SpeedFactor);
        }

        [Test]
        public void CreateCrew_IsCutDownToTheCapacity()
        {
            // A new ship definition has room for 10.
            Set("shipDefinition", property => property.objectReferenceValue = shipDefinition);

            Assert.AreEqual(10, definition.CreateCrew().Count);
        }

        [Test]
        public void CreateCrew_WithoutAShip_IsNull()
        {
            Assert.IsNull(definition.CreateCrew());
        }
    }
}
