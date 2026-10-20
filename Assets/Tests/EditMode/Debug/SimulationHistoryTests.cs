using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class SimulationHistoryTests
    {
        private const int Fish = 0;
        private const int Iron = 1;

        // Fish: 2 barrels a day for a thousand, not produced, 60 in stock. Iron: a full workshop of 30 making 3.
        private static CityMarket Market(double startingDays = 30)
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

        // One city of two goods, each day's stock of fish set to the day.
        private static SimulationHistory HistoryOfDays(int? horizon, int firstDay, int lastDay)
        {
            var history = new SimulationHistory(1, 2, horizon);

            for (int day = firstDay; day <= lastDay; day++)
            {
                history.BeginDay(day)[0][CitySnapshot.IndexOf(GoodIndicator.Stock, Fish)] = day;
            }

            return history;
        }

        [Test]
        public void Capture_WritesEveryIndicator()
        {
            CityMarket market = Market(0);
            market.StartDay();
            var history = new SimulationHistory(1, 2, null);

            CitySnapshot.Capture(market, good => good == Fish ? 7 : 0, history.BeginDay(1)[0]);

            Assert.AreEqual((float)market.StockOf(Iron), history.ValueOf(0, 0, GoodIndicator.Stock, Iron), 1e-4);
            Assert.AreEqual((float)market.ThresholdsOf(Fish).T0, history.ValueOf(0, 0, GoodIndicator.T0, Fish), 1e-4);
            Assert.AreEqual((float)market.ThresholdsOf(Fish).T3, history.ValueOf(0, 0, GoodIndicator.T3, Fish), 1e-4);
            Assert.AreEqual(market.BuyPriceOf(Fish), history.ValueOf(0, 0, GoodIndicator.BuyPrice, Fish), 1e-4);
            Assert.AreEqual(market.SellPriceOf(Iron), history.ValueOf(0, 0, GoodIndicator.SellPrice, Iron), 1e-4);
            Assert.AreEqual(2f, history.ValueOf(0, 0, GoodIndicator.Missing, Fish), 1e-4);
            Assert.AreEqual((float)market.RecentProductionOf(Iron), history.ValueOf(0, 0, GoodIndicator.RecentProduction, Iron), 1e-4);
            Assert.AreEqual((float)market.NominalWeeklyConsumptionOf(Fish), history.ValueOf(0, 0, GoodIndicator.NominalWeeklyConsumption, Fish), 1e-4);
            Assert.AreEqual(market.WorkersOf(Iron), history.ValueOf(0, 0, GoodIndicator.Workers, Iron), 1e-4);
            Assert.AreEqual(7f, history.ValueOf(0, 0, GoodIndicator.UnderWay, Fish));
            Assert.AreEqual(0f, history.ValueOf(0, 0, GoodIndicator.UnderWay, Iron));
            Assert.AreEqual(1000f, history.ValueOf(0, 0, CityIndicator.Inhabitants));
            Assert.AreEqual(1000f, history.ValueOf(0, 0, CityIndicator.Poor));
            Assert.AreEqual(market.Population.RoomOf(SocialClass.Poor), history.ValueOf(0, 0, CityIndicator.PoorRoom));
            Assert.AreEqual(market.Population.SatisfactionOf(SocialClass.Rich), history.ValueOf(0, 0, CityIndicator.RichSatisfaction));
            Assert.AreEqual(market.BeggarTarget, history.ValueOf(0, 0, CityIndicator.BeggarTarget));
            Assert.AreEqual(0f, history.ValueOf(0, 0, CityIndicator.CityHouses));
        }

        [Test]
        public void History_KeepsOnlyTheHorizon()
        {
            SimulationHistory history = HistoryOfDays(3, 1, 5);

            Assert.AreEqual(3, history.DayCount);
            Assert.AreEqual(3, history.DayAt(0));
            Assert.AreEqual(3, history.FirstDay);
            Assert.AreEqual(5, history.DayAt(2));
            Assert.AreEqual(5f, history.ValueOf(2, 0, GoodIndicator.Stock, Fish));
        }

        [Test]
        public void SetHorizon_Shorter_TrimsAtOnce()
        {
            SimulationHistory history = HistoryOfDays(null, 1, 10);

            history.SetHorizon(4);

            Assert.AreEqual(4, history.HorizonDays);
            Assert.AreEqual(4, history.DayCount);
            Assert.AreEqual(7, history.DayAt(0));
            Assert.AreEqual(7f, history.ValueOf(0, 0, GoodIndicator.Stock, Fish));
        }

        [Test]
        public void SetHorizon_Longer_BringsNothingBack()
        {
            SimulationHistory history = HistoryOfDays(2, 1, 5);

            history.SetHorizon(null);
            history.BeginDay(6);

            Assert.IsNull(history.HorizonDays);
            Assert.AreEqual(3, history.DayCount);
            Assert.AreEqual(4, history.DayAt(0));
        }

        [Test]
        public void History_NeverDropsTheLastDay()
        {
            SimulationHistory history = HistoryOfDays(1, 1, 3);

            Assert.Throws<ArgumentOutOfRangeException>(() => history.SetHorizon(0));
            Assert.AreEqual(1, history.DayCount);
            Assert.AreEqual(3, history.DayAt(0));
        }

        [Test]
        public void ATrimmedDay_IsReusedClean()
        {
            SimulationHistory history = HistoryOfDays(1, 1, 1);

            float[][] rows = history.BeginDay(2);

            Assert.AreEqual(0f, rows[0][CitySnapshot.IndexOf(GoodIndicator.Stock, Fish)]);
            Assert.AreEqual(CitySnapshot.SizeFor(2), rows[0].Length);
        }

        [Test]
        public void BeginDay_RefusesADayNotAfterTheLast()
        {
            SimulationHistory history = HistoryOfDays(null, 1, 3);

            Assert.Throws<ArgumentOutOfRangeException>(() => history.BeginDay(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => history.BeginDay(2));
        }

        [Test]
        public void AnEmptyHistory_HasNoFirstDay()
        {
            var history = new SimulationHistory(2, 2, 360);

            Assert.AreEqual(0, history.DayCount);
            Assert.AreEqual(-1, history.FirstDay);
        }

        [Test]
        public void AGoodTheMarketLacks_IsNaN()
        {
            var village = new CityMarket(new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, TestMarkets.Workforce, 30,
                TestMarkets.Pricing, 7, new CityPopulation(500));
            var history = new SimulationHistory(1, 2, null);

            CitySnapshot.Capture(village, good => 0, history.BeginDay(0)[0]);

            Assert.IsTrue(float.IsNaN(history.ValueOf(0, 0, GoodIndicator.Stock, Iron)));
            Assert.AreEqual(30f, history.ValueOf(0, 0, GoodIndicator.Stock, Fish), 1e-4);
        }
    }
}
