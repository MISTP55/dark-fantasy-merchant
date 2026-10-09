using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The stocks of goods of one city. Every day the city produces some goods and
    /// consumes all of them; the price of a good follows its stock against a target.
    /// There is no limit to what a city can store. Goods are indices into the catalogue.
    /// </summary>
    public sealed class CityMarket
    {
        private const double WholeBarrelMargin = 1e-9;

        private readonly MarketGood[] goods;
        private readonly double[] stocks;
        private readonly double targetDays;
        private readonly MarketPricing pricing;

        /// <param name="targetDays">Days of consumption a city holds when a good is at its base price.</param>
        public CityMarket(IReadOnlyList<MarketGood> goods, double targetDays, MarketPricing pricing)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            // The negated comparison also rejects NaN.
            if (!(targetDays >= 0d) || double.IsInfinity(targetDays))
            {
                throw new ArgumentOutOfRangeException(nameof(targetDays));
            }

            this.pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
            this.targetDays = targetDays;
            this.goods = new MarketGood[goods.Count];
            stocks = new double[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                this.goods[i] = goods[i];

                // The game opens balanced: producers are full, the others at their target.
                stocks[i] = goods[i].IsProduced ? goods[i].Ceiling : TargetOf(i);
            }
        }

        public int GoodCount => goods.Length;

        /// <summary>Raised once by a day that starts, and once when barrels were taken or put.</summary>
        public event Action Changed;

        /// <summary>Barrels in stock; a city consumes fractions of a barrel, so it is not whole.</summary>
        public double StockOf(int good)
        {
            RequireGood(good);

            return stocks[good];
        }

        /// <summary>Whole barrels in stock: what is shown and what can be bought.</summary>
        public int AvailableOf(int good)
        {
            // Days of fractional consumption leave a whole stock a hair short of itself.
            return (int)Math.Min(int.MaxValue, Math.Floor(StockOf(good) + WholeBarrelMargin));
        }

        /// <summary>The stock at which the good is at its base price.</summary>
        public double TargetOf(int good)
        {
            RequireGood(good);

            return targetDays * goods[good].DailyConsumption;
        }

        /// <summary>What the player pays for the next barrel, or for a later one of the same purchase.</summary>
        /// <param name="barrelsAlreadyBought">Barrels of this purchase that come before it.</param>
        public long BuyPriceOf(int good, int barrelsAlreadyBought = 0)
        {
            RequireGood(good);
            RequireNotNegative(barrelsAlreadyBought, nameof(barrelsAlreadyBought));

            return pricing.BuyPrice(goods[good].BasePrice, StockOf(good) - barrelsAlreadyBought, TargetOf(good));
        }

        /// <summary>What the player is paid for the next barrel, or for a later one of the same sale.</summary>
        /// <param name="barrelsAlreadySold">Barrels of this sale that come before it.</param>
        public long SellPriceOf(int good, int barrelsAlreadySold = 0)
        {
            RequireGood(good);
            RequireNotNegative(barrelsAlreadySold, nameof(barrelsAlreadySold));

            return pricing.SellPrice(goods[good].BasePrice, StockOf(good) + barrelsAlreadySold, TargetOf(good));
        }

        /// <summary>Takes whole barrels out of the stock. Throws when fewer are available.</summary>
        public void Take(int good, int barrels)
        {
            RequirePositive(barrels);

            if (barrels > AvailableOf(good))
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }

            // Not below nothing: the last barrel can be that hair short.
            stocks[good] = Math.Max(0d, stocks[good] - barrels);
            Changed?.Invoke();
        }

        /// <summary>Adds whole barrels to the stock, whatever it already holds.</summary>
        public void Put(int good, int barrels)
        {
            RequireGood(good);
            RequirePositive(barrels);

            stocks[good] += barrels;
            Changed?.Invoke();
        }

        /// <summary>
        /// One day of the city: it produces, up to each good's ceiling and never above,
        /// then consumes, down to nothing. A stock that deliveries raised above its
        /// ceiling is only consumed.
        /// </summary>
        public void StartDay()
        {
            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                double stock = stocks[i];

                if (stock < good.Ceiling)
                {
                    stock = Math.Min(good.Ceiling, stock + good.DailyProduction);
                }

                stocks[i] = Math.Max(0d, stock - good.DailyConsumption);
            }

            Changed?.Invoke();
        }

        private void RequireGood(int good)
        {
            if (good < 0 || good >= goods.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }
        }

        private static void RequirePositive(int barrels)
        {
            if (barrels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }
        }

        private static void RequireNotNegative(int barrels, string name)
        {
            if (barrels < 0)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
