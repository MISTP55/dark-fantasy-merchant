using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class SupplyPlannerTests
    {
        // With 2 consumed a day (population 1000): T0 = 20, T1 = 60, T2 = 60, T3 = 80, the middle
        // of T0 and T1 is 40 and the target point 20 + 3/8 × 40 = 35. Factors: the curves of TestMarkets.

        /// <summary>One good, 10 gold, consumed by the day; its stock set by the days it starts with.</summary>
        private static CityMarket Market(double stock, double dailyConsumption = 2, int maxWorkers = 0, double output = 0)
        {
            var market = new CityMarket(
                new[] { new MarketGood(10, dailyConsumption, maxWorkers, output) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                dailyConsumption > 0 ? stock / dailyConsumption : 0,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));

            if (dailyConsumption <= 0 && stock > 0)
            {
                market.Put(0, (int)stock);
            }

            return market;
        }

        /// <summary>Two goods, 2 consumed a day of each, with the given stocks.</summary>
        private static CityMarket MarketOf(double first, double second)
        {
            var market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 2, 0, 0) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                0,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));

            if (first > 0)
            {
                market.Put(0, (int)first);
            }

            if (second > 0)
            {
                market.Put(1, (int)second);
            }

            return market;
        }

        /// <summary>An empty market of the same good, on other curves.</summary>
        private static CityMarket MarketPricedBy(MarketPricing pricing)
        {
            return new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                0,
                pricing,
                7,
                new CityPopulation(1000));
        }

        /// <summary>A symmetric matrix of the same days between any two ports, 0 between a port and itself.</summary>
        private static double[,] Days(int n, double value)
        {
            var days = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    days[i, j] = i == j ? 0d : value;
                }
            }

            return days;
        }

        private static void SetDays(double[,] days, int a, int b, double value)
        {
            days[a, b] = value;
            days[b, a] = value;
        }

        private static SupplyPlanner Planner(params CityMarket[] markets)
        {
            return new SupplyPlanner(markets, Days(markets.Length, 1));
        }

        private static SupplyPlanner Planner(CityMarket[] markets, double[,] days, GoodUnits units = null, int[] weights = null)
        {
            return new SupplyPlanner(markets, days, units, weights);
        }

        [Test]
        public void TheProjectedStock_AddsWhatIsUnderWay_AndTheDaysOfTheTrip()
        {
            // 2 consumed a day; 40 in stock; 4 under way; 5 days: 40 + 4 − 10.
            var planner = Planner(Market(40), Market(0));
            Assert.AreEqual(34d, planner.ProjectedStockOf(0, 0, 5, 4), 1e-9);
        }

        [Test]
        public void TheProjectedStock_CountsTheProductionOfTheDays()
        {
            // A workshop of 30 workers making 0.1 a day: 3 a day, 2 consumed.
            var planner = Planner(Market(40, 2, 30, 0.1), Market(0));
            Assert.AreEqual(45d, planner.ProjectedStockOf(0, 0, 5, 0), 1e-9);
        }

        [Test]
        public void TheProjectedStock_IsNeverBelowNothing()
        {
            var planner = Planner(Market(6), Market(0));
            Assert.AreEqual(0d, planner.ProjectedStockOf(0, 0, 5, 0));
        }

        [Test]
        public void TheScore_IsTheGapOfFactors_TimesTheWeight()
        {
            // A at 120 (above T3: buys at 0.6), B empty (sells at 2.0); weight 16.
            var planner = Planner(new[] { Market(120), Market(0) }, Days(2, 1), weights: new[] { 16 });

            Assert.IsTrue(planner.TryEvaluate(0, 1, 0, null, out int units, out double score));
            Assert.AreEqual(60, units);
            Assert.AreEqual((2.0 - 0.6) * 16, score, 1e-9);
        }

        [Test]
        public void AWeightOfZero_IsNeverCarried()
        {
            var planner = Planner(new[] { Market(120), Market(0) }, Days(2, 1), weights: new[] { 0 });
            Assert.IsFalse(planner.TryEvaluate(0, 1, 0, null, out _, out _));
        }

        [Test]
        public void AScoreOfZeroOrBelow_IsNoTransport()
        {
            // The destination pays 0.5 at any stock: under the 0.6 the source sells at.
            CityMarket flat = MarketPricedBy(new MarketPricing(TestMarkets.BuyCurve, new PriceCurve(0.5, 0.5, 0.5, 0.5, 0.5)));
            var planner = Planner(Market(120), flat);
            Assert.IsFalse(planner.TryEvaluate(0, 1, 0, null, out _, out _));
        }

        [Test]
        public void UnderTheSmallestLot_IsNoTransport()
        {
            // B projected at 56: 4 short of T1, under 5 barrels.
            var planner = Planner(new[] { Market(120), Market(58) }, Days(2, 1));
            Assert.IsFalse(planner.TryEvaluate(0, 1, 0, null, out _, out _));
        }

        [Test]
        public void WhatIsUnderWay_LowersTheQuantity()
        {
            // B projected at 0 + 30 − 2 = 28: 32 short of T1.
            var underWay = new int[2, 1];
            underWay[1, 0] = 30;
            var planner = Planner(new[] { Market(120), Market(0) }, Days(2, 1));

            Assert.IsTrue(planner.TryEvaluate(0, 1, 0, underWay, out int units, out _));
            Assert.AreEqual(32, units);
        }

        [Test]
        public void TheDestination_IsTheHighestTotal()
        {
            // Two goods; home (0) holds 120 of both. C1 is empty of the first only: 2.0 − 0.6 = 1.4.
            // C2 is empty of both: 1.4 + 1.4 = 2.8. C1 holds 100 of the second, which it needs no more of.
            var planner = Planner(MarketOf(120, 120), MarketOf(0, 100), MarketOf(0, 0));
            Assert.AreEqual(2, planner.ChooseDestination(0, null));
        }

        [Test]
        public void TheDestination_CountsWhatItWouldBringBack()
        {
            // Home (0) is empty and has nothing to send; C1 is empty too; C2 holds 120, which home, projected
            // at 0, lacks: 2.0 − 0.6 = 1.4 for what C2 sends back.
            var planner = Planner(Market(0), Market(0), Market(120));
            Assert.AreEqual(2, planner.ChooseDestination(0, null));
        }

        [Test]
        public void TheDestination_LeavesOutHome_AndUnreachableCities()
        {
            // Home holds 120 and has no need; the only city that has one is unreachable.
            double[,] days = Days(2, 1);
            SetDays(days, 0, 1, double.PositiveInfinity);
            var planner = Planner(new[] { Market(120), Market(0) }, days);

            Assert.AreEqual(-1, planner.ChooseDestination(0, null));
        }

        [Test]
        public void WithNothingWorthItAnywhere_ThereIsNoDestination()
        {
            // Every market at 15 days: 30, under the middle of T0 and T1 (40), so nobody gives.
            var planner = Planner(Market(30), Market(30), Market(30));
            Assert.AreEqual(-1, planner.ChooseDestination(0, null));
        }

        [Test]
        public void TheTripsLength_DividesNothing()
        {
            // Source at 120, near city (1 day) with 1 in stock, far city (10 days) with 0, neither consuming:
            // thresholds at the floors 5, 10, 15, 20. Near: sells at 2.0 − 0.6 × 1/5 = 1.88, so 1.28;
            // far: 2.0 − 0.6 = 1.4. The far city scores more, so it is chosen although it is ten times farther.
            double[,] days = Days(3, 1);
            SetDays(days, 0, 2, 10);
            SetDays(days, 1, 2, 10);
            var planner = Planner(new[] { Market(120), Market(1, 0), Market(0, 0) }, days);

            Assert.AreEqual(2, planner.ChooseDestination(0, null));
        }

        // Stopover: port 0, home 1, candidate 2.
        private static SupplyPlanner PlannerForAStopover(double candidateToHome)
        {
            double[,] days = Days(3, 1);
            SetDays(days, 0, 1, 2);
            SetDays(days, 0, 2, 1);
            SetDays(days, 1, 2, candidateToHome);

            return Planner(new[] { Market(120), Market(0), Market(0) }, days);
        }

        [Test]
        public void AStopover_MustBeAShortDetour()
        {
            // Days 0–1 = 2; 0–2 = 1 and 2–1 = 1.3: 2.3 is under 1.2 × 2 = 2.4 → 2; with 2–1 = 1.4 the detour is 2.4 → none.
            Assert.AreEqual(2, PlannerForAStopover(1.3).ChooseStopover(0, 1, null, null));
            Assert.AreEqual(-1, PlannerForAStopover(1.4).ChooseStopover(0, 1, null, null));
        }

        [Test]
        public void TheStopover_IsWhereTheShipIsMostUseful()
        {
            // Port 0 holds 120. Candidate 2 holds 20 (projected 18: sells at 2.0 − 0.6 × 18/20 = 1.46, so 0.86);
            // candidate 3 is empty (1.4). Both detours are 2 (under 2.4); the higher value wins over the lower index.
            double[,] days = Days(4, 1);
            SetDays(days, 0, 1, 2);
            var planner = Planner(new[] { Market(120), Market(0), Market(20), Market(0) }, days);

            Assert.AreEqual(3, planner.ChooseStopover(0, 1, null, null));
        }

        [Test]
        public void AStopoverTie_GoesToTheShortestDetour()
        {
            // Candidates 2 and 3 are empty and equally far from port 0 (1 day): equal values.
            // Detours 1 + 1.2 = 2.2 for 2 and 1 + 1.1 = 2.1 for 3, both under 2.4 → 3.
            double[,] days = Days(4, 1);
            SetDays(days, 0, 1, 2);
            SetDays(days, 1, 2, 1.2);
            SetDays(days, 1, 3, 1.1);
            var planner = Planner(new[] { Market(120), Market(0), Market(0), Market(0) }, days);

            Assert.AreEqual(3, planner.ChooseStopover(0, 1, null, null));
        }

        [Test]
        public void AnExcludedCity_IsNoStopover()
        {
            var planner = PlannerForAStopover(1.3);
            Assert.AreEqual(-1, planner.ChooseStopover(0, 1, null, new[] { false, false, true }));
        }

        [Test]
        public void WithoutACandidate_ThereIsNoStopover()
        {
            // Only port and home.
            var planner = Planner(Market(120), Market(0));
            Assert.AreEqual(-1, planner.ChooseStopover(0, 1, null, null));
        }

        [Test]
        public void Load_TakesTheGoods_ByDecreasingScore_WithinTheRoom()
        {
            // Weights 16 and 9, same stocks (120 → empty) and needs (60 each); room 80: 60 of the first, 20 of the second.
            var planner = Planner(new[] { MarketOf(120, 120), MarketOf(0, 0) }, Days(2, 1), weights: new[] { 16, 9 });
            var units = new int[2];

            Assert.AreEqual(80, planner.Load(0, 1, 80, null, units));
            Assert.AreEqual(60, units[0]);
            Assert.AreEqual(20, units[1]);
        }

        [Test]
        public void Load_TakesTheBetterScoreFirst_WhateverTheIndex()
        {
            // The same, with the heavier good second: it fills the room first.
            var planner = Planner(new[] { MarketOf(120, 120), MarketOf(0, 0) }, Days(2, 1), weights: new[] { 9, 16 });
            var units = new int[2];

            Assert.AreEqual(80, planner.Load(0, 1, 80, null, units));
            Assert.AreEqual(20, units[0]);
            Assert.AreEqual(60, units[1]);
        }

        [Test]
        public void Load_CountsABushelForTenBarrels()
        {
            // Bushels of 10 barrels; a surplus of 60 bushels, room 25: 2 bushels, 20 barrels.
            var planner = Planner(new[] { Market(120), Market(0) }, Days(2, 1), new GoodUnits(new[] { 10 }));
            var units = new int[1];

            Assert.AreEqual(20, planner.Load(0, 1, 25, null, units));
            Assert.AreEqual(2, units[0]);
        }

        [Test]
        public void Load_SkipsABushelThatNoLongerFits_AndLoadsBarrels()
        {
            // The bushel good (weight 16) scores higher than the barrel one (9), but room 9 holds no bushel: 9 barrels.
            var planner = Planner(
                new[] { MarketOf(120, 120), MarketOf(0, 0) }, Days(2, 1), new GoodUnits(new[] { 10, 1 }), new[] { 16, 9 });
            var units = new int[2];

            Assert.AreEqual(9, planner.Load(0, 1, 9, null, units));
            Assert.AreEqual(0, units[0]);
            Assert.AreEqual(9, units[1]);
        }

        [Test]
        public void Load_TakesTheUnitsThatFit_EvenUnderTheSmallestLot()
        {
            // Quantity 60, room 3 → 3, under the lot of 5.
            var planner = Planner(Market(120), Market(0));
            var units = new int[1];

            Assert.AreEqual(3, planner.Load(0, 1, 3, null, units));
            Assert.AreEqual(3, units[0]);
        }

        [Test]
        public void Load_ClearsTheUnitsFirst()
        {
            var planner = Planner(Market(30), Market(0));
            var units = new int[] { 7 };

            Assert.AreEqual(0, planner.Load(0, 1, 50, null, units));
            Assert.AreEqual(0, units[0]);
        }

        [Test]
        public void ANegativeWeight_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Planner(new[] { Market(0), Market(0) }, Days(2, 1), weights: new[] { -1 }));
        }

        [Test]
        public void TheArguments_AreChecked()
        {
            SupplyPlanner planner = Planner(Market(0), Market(0), Market(0));

            Assert.Throws<ArgumentOutOfRangeException>(() => planner.TryEvaluate(3, 0, 0, null, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.Load(0, 1, -1, null, new int[1]));
            Assert.Throws<ArgumentException>(() => planner.ChooseDestination(0, new int[1, 1]));
            Assert.Throws<ArgumentException>(() => planner.ChooseStopover(0, 1, null, new[] { false }));
            Assert.Throws<ArgumentException>(() => planner.Load(0, 1, 10, null, new int[0]));
        }
    }
}
