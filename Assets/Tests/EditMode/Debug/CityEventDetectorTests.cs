using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityEventDetectorTests
    {
        private const int Fish = 0;
        private const int Iron = 1;
        private const int Day = 12;
        private const int City = 3;

        private readonly List<SimulationEvent> events = new List<SimulationEvent>();

        // NUnit runs every test on the same instance.
        [SetUp]
        public void SetUp()
        {
            events.Clear();
        }

        // Fish: 2 barrels a day for a thousand, not produced. Iron: 2 barrels a day, and a full workshop of 30 workers making 3.
        private static CityMarket FishAndIron(double startingDays)
        {
            return new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 30, 0.1) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                startingDays,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));
        }

        private static CityMarket LabourMarket(double startingDays, CityPopulation population, bool withSocial = false)
        {
            return new CityMarket(new[] { new MarketGood(10, 2, 60, 0.1) }, TestMarkets.Thresholds, new WorkforceRule(16, 8, 4, 1, 1, 10),
                startingDays, TestMarkets.Pricing, 7, population, TestMarkets.Labour, null, withSocial ? TestMarkets.Social : null);
        }

        private static CityPopulation Population(int rich, int wealthy, int poor, int beggars, int richRoom, int wealthyRoom, int poorRoom)
        {
            return new CityPopulation(new PerClass<int>(rich, wealthy, poor), beggars, new PerClass<int>(richRoom, wealthyRoom, poorRoom));
        }

        private static float[] Row(CityMarket market)
        {
            var row = new float[CitySnapshot.SizeFor(market.GoodCount)];
            CitySnapshot.Capture(market, good => 0, row);
            return row;
        }

        // A row of the market with the stock of a good set: T0 20, T1 60, T2 60, T3 80 for fish at a thousand.
        private static float[] RowWithStock(CityMarket market, int good, float stock)
        {
            float[] row = Row(market);
            row[CitySnapshot.IndexOf(GoodIndicator.Stock, good)] = stock;
            return row;
        }

        private void Detect(CityMarket market, float[] previous, float[] current, bool[] wasHalted = null)
        {
            CityEventDetector.Detect(Day, City, market, market.LastDay, previous, current, wasHalted ?? new bool[market.GoodCount], events);
        }

        private List<SimulationEvent> OfType(SimulationEventType type)
        {
            return events.FindAll(e => e.Type == type);
        }

        [Test]
        public void StockBands_PutAThresholdInTheBandAboveIt()
        {
            var thresholds = new StockThresholds(10, 30, 30, 40);

            Assert.AreEqual(StockBand.BelowT0, StockBands.Of(9.9, thresholds));
            Assert.AreEqual(StockBand.T0ToT1, StockBands.Of(10, thresholds));
            Assert.AreEqual(StockBand.T2ToT3, StockBands.Of(30, thresholds));
            Assert.AreEqual(StockBand.AboveT3, StockBands.Of(40, thresholds));
        }

        [Test]
        public void AShortage_IsAnEvent_WithTheUnitsMissing()
        {
            CityMarket market = FishAndIron(0);
            market.StartDay();

            Detect(market, null, Row(market));

            List<SimulationEvent> shortages = OfType(SimulationEventType.Shortage);
            Assert.AreEqual(2, shortages.Count);
            Assert.AreEqual(Day, shortages[0].Day);
            Assert.AreEqual(City, shortages[0].City);
            Assert.AreEqual(Fish, shortages[0].Good);
            Assert.AreEqual(2d, shortages[0].Amount, 1e-9);
        }

        [Test]
        public void ACrossing_IsAnEvent_WhateverMoved()
        {
            CityMarket market = FishAndIron(30);

            Detect(market, RowWithStock(market, Fish, 70), RowWithStock(market, Fish, 50));

            List<SimulationEvent> crossings = OfType(SimulationEventType.ThresholdCrossing);
            Assert.AreEqual(1, crossings.Count);
            Assert.AreEqual(Fish, crossings[0].Good);
            Assert.AreEqual((int)StockBand.T2ToT3, crossings[0].From);
            Assert.AreEqual((int)StockBand.T0ToT1, crossings[0].To);
        }

        [Test]
        public void TheFirstDay_HasNoCrossing()
        {
            CityMarket market = FishAndIron(30);

            Detect(market, null, RowWithStock(market, Fish, 5));

            Assert.AreEqual(0, OfType(SimulationEventType.ThresholdCrossing).Count);
        }

        [Test]
        public void HaltedThenResumed_AreTwoEvents()
        {
            // 100 days, 200 barrels of iron: above T3.
            CityMarket market = FishAndIron(100);
            var wasHalted = new bool[2];
            market.StartDay();
            Detect(market, null, Row(market), wasHalted);
            market.Take(Iron, market.AvailableOf(Iron));
            market.StartDay();
            Detect(market, null, Row(market), wasHalted);

            Assert.AreEqual(1, OfType(SimulationEventType.ProductionHalted).Count);
            Assert.AreEqual(Iron, OfType(SimulationEventType.ProductionHalted)[0].Good);
            Assert.AreEqual(1, OfType(SimulationEventType.ProductionResumed).Count);
            Assert.IsFalse(wasHalted[Iron]);
        }

        [Test]
        public void AWorkshopStillHalted_IsNoNewEvent()
        {
            CityMarket market = FishAndIron(100);
            var wasHalted = new bool[2];
            market.StartDay();
            Detect(market, null, Row(market), wasHalted);
            market.StartDay();
            Detect(market, null, Row(market), wasHalted);

            Assert.AreEqual(1, OfType(SimulationEventType.ProductionHalted).Count);
        }

        [Test]
        public void HiresAndLayOffs_AreOneEventEachPerGood()
        {
            // 24 places take 6 of the 40 beggars.
            CityMarket market = LabourMarket(0, new CityPopulation(1000, 40, 1024));
            market.StartDay();

            Detect(market, null, Row(market));

            Assert.AreEqual(1, OfType(SimulationEventType.Hired).Count);
            Assert.AreEqual(6d, OfType(SimulationEventType.Hired)[0].Amount);
            Assert.AreEqual(0, OfType(SimulationEventType.LaidOff).Count);
        }

        [Test]
        public void ALayOff_IsAnEvent()
        {
            CityMarket market = LabourMarket(100, new CityPopulation(1000));
            market.StartDay();

            Detect(market, null, Row(market));

            Assert.AreEqual(9d, OfType(SimulationEventType.LaidOff)[0].Amount);
        }

        [Test]
        public void BeggarsWhoCome_AreAnEvent()
        {
            CityMarket market = LabourMarket(0, new CityPopulation(1000, 40, 1024));
            market.StartDay();

            Detect(market, null, Row(market));

            Assert.AreEqual(7d, OfType(SimulationEventType.BeggarsArrived)[0].Amount);
            Assert.AreEqual(-1, OfType(SimulationEventType.BeggarsArrived)[0].Good);
            Assert.AreEqual(0, OfType(SimulationEventType.BeggarsLeft).Count);
        }

        [Test]
        public void ClassMoves_AreEvents_OnePerClass()
        {
            CityMarket market = LabourMarket(7, Population(60, 180, 600, 0, 200, 400, 700), true);
            market.StartDay();

            Detect(market, null, Row(market));

            List<SimulationEvent> moves = OfType(SimulationEventType.ClassChange);
            Assert.AreEqual(2, moves.Count);
            Assert.AreEqual((int)SocialClass.Rich, moves[0].To);
            Assert.AreEqual(6d, moves[0].Amount);
            Assert.AreEqual((int)SocialClass.Wealthy, moves[1].To);
            Assert.AreEqual(4d, moves[1].Amount);
        }

        [Test]
        public void ThePoorWhoLeave_AreAnEvent_WithTheirWorkers()
        {
            CityMarket market = LabourMarket(100, Population(0, 0, 1000, 0, 0, 0, 980), true);
            market.StartDay();

            Detect(market, null, Row(market));

            SimulationEvent leaving = OfType(SimulationEventType.PoorLeft)[0];
            Assert.AreEqual(4d, leaving.Amount);
            Assert.AreEqual(1, leaving.To);
        }

        [Test]
        public void AHouseOfTheCity_IsAnEvent()
        {
            var material = new MarketGood(10, 0, 0, 0);
            var housing = new CityHousingRule(SocialClass.Poor, 0.95, 100, new[] { new MaterialCost(0, 10), new MaterialCost(1, 10) });
            var market = new CityMarket(new[] { material, material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(960, 0, 1000), TestMarkets.Labour, new[] { housing });
            market.Put(0, 25);
            market.Put(1, 12);
            market.StartDay();

            Detect(market, null, Row(market));

            SimulationEvent house = OfType(SimulationEventType.CityHouseBuilt)[0];
            Assert.AreEqual((int)SocialClass.Poor, house.To);
            Assert.AreEqual(1d, house.Amount);
        }
    }
}
