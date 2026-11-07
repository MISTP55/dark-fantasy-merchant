using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityTests
    {
        private const double Tolerance = 1e-9;

        // The tests start a good at its threshold T1 unless they say otherwise: 30 days.
        private const double StartingDays = 30;

        private const int DaysPerWeek = 7;

        private static ThresholdRule Thresholds => TestMarkets.Thresholds;

        private static WorkforceRule Workforce => TestMarkets.Workforce;

        private static MarketPricing Pricing => TestMarkets.Pricing;

        // Two barrels a day, base price 10.
        private static MarketGood NotProduced => new MarketGood(10, 2, 0, 0);

        // Workshops of 30 and 22 workers making a tenth of a barrel each: 3 and 2.2 a day.
        private static MarketGood Efficient => new MarketGood(10, 2, 30, 0.1);
        private static MarketGood Inefficient => new MarketGood(10, 2, 22, 0.1);

        private static City CityOf(params MarketGood[] goods)
        {
            return new City(goods, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000));
        }


        [Test]
        public void EveryGood_StartsWithTheSameDays_ProducedOrNot()
        {
            // A week of two barrels a day, whatever the city makes of the good.
            var city = new City(new[] { NotProduced, Efficient, Inefficient }, Thresholds, Workforce, 7, Pricing, DaysPerWeek, new CityPopulation(1000));

            Assert.AreEqual(14, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(14, city.Market.StockOf(1), Tolerance);
            Assert.AreEqual(14, city.Market.StockOf(2), Tolerance);
            Assert.AreEqual(60, city.Market.ThresholdsOf(0).T1, Tolerance);
        }


        [Test]
        public void AMarketCanStartEmpty()
        {
            var city = new City(new[] { NotProduced }, Thresholds, Workforce, 0, Pricing, DaysPerWeek, new CityPopulation(1000));

            Assert.AreEqual(0, city.Market.AvailableOf(0));
        }


        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsStartingDaysThatMakeNoSense(double startingDays)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new City(new[] { NotProduced }, Thresholds, Workforce, startingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
        }


        [Test]
        public void StartDay_ConsumesWhatIsNotProduced_DownToNothing()
        {
            City city = CityOf(NotProduced);

            city.StartDay();
            Assert.AreEqual(58, city.Market.StockOf(0), Tolerance);

            for (int day = 0; day < 40; day++)
            {
                city.StartDay();
            }

            Assert.AreEqual(0, city.Market.StockOf(0), Tolerance);
        }


        [Test]
        public void AboveT3_AProducerStops_AndKeepsOneWorker()
        {
            City city = CityOf(Efficient);
            city.Market.Put(0, 60);

            city.StartDay();

            // 120, then 118 once consumed: above T3, nothing produced.
            Assert.AreEqual(118, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(1, city.WorkersOf(0));
            Assert.AreEqual(0, city.RecentProductionOf(0), Tolerance);
        }


        [Test]
        public void StartDay_RefillsAProducedGoodByItsSurplus()
        {
            City city = CityOf(Efficient);
            city.Market.Put(0, 10);

            city.StartDay();

            // 70 + 3 produced - 2 consumed.
            Assert.AreEqual(71, city.Market.StockOf(0), Tolerance);
        }


        [Test]
        public void StartDay_ConsumesBeforeItProduces()
        {
            // 111 in stock: above T3 before the day's consumption, 109 after it, so it produces.
            City city = CityOf(Efficient);
            city.Market.Put(0, 51);

            city.StartDay();

            Assert.AreEqual(112, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(30, city.WorkersOf(0));
        }


        [Test]
        public void AWorkshop_HiresByTheStock_AndProducesWithTheWorkersOfTheDay()
        {
            // Two workers to start with; 58 once consumed is above T0 (20), so 8 more are hired.
            var city = new City(
                new[] { Efficient }, Thresholds, new WorkforceRule(16, 8, 4, 1, 1, 2), StartingDays, Pricing, DaysPerWeek,
                new CityPopulation(1000));

            Assert.AreEqual(2, city.WorkersOf(0));

            city.StartDay();

            Assert.AreEqual(10, city.WorkersOf(0));
            Assert.AreEqual(59, city.Market.StockOf(0), Tolerance);
        }


        [Test]
        public void TheStartingWorkforce_IsSpreadOverTheWorkshops()
        {
            // 50 workers over two workshops: 25 each, cut to 22 in the smaller one.
            var city = new City(
                new[] { Efficient, Inefficient, NotProduced }, Thresholds, new WorkforceRule(16, 8, 4, 1, 1, 50), StartingDays,
                Pricing, DaysPerWeek, new CityPopulation(1000));

            Assert.AreEqual(25, city.WorkersOf(0));
            Assert.AreEqual(22, city.WorkersOf(1));
            Assert.AreEqual(0, city.WorkersOf(2));
            Assert.AreEqual(30, city.MaxWorkersOf(0));
            Assert.AreEqual(22, city.MaxWorkersOf(1));
            Assert.AreEqual(0, city.MaxWorkersOf(2));
        }


        [Test]
        public void AGoodNotProduced_HasNoWorkers_AndKeepsThem()
        {
            City city = CityOf(NotProduced);

            city.StartDay();

            Assert.AreEqual(0, city.WorkersOf(0));
            Assert.AreEqual(0, city.NominalWeeklyProductionOf(0), Tolerance);
        }


        [Test]
        public void AStockAboveT3_IsOnlyConsumed()
        {
            City city = CityOf(Efficient);
            city.Market.Put(0, 160);

            city.StartDay();

            Assert.AreEqual(218, city.Market.StockOf(0), Tolerance);
        }


        [Test]
        public void AProducerLeftAlone_StaysBetweenT2AndT3()
        {
            City city = CityOf(Efficient);

            for (int day = 0; day < 200; day++)
            {
                city.StartDay();
            }

            // It drops to one worker above T3 (110) and hires again under it; T2 is 90.
            for (int day = 0; day < 200; day++)
            {
                city.StartDay();
                Assert.GreaterOrEqual(city.Market.StockOf(0), 90d, $"day {day}");
                Assert.LessOrEqual(city.Market.StockOf(0), 113d, $"day {day}");
            }
        }


        [Test]
        public void AvailableOf_IsTheWholeBarrels()
        {
            // A village: 0.3 barrel a day, 9 in stock.
            City city = CityOf(new MarketGood(40, 0.3, 0, 0));

            city.StartDay();

            Assert.AreEqual(8.7, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(8, city.Market.AvailableOf(0));
        }


        [Test]
        public void AvailableOf_CountsABarrelThatFloatingPointLeavesAHairShort()
        {
            // 72 in stock, 2.4 a day: 60 after five days, but 59.999999999999986 in floating point.
            City city = CityOf(new MarketGood(10, 2.4, 0, 0));

            for (int day = 0; day < 5; day++)
            {
                city.StartDay();
            }

            Assert.AreEqual(60, city.Market.AvailableOf(0));

            city.Market.Take(0, 60);

            Assert.AreEqual(0, city.Market.AvailableOf(0));
            Assert.GreaterOrEqual(city.Market.StockOf(0), 0d);
        }


        [Test]
        public void AStockUnderOneBarrel_HasNoneAvailable()
        {
            City city = CityOf(new MarketGood(40, 0.3, 0, 0));

            for (int day = 0; day < 28; day++)
            {
                city.StartDay();
            }

            Assert.Greater(city.Market.StockOf(0), 0d);
            Assert.AreEqual(0, city.Market.AvailableOf(0));
        }


        [Test]
        public void Changed_IsRaisedOnceByADay_ATakeAndAPut()
        {
            City city = CityOf(NotProduced, Efficient);
            int changes = 0;
            city.Changed += () => changes++;

            city.StartDay();
            Assert.AreEqual(1, changes);

            city.Market.Take(0, 5);
            Assert.AreEqual(2, changes);

            city.Market.Put(1, 5);
            Assert.AreEqual(3, changes);
        }


        [Test]
        public void AMarketWithoutGoods_IsValid()
        {
            City city = CityOf();

            Assert.AreEqual(0, city.Market.GoodCount);
            Assert.DoesNotThrow(() => city.StartDay());
        }


        [Test]
        public void DailyConsumption_IsTheHousedAtTheGoodsRate_TheBeggarsConsumingNothing()
        {
            // 7 per thousand a day; 1 000 housed and 500 beggars.
            var good = new MarketGood(220, 7, 0, 0);
            var population = new CityPopulation(1000, 500, 1000);
            var city = new City(new[] { good }, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, population);

            Assert.AreEqual(7d, good.DailyConsumptionPerThousand);
            Assert.AreEqual(7, city.DailyConsumptionOf(0), Tolerance);
        }


        [Test]
        public void NominalWeeklyFigures_AreTheDailyOnesOverAWeek()
        {
            // Two barrels a day consumed, and three produced by the efficient good.
            City city = CityOf(NotProduced, Efficient);

            Assert.AreEqual(14, city.NominalWeeklyConsumptionOf(0), Tolerance);
            Assert.AreEqual(0, city.NominalWeeklyProductionOf(0), Tolerance);
            Assert.AreEqual(14, city.NominalWeeklyConsumptionOf(1), Tolerance);
            Assert.AreEqual(21, city.NominalWeeklyProductionOf(1), Tolerance);
        }


        [Test]
        public void NominalWeeklyFigures_FollowTheDaysOfTheWeek()
        {
            var city = new City(new[] { Efficient }, Thresholds, Workforce, StartingDays, Pricing, 10, new CityPopulation(1000));

            Assert.AreEqual(20, city.NominalWeeklyConsumptionOf(0), Tolerance);
            Assert.AreEqual(30, city.NominalWeeklyProductionOf(0), Tolerance);
        }


        [Test]
        public void BeforeTheFirstDay_NothingWasProducedOrConsumed()
        {
            City city = CityOf(Efficient);

            Assert.AreEqual(0, city.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(0, city.RecentConsumptionOf(0), Tolerance);
        }


        [Test]
        public void RecentFigures_AreWhatTheDaysProducedAndConsumed()
        {
            City city = CityOf(NotProduced, Efficient);

            for (int day = 0; day < 3; day++)
            {
                city.StartDay();
            }

            Assert.AreEqual(0, city.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(6, city.RecentConsumptionOf(0), Tolerance);
            Assert.AreEqual(9, city.RecentProductionOf(1), Tolerance);
            Assert.AreEqual(6, city.RecentConsumptionOf(1), Tolerance);
        }


        [Test]
        public void RecentProduction_IsNothingWhileAboveT3()
        {
            // 119 in stock: 117, 115 and 113 after the days' consumption, all above T3.
            City city = CityOf(Efficient);
            city.Market.Put(0, 59);

            for (int day = 0; day < 3; day++)
            {
                city.StartDay();
            }

            Assert.AreEqual(0, city.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(6, city.RecentConsumptionOf(0), Tolerance);
        }


        [Test]
        public void RecentProduction_IsNothingAboveT3()
        {
            City city = CityOf(Efficient);
            city.Market.Put(0, 160);

            city.StartDay();

            Assert.AreEqual(0, city.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(2, city.RecentConsumptionOf(0), Tolerance);
        }


        [Test]
        public void AtT3Itself_TheWorkshopStillProduces()
        {
            // 112 in stock, 110 once consumed: T3 itself.
            City city = CityOf(Efficient);
            city.Market.Put(0, 52);

            city.StartDay();

            Assert.AreEqual(113, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(3, city.RecentProductionOf(0), Tolerance);
        }


        [Test]
        public void RecentConsumption_StopsAtAnEmptyStock()
        {
            // Three barrels in stock for two a day.
            var city = new City(new[] { NotProduced }, Thresholds, Workforce, 1.5, Pricing, DaysPerWeek, new CityPopulation(1000));

            for (int day = 0; day < 4; day++)
            {
                city.StartDay();
            }

            Assert.AreEqual(3, city.RecentConsumptionOf(0), Tolerance);
        }


        [Test]
        public void RecentFigures_CoverOneWeekOnly()
        {
            City city = CityOf(Efficient);

            for (int day = 0; day < 10; day++)
            {
                city.StartDay();
            }

            Assert.AreEqual(21, city.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(14, city.RecentConsumptionOf(0), Tolerance);
        }


        [Test]
        public void RecentFigures_DoNotCountWhatThePlayerTrades()
        {
            City city = CityOf(NotProduced);
            city.StartDay();

            city.Market.Take(0, 20);
            city.Market.Put(0, 50);

            Assert.AreEqual(0, city.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(2, city.RecentConsumptionOf(0), Tolerance);
        }


        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new City(new[] { NotProduced }, null, Workforce, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
            Assert.Throws<ArgumentNullException>(() => new City(null, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
            Assert.Throws<ArgumentNullException>(() => new City(new[] { NotProduced }, Thresholds, Workforce, StartingDays, null, DaysPerWeek, new CityPopulation(1000)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new City(new[] { NotProduced }, Thresholds, Workforce, StartingDays, Pricing, 0, new CityPopulation(1000)));
            Assert.Throws<ArgumentNullException>(() => new City(new[] { NotProduced }, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, null));
            Assert.Throws<ArgumentNullException>(() => new City(new[] { NotProduced }, Thresholds, null, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
        }


        [Test]
        public void WhatACityConsumes_FollowsItsInhabitants()
        {
            var city = new City(
                new[] { Efficient }, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, new CityPopulation(2500));

            // Two barrels a day per thousand: five for 2 500. The workshop makes 3 whatever the size.
            Assert.AreEqual(5, city.DailyConsumptionOf(0), Tolerance);
            Assert.AreEqual(150, city.Market.StockOf(0), Tolerance);
            Assert.AreEqual(150, city.Market.ThresholdsOf(0).T1, Tolerance);
            Assert.AreEqual(35, city.NominalWeeklyConsumptionOf(0), Tolerance);
            Assert.AreEqual(21, city.NominalWeeklyProductionOf(0), Tolerance);

            city.StartDay();

            // 150 - 5 consumed + 3 produced.
            Assert.AreEqual(148, city.Market.StockOf(0), Tolerance);
        }


        [Test]
        public void DailyProduction_IsTheWorkersOfTheMomentTimesTheirOutput()
        {
            City city = CityOf(NotProduced, Efficient);

            Assert.AreEqual(0d, city.DailyProductionOf(0), Tolerance);
            Assert.AreEqual(3d, city.DailyProductionOf(1), Tolerance);
            Assert.AreEqual(city.NominalWeeklyProductionOf(1) / DaysPerWeek, city.DailyProductionOf(1), Tolerance);
        }


        [Test]
        public void TheFactorsAndTheProduction_RejectAGoodOutOfRange()
        {
            City city = CityOf(NotProduced);

            Assert.Throws<ArgumentOutOfRangeException>(() => city.Market.BuyFactorAt(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => city.Market.SellFactorAt(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => city.DailyProductionOf(1));
        }
    }
}
