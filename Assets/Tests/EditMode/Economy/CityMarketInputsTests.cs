using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketInputsTests
    {
        private const double Tolerance = 1e-9;

        private const double StartingDays = 30;

        private const int DaysPerWeek = 7;

        // Two units a day for the inhabitants, not produced.
        private static MarketGood Grain => new MarketGood(10, 2, 0, 0);

        // Two barrels a day for the inhabitants, 30 workers making 3 a day out of half a unit of grain each.
        private static MarketGood Beer => BeerTaking(0);

        private static MarketGood BeerTaking(int input, double unitsPerUnit = 0.5)
        {
            return new MarketGood(10, 2, 30, 0.1).WithInputs(new[] { new MarketInput(input, unitsPerUnit) });
        }

        private static CityMarket MarketOf(params MarketGood[] goods)
        {
            return new CityMarket(
                goods, TestMarkets.Thresholds, TestMarkets.Workforce, StartingDays, TestMarkets.Pricing, DaysPerWeek, new CityPopulation(1000));
        }

        [Test]
        public void TheInputNeed_IsWhatTheWorkshopsWouldTake_WithTheirWorkers()
        {
            CityMarket market = MarketOf(Grain, Beer);

            Assert.AreEqual(1.5, market.DailyInputNeedOf(0), Tolerance);
            Assert.AreEqual(0, market.DailyInputNeedOf(1), Tolerance);
            Assert.AreEqual(3.5, market.DailyDemandOf(0), Tolerance);
            Assert.AreEqual(2, market.DailyConsumptionOf(0), Tolerance);
        }

        [Test]
        public void TheThresholdsAndTheStartingStock_FollowTheDemand()
        {
            CityMarket market = MarketOf(Grain, Beer);

            // 10 and 30 days of 3.5 a day.
            Assert.AreEqual(35, market.ThresholdsOf(0).T0, Tolerance);
            Assert.AreEqual(105, market.ThresholdsOf(0).T1, Tolerance);
            Assert.AreEqual(105, market.StockOf(0), Tolerance);

            // Nothing takes beer as an input: its inhabitants only.
            Assert.AreEqual(60, market.StockOf(1), Tolerance);
        }

        [Test]
        public void AGoodWithoutRecipe_HasNoInputs()
        {
            Assert.AreEqual(0, Grain.Inputs.Count);
            Assert.AreEqual(0, default(MarketGood).Inputs.Count);
        }

        [Test]
        public void AnInputOutOfTheMarket_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MarketOf(Grain, BeerTaking(5)));
        }

        [Test]
        public void AGoodThatIsItsOwnInput_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => MarketOf(Grain, BeerTaking(1)));
        }

        [Test]
        public void AGoodListedTwiceInARecipe_IsRefused()
        {
            Assert.Throws<ArgumentException>(
                () => new MarketGood(10, 2, 30, 0.1).WithInputs(new[] { new MarketInput(0, 0.5), new MarketInput(0, 0.1) }));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void AQuantityThatIsNotAboveZero_IsRefused(double units)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketInput(0, units));
        }

        [Test]
        public void ANegativeInputGood_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketInput(-1, 0.5));
        }
    }
}
