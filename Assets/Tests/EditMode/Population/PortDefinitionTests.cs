using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PortDefinitionTests
    {
        private PortDefinition port;

        [SetUp]
        public void SetUp()
        {
            port = TestBuildings.CreatePort();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(port);
        }

        private void SetSpeed(float cellsPerSecond)
        {
            var serialized = new SerializedObject(port);
            serialized.FindProperty("shipCellsPerSecond").floatValue = cellsPerSecond;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void ShipCellsPerSecond_IsTwoByDefault()
        {
            Assert.AreEqual(2f, port.ShipCellsPerSecond);
            Assert.IsTrue(port.TryValidate(out string problem), problem);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TryValidate_SpeedNotPositive_Fails(float cellsPerSecond)
        {
            SetSpeed(cellsPerSecond);

            Assert.IsFalse(port.TryValidate(out string problem));
            StringAssert.Contains("cells per second", problem);
        }
    }
}
