using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What a barrel costs in a city, from the city's stock against its target stock:
    /// dear when the stock is low, cheap when it is high. A city sells a little above
    /// that price and buys a little below it.
    /// </summary>
    public sealed class MarketPricing
    {
        // Prices are worked out in floating point: 20 x 1.05 must round up to 21, not 22.
        private const double RoundingMargin = 1e-9;

        // The stock, as a share of the target, from which the price stops falling.
        private const double SurplusRatio = 2d;

        private readonly double emptyStockMultiplier;
        private readonly double surplusMultiplier;
        private readonly double buyMargin;
        private readonly double sellMargin;

        /// <param name="emptyStockMultiplier">Share of the base price at an empty stock, 1 or more.</param>
        /// <param name="surplusMultiplier">Share of the base price at twice the target and above, above 0 and up to 1.</param>
        /// <param name="buyMargin">What the city adds when it sells to the player, from 0 to below 1.</param>
        /// <param name="sellMargin">What the city takes off when it buys from the player, from 0 to below 1.</param>
        public MarketPricing(double emptyStockMultiplier, double surplusMultiplier, double buyMargin, double sellMargin)
        {
            // The negated comparisons also reject NaN.
            if (!(emptyStockMultiplier >= 1d) || double.IsInfinity(emptyStockMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(emptyStockMultiplier));
            }

            if (!(surplusMultiplier > 0d && surplusMultiplier <= 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(surplusMultiplier));
            }

            if (!(buyMargin >= 0d && buyMargin < 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(buyMargin));
            }

            if (!(sellMargin >= 0d && sellMargin < 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(sellMargin));
            }

            this.emptyStockMultiplier = emptyStockMultiplier;
            this.surplusMultiplier = surplusMultiplier;
            this.buyMargin = buyMargin;
            this.sellMargin = sellMargin;
        }

        /// <summary>
        /// Share of the base price at a stock: a straight line from the empty stock's
        /// to 1 at the target, then down to the surplus one at twice the target. 1 for
        /// a good without a target, which the city does not consume.
        /// </summary>
        public double Multiplier(double stock, double target)
        {
            if (!(target > 0d))
            {
                return 1d;
            }

            double ratio = Math.Max(0d, stock) / target;

            if (ratio <= 1d)
            {
                return emptyStockMultiplier + (1d - emptyStockMultiplier) * ratio;
            }

            if (ratio < SurplusRatio)
            {
                return 1d + (surplusMultiplier - 1d) * (ratio - 1d) / (SurplusRatio - 1d);
            }

            return surplusMultiplier;
        }

        /// <summary>
        /// What the player pays for one barrel from a city whose stock is
        /// <paramref name="stock"/>: the price once that barrel is gone, plus the margin,
        /// rounded up, at least one coin.
        /// </summary>
        public long BuyPrice(long basePrice, double stock, double target)
        {
            double price = basePrice * Multiplier(stock - 1d, target) * (1d + buyMargin);

            return Math.Max(1L, (long)Math.Ceiling(price - RoundingMargin));
        }

        /// <summary>
        /// What the player is paid for one barrel by a city whose stock is
        /// <paramref name="stock"/>: the price before that barrel is in, minus the margin,
        /// rounded down. Both prices are taken at the lower of the two stocks, so buying
        /// a barrel and selling it back loses gold.
        /// </summary>
        public long SellPrice(long basePrice, double stock, double target)
        {
            double price = basePrice * Multiplier(stock, target) * (1d - sellMargin);

            return Math.Max(0L, (long)Math.Floor(price + RoundingMargin));
        }
    }
}
