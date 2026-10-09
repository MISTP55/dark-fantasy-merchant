using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ExpensesDefinitionTests
    {
        private ExpensesDefinition expenses;

        [SetUp]
        public void SetUp()
        {
            expenses = ScriptableObject.CreateInstance<ExpensesDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(expenses);
        }

        [Test]
        public void ANewDefinition_IsTheGamesExpenses()
        {
            Assert.AreEqual(10, expenses.SailorWeeklyWage);
        }

        [Test]
        public void CreateBilling_PaysTheSailorsTheirWage_EveryWeekOfTheGivenDays()
        {
            var treasury = new Treasury(1000);
            WeeklyBilling billing = expenses.CreateBilling(treasury, 2, () => 12);

            billing.StartDay(1);
            Assert.AreEqual(1000, treasury.Gold);

            billing.StartDay(2);
            Assert.AreEqual(880, treasury.Gold);
        }

        [Test]
        public void CreateBilling_RejectsAWeekWithoutADay()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => expenses.CreateBilling(new Treasury(1000), 0, () => 12));
        }

        [Test]
        public void CreateBilling_RejectsANegativeWage()
        {
            var serialized = new SerializedObject(expenses);
            serialized.FindProperty("sailorWeeklyWage").longValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => expenses.CreateBilling(new Treasury(1000), 7, () => 12));
        }
    }
}
