using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PlayerStartDefinitionTests
    {
        private PlayerStartDefinition playerStart;

        [SetUp]
        public void SetUp()
        {
            playerStart = ScriptableObject.CreateInstance<PlayerStartDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerStart);
        }

        [Test]
        public void ANewDefinition_IsTheGamesStart()
        {
            Assert.AreEqual(10000, playerStart.StartingGold);
            Assert.AreEqual(12, playerStart.StartingCrew);
        }

        [Test]
        public void CreateShipCrew_StartsWithTheStartingCrew_InAShipOfTheGivenCapacity()
        {
            ShipCrew crew = playerStart.CreateShipCrew(28);

            Assert.AreEqual(12, crew.Count);
            Assert.AreEqual(28, crew.Capacity);
        }

        [Test]
        public void CreateShipCrew_LeavesAshoreTheSailorsTheShipHasNoRoomFor()
        {
            Assert.AreEqual(8, playerStart.CreateShipCrew(8).Count);
        }

        [Test]
        public void CreateShipCrew_TakesANegativeStartingCrewAsNone()
        {
            var serialized = new SerializedObject(playerStart);
            serialized.FindProperty("startingCrew").intValue = -3;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(0, playerStart.CreateShipCrew(28).Count);
        }

        [Test]
        public void CreateTreasury_StartsWithTheStartingGold()
        {
            SetStartingGold(250);

            Assert.AreEqual(250, playerStart.CreateTreasury().Gold);
        }

        [Test]
        public void CreateTreasury_CanStartInDebt()
        {
            SetStartingGold(-500);

            Treasury treasury = playerStart.CreateTreasury();

            Assert.AreEqual(-500, treasury.Gold);
            Assert.IsTrue(treasury.IsInDebt);
        }

        [Test]
        public void EachTreasury_IsItsOwn()
        {
            Treasury first = playerStart.CreateTreasury();
            first.Withdraw(100);

            Assert.AreEqual(10000, playerStart.CreateTreasury().Gold);
            Assert.AreEqual(10000, playerStart.StartingGold);
        }

        private void SetStartingGold(long gold)
        {
            var serialized = new SerializedObject(playerStart);
            serialized.FindProperty("startingGold").longValue = gold;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
