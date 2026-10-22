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

        // After the inhabitants, every test market below holds 103 grain and 58 beer, under the
        // beer's T3 of 110, with its 30 workers.

        [Test]
        public void AWorkshopWithAllItsInputs_ProducesAsBefore_AndTakesThem()
        {
            CityMarket market = MarketOf(Grain, Beer);

            market.StartDay();

            Assert.AreEqual(61, market.StockOf(1), Tolerance);
            Assert.AreEqual(101.5, market.StockOf(0), Tolerance);
            Assert.AreEqual(1.5, market.RecentInputsOf(0), Tolerance);
            Assert.AreEqual(2, market.RecentConsumptionOf(0), Tolerance);
            Assert.AreEqual(1.5, market.LastDay.InputNeedOf(0), Tolerance);
            Assert.AreEqual(1.5, market.LastDay.InputsConsumedOf(0), Tolerance);
            Assert.AreEqual(0, market.LastDay.MissingOf(0), Tolerance);
        }

        [Test]
        public void AShortInput_BringsProductionDownToItsProportion()
        {
            CityMarket market = MarketOf(Grain, Beer);
            market.Take(0, 102);

            market.StartDay();

            // One grain left after the inhabitants, for 1.5 needed.
            Assert.AreEqual(60, market.StockOf(1), Tolerance);
            Assert.AreEqual(0, market.StockOf(0), Tolerance);
            Assert.AreEqual(2, market.RecentProductionOf(1), Tolerance);
            Assert.AreEqual(1.5, market.LastDay.InputNeedOf(0), Tolerance);
            Assert.AreEqual(1, market.LastDay.InputsConsumedOf(0), Tolerance);

            // The inhabitants had their 2: what the workshop lacked is no shortage of theirs.
            Assert.AreEqual(0, market.LastDay.MissingOf(0), Tolerance);
            Assert.AreEqual(2, market.LastDay.NeededOf(0), Tolerance);
        }

        [Test]
        public void TheScarcestOfTwoInputs_Decides()
        {
            // Grain and timber, half a unit of each per barrel: both start at 105.
            MarketGood beer = new MarketGood(10, 2, 30, 0.1).WithInputs(new[] { new MarketInput(0, 0.5), new MarketInput(1, 0.5) });
            CityMarket market = MarketOf(Grain, Grain, beer);
            market.Take(1, 102);

            market.StartDay();

            Assert.AreEqual(60, market.StockOf(2), Tolerance);
            Assert.AreEqual(102, market.StockOf(0), Tolerance);
            Assert.AreEqual(0, market.StockOf(1), Tolerance);
        }

        [Test]
        public void AnInputAtZero_StopsTheWorkshop()
        {
            CityMarket market = MarketOf(Grain, Beer);
            market.Take(0, 105);

            market.StartDay();

            Assert.AreEqual(58, market.StockOf(1), Tolerance);
            Assert.AreEqual(0, market.StockOf(0), Tolerance);
            Assert.AreEqual(0, market.RecentInputsOf(0), Tolerance);
        }

        [Test]
        public void AboveT3_NothingIsProduced_ButTheInputsAreTaken()
        {
            CityMarket market = MarketOf(Grain, Beer);
            market.Put(1, 100);

            market.StartDay();

            // 158 beer after the inhabitants: the workshop keeps one worker, who takes his grain.
            Assert.AreEqual(158, market.StockOf(1), Tolerance);
            Assert.AreEqual(0, market.RecentProductionOf(1), Tolerance);
            Assert.AreEqual(103 - 0.05, market.StockOf(0), Tolerance);
            Assert.AreEqual(0.05, market.RecentInputsOf(0), Tolerance);
            Assert.AreEqual(0.05, market.DailyInputNeedOf(0), Tolerance);
        }

        [Test]
        public void TwoWorkshops_ShareAScarceInput_InTheCataloguesOrder()
        {
            // 150 grain for a demand of 5; 2 left after the inhabitants, for 1.5 and 1.5.
            CityMarket market = MarketOf(Grain, Beer, Beer);
            market.Take(0, 146);

            market.StartDay();

            Assert.AreEqual(61, market.StockOf(1), Tolerance);
            Assert.AreEqual(59, market.StockOf(2), Tolerance);
            Assert.AreEqual(0, market.StockOf(0), Tolerance);
        }

        [Test]
        public void AnInputTakenWhole_LeavesAStockOfZero_NotBelow()
        {
            CityMarket market = MarketOf(Grain, Beer);
            market.Take(0, 102);

            for (int day = 0; day < 30; day++)
            {
                market.StartDay();

                Assert.GreaterOrEqual(market.StockOf(0), 0d);

                if (day == 0)
                {
                    Assert.AreEqual(0d, market.StockOf(0));
                }
            }
        }

        [Test]
        public void AWorkshopWithoutWorkers_TakesNothing_AndNoStockBecomesNaN()
        {
            MarketGood idle = new MarketGood(10, 2, 0, 0).WithInputs(new[] { new MarketInput(0, 0.5) });
            CityMarket market = MarketOf(Grain, idle);
            market.Take(0, 60);

            market.StartDay();

            Assert.AreEqual(0, market.StockOf(0), Tolerance);
            Assert.AreEqual(58, market.StockOf(1), Tolerance);

            market.StartDay();

            Assert.IsFalse(double.IsNaN(market.StockOf(0)));
            Assert.IsFalse(double.IsNaN(market.StockOf(1)));
            Assert.AreEqual(0, market.RecentInputsOf(0), Tolerance);
        }

        [Test]
        public void TheNominalWeeklyInputNeed_IsSevenDaysOfIt()
        {
            Assert.AreEqual(10.5, MarketOf(Grain, Beer).NominalWeeklyInputNeedOf(0), Tolerance);
        }

        [Test]
        public void TheDayReport_HoldsTheInputsOfTheLastDayOnly()
        {
            CityMarket market = MarketOf(Grain, Beer);
            market.StartDay();
            market.Take(0, 101);

            market.StartDay();

            Assert.AreEqual(1.5, market.LastDay.InputNeedOf(0), Tolerance);
            Assert.AreEqual(0, market.LastDay.InputsConsumedOf(0), Tolerance);
            Assert.AreEqual(1.5, market.RecentInputsOf(0), Tolerance);
        }
    }
}
