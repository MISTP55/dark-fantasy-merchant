using System.Linq;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class SimulationRecordTests
    {
        private const int Fish = 0;
        private const int Timber = 1;

        private CityMarket first;
        private CityMarket second;
        private SimulationRecord record;

        // Fish: 2 barrels a day for a thousand, none in stock: a shortage every day. Timber: consumed by no one.
        private static CityMarket Market()
        {
            return new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 0, 0, 0) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                0,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));
        }

        [SetUp]
        public void SetUp()
        {
            first = Market();
            second = Market();
            record = new SimulationRecord(new[] { first, second }, 2, null);
        }

        private void PlayDays(int from, int to)
        {
            for (int day = from; day <= to; day++)
            {
                first.StartDay();
                second.StartDay();
                record.RecordDay(day, (city, good) => city * 10 + good);
                record.AddPlayerTrade(day, first, Timber, 1, 10, PlayerTradeKind.Buy);
            }
        }

        [Test]
        public void RecordDay_RecordsEveryCity_AndItsEvents()
        {
            int recorded = 0;
            record.Recorded += () => recorded++;

            PlayDays(1, 1);

            Assert.AreEqual(1, record.History.DayCount);
            Assert.AreEqual(11f, record.History.ValueOf(0, 1, GoodIndicator.UnderWay, Timber));
            Assert.AreEqual(2, record.Events.Count);
            Assert.AreEqual(SimulationEventType.Shortage, record.Events[0].Type);
            Assert.AreEqual(0, record.Events[0].City);
            Assert.AreEqual(1, record.Events[1].City);
            Assert.AreEqual(1, recorded);
        }

        [Test]
        public void ACrossing_ComparesWithTheDayBefore()
        {
            PlayDays(1, 1);
            first.Put(Fish, 100);

            PlayDays(2, 2);

            Assert.IsTrue(record.Events.Any(e => e.Type == SimulationEventType.ThresholdCrossing && e.City == 0 && e.Day == 2));
            Assert.IsFalse(record.Events.Any(e => e.Type == SimulationEventType.ThresholdCrossing && e.City == 1));
        }

        [Test]
        public void SetHorizon_TrimsHistoryEventsAndTradesTogether()
        {
            PlayDays(1, 10);
            int recorded = 0;
            record.Recorded += () => recorded++;

            record.SetHorizon(3);

            Assert.AreEqual(8, record.History.FirstDay);
            Assert.IsTrue(record.Events.All(e => e.Day >= 8));
            Assert.AreEqual(6, record.Events.Count);
            Assert.AreEqual(14, record.EventsTrimmed);
            Assert.AreEqual(3, record.Trades.Count);
            Assert.AreEqual(7, record.TradesTrimmed);
            Assert.AreEqual(8, record.Trades[0].Day);
            Assert.AreEqual(1, recorded);
        }

        [Test]
        public void TheHorizon_TrimsAsDaysPass()
        {
            record.SetHorizon(2);

            PlayDays(1, 5);

            Assert.AreEqual(4, record.Events.Count);
            Assert.AreEqual(2, record.Trades.Count);
        }

        [Test]
        public void AnAiTrade_IsRecorded_WithItsProfit()
        {
            PlayDays(1, 1);
            var ship = new Ship(new MapProjection(40f, 2f), UnityEngine.Vector2.zero, 4f, TestCrew.Full,
                new NavigationPathfinder(GridAssert.FromRows("....")), new CargoHold(200));

            record.AddTrade(1, second, new FleetTrade(ship, second, Fish, 40, 2640, true, 1480));

            TradeRecord trade = record.Trades[record.Trades.Count - 1];
            Assert.AreEqual(1, trade.City);
            Assert.AreEqual(Trader.AiMerchant, trade.Trader);
            Assert.AreSame(ship, trade.Ship);
            Assert.AreEqual(PlayerTradeKind.Sell, trade.Kind);
            Assert.AreEqual(40, trade.Units);
            Assert.AreEqual(2640, trade.Gold);
            Assert.AreEqual(1480, trade.Profit);
        }

        [Test]
        public void Flows_CountTheAiAndThePlayer()
        {
            var ship = new Ship(new MapProjection(40f, 2f), UnityEngine.Vector2.zero, 4f, TestCrew.Full,
                new NavigationPathfinder(GridAssert.FromRows("....")), new CargoHold(200));
            record.AddTrade(1, second, new FleetTrade(ship, second, Fish, 40, 2640, true, 1480));
            record.AddTrade(1, first, new FleetTrade(ship, first, Fish, 30, 900, false, null));
            record.AddPlayerTrade(1, second, Timber, 6, 60, PlayerTradeKind.Construction);
            record.AddPlayerTrade(1, second, Fish, 5, 60, PlayerTradeKind.Sell);

            record.FlowOf(1, Fish, out long received, out long shipped);
            Assert.AreEqual(45, received);
            Assert.AreEqual(0, shipped);

            record.FlowOf(1, Timber, out received, out shipped);
            Assert.AreEqual(0, received);
            Assert.AreEqual(6, shipped);

            record.FlowOf(0, Fish, out received, out shipped);
            Assert.AreEqual(30, shipped);
        }

        [Test]
        public void ATradeInACityNotRecorded_IsIgnored()
        {
            record.AddPlayerTrade(1, Market(), Fish, 5, 60, PlayerTradeKind.Buy);

            Assert.AreEqual(0, record.Trades.Count);
        }
    }
}
