using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MarketPricingTests
    {
        private static readonly StockThresholds Thresholds = new StockThresholds(20, 40, 60, 80);

        // A good the city does not produce: T2 is T1.
        private static readonly StockThresholds Step = new StockThresholds(20, 60, 60, 80);

        private static MarketPricing Pricing => TestMarkets.Pricing;

        [Test]
        public void AroundT1_TheTwoPricesAreCloseToTheBasePrice()
        {
            // Bought: 100 x 1.0125, the average over [39, 40], rounded up.
            Assert.AreEqual(102, Pricing.BuyPrice(100, 40, Thresholds));

            // Sold: 100 x 0.9925, the average over [40, 41], rounded down.
            Assert.AreEqual(99, Pricing.SellPrice(100, 40, Thresholds));
        }

        [Test]
        public void BuyPrice_IsTheAverageOverTheBarrelThatLeaves_RoundedUp()
        {
            // The last barrel: 10 x 3.9375, the average from 4 at 0 to 3.875 at 1.
            Assert.AreEqual(40, Pricing.BuyPrice(10, 1, Thresholds));
        }

        [Test]
        public void SellPrice_IsTheAverageOverTheBarrelThatComesIn_RoundedDown()
        {
            // Into an empty stock: 10 x 1.985, the average from 2 at 0 to 1.97 at 1.
            Assert.AreEqual(19, Pricing.SellPrice(10, 0, Thresholds));
        }

        [Test]
        public void ABarrelPastAnEmptyStock_CostsTheEmptyFactor()
        {
            Assert.AreEqual(40, Pricing.BuyPrice(10, 0, Thresholds));
            Assert.AreEqual(40, Pricing.BuyPrice(10, -3, Thresholds));
        }

        [Test]
        public void Prices_ThatAreWholeNumbers_AreNotRoundedAway()
        {
            // 20 x 1.1 is 22.000000000000004 and 100 x 0.29 is 28.999999999999996 in floating
            // point: still 22 and 29, however far above T3.
            var pricing = new MarketPricing(new PriceCurve(4, 1.5, 1.2, 1.1, 1.1), new PriceCurve(2, 1.2, 1, 0.5, 0.29));

            for (int stock = 81; stock <= 1000; stock++)
            {
                Assert.AreEqual(22, pricing.BuyPrice(20, stock, Thresholds), $"stock {stock}");
                Assert.AreEqual(29, pricing.SellPrice(100, stock, Thresholds), $"stock {stock}");
            }

            Assert.AreEqual(22, pricing.BuyPrice(20, 100000, Thresholds));
            Assert.AreEqual(29, pricing.SellPrice(100, 100000, Thresholds));
        }

        [Test]
        public void BuyPrice_IsAtLeastOneCoin_AndSellPriceCanBeNothing()
        {
            // 1 x 0.6 and 1 x 0.5.
            Assert.AreEqual(1, Pricing.BuyPrice(1, 500, Thresholds));
            Assert.AreEqual(0, Pricing.SellPrice(1, 500, Thresholds));
        }

        [Test]
        public void ATradeOfSeveralBarrels_CostsTheIntegralOfTheCurve_UpToTheRounding()
        {
            MarketPricing pricing = Pricing;
            long bought = 0;
            long sold = 0;

            for (int i = 0; i < 50; i++)
            {
                bought += pricing.BuyPrice(10, 70 - i, Thresholds);
                sold += pricing.SellPrice(10, 20 + i, Thresholds);
            }

            // From 70 barrels down to 20, and from 20 up to 70.
            double boughtIntegral = 10 * 50 * pricing.BuyCurve.AverageFactor(20, 70, Thresholds);
            double soldIntegral = 10 * 50 * pricing.SellCurve.AverageFactor(20, 70, Thresholds);

            Assert.GreaterOrEqual(bought, boughtIntegral - 1e-6);
            Assert.Less(bought, boughtIntegral + 50);
            Assert.LessOrEqual(sold, soldIntegral + 1e-6);
            Assert.Greater(sold, soldIntegral - 50);
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(600)]
        public void BuyingABarrelAndSellingItBack_NeverGainsGold(long basePrice)
        {
            MarketPricing pricing = Pricing;

            foreach (StockThresholds thresholds in new[] { Thresholds, Step })
            {
                for (int stock = 1; stock <= 300; stock++)
                {
                    long paid = pricing.BuyPrice(basePrice, stock, thresholds);
                    long received = pricing.SellPrice(basePrice, stock - 1, thresholds);

                    Assert.LessOrEqual(received, paid, $"stock {stock}");
                }
            }
        }

        [Test]
        public void WithTheSameCurveForBoth_BuyingAndSellingBackIsStillNeverAGain()
        {
            var pricing = new MarketPricing(TestMarkets.BuyCurve, TestMarkets.BuyCurve);

            for (int stock = 1; stock <= 300; stock++)
            {
                Assert.LessOrEqual(pricing.SellPrice(10, stock - 1, Step), pricing.BuyPrice(10, stock, Step));
            }
        }

        [Test]
        public void RejectsASellCurveAboveTheBuyCurve_AtAnyPoint()
        {
            // 0.9 at T2, above the 0.8 at which the player buys.
            var above = new PriceCurve(2, 1.4, 1, 0.9, 0.5);

            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketPricing(TestMarkets.BuyCurve, above));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketPricing(TestMarkets.SellCurve, TestMarkets.BuyCurve));
        }

        [Test]
        public void RejectsMissingCurves()
        {
            Assert.Throws<ArgumentNullException>(() => new MarketPricing(null, TestMarkets.SellCurve));
            Assert.Throws<ArgumentNullException>(() => new MarketPricing(TestMarkets.BuyCurve, null));
        }
    }
}
