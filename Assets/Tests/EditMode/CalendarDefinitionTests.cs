using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CalendarDefinitionTests
    {
        private CalendarDefinition calendar;

        [SetUp]
        public void SetUp()
        {
            calendar = ScriptableObject.CreateInstance<CalendarDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(calendar);
        }

        [Test]
        public void ANewCalendar_IsTheGamesCalendar()
        {
            Assert.AreEqual(932, calendar.StartYear);
            Assert.AreEqual(12, calendar.MonthsPerYear);
            Assert.AreEqual(30, calendar.DaysPerMonth);
            Assert.AreEqual(30f, calendar.SecondsPerDay);
            Assert.AreEqual(60f, calendar.FastForwardMultiplier);
        }

        [Test]
        public void MonthNames_AreTheInventedOnes_InOrder()
        {
            string[] expected =
            {
                "Janvis", "Févrin", "Marsis", "Avrilis", "Maïus", "Juinis",
                "Juillis", "Aoûtis", "Septem", "Octem", "Novem", "Décem",
            };

            for (int month = 1; month <= expected.Length; month++)
            {
                Assert.AreEqual(expected[month - 1], calendar.MonthName(month));
            }
        }

        [Test]
        public void Format_IsDayMonthNameYear()
        {
            Assert.AreEqual("1 Janvis 932", calendar.Format(new GameDate(932, 1, 1)));
            Assert.AreEqual("30 Décem 933", calendar.Format(new GameDate(933, 12, 30)));
        }

        [Test]
        public void CreateClock_StartsOnTheFirstDayOfTheStartYear_AtTheCalendarsPace()
        {
            GameClock clock = calendar.CreateClock();

            Assert.AreEqual("1 Janvis 932", calendar.Format(clock.Date));

            clock.Advance(30f);
            Assert.AreEqual("2 Janvis 932", calendar.Format(clock.Date));

            clock.SetFastForward(true);
            clock.Advance(0.5f);
            Assert.AreEqual("3 Janvis 932", calendar.Format(clock.Date));
        }

        [TestCase(0)]
        [TestCase(13)]
        [TestCase(-1)]
        public void AMonthWithoutAName_IsNamedByItsNumber(int month)
        {
            Assert.AreEqual($"Mois {month}", calendar.MonthName(month));
        }

        [Test]
        public void ABlankMonthName_IsReplacedByTheMonthNumber()
        {
            var serialized = new SerializedObject(calendar);
            serialized.FindProperty("monthNames").GetArrayElementAtIndex(2).stringValue = "  ";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("4 Mois 3 932", calendar.Format(new GameDate(932, 3, 4)));
        }

        [Test]
        public void ACalendarWithoutMonths_CannotCreateAClock()
        {
            var serialized = new SerializedObject(calendar);
            serialized.FindProperty("monthNames").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(0, calendar.MonthsPerYear);
            Assert.Throws<ArgumentOutOfRangeException>(() => calendar.CreateClock());
        }

        [Test]
        public void ACalendarWithoutDaysInAMonth_CannotCreateAClock()
        {
            var serialized = new SerializedObject(calendar);
            serialized.FindProperty("daysPerMonth").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<ArgumentOutOfRangeException>(() => calendar.CreateClock());
        }
    }
}
