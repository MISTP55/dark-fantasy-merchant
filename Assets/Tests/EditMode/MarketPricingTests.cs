using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MarketPricingTests
    {
        private static MarketPricing Pricing => new MarketPricing(2.5, 0.5, 0.05, 0.05);

        [TestCase(0, 2.5)]
        [TestCase(50, 1.75)]
        [TestCase(100, 1.0)]
        [TestCase(150, 0.75)]
        [TestCase(200, 0.5)]
        [TestCase(5000, 0.5)]
        public void Multiplier_FollowsTheStockAgainstTheTarget(double stock, double expected)
        {
            Assert.AreEqual(expected, Pricing.Multiplier(stock, 100), 1e-9);
        }

        [Test]
        public void Multiplier_OfANegativeStock_IsThatOfAnEmptyOne()
        {
            Assert.AreEqual(2.5, Pricing.Multiplier(-1, 100), 1e-9);
        }

        [Test]
        public void Multiplier_WithoutATarget_IsOne()
        {
            Assert.AreEqual(1.0, Pricing.Multiplier(0, 0), 1e-9);
            Assert.AreEqual(1.0, Pricing.Multiplier(40, 0), 1e-9);
        }

        [Test]
        public void BuyPrice_IsThePriceOnceTheBarrelIsGone_PlusTheMargin_RoundedUp()
        {
            // One barrel left of a target of 100: the price of an empty stock, 25, plus 5 %.
            Assert.AreEqual(27, Pricing.BuyPrice(10, 1, 100));
        }

        [Test]
        public void SellPrice_IsThePriceBeforeTheBarrelIsIn_MinusTheMargin_RoundedDown()
        {
            // Empty stock: 25, minus 5 %.
            Assert.AreEqual(23, Pricing.SellPrice(10, 0, 100));
        }

        [Test]
        public void Prices_ThatAreWholeNumbers_AreNotRoundedAway()
        {
            // 20 x 1.05 is 21.000000000000004 in floating point: still 21, not 22.
            Assert.AreEqual(21, Pricing.BuyPrice(20, 101, 100));
            Assert.AreEqual(19, Pricing.SellPrice(20, 100, 100));
        }

        [Test]
        public void BuyPrice_IsAtLeastOneCoin()
        {
            Assert.AreEqual(1, Pricing.BuyPrice(1, 500, 100));
        }

        [Test]
        public void SellPrice_CanBeNothing()
        {
            Assert.AreEqual(0, Pricing.SellPrice(1, 500, 100));
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(150)]
        public void BuyingABarrelAndSellingItBack_AlwaysLosesGold(long basePrice)
        {
            MarketPricing pricing = Pricing;

            for (int stock = 1; stock <= 300; stock++)
            {
                long paid = pricing.BuyPrice(basePrice, stock, 100);
                long received = pricing.SellPrice(basePrice, stock - 1, 100);

                Assert.Less(received, paid, $"stock {stock}");
            }
        }

        [Test]
        public void WithoutMargins_BuyingAndSellingBackIsNeverAGain()
        {
            var pricing = new MarketPricing(2.5, 0.5, 0, 0);

            for (int stock = 1; stock <= 300; stock++)
            {
                Assert.LessOrEqual(pricing.SellPrice(10, stock - 1, 100), pricing.BuyPrice(10, stock, 100));
            }
        }

        [TestCase(0.9, 0.5, 0.05, 0.05)]
        [TestCase(2.5, 0.0, 0.05, 0.05)]
        [TestCase(2.5, 1.5, 0.05, 0.05)]
        [TestCase(2.5, 0.5, -0.1, 0.05)]
        [TestCase(2.5, 0.5, 0.05, 1.0)]
        [TestCase(double.NaN, 0.5, 0.05, 0.05)]
        public void RejectsSettingsThatMakeNoSense(double empty, double surplus, double buyMargin, double sellMargin)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketPricing(empty, surplus, buyMargin, sellMargin));
        }
    }
}
