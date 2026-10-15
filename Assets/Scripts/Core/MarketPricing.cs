using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What a barrel costs in a city, as in Patrician III: a good's base price times a factor
    /// that follows the city's stock against its thresholds. The player buys on one curve and
    /// sells on another, never above it. Every barrel is priced at the average factor over the
    /// stock it takes away or adds, so a trade of several barrels costs the integral of the curve.
    /// </summary>
    public sealed class MarketPricing
    {
        // Prices are worked out in floating point: 20 x 1.1 must round up to 22, not 23.
        private const double RoundingMargin = 1e-9;

        /// <param name="buyCurve">The factors of what the player pays.</param>
        /// <param name="sellCurve">The factors of what the player is paid, never above the buy curve.</param>
        public MarketPricing(PriceCurve buyCurve, PriceCurve sellCurve)
        {
            BuyCurve = buyCurve ?? throw new ArgumentNullException(nameof(buyCurve));
            SellCurve = sellCurve ?? throw new ArgumentNullException(nameof(sellCurve));

            // Both curves are straight lines between the same thresholds: checking the five
            // points checks every stock.
            RequireSellNotAboveBuy(sellCurve.Empty, buyCurve.Empty, "an empty stock");
            RequireSellNotAboveBuy(sellCurve.AtT0, buyCurve.AtT0, "T0");
            RequireSellNotAboveBuy(sellCurve.AtT1, buyCurve.AtT1, "T1");
            RequireSellNotAboveBuy(sellCurve.AtT2, buyCurve.AtT2, "T2");
            RequireSellNotAboveBuy(sellCurve.AtT3, buyCurve.AtT3, "T3");
        }

        public PriceCurve BuyCurve { get; }

        public PriceCurve SellCurve { get; }

        /// <summary>
        /// What the player pays for one barrel from a city whose stock is
        /// <paramref name="stock"/>: the base price times the average buy factor from the
        /// stock once the barrel is gone to the stock before, rounded up, at least one coin.
        /// </summary>
        public long BuyPrice(long basePrice, double stock, StockThresholds thresholds)
        {
            double price = basePrice * BuyCurve.AverageFactor(stock - 1d, stock, thresholds);

            return Math.Max(1L, (long)Math.Ceiling(price - RoundingMargin));
        }

        /// <summary>
        /// What the player is paid for one barrel by a city whose stock is
        /// <paramref name="stock"/>: the base price times the average sell factor from the
        /// stock before to the stock once the barrel is in, rounded down. Buying a barrel and
        /// selling it back never gains gold.
        /// </summary>
        public long SellPrice(long basePrice, double stock, StockThresholds thresholds)
        {
            double price = basePrice * SellCurve.AverageFactor(stock, stock + 1d, thresholds);

            return Math.Max(0L, (long)Math.Floor(price + RoundingMargin));
        }

        private static void RequireSellNotAboveBuy(double sellFactor, double buyFactor, string point)
        {
            if (sellFactor > buyFactor)
            {
                throw new ArgumentOutOfRangeException(
                    "sellCurve",
                    $"At {point}, the sell factor is above the buy factor: a barrel would sell for more than it costs.");
            }
        }
    }
}
