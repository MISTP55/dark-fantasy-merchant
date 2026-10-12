using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MarketPricingTests
    {
        private static MarketPricing Pricing => new MarketPricing(0.05, 0.05);

        [TestCase(0, 25)]
        [TestCase(50, 17.5)]
        [TestCase(100, 10)]
        [TestCase(150, 7.5)]
        [TestCase(200, 5)]
        [TestCase(5000, 5)]
        public void Price_GoesFromTheMaximumToTheBasePriceToTheMinimum_AsTheStockRises(double stock, double expected)
        {
            Assert.AreEqual(expected, Pricing.Price(5, 10, 25, stock, 100), 1e-9);
        }

        [TestCase(0, 100)]
        [TestCase(50, 55)]
        [TestCase(100, 10)]
        [TestCase(150, 6)]
        [TestCase(200, 2)]
        public void Price_FollowsTheBoundsOfTheGood_NotAShareOfItsBasePrice(double stock, double expected)
        {
            Assert.AreEqual(expected, Pricing.Price(2, 10, 100, stock, 100), 1e-9);
        }

        [Test]
        public void Price_OfANegativeStock_IsThatOfAnEmptyOne()
        {
            Assert.AreEqual(25, Pricing.Price(5, 10, 25, -1, 100), 1e-9);
        }

        [Test]
        public void Price_WithoutATarget_IsTheBasePrice()
        {
            Assert.AreEqual(10, Pricing.Price(5, 10, 25, 0, 0), 1e-9);
            Assert.AreEqual(10, Pricing.Price(5, 10, 25, 40, 0), 1e-9);
        }

        [Test]
        public void AGoodWhoseBoundsAreItsBasePrice_HasOnePrice()
        {
            Assert.AreEqual(10, Pricing.Price(10, 10, 10, 0, 100), 1e-9);
            Assert.AreEqual(10, Pricing.Price(10, 10, 10, 300, 100), 1e-9);
        }

        [Test]
        public void BuyPrice_IsThePriceOnceTheBarrelIsGone_PlusTheMargin_RoundedUp()
        {
            // One barrel left of a target of 100: the maximum price, 25, plus 5 %.
            Assert.AreEqual(27, Pricing.BuyPrice(5, 10, 25, 1, 100));
        }

        [Test]
        public void SellPrice_IsThePriceBeforeTheBarrelIsIn_MinusTheMargin_RoundedDown()
        {
            // Empty stock: 25, minus 5 %.
            Assert.AreEqual(23, Pricing.SellPrice(5, 10, 25, 0, 100));
        }

        [Test]
        public void Prices_ThatAreWholeNumbers_AreNotRoundedAway()
        {
            // 20 x 1.05 is 21.000000000000004 in floating point: still 21, not 22.
            Assert.AreEqual(21, Pricing.BuyPrice(10, 20, 50, 101, 100));
            Assert.AreEqual(19, Pricing.SellPrice(10, 20, 50, 100, 100));
        }

        [Test]
        public void TheMargins_ComeOnTopOfTheBounds()
        {
            // Bought at the maximum plus the margin, sold at the minimum minus it.
            Assert.AreEqual(105, Pricing.BuyPrice(40, 60, 100, 1, 100));
            Assert.AreEqual(38, Pricing.SellPrice(40, 60, 100, 500, 100));
        }

        [Test]
        public void SellPrice_CanBeNothing()
        {
            Assert.AreEqual(0, Pricing.SellPrice(1, 1, 1, 500, 100));
        }

        [TestCase(1, 1, 1)]
        [TestCase(5, 10, 25)]
        [TestCase(75, 150, 375)]
        [TestCase(2, 10, 100)]
        public void BuyingABarrelAndSellingItBack_AlwaysLosesGold(long minPrice, long basePrice, long maxPrice)
        {
            MarketPricing pricing = Pricing;

            for (int stock = 1; stock <= 300; stock++)
            {
                long paid = pricing.BuyPrice(minPrice, basePrice, maxPrice, stock, 100);
                long received = pricing.SellPrice(minPrice, basePrice, maxPrice, stock - 1, 100);

                Assert.Less(received, paid, $"stock {stock}");
            }
        }

        [Test]
        public void WithoutMargins_BuyingAndSellingBackIsNeverAGain()
        {
            var pricing = new MarketPricing(0, 0);

            for (int stock = 1; stock <= 300; stock++)
            {
                Assert.LessOrEqual(pricing.SellPrice(5, 10, 25, stock - 1, 100), pricing.BuyPrice(5, 10, 25, stock, 100));
            }
        }

        [TestCase(-0.1, 0.05)]
        [TestCase(0.05, 1.0)]
        [TestCase(double.NaN, 0.05)]
        [TestCase(0.05, double.NaN)]
        public void RejectsMarginsThatMakeNoSense(double buyMargin, double sellMargin)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketPricing(buyMargin, sellMargin));
        }
    }
}
