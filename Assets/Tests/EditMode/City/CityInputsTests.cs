using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityInputsTests
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

        private static City CityOf(params MarketGood[] goods)
        {
            return new City(
                goods, TestMarkets.Thresholds, TestMarkets.Workforce, StartingDays, TestMarkets.Pricing, DaysPerWeek, new CityPopulation(1000));
        }

        [Test]
        public void TheInputNeed_IsWhatTheWorkshopsWouldTake_WithTheirWorkers()
        {
            City city = CityOf(Grain, Beer);

            Assert.AreEqual(1.5, city.DailyInputNeedOf(0), Tolerance);
            Assert.AreEqual(0, city.DailyInputNeedOf(1), Tolerance);
            Assert.AreEqual(3.5, city.DailyDemandOf(0), Tolerance);
            Assert.AreEqual(2, city.DailyConsumptionOf(0), Tolerance);
        }

        [Test]
        public void TheThresholdsAndTheStartingStock_FollowTheDemand()
        {
            City city = CityOf(Grain, Beer);

            // 10 and 30 days of 3.5 a day.
            Assert.AreEqual(35, city.Market.ThresholdsOf(0).T0, Tolerance);
            Assert.AreEqual(105, city.Market.ThresholdsOf(0).T1, Tolerance);
            Assert.AreEqual(105, city.Market.StockOf(0), Tolerance);

            // Nothing takes beer as an input: its inhabitants only.
            Assert.AreEqual(60, city.Market.StockOf(1), Tolerance);
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
            Assert.Throws<ArgumentOutOfRangeException>(() => CityOf(Grain, BeerTaking(5)));
        }

        [Test]
        public void AGoodThatIsItsOwnInput_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => CityOf(Grain, BeerTaking(1)));
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
            City city = CityOf(Grain, Beer);

            city.StartDay();

            Assert.AreEqual(61, city.Market.StockOf(1), Tolerance);
            Assert.AreEqual(101.5, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(1.5, city.RecentInputsOf(0), Tolerance);
            Assert.AreEqual(2, city.RecentConsumptionOf(0), Tolerance);
            Assert.AreEqual(1.5, city.LastDay.InputNeedOf(0), Tolerance);
            Assert.AreEqual(1.5, city.LastDay.InputsConsumedOf(0), Tolerance);
            Assert.AreEqual(0, city.LastDay.MissingOf(0), Tolerance);
        }

        [Test]
        public void AShortInput_BringsProductionDownToItsProportion()
        {
            City city = CityOf(Grain, Beer);
            city.Market.Take(0, 102);

            city.StartDay();

            // One grain left after the inhabitants, for 1.5 needed.
            Assert.AreEqual(60, city.Market.StockOf(1), Tolerance);
            Assert.AreEqual(0, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(2, city.RecentProductionOf(1), Tolerance);
            Assert.AreEqual(1.5, city.LastDay.InputNeedOf(0), Tolerance);
            Assert.AreEqual(1, city.LastDay.InputsConsumedOf(0), Tolerance);

            // The inhabitants had their 2: what the workshop lacked is no shortage of theirs.
            Assert.AreEqual(0, city.LastDay.MissingOf(0), Tolerance);
            Assert.AreEqual(2, city.LastDay.NeededOf(0), Tolerance);
        }

        [Test]
        public void TheScarcestOfTwoInputs_Decides()
        {
            // Grain and timber, half a unit of each per barrel: both start at 105.
            MarketGood beer = new MarketGood(10, 2, 30, 0.1).WithInputs(new[] { new MarketInput(0, 0.5), new MarketInput(1, 0.5) });
            City city = CityOf(Grain, Grain, beer);
            city.Market.Take(1, 102);

            city.StartDay();

            Assert.AreEqual(60, city.Market.StockOf(2), Tolerance);
            Assert.AreEqual(102, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(0, city.Market.StockOf(1), Tolerance);
        }

        [Test]
        public void AnInputAtZero_StopsTheWorkshop()
        {
            City city = CityOf(Grain, Beer);
            city.Market.Take(0, 105);

            city.StartDay();

            Assert.AreEqual(58, city.Market.StockOf(1), Tolerance);
            Assert.AreEqual(0, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(0, city.RecentInputsOf(0), Tolerance);
        }

        [Test]
        public void AboveT3_NothingIsProduced_ButTheInputsAreTaken()
        {
            City city = CityOf(Grain, Beer);
            city.Market.Put(1, 100);

            city.StartDay();

            // 158 beer after the inhabitants: the workshop keeps one worker, who takes his grain.
            Assert.AreEqual(158, city.Market.StockOf(1), Tolerance);
            Assert.AreEqual(0, city.RecentProductionOf(1), Tolerance);
            Assert.AreEqual(103 - 0.05, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(0.05, city.RecentInputsOf(0), Tolerance);
            Assert.AreEqual(0.05, city.DailyInputNeedOf(0), Tolerance);
        }

        [Test]
        public void TwoWorkshops_ShareAScarceInput_InTheCataloguesOrder()
        {
            // 150 grain for a demand of 5; 2 left after the inhabitants, for 1.5 and 1.5.
            City city = CityOf(Grain, Beer, Beer);
            city.Market.Take(0, 146);

            city.StartDay();

            Assert.AreEqual(61, city.Market.StockOf(1), Tolerance);
            Assert.AreEqual(59, city.Market.StockOf(2), Tolerance);
            Assert.AreEqual(0, city.Market.StockOf(0), Tolerance);
        }

        [Test]
        public void AnInputTakenWhole_LeavesAStockOfZero_NotBelow()
        {
            City city = CityOf(Grain, Beer);
            city.Market.Take(0, 102);

            for (int day = 0; day < 30; day++)
            {
                city.StartDay();

                Assert.GreaterOrEqual(city.Market.StockOf(0), 0d);

                if (day == 0)
                {
                    Assert.AreEqual(0d, city.Market.StockOf(0));
                }
            }
        }

        [Test]
        public void AWorkshopWithoutWorkers_TakesNothing_AndNoStockBecomesNaN()
        {
            MarketGood idle = new MarketGood(10, 2, 0, 0).WithInputs(new[] { new MarketInput(0, 0.5) });
            City city = CityOf(Grain, idle);
            city.Market.Take(0, 60);

            city.StartDay();

            Assert.AreEqual(0, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(58, city.Market.StockOf(1), Tolerance);

            city.StartDay();

            Assert.IsFalse(double.IsNaN(city.Market.StockOf(0)));
            Assert.IsFalse(double.IsNaN(city.Market.StockOf(1)));
            Assert.AreEqual(0, city.RecentInputsOf(0), Tolerance);
        }

        [Test]
        public void TheNominalWeeklyInputNeed_IsSevenDaysOfIt()
        {
            Assert.AreEqual(10.5, CityOf(Grain, Beer).NominalWeeklyInputNeedOf(0), Tolerance);
        }

        [Test]
        public void TheDayReport_HoldsTheInputsOfTheLastDayOnly()
        {
            City city = CityOf(Grain, Beer);
            city.StartDay();
            city.Market.Take(0, 101);

            city.StartDay();

            Assert.AreEqual(1.5, city.LastDay.InputNeedOf(0), Tolerance);
            Assert.AreEqual(0, city.LastDay.InputsConsumedOf(0), Tolerance);
            Assert.AreEqual(1.5, city.RecentInputsOf(0), Tolerance);
        }
    }
}
