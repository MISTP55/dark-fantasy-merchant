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

        private static City CityOf(params MarketGood[] goods)
        {
            return new City(goods, Thresholds, Workforce, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000));
        }

        [Test]
        public void AMarket_StartsWithTheDaysOfTheDemandItIsGiven()
        {
            var market = new CityMarket(new[] { NotProduced }, Thresholds, Pricing, good => 3d, StartingDays);

            Assert.AreEqual(90, market.StockOf(0), Tolerance);
            Assert.AreEqual(90, market.ThresholdsOf(0).T1, Tolerance);
        }

        [Test]
        public void TheThresholds_FollowTheDemand_OnlyAtTheEndOfADay()
        {
            double demand = 3d;
            var market = new CityMarket(new[] { NotProduced }, Thresholds, Pricing, good => demand, StartingDays);
            int changes = 0;
            market.Changed += () => changes++;

            demand = 6d;
            Assert.AreEqual(90, market.ThresholdsOf(0).T1, Tolerance);
            Assert.AreEqual(0, changes);

            market.EndDay();

            Assert.AreEqual(180, market.ThresholdsOf(0).T1, Tolerance);
            Assert.AreEqual(90, market.StockOf(0), Tolerance);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Withdraw_TakesAtMostTheStock_AndTellsNobody()
        {
            var market = new CityMarket(new[] { NotProduced }, Thresholds, Pricing, good => 1d, StartingDays);
            int changes = 0;
            market.Changed += () => changes++;

            Assert.AreEqual(10.5, market.Withdraw(0, 10.5), Tolerance);
            Assert.AreEqual(19.5, market.Withdraw(0, 100), Tolerance);
            Assert.AreEqual(0, market.StockOf(0));
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void RejectsADemandThatIsMissing()
        {
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, Thresholds, Pricing, null, StartingDays));
        }

        [Test]
        public void AGood_StartsWithTheStartingDaysOfItsConsumption()
        {
            CityMarket market = CityOf(NotProduced).Market;

            Assert.AreEqual(1, market.GoodCount);
            Assert.AreEqual(60, market.ThresholdsOf(0).T1, Tolerance);
            Assert.AreEqual(60, market.StockOf(0), Tolerance);
        }


        [Test]
        public void Prices_AreThoseOfTheNextBarrel()
        {
            CityMarket market = CityOf(NotProduced).Market;

            // Stock 60, the threshold T1. Bought: 10 x 1.00625, the average buy factor over [59, 60].
            Assert.AreEqual(11, market.BuyPriceOf(0));

            // Sold: T2 is T1 for a good the city does not produce, so the barrel is already on
            // the segment from T2 to T3: 10 x 0.695.
            Assert.AreEqual(6, market.SellPriceOf(0));
        }


        [Test]
        public void Prices_OfLaterBarrels_FollowTheStockTheyWouldLeave()
        {
            CityMarket market = CityOf(NotProduced).Market;

            // The last of the 60 barrels: 10 x 3.9375, the average over [0, 1].
            Assert.AreEqual(40, market.BuyPriceOf(0, 59));

            // The 61st barrel sold: 120 in stock, above T3 at 80, so 10 x 0.5.
            Assert.AreEqual(5, market.SellPriceOf(0, 60));
        }


        [Test]
        public void AGoodTheCityDoesNotConsume_IsPricedAgainstTheFloors()
        {
            CityMarket market = CityOf(new MarketGood(10, 0, 0, 0)).Market;
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
            CityMarket market = CityOf(NotProduced, Efficient, Inefficient).Market;

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
            CityMarket market = CityOf(NotProduced).Market;

            market.Take(0, 25);
            Assert.AreEqual(35, market.StockOf(0), Tolerance);

            market.Put(0, 300);
            Assert.AreEqual(335, market.StockOf(0), Tolerance);
        }


        [Test]
        public void Take_MoreThanAvailable_Throws()
        {
            CityMarket market = CityOf(NotProduced).Market;

            Assert.Throws<ArgumentOutOfRangeException>(() => market.Take(0, 61));
            Assert.AreEqual(60, market.StockOf(0), Tolerance);
        }


        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsAQuantityThatIsNotPositive(int barrels)
        {
            CityMarket market = CityOf(NotProduced).Market;

            Assert.Throws<ArgumentOutOfRangeException>(() => market.Take(0, barrels));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.Put(0, barrels));
        }


        [TestCase(-1)]
        [TestCase(1)]
        public void RejectsAGoodOutsideTheCatalogue(int good)
        {
            CityMarket market = CityOf(NotProduced).Market;

            Assert.Throws<ArgumentOutOfRangeException>(() => market.StockOf(good));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.BuyPriceOf(good));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.Put(good, 1));
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
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, 30, 0.1, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, 30, 0.1, double.NaN));
        }


        [Test]
        public void MarketGood_RefusesANegativeConsumption()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, -1, 0, 0));
            Assert.DoesNotThrow(() => new MarketGood(10, 1, 0, 0));
        }


        [Test]
        public void TheRate_IsKept()
        {
            var good = new MarketGood(10, 2, 0, 0);

            Assert.AreEqual(2d, good.DailyConsumptionPerThousand);
        }


        [Test]
        public void TheFactors_AreReadOnTheCurves_AtTheGivenStock()
        {
            // Two barrels a day: T0 20, T1 60, T2 60, T3 80.
            CityMarket market = CityOf(NotProduced).Market;

            Assert.AreEqual(4.0, market.BuyFactorAt(0, 0), Tolerance);
            Assert.AreEqual(1.25, market.BuyFactorAt(0, 40), Tolerance);
            Assert.AreEqual(0.6, market.BuyFactorAt(0, 120), Tolerance);
            Assert.AreEqual(2.0, market.SellFactorAt(0, 0), Tolerance);
            Assert.AreEqual(1.2, market.SellFactorAt(0, 40), Tolerance);
            Assert.AreEqual(1.0, market.SellFactorAt(0, 60), Tolerance);
        }
    }
}
