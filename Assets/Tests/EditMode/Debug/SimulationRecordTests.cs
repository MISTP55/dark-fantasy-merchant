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
                record.AddPlayerTrade(day, first, Timber, 1, PlayerTradeKind.Buy);
            }
        }

        [Test]
        public void RecordDay_RecordsEveryCity()
        {
            int recorded = 0;
            record.Recorded += () => recorded++;

            PlayDays(1, 1);

            Assert.AreEqual(1, record.History.DayCount);
            Assert.AreEqual(11f, record.History.ValueOf(0, 1, GoodIndicator.UnderWay, Timber));
            Assert.AreEqual(2f, record.History.ValueOf(0, 0, GoodIndicator.Missing, Fish));
            Assert.AreEqual(1, recorded);
        }

        [Test]
        public void SetHorizon_TrimsHistoryAndTradesTogether()
        {
            PlayDays(1, 10);
            int recorded = 0;
            record.Recorded += () => recorded++;

            record.SetHorizon(3);

            Assert.AreEqual(8, record.History.FirstDay);
            Assert.AreEqual(3, record.Trades.Count);
            Assert.AreEqual(8, record.Trades[0].Day);
            Assert.AreEqual(1, recorded);
        }

        [Test]
        public void TheHorizon_TrimsAsDaysPass()
        {
            record.SetHorizon(2);

            PlayDays(1, 5);

            Assert.AreEqual(2, record.History.DayCount);
            Assert.AreEqual(2, record.Trades.Count);
        }

        [Test]
        public void Flows_CountTheAiAndThePlayer()
        {
            var ship = new Ship(new MapProjection(40f, 2f), UnityEngine.Vector2.zero, 4f, TestCrew.Full,
                new NavigationPathfinder(GridAssert.FromRows("....")), new CargoHold(200));
            record.AddTrade(1, second, new FleetTrade(ship, second, Fish, 40, 2640, true));
            record.AddTrade(1, first, new FleetTrade(ship, first, Fish, 30, 900, false));
            record.AddPlayerTrade(1, second, Timber, 6, PlayerTradeKind.Construction);
            record.AddPlayerTrade(1, second, Fish, 5, PlayerTradeKind.Sell);

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
        public void FlowsOf_GivesEveryCityInOnePass_AsFlowOf()
        {
            var ship = new Ship(new MapProjection(40f, 2f), UnityEngine.Vector2.zero, 4f, TestCrew.Full,
                new NavigationPathfinder(GridAssert.FromRows("....")), new CargoHold(200));
            record.AddTrade(1, second, new FleetTrade(ship, second, Fish, 40, 2640, true));
            record.AddTrade(1, first, new FleetTrade(ship, first, Fish, 30, 900, false));
            record.AddPlayerTrade(1, second, Fish, 5, PlayerTradeKind.Sell);
            record.AddPlayerTrade(1, second, Timber, 6, PlayerTradeKind.Construction);
            var received = new long[2];
            var shipped = new long[2];

            record.FlowsOf(Fish, received, shipped);

            CollectionAssert.AreEqual(new long[] { 0, 45 }, received);
            CollectionAssert.AreEqual(new long[] { 30, 0 }, shipped);
        }

        [Test]
        public void ATradeInACityNotRecorded_IsIgnored()
        {
            record.AddPlayerTrade(1, Market(), Fish, 5, PlayerTradeKind.Buy);

            Assert.AreEqual(0, record.Trades.Count);
        }
    }
}
