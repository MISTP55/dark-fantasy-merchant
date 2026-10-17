using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketTests
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

        // Food, two barrels a day for a thousand inhabitants.
        private static MarketGood Food => new MarketGood(10, 2, 0, 0, isFood: true);

        // A week of food keeps the population, two make it grow by 5 % a month, none loses 10 %.
        private static FoodGrowth Growth => new FoodGrowth(7, 14, 0.05, 0.1, 30);

        private static CityMarket FedMarket(double startingDays, params MarketGood[] goods)
        {
            return new CityMarket(
                goods, Thresholds, Workforce, startingDays, Pricing, DaysPerWeek, new CityPopulation(1000), Growth);
        }

        private static CityMarket MarketOf(params MarketGood[] goods)
        {
            return new CityMarket(goods, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000));
        }

        [Test]
        public void AGood_StartsWithTheStartingDaysOfItsConsumption()
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.AreEqual(1, market.GoodCount);
            Assert.AreEqual(60, market.ThresholdsOf(0).T1, Tolerance);
            Assert.AreEqual(60, market.StockOf(0), Tolerance);
        }

        [Test]
        public void EveryGood_StartsWithTheSameDays_ProducedOrNot()
        {
            // A week of two barrels a day, whatever the city makes of the good.
            var market = new CityMarket(new[] { NotProduced, Efficient, Inefficient }, Thresholds, Workforce, 7, Pricing, DaysPerWeek, new CityPopulation(1000));

            Assert.AreEqual(14, market.StockOf(0), Tolerance);
            Assert.AreEqual(14, market.StockOf(1), Tolerance);
            Assert.AreEqual(14, market.StockOf(2), Tolerance);
            Assert.AreEqual(60, market.ThresholdsOf(0).T1, Tolerance);
        }

        [Test]
        public void AMarketCanStartEmpty()
        {
            var market = new CityMarket(new[] { NotProduced }, Thresholds, Workforce, 0, Pricing, DaysPerWeek, new CityPopulation(1000));

            Assert.AreEqual(0, market.AvailableOf(0));
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsStartingDaysThatMakeNoSense(double startingDays)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CityMarket(new[] { NotProduced }, Thresholds, Workforce, startingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
        }

        [Test]
        public void StartDay_ConsumesWhatIsNotProduced_DownToNothing()
        {
            CityMarket market = MarketOf(NotProduced);

            market.StartDay();
            Assert.AreEqual(58, market.StockOf(0), Tolerance);

            for (int day = 0; day < 40; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(0, market.StockOf(0), Tolerance);
        }

        [Test]
        public void AboveT3_AProducerStops_AndKeepsOneWorker()
        {
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 60);

            market.StartDay();

            // 120, then 118 once consumed: above T3, nothing produced.
            Assert.AreEqual(118, market.StockOf(0), Tolerance);
            Assert.AreEqual(1, market.WorkersOf(0));
            Assert.AreEqual(0, market.RecentProductionOf(0), Tolerance);
        }

        [Test]
        public void StartDay_RefillsAProducedGoodByItsSurplus()
        {
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 10);

            market.StartDay();

            // 70 + 3 produced - 2 consumed.
            Assert.AreEqual(71, market.StockOf(0), Tolerance);
        }

        [Test]
        public void StartDay_ConsumesBeforeItProduces()
        {
            // 111 in stock: above T3 before the day's consumption, 109 after it, so it produces.
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 51);

            market.StartDay();

            Assert.AreEqual(112, market.StockOf(0), Tolerance);
            Assert.AreEqual(30, market.WorkersOf(0));
        }

        [Test]
        public void AWorkshop_HiresByTheStock_AndProducesWithTheWorkersOfTheDay()
        {
            // Two workers to start with; 58 once consumed is above T0 (20), so 8 more are hired.
            var market = new CityMarket(
                new[] { Efficient }, Thresholds, new WorkforceRule(16, 8, 4, 1, 1, 2), StartingDays, Pricing, DaysPerWeek,
                new CityPopulation(1000));

            Assert.AreEqual(2, market.WorkersOf(0));

            market.StartDay();

            Assert.AreEqual(10, market.WorkersOf(0));
            Assert.AreEqual(59, market.StockOf(0), Tolerance);
        }

        [Test]
        public void TheStartingWorkforce_IsSpreadOverTheWorkshops()
        {
            // 50 workers over two workshops: 25 each, cut to 22 in the smaller one.
            var market = new CityMarket(
                new[] { Efficient, Inefficient, NotProduced }, Thresholds, new WorkforceRule(16, 8, 4, 1, 1, 50), StartingDays,
                Pricing, DaysPerWeek, new CityPopulation(1000));

            Assert.AreEqual(25, market.WorkersOf(0));
            Assert.AreEqual(22, market.WorkersOf(1));
            Assert.AreEqual(0, market.WorkersOf(2));
            Assert.AreEqual(30, market.MaxWorkersOf(0));
            Assert.AreEqual(22, market.MaxWorkersOf(1));
            Assert.AreEqual(0, market.MaxWorkersOf(2));
        }

        [Test]
        public void AGoodNotProduced_HasNoWorkers_AndKeepsThem()
        {
            CityMarket market = MarketOf(NotProduced);

            market.StartDay();

            Assert.AreEqual(0, market.WorkersOf(0));
            Assert.AreEqual(0, market.NominalWeeklyProductionOf(0), Tolerance);
        }

        [Test]
        public void AStockAboveT3_IsOnlyConsumed()
        {
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 160);

            market.StartDay();

            Assert.AreEqual(218, market.StockOf(0), Tolerance);
        }

        [Test]
        public void AProducerLeftAlone_StaysBetweenT2AndT3()
        {
            CityMarket market = MarketOf(Efficient);

            for (int day = 0; day < 200; day++)
            {
                market.StartDay();
            }

            // It drops to one worker above T3 (110) and hires again under it; T2 is 90.
            for (int day = 0; day < 200; day++)
            {
                market.StartDay();
                Assert.GreaterOrEqual(market.StockOf(0), 90d, $"day {day}");
                Assert.LessOrEqual(market.StockOf(0), 113d, $"day {day}");
            }
        }

        [Test]
        public void AvailableOf_IsTheWholeBarrels()
        {
            // A village: 0.3 barrel a day, 9 in stock.
            CityMarket market = MarketOf(new MarketGood(40, 0.3, 0, 0));

            market.StartDay();

            Assert.AreEqual(8.7, market.StockOf(0), Tolerance);
            Assert.AreEqual(8, market.AvailableOf(0));
        }

        [Test]
        public void AvailableOf_CountsABarrelThatFloatingPointLeavesAHairShort()
        {
            // 72 in stock, 2.4 a day: 60 after five days, but 59.999999999999986 in floating point.
            CityMarket market = MarketOf(new MarketGood(10, 2.4, 0, 0));

            for (int day = 0; day < 5; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(60, market.AvailableOf(0));

            market.Take(0, 60);

            Assert.AreEqual(0, market.AvailableOf(0));
            Assert.GreaterOrEqual(market.StockOf(0), 0d);
        }

        [Test]
        public void AStockUnderOneBarrel_HasNoneAvailable()
        {
            CityMarket market = MarketOf(new MarketGood(40, 0.3, 0, 0));

            for (int day = 0; day < 28; day++)
            {
                market.StartDay();
            }

            Assert.Greater(market.StockOf(0), 0d);
            Assert.AreEqual(0, market.AvailableOf(0));
        }

        [Test]
        public void Prices_AreThoseOfTheNextBarrel()
        {
            CityMarket market = MarketOf(NotProduced);

            // Stock 60, the threshold T1. Bought: 10 x 1.00625, the average buy factor over [59, 60].
            Assert.AreEqual(11, market.BuyPriceOf(0));

            // Sold: T2 is T1 for a good the city does not produce, so the barrel is already on
            // the segment from T2 to T3: 10 x 0.695.
            Assert.AreEqual(6, market.SellPriceOf(0));
        }

        [Test]
        public void Prices_OfLaterBarrels_FollowTheStockTheyWouldLeave()
        {
            CityMarket market = MarketOf(NotProduced);

            // The last of the 60 barrels: 10 x 3.9375, the average over [0, 1].
            Assert.AreEqual(40, market.BuyPriceOf(0, 59));

            // The 61st barrel sold: 120 in stock, above T3 at 80, so 10 x 0.5.
            Assert.AreEqual(5, market.SellPriceOf(0, 60));
        }

        [Test]
        public void AGoodTheCityDoesNotConsume_IsPricedAgainstTheFloors()
        {
            CityMarket market = MarketOf(new MarketGood(10, 0, 0, 0));
            StockThresholds thresholds = market.ThresholdsOf(0);

            Assert.AreEqual(5, thresholds.T0, Tolerance);
            Assert.AreEqual(10, thresholds.T1, Tolerance);
            Assert.AreEqual(15, thresholds.T2, Tolerance);
            Assert.AreEqual(20, thresholds.T3, Tolerance);
            Assert.AreEqual(0, market.AvailableOf(0));

            // An empty stock: 10 x 4 to buy, 10 x 1.94 (the average over [0, 1]) to sell.
            Assert.AreEqual(40, market.BuyPriceOf(0));
            Assert.AreEqual(19, market.SellPriceOf(0));
        }

        [Test]
        public void ThresholdsOf_FollowTheProduction()
        {
            CityMarket market = MarketOf(NotProduced, Efficient, Inefficient);

            // Two barrels a day consumed; 0, 3 and 2.2 produced: T2 adds 10 days of production.
            StockThresholds notProduced = market.ThresholdsOf(0);
            StockThresholds efficient = market.ThresholdsOf(1);
            StockThresholds inefficient = market.ThresholdsOf(2);

            Assert.AreEqual(20, notProduced.T0, Tolerance);
            Assert.AreEqual(60, notProduced.T1, Tolerance);
            Assert.AreEqual(60, notProduced.T2, Tolerance);
            Assert.AreEqual(80, notProduced.T3, Tolerance);
            Assert.AreEqual(90, efficient.T2, Tolerance);
            Assert.AreEqual(110, efficient.T3, Tolerance);
            Assert.AreEqual(82, inefficient.T2, Tolerance);
            Assert.AreEqual(102, inefficient.T3, Tolerance);
        }

        [Test]
        public void TakeAndPut_MoveWholeBarrels()
        {
            CityMarket market = MarketOf(NotProduced);

            market.Take(0, 25);
            Assert.AreEqual(35, market.StockOf(0), Tolerance);

            market.Put(0, 300);
            Assert.AreEqual(335, market.StockOf(0), Tolerance);
        }

        [Test]
        public void Take_MoreThanAvailable_Throws()
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.Throws<ArgumentOutOfRangeException>(() => market.Take(0, 61));
            Assert.AreEqual(60, market.StockOf(0), Tolerance);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsAQuantityThatIsNotPositive(int barrels)
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.Throws<ArgumentOutOfRangeException>(() => market.Take(0, barrels));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.Put(0, barrels));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void RejectsAGoodOutsideTheCatalogue(int good)
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.Throws<ArgumentOutOfRangeException>(() => market.StockOf(good));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.BuyPriceOf(good));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.Put(good, 1));
        }

        [Test]
        public void Changed_IsRaisedOnceByADay_ATakeAndAPut()
        {
            CityMarket market = MarketOf(NotProduced, Efficient);
            int changes = 0;
            market.Changed += () => changes++;

            market.StartDay();
            Assert.AreEqual(1, changes);

            market.Take(0, 5);
            Assert.AreEqual(2, changes);

            market.Put(1, 5);
            Assert.AreEqual(3, changes);
        }

        [Test]
        public void AMarketWithoutGoods_IsValid()
        {
            CityMarket market = MarketOf();

            Assert.AreEqual(0, market.GoodCount);
            Assert.DoesNotThrow(() => market.StartDay());
        }

        [Test]
        public void MarketGood_RejectsFiguresThatMakeNoSense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(0, 2, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(-1.5, 2, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(double.NaN, 2, 0, 0));
            Assert.DoesNotThrow(() => new MarketGood(0.5, 2, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, double.NaN, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, -1, 0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, 30, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, 30, double.NaN));
        }

        [Test]
        public void NominalWeeklyFigures_AreTheDailyOnesOverAWeek()
        {
            // Two barrels a day consumed, and three produced by the efficient good.
            CityMarket market = MarketOf(NotProduced, Efficient);

            Assert.AreEqual(14, market.NominalWeeklyConsumptionOf(0), Tolerance);
            Assert.AreEqual(0, market.NominalWeeklyProductionOf(0), Tolerance);
            Assert.AreEqual(14, market.NominalWeeklyConsumptionOf(1), Tolerance);
            Assert.AreEqual(21, market.NominalWeeklyProductionOf(1), Tolerance);
        }

        [Test]
        public void NominalWeeklyFigures_FollowTheDaysOfTheWeek()
        {
            var market = new CityMarket(new[] { Efficient }, Thresholds, Workforce, StartingDays, Pricing, 10, new CityPopulation(1000));

            Assert.AreEqual(20, market.NominalWeeklyConsumptionOf(0), Tolerance);
            Assert.AreEqual(30, market.NominalWeeklyProductionOf(0), Tolerance);
        }

        [Test]
        public void BeforeTheFirstDay_NothingWasProducedOrConsumed()
        {
            CityMarket market = MarketOf(Efficient);

            Assert.AreEqual(0, market.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(0, market.RecentConsumptionOf(0), Tolerance);
        }

        [Test]
        public void RecentFigures_AreWhatTheDaysProducedAndConsumed()
        {
            CityMarket market = MarketOf(NotProduced, Efficient);

            for (int day = 0; day < 3; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(0, market.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(6, market.RecentConsumptionOf(0), Tolerance);
            Assert.AreEqual(9, market.RecentProductionOf(1), Tolerance);
            Assert.AreEqual(6, market.RecentConsumptionOf(1), Tolerance);
        }

        [Test]
        public void RecentProduction_IsNothingWhileAboveT3()
        {
            // 119 in stock: 117, 115 and 113 after the days' consumption, all above T3.
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 59);

            for (int day = 0; day < 3; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(0, market.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(6, market.RecentConsumptionOf(0), Tolerance);
        }

        [Test]
        public void RecentProduction_IsNothingAboveT3()
        {
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 160);

            market.StartDay();

            Assert.AreEqual(0, market.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(2, market.RecentConsumptionOf(0), Tolerance);
        }

        [Test]
        public void AtT3Itself_TheWorkshopStillProduces()
        {
            // 112 in stock, 110 once consumed: T3 itself.
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 52);

            market.StartDay();

            Assert.AreEqual(113, market.StockOf(0), Tolerance);
            Assert.AreEqual(3, market.RecentProductionOf(0), Tolerance);
        }

        [Test]
        public void RecentConsumption_StopsAtAnEmptyStock()
        {
            // Three barrels in stock for two a day.
            var market = new CityMarket(new[] { NotProduced }, Thresholds, Workforce, 1.5, Pricing, DaysPerWeek, new CityPopulation(1000));

            for (int day = 0; day < 4; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(3, market.RecentConsumptionOf(0), Tolerance);
        }

        [Test]
        public void RecentFigures_CoverOneWeekOnly()
        {
            CityMarket market = MarketOf(Efficient);

            for (int day = 0; day < 10; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(21, market.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(14, market.RecentConsumptionOf(0), Tolerance);
        }

        [Test]
        public void RecentFigures_DoNotCountWhatThePlayerTrades()
        {
            CityMarket market = MarketOf(NotProduced);
            market.StartDay();

            market.Take(0, 20);
            market.Put(0, 50);

            Assert.AreEqual(0, market.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(2, market.RecentConsumptionOf(0), Tolerance);
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, null, Workforce, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(null, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, Thresholds, Workforce, StartingDays, null, DaysPerWeek, new CityPopulation(1000)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityMarket(new[] { NotProduced }, Thresholds, Workforce, StartingDays, Pricing, 0, new CityPopulation(1000)));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, null));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, Thresholds, null, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
        }

        [Test]
        public void WhatACityConsumes_FollowsItsInhabitants()
        {
            var market = new CityMarket(
                new[] { Efficient }, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, new CityPopulation(2500));

            // Two barrels a day per thousand: five for 2 500. The workshop makes 3 whatever the size.
            Assert.AreEqual(5, market.DailyConsumptionOf(0), Tolerance);
            Assert.AreEqual(150, market.StockOf(0), Tolerance);
            Assert.AreEqual(150, market.ThresholdsOf(0).T1, Tolerance);
            Assert.AreEqual(35, market.NominalWeeklyConsumptionOf(0), Tolerance);
            Assert.AreEqual(21, market.NominalWeeklyProductionOf(0), Tolerance);

            market.StartDay();

            // 150 - 5 consumed + 3 produced.
            Assert.AreEqual(148, market.StockOf(0), Tolerance);
        }

        [Test]
        public void WithoutAFoodGrowth_ThePopulationStaysWhatItIs()
        {
            var market = new CityMarket(new[] { Food }, Thresholds, Workforce, 0, Pricing, DaysPerWeek, new CityPopulation(1000));

            market.StartDay();

            Assert.AreEqual(0, market.MonthlyGrowthRate, Tolerance);
            Assert.AreEqual(1000, market.Population.Count, Tolerance);
        }

        [Test]
        public void FoodDays_AreTheDaysOfConsumptionInStock()
        {
            CityMarket market = FedMarket(10, Food);

            Assert.IsTrue(market.TryGetFoodDays(14, out double days));
            Assert.AreEqual(10, days, Tolerance);
        }

        [Test]
        public void FoodDays_AreTheAverageOfTheFoods_EachCountedUpToTheGivenDays()
        {
            // Two foods at 2 and 4 barrels a day, with 10 days of each to start with.
            CityMarket market = FedMarket(10, Food, new MarketGood(10, 4, 0, 0, isFood: true));

            // 84 and 0 barrels: 42 days, counted for 14, and none.
            market.Put(0, 64);
            market.Take(1, 40);

            Assert.IsTrue(market.TryGetFoodDays(14, out double days));
            Assert.AreEqual(7, days, Tolerance);

            // 28 and 28 barrels: 14 and 7 days.
            market.Take(0, 56);
            market.Put(1, 28);

            Assert.IsTrue(market.TryGetFoodDays(14, out days));
            Assert.AreEqual(10.5, days, Tolerance);
        }

        [Test]
        public void FoodDays_LeaveOutWhatIsNotFood()
        {
            // No fish but plenty of a good nobody eats.
            CityMarket market = FedMarket(0, Food, NotProduced);
            market.Put(1, 500);

            Assert.IsTrue(market.TryGetFoodDays(14, out double days));
            Assert.AreEqual(0, days, Tolerance);
        }

        [Test]
        public void ACityThatEatsNothing_HasNoFoodDays_AndKeepsItsPopulation()
        {
            // No food in the catalogue, then a food nobody consumes.
            CityMarket withoutFood = FedMarket(0, NotProduced);
            CityMarket notEaten = FedMarket(0, new MarketGood(10, 0, 0, 0, isFood: true));

            Assert.IsFalse(withoutFood.TryGetFoodDays(14, out _));
            Assert.IsFalse(notEaten.TryGetFoodDays(14, out _));

            withoutFood.StartDay();

            Assert.AreEqual(0, withoutFood.MonthlyGrowthRate, Tolerance);
            Assert.AreEqual(1000, withoutFood.Population.Count, Tolerance);
        }

        [Test]
        public void MonthlyGrowthRate_FollowsTheFoodInStock_AtOnce()
        {
            CityMarket market = FedMarket(7, Food);
            Assert.AreEqual(0, market.MonthlyGrowthRate, Tolerance);

            // 28 barrels: two weeks.
            market.Put(0, 14);
            Assert.AreEqual(0.05, market.MonthlyGrowthRate, Tolerance);

            market.Take(0, 28);
            Assert.AreEqual(-0.1, market.MonthlyGrowthRate, Tolerance);
        }

        [Test]
        public void AMonthWithoutFood_LosesATenthOfThePopulation()
        {
            CityMarket market = FedMarket(0, Food);

            for (int day = 0; day < 30; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(900, market.Population.Count, 1e-6);
            Assert.AreEqual(900, market.Population.Inhabitants);

            // And it eats less: 1.8 barrels a day.
            Assert.AreEqual(1.8, market.DailyConsumptionOf(0), 1e-6);
        }

        [Test]
        public void AMonthOfPlenty_GrowsThePopulationByItsFastestRate()
        {
            // Far more than two weeks of food, for the whole month.
            CityMarket market = FedMarket(0, Food);
            market.Put(0, 1000);

            for (int day = 0; day < 30; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(1050, market.Population.Count, 1e-6);
        }

        [Test]
        public void StartDay_ChangesThePopulation_ByTheFoodLeftAfterTheDay()
        {
            // 14 barrels: a week before the day, 6 days after it, so the population declines.
            CityMarket market = FedMarket(7, Food);

            market.StartDay();

            Assert.AreEqual(12, market.StockOf(0), Tolerance);
            Assert.Less(market.Population.Count, 1000d);
        }

        [Test]
        public void Changed_IsRaisedAfterThePopulationHasChanged()
        {
            CityMarket market = FedMarket(0, Food);
            double seen = 0d;
            int changes = 0;
            market.Changed += () =>
            {
                seen = market.Population.Count;
                changes++;
            };

            market.StartDay();

            Assert.AreEqual(1, changes);
            Assert.Less(seen, 1000d);
        }

        [Test]
        public void TheFactors_AreReadOnTheCurves_AtTheGivenStock()
        {
            // Two barrels a day: T0 20, T1 60, T2 60, T3 80.
            CityMarket market = MarketOf(NotProduced);

            Assert.AreEqual(4.0, market.BuyFactorAt(0, 0), Tolerance);
            Assert.AreEqual(1.25, market.BuyFactorAt(0, 40), Tolerance);
            Assert.AreEqual(0.6, market.BuyFactorAt(0, 120), Tolerance);
            Assert.AreEqual(2.0, market.SellFactorAt(0, 0), Tolerance);
            Assert.AreEqual(1.2, market.SellFactorAt(0, 40), Tolerance);
            Assert.AreEqual(1.0, market.SellFactorAt(0, 60), Tolerance);
        }

        [Test]
        public void DailyProduction_IsTheWorkersOfTheMomentTimesTheirOutput()
        {
            CityMarket market = MarketOf(NotProduced, Efficient);

            Assert.AreEqual(0d, market.DailyProductionOf(0), Tolerance);
            Assert.AreEqual(3d, market.DailyProductionOf(1), Tolerance);
            Assert.AreEqual(market.NominalWeeklyProductionOf(1) / DaysPerWeek, market.DailyProductionOf(1), Tolerance);
        }

        [Test]
        public void TheFactorsAndTheProduction_RejectAGoodOutOfRange()
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.Throws<ArgumentOutOfRangeException>(() => market.BuyFactorAt(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.SellFactorAt(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.DailyProductionOf(1));
        }
    }
}
