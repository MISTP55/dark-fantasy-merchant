using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketTests
    {
        private const double Tolerance = 1e-9;
        private const double TargetDays = 30;

        private static MarketPricing Pricing => new MarketPricing(2.5, 0.5, 0.05, 0.05);

        // Two barrels a day, base price 10.
        private static MarketGood NotProduced => new MarketGood(10, 2, 0, 0);
        private static MarketGood Efficient => new MarketGood(10, 2, 1.5, 60);
        private static MarketGood Inefficient => new MarketGood(10, 2, 1.1, 36);

        private static CityMarket MarketOf(params MarketGood[] goods)
        {
            return new CityMarket(goods, TargetDays, Pricing);
        }

        [Test]
        public void AGoodThatIsNotProduced_StartsAtTheTargetStock()
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.AreEqual(1, market.GoodCount);
            Assert.AreEqual(60, market.TargetOf(0), Tolerance);
            Assert.AreEqual(60, market.StockOf(0), Tolerance);
        }

        [Test]
        public void AGoodThatIsProduced_StartsAtItsCeiling()
        {
            CityMarket market = MarketOf(Efficient, Inefficient);

            Assert.AreEqual(120, market.StockOf(0), Tolerance);
            Assert.AreEqual(72, market.StockOf(1), Tolerance);
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
        public void StartDay_KeepsAnEfficientGoodAtItsCeiling()
        {
            CityMarket market = MarketOf(Efficient);

            for (int day = 0; day < 10; day++)
            {
                market.StartDay();
            }

            // Produced up to the ceiling of 120, then a day's consumption taken.
            Assert.AreEqual(118, market.StockOf(0), Tolerance);
        }

        [Test]
        public void StartDay_RefillsAProducedGoodByItsSurplus()
        {
            CityMarket market = MarketOf(Efficient);
            market.Take(0, 50);

            market.StartDay();

            // 70 + 3 produced - 2 consumed.
            Assert.AreEqual(71, market.StockOf(0), Tolerance);
        }

        [Test]
        public void StartDay_LeavesAloneAStockAboveItsCeiling()
        {
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 100);

            market.StartDay();

            // Nothing produced above the ceiling: only consumed.
            Assert.AreEqual(218, market.StockOf(0), Tolerance);
        }

        [Test]
        public void AnInefficientGood_SettlesJustUnderItsCeiling()
        {
            CityMarket market = MarketOf(Inefficient);
            market.Take(0, 20);

            for (int day = 0; day < 200; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(70, market.StockOf(0), Tolerance);
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

            // Stock 60 of a target of 60. Bought: 10 x multiplier(59/60) x 1.05 = 10.7625.
            Assert.AreEqual(11, market.BuyPriceOf(0));

            // Sold: 10 x 1 x 0.95.
            Assert.AreEqual(9, market.SellPriceOf(0));
        }

        [Test]
        public void Prices_OfLaterBarrels_FollowTheStockTheyWouldLeave()
        {
            CityMarket market = MarketOf(NotProduced);

            // The last of the 60 barrels: the price of an empty stock.
            Assert.AreEqual(27, market.BuyPriceOf(0, 59));

            // The 61st barrel sold: 10 x multiplier(120/60) x 0.95 = 4.75.
            Assert.AreEqual(4, market.SellPriceOf(0, 60));
        }

        [Test]
        public void AGoodTheCityDoesNotConsume_IsAtItsBasePrice_AndOutOfStock()
        {
            CityMarket market = MarketOf(new MarketGood(10, 0, 0, 0));

            Assert.AreEqual(0, market.AvailableOf(0));
            Assert.AreEqual(11, market.BuyPriceOf(0));
            Assert.AreEqual(9, market.SellPriceOf(0));
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
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, double.NaN, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, -0.5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, 1.5, -1));
        }

        [Test]
        public void RejectsANegativeTarget_AndMissingArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityMarket(new[] { NotProduced }, -1, Pricing));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(null, TargetDays, Pricing));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, TargetDays, null));
        }
    }
}
