using System.Collections.Generic;
using System.Linq;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ChartTimeAxisTests
    {
        // Twelve months of 30 days, from year 932.
        private static readonly ChartCalendar Calendar = new ChartCalendar(932, 12, 30, month => "M" + month);

        private static List<ChartTick> Ticks(int firstDay, int lastDay, int maxTicks)
        {
            var ticks = new List<ChartTick>();
            ChartTimeAxis.Ticks(firstDay, lastDay, maxTicks, Calendar, ticks);
            return ticks;
        }

        [TestCase(60, ChartTimeUnit.Day)]
        [TestCase(61, ChartTimeUnit.Month)]
        [TestCase(1080, ChartTimeUnit.Month)]
        [TestCase(1081, ChartTimeUnit.Year)]
        public void TheUnit_GrowsWithTheLengthOfTheChart(int spanDays, ChartTimeUnit unit)
        {
            Assert.AreEqual(unit, ChartTimeAxis.UnitOf(spanDays, 30, 12));
        }

        [Test]
        public void AShortChart_IsInDays()
        {
            List<ChartTick> ticks = Ticks(0, 20, 10);

            CollectionAssert.AreEqual(new[] { 0, 5, 10, 15, 20 }, ticks.Select(tick => tick.Day));
            CollectionAssert.AreEqual(new[] { "1 M1", "6 M1", "11 M1", "16 M1", "21 M1" }, ticks.Select(tick => tick.Label));
        }

        [Test]
        public void AYear_IsInMonths_EveryOtherOneWhenTheyDoNotFit()
        {
            List<ChartTick> ticks = Ticks(0, 359, 6);

            CollectionAssert.AreEqual(new[] { 0, 60, 120, 180, 240, 300 }, ticks.Select(tick => tick.Day));
            CollectionAssert.AreEqual(new[] { "M1 932", "M3", "M5", "M7", "M9", "M11" }, ticks.Select(tick => tick.Label));
        }

        [Test]
        public void Months_StartOnTheFirstDayOfAMonth_AndNameTheYearInItsFirstMonth()
        {
            List<ChartTick> ticks = Ticks(15, 375, 13);

            Assert.AreEqual(12, ticks.Count);
            Assert.AreEqual(30, ticks[0].Day);
            Assert.AreEqual("M2", ticks[0].Label);
            Assert.AreEqual(360, ticks[11].Day);
            Assert.AreEqual("M1 933", ticks[11].Label);
        }

        [Test]
        public void ALongChart_IsInYears()
        {
            List<ChartTick> ticks = Ticks(0, 3600, 6);

            CollectionAssert.AreEqual(new[] { 0, 720, 1440, 2160, 2880, 3600 }, ticks.Select(tick => tick.Day));
            CollectionAssert.AreEqual(new[] { "932", "934", "936", "938", "940", "942" }, ticks.Select(tick => tick.Label));
        }

        [Test]
        public void ATick_IsAlwaysInTheChart_EvenWithNoRoom()
        {
            List<ChartTick> ticks = Ticks(3, 50, 0);

            Assert.LessOrEqual(ticks.Count, 1);
            Assert.IsTrue(ticks.All(tick => tick.Day >= 3 && tick.Day <= 50));
        }

        [Test]
        public void ASingleDay_HasItsTick()
        {
            List<ChartTick> ticks = Ticks(7, 7, 5);

            Assert.AreEqual(1, ticks.Count);
            Assert.AreEqual("8 M1", ticks[0].Label);
        }

        [Test]
        public void FormatDay_WritesTheWholeDate()
        {
            Assert.AreEqual("6 M2 933", Calendar.FormatDay(395));
        }
    }
}
