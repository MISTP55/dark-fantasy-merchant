using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What a barrel costs in a city, from the city's stock against its target stock:
    /// the good's maximum price when the stock is empty, its base price at the target,
    /// its minimum price when the stock is high. A city sells a little above that price
    /// and buys a little below it.
    /// </summary>
    public sealed class MarketPricing
    {
        // Prices are worked out in floating point: 20 x 1.05 must round up to 21, not 22.
        private const double RoundingMargin = 1e-9;

        // The stock, as a share of the target, from which the price stops falling.
        private const double SurplusRatio = 2d;

        private readonly double buyMargin;
        private readonly double sellMargin;

        /// <param name="buyMargin">What the city adds when it sells to the player, from 0 to below 1.</param>
        /// <param name="sellMargin">What the city takes off when it buys from the player, from 0 to below 1.</param>
        public MarketPricing(double buyMargin, double sellMargin)
        {
            // The negated comparisons also reject NaN.
            if (!(buyMargin >= 0d && buyMargin < 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(buyMargin));
            }

            if (!(sellMargin >= 0d && sellMargin < 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(sellMargin));
            }

            this.buyMargin = buyMargin;
            this.sellMargin = sellMargin;
        }

        /// <summary>
        /// The price of a good at a stock, before the margins: a straight line from its
        /// maximum price at an empty stock to its base price at the target, then down to
        /// its minimum price at twice the target, where it stays. The base price for a
        /// good without a target, which the city does not consume.
        /// </summary>
        public double Price(long minPrice, long basePrice, long maxPrice, double stock, double target)
        {
            if (!(target > 0d))
            {
                return basePrice;
            }

            double ratio = Math.Max(0d, stock) / target;

            if (ratio <= 1d)
            {
                return maxPrice + (basePrice - maxPrice) * ratio;
            }

            if (ratio < SurplusRatio)
            {
                return basePrice + (minPrice - basePrice) * (ratio - 1d) / (SurplusRatio - 1d);
            }

            return minPrice;
        }

        /// <summary>
        /// What the player pays for one barrel from a city whose stock is
        /// <paramref name="stock"/>: the price once that barrel is gone, plus the margin,
        /// rounded up, at least one coin. The margin comes on top of the maximum price.
        /// </summary>
        public long BuyPrice(long minPrice, long basePrice, long maxPrice, double stock, double target)
        {
            double price = Price(minPrice, basePrice, maxPrice, stock - 1d, target) * (1d + buyMargin);

            return Math.Max(1L, (long)Math.Ceiling(price - RoundingMargin));
        }

        /// <summary>
        /// What the player is paid for one barrel by a city whose stock is
        /// <paramref name="stock"/>: the price before that barrel is in, minus the margin,
        /// rounded down. The margin comes off the minimum price too. Both prices are taken
        /// at the lower of the two stocks, so buying a barrel and selling it back loses gold.
        /// </summary>
        public long SellPrice(long minPrice, long basePrice, long maxPrice, double stock, double target)
        {
            double price = Price(minPrice, basePrice, maxPrice, stock, target) * (1d - sellMargin);

            return Math.Max(0L, (long)Math.Floor(price + RoundingMargin));
        }
    }
}
