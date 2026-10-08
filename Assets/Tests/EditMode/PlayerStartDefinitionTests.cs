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
