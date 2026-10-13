using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TradeRoutePlannerTests
    {
        private const int Fish = 0;
        private const int Iron = 1;

        // Days of consumption a market starts with. The target is 30 days.
        private const double Empty = 0d;
        private const double AtTarget = 30d;
        private const double Surplus = 60d;

        // Two goods, 2 barrels a day each: 60 barrels at the target, 120 in surplus.
        private static CityMarket Market(double fishDays, double ironDays)
        {
            var market = new CityMarket(
                new[] { new MarketGood(5, 10, 25, 2, 0, 0), new MarketGood(25, 50, 125, 2, 0, 0) },
                30,
                0,
                new MarketPricing(0.05, 0.05),
                7,
                new CityPopulation(1000));

            Put(market, Fish, (int)(fishDays * 2));
            Put(market, Iron, (int)(ironDays * 2));
            return market;
        }

        private static void Put(CityMarket market, int good, int barrels)
        {
            if (barrels > 0)
            {
                market.Put(good, barrels);
            }
        }

        /// <summary>Every port a number of days from every other.</summary>
        private static double[,] Days(int ports, double days)
        {
            var table = new double[ports, ports];

            for (int i = 0; i < ports; i++)
            {
                for (int j = 0; j < ports; j++)
                {
                    table[i, j] = i == j ? 0d : days;
                }
            }

            return table;
        }

        private static void SetDays(double[,] table, int a, int b, double days)
        {
            table[a, b] = days;
            table[b, a] = days;
        }

        /// <summary>What loading one good alone gives: barrels while the next one pays.</summary>
        private static long ProfitOfOneGood(CityMarket source, CityMarket destination, int good, int capacity, out int barrels)
        {
            long profit = 0;
            barrels = 0;

            while (barrels < capacity && barrels < source.AvailableOf(good))
            {
                long margin = destination.SellPriceOf(good, barrels) - source.BuyPriceOf(good, barrels);

                if (margin <= 0)
                {
                    break;
                }

                profit += margin;
                barrels++;
            }

            return profit;
        }

        [Test]
        public void ItCarriesTheSurplusToTheShortage()
        {
            CityMarket source = Market(Surplus, AtTarget);
            CityMarket shortage = Market(Empty, AtTarget);
            CityMarket neutral = Market(AtTarget, AtTarget);
            var planner = new TradeRoutePlanner(new[] { source, neutral, shortage }, Days(3, 1d), 1d);

            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));

            long expected = ProfitOfOneGood(source, shortage, Fish, 200, out int barrels);
            Assert.AreEqual(2, plan.Destination);
            Assert.AreEqual(barrels, plan.BarrelsOf(Fish));
            Assert.AreEqual(0, plan.BarrelsOf(Iron));
            Assert.AreEqual(expected, plan.Profit);
            Assert.AreEqual(barrels, plan.TotalBarrels);
        }

        [Test]
        public void LoadingStopsWhenTheNextBarrelNoLongerPays()
        {
            CityMarket source = Market(Surplus, AtTarget);
            CityMarket shortage = Market(Empty, AtTarget);
            var planner = new TradeRoutePlanner(new[] { source, shortage }, Days(2, 1d), 1d);

            planner.TryPlanTrade(0, 200, null, out TradePlan plan);

            int loaded = plan.BarrelsOf(Fish);
            Assert.Greater(loaded, 0);
            Assert.Less(loaded, 120);
            Assert.Greater(shortage.SellPriceOf(Fish, loaded - 1), source.BuyPriceOf(Fish, loaded - 1));
            Assert.LessOrEqual(shortage.SellPriceOf(Fish, loaded), source.BuyPriceOf(Fish, loaded));
        }

        [Test]
        public void LoadingStopsAtTheCapacity()
        {
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget) }, Days(2, 1d), 1d);

            planner.TryPlanTrade(0, 7, null, out TradePlan plan);

            Assert.AreEqual(7, plan.TotalBarrels);
        }

        [Test]
        public void LoadingStopsAtTheStock()
        {
            // A city that does not consume a good sells it at its base price down to the
            // last barrel: only its stock of 10 stops the loading.
            var source = new CityMarket(
                new[] { new MarketGood(5, 10, 25, 0, 0, 0), new MarketGood(25, 50, 125, 0, 0, 0) },
                30,
                0,
                new MarketPricing(0.05, 0.05),
                7,
                new CityPopulation(1000));
            source.Put(Fish, 10);
            var planner = new TradeRoutePlanner(new[] { source, Market(Empty, AtTarget) }, Days(2, 1d), 1d);

            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(10, plan.BarrelsOf(Fish));
            Assert.AreEqual(0, plan.BarrelsOf(Iron));
        }

        [Test]
        public void ACargoMixesTheGoodsThatPayBest()
        {
            CityMarket source = Market(Surplus, Surplus);
            CityMarket shortage = Market(Empty, Empty);
            var planner = new TradeRoutePlanner(new[] { source, shortage }, Days(2, 1d), 1d);

            planner.TryPlanTrade(0, 60, null, out TradePlan plan);

            long fishAlone = ProfitOfOneGood(source, shortage, Fish, 60, out _);
            long ironAlone = ProfitOfOneGood(source, shortage, Iron, 60, out _);
            Assert.AreEqual(60, plan.TotalBarrels);
            Assert.Greater(plan.BarrelsOf(Fish), 0);
            Assert.Greater(plan.BarrelsOf(Iron), 0);
            Assert.GreaterOrEqual(plan.Profit, fishAlone);
            Assert.GreaterOrEqual(plan.Profit, ironAlone);
        }

        [Test]
        public void AtEqualProfit_TheNearerDestinationWins()
        {
            double[,] days = Days(3, 1d);
            SetDays(days, 0, 1, 3d);
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget), Market(Empty, AtTarget) }, days, 1d);

            planner.TryPlanTrade(0, 200, null, out TradePlan plan);

            Assert.AreEqual(2, plan.Destination);
        }

        [Test]
        public void APortThatCannotBeReached_IsIgnored()
        {
            double[,] days = Days(3, 1d);
            SetDays(days, 0, 2, double.PositiveInfinity);
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(AtTarget, AtTarget), Market(Empty, AtTarget) }, days, 1d);

            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(1, plan.Destination);
        }

        [Test]
        public void CargoUnderWayToAPort_CountsAsDeliveredThere()
        {
            double[,] days = Days(3, 1d);
            SetDays(days, 0, 2, 3d);
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget), Market(Empty, AtTarget) }, days, 1d);
            var incoming = new int[3, 2];
            incoming[1, Fish] = 200;

            planner.TryPlanTrade(0, 200, incoming, out TradePlan plan);

            Assert.AreEqual(2, plan.Destination);
        }

        [Test]
        public void WhenNothingPays_ThereIsNoTrade()
        {
            var planner = new TradeRoutePlanner(
                new[] { Market(AtTarget, AtTarget), Market(AtTarget, AtTarget) }, Days(2, 1d), 1d);

            Assert.IsFalse(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(0, plan.TotalBarrels);
            Assert.IsFalse(planner.TryPlanRelocation(0, 200, null, null, out _));
        }

        [Test]
        public void ASinglePort_HasNoTrade()
        {
            var planner = new TradeRoutePlanner(new[] { Market(Surplus, Surplus) }, Days(1, 0d), 1d);

            Assert.IsFalse(planner.TryPlanTrade(0, 200, null, out _));
            Assert.IsFalse(planner.TryPlanRelocation(0, 200, null, null, out _));
        }

        [Test]
        public void NoPort_IsAPlannerWithoutGoods()
        {
            var planner = new TradeRoutePlanner(new CityMarket[0], new double[0, 0], 1d);

            Assert.AreEqual(0, planner.PortCount);
            Assert.AreEqual(0, planner.GoodCount);
        }

        [Test]
        public void WithoutRoom_ThereIsNoTrade()
        {
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget) }, Days(2, 1d), 1d);

            Assert.IsFalse(planner.TryPlanTrade(0, 0, null, out _));
        }

        [Test]
        public void ATripOfNoTime_IsStillCompared()
        {
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget) }, Days(2, 0d), 0d);

            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(1, plan.Destination);
            Assert.Greater(plan.Profit, 0);
        }

        [Test]
        public void MarketsOfDifferentCatalogues_TradeTheGoodsTheyShare()
        {
            var small = new CityMarket(
                new[] { new MarketGood(5, 10, 25, 2, 0, 0) }, 30, 0, new MarketPricing(0.05, 0.05), 7, new CityPopulation(1000));
            var planner = new TradeRoutePlanner(new[] { Market(Surplus, Surplus), small }, Days(2, 1d), 1d);

            Assert.AreEqual(1, planner.GoodCount);
            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(0, plan.BarrelsOf(Iron));
        }

        [Test]
        public void Relocation_GoesWhereTheBestTradeStarts()
        {
            // Nothing to buy here; port 1 has a surplus that port 2 lacks.
            var planner = new TradeRoutePlanner(
                new[] { Market(Empty, AtTarget), Market(Surplus, AtTarget), Market(Empty, AtTarget) }, Days(3, 1d), 1d);

            Assert.IsFalse(planner.TryPlanTrade(0, 200, null, out _));
            Assert.IsTrue(planner.TryPlanRelocation(0, 200, null, null, out int destination));
            Assert.AreEqual(1, destination);
        }

        [Test]
        public void Relocation_IgnoresASourceThatCannotBeReached()
        {
            double[,] days = Days(3, 1d);
            SetDays(days, 0, 1, double.PositiveInfinity);
            var planner = new TradeRoutePlanner(
                new[] { Market(Empty, AtTarget), Market(Surplus, AtTarget), Market(Empty, AtTarget) }, days, 1d);

            Assert.IsFalse(planner.TryPlanRelocation(0, 200, null, null, out _));
        }

        [Test]
        public void Relocation_LeavesAClaimedSourceToTheShipThatSailsThere()
        {
            // Port 1 is the best place to trade from; port 3, with less to sell, is the next.
            var planner = new TradeRoutePlanner(
                new[]
                {
                    Market(Empty, AtTarget), Market(Surplus, AtTarget), Market(Empty, AtTarget), Market(AtTarget, AtTarget),
                },
                Days(4, 1d),
                1d);

            Assert.IsTrue(planner.TryPlanRelocation(0, 200, null, null, out int free));
            Assert.AreEqual(1, free);

            Assert.IsTrue(planner.TryPlanRelocation(0, 200, null, new[] { false, true, false, false }, out int other));
            Assert.AreEqual(3, other);

            Assert.IsFalse(planner.TryPlanRelocation(0, 200, null, new[] { false, true, false, true }, out _));
            Assert.Throws<ArgumentException>(() => planner.TryPlanRelocation(0, 200, null, new bool[2], out _));
        }

        [Test]
        public void Plans_GiveTheirProfitPerDay()
        {
            // Two days to the shortage and one of stopover.
            var planner = new TradeRoutePlanner(
                new[] { Market(Empty, AtTarget), Market(Surplus, AtTarget), Market(Empty, AtTarget) }, Days(3, 2d), 1d);

            Assert.IsTrue(planner.TryPlanTrade(1, 200, null, out TradePlan trade));
            Assert.AreEqual(trade.Profit / 3d, trade.ProfitPerDay, 1e-9);

            // From port 0: three days to get to port 1, then the three of its trade.
            Assert.IsTrue(planner.TryPlanRelocation(0, 200, null, null, out int source, out double profitPerDay));
            Assert.AreEqual(1, source);
            Assert.AreEqual(trade.Profit / 6d, profitPerDay, 1e-9);
        }

        [Test]
        public void RejectsBadArguments()
        {
            CityMarket[] markets = { Market(Surplus, AtTarget), Market(Empty, AtTarget) };
            var planner = new TradeRoutePlanner(markets, Days(2, 1d), 1d);

            Assert.Throws<ArgumentNullException>(() => new TradeRoutePlanner(null, Days(2, 1d), 1d));
            Assert.Throws<ArgumentNullException>(() => new TradeRoutePlanner(markets, null, 1d));
            Assert.Throws<ArgumentException>(() => new TradeRoutePlanner(markets, Days(3, 1d), 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TradeRoutePlanner(markets, Days(2, 1d), -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.TryPlanTrade(2, 200, null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.TryPlanTrade(0, -1, null, out _));
            Assert.Throws<ArgumentException>(() => planner.TryPlanTrade(0, 200, new int[1, 2], out _));
        }
    }
}
