using System.Linq;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugEventRowsTests
    {
        private CityMarket first;
        private CityMarket second;
        private SimulationRecord record;
        private DebugEventFilter filter;
        private DebugEventRows rows;

        // Fish: 2 barrels a day for a thousand, none in stock: a shortage in each city every day.
        private static CityMarket Market()
        {
            return new CityMarket(new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(1000));
        }

        [SetUp]
        public void SetUp()
        {
            first = Market();
            second = Market();
            record = new SimulationRecord(new[] { first, second }, 1, null);
            filter = new DebugEventFilter();
            rows = new DebugEventRows(filter);
        }

        private void PlayDays(int from, int to)
        {
            for (int day = from; day <= to; day++)
            {
                first.StartDay();
                second.StartDay();
                record.RecordDay(day, (city, good) => 0);
            }
        }

        [Test]
        public void Refresh_ShowsTheNewestFirst()
        {
            PlayDays(1, 3);

            rows.Refresh(record);

            Assert.AreEqual(6, rows.Count);
            Assert.AreEqual(3, rows.NewestAt(0).Day);
            Assert.AreEqual(1, rows.NewestAt(5).Day);
        }

        [Test]
        public void Frozen_KeepsItsRows_WhileDaysPassAndAreForgotten()
        {
            PlayDays(1, 3);
            rows.Refresh(record);
            rows.IsFrozen = true;

            PlayDays(4, 6);
            record.SetHorizon(1);
            rows.Refresh(record);

            Assert.AreEqual(6, rows.Count);
            Assert.AreEqual(3, rows.NewestAt(0).Day);
        }

        [Test]
        public void AFilterChange_WhileFrozen_ShowsTheMatchingRows()
        {
            PlayDays(1, 3);
            rows.Refresh(record);
            rows.IsFrozen = true;

            filter.City = 1;
            rows.Rebuild(record);

            Assert.AreEqual(3, rows.Count);
            Assert.IsTrue(Enumerable.Range(0, rows.Count).All(i => rows.NewestAt(i).City == 1));
            Assert.IsTrue(rows.IsFrozen);
        }

        [Test]
        public void Thawed_CatchesUp()
        {
            PlayDays(1, 2);
            rows.Refresh(record);
            rows.IsFrozen = true;
            PlayDays(3, 4);

            rows.IsFrozen = false;
            rows.Refresh(record);

            Assert.AreEqual(8, rows.Count);
            Assert.AreEqual(4, rows.NewestAt(0).Day);
        }

        [Test]
        public void ADayLabel_PastTheHistory_IsADash()
        {
            PlayDays(1, 3);

            Assert.AreEqual("jour 2", DebugDayLabel.Of(record.History, 1, day => $"jour {day}"));
            Assert.AreEqual(DebugCitiesTable.NoFigure, DebugDayLabel.Of(record.History, 3, day => $"jour {day}"));
            Assert.AreEqual(DebugCitiesTable.NoFigure, DebugDayLabel.Of(record.History, -1, day => $"jour {day}"));
        }
    }
}
