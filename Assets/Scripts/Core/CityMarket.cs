using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The stocks of goods of one city. Every day the city produces some goods and
    /// consumes all of them; the price of a good follows its stock against a target.
    /// There is no limit to what a city can store. Goods are indices into the catalogue.
    /// It also remembers what the days of the last week actually produced and consumed.
    /// </summary>
    public sealed class CityMarket
    {
        private const double WholeBarrelMargin = 1e-9;

        private readonly MarketGood[] goods;
        private readonly double[] stocks;
        private readonly double targetDays;
        private readonly MarketPricing pricing;
        private readonly int daysPerWeek;
        private readonly MarketWeekLog weekLog;

        /// <param name="targetDays">Days of consumption a city holds when a good is at its base price.</param>
        /// <param name="startingDays">Days of consumption the city holds of every good when the game starts.</param>
        /// <param name="daysPerWeek">Days of the calendar's week, over which the weekly figures are given.</param>
        public CityMarket(
            IReadOnlyList<MarketGood> goods,
            double targetDays,
            double startingDays,
            MarketPricing pricing,
            int daysPerWeek)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            // The negated comparisons also reject NaN.
            if (!(targetDays >= 0d) || double.IsInfinity(targetDays))
            {
                throw new ArgumentOutOfRangeException(nameof(targetDays));
            }

            if (!(startingDays >= 0d) || double.IsInfinity(startingDays))
            {
                throw new ArgumentOutOfRangeException(nameof(startingDays));
            }

            if (daysPerWeek < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerWeek));
            }

            this.pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
            this.targetDays = targetDays;
            this.daysPerWeek = daysPerWeek;
            weekLog = new MarketWeekLog(goods.Count, daysPerWeek);
            this.goods = new MarketGood[goods.Count];
            stocks = new double[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                this.goods[i] = goods[i];

                // The same for every good, produced or not: producers fill up as the days pass.
                stocks[i] = startingDays * goods[i].DailyConsumption;
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

        /// <summary>Barrels the city produces in a week while its stock is under the ceiling.</summary>
        public double NominalWeeklyProductionOf(int good)
        {
            RequireGood(good);

            return goods[good].DailyProduction * daysPerWeek;
        }

        /// <summary>Barrels the city consumes in a week while it has them.</summary>
        public double NominalWeeklyConsumptionOf(int good)
        {
            RequireGood(good);

            return goods[good].DailyConsumption * daysPerWeek;
        }

        /// <summary>
        /// Barrels the city actually produced over the last days, a week of them at most:
        /// less than the nominal figure when the ceiling stopped the production.
        /// </summary>
        public double RecentProductionOf(int good)
        {
            return weekLog.ProducedOf(good);
        }

        /// <summary>
        /// Barrels the city actually consumed over the last days, a week of them at most:
        /// less than the nominal figure when the stock ran out. What the player buys is not counted.
        /// </summary>
        public double RecentConsumptionOf(int good)
        {
            return weekLog.ConsumedOf(good);
        }

        /// <summary>What the player pays for the next barrel, or for a later one of the same purchase.</summary>
        /// <param name="barrelsAlreadyBought">Barrels of this purchase that come before it.</param>
        public long BuyPriceOf(int good, int barrelsAlreadyBought = 0)
        {
            RequireGood(good);
            RequireNotNegative(barrelsAlreadyBought, nameof(barrelsAlreadyBought));

            MarketGood figures = goods[good];

            return pricing.BuyPrice(
                figures.MinPrice, figures.BasePrice, figures.MaxPrice, StockOf(good) - barrelsAlreadyBought, TargetOf(good));
        }

        /// <summary>What the player is paid for the next barrel, or for a later one of the same sale.</summary>
        /// <param name="barrelsAlreadySold">Barrels of this sale that come before it.</param>
        public long SellPriceOf(int good, int barrelsAlreadySold = 0)
        {
            RequireGood(good);
            RequireNotNegative(barrelsAlreadySold, nameof(barrelsAlreadySold));

            MarketGood figures = goods[good];

            return pricing.SellPrice(
                figures.MinPrice, figures.BasePrice, figures.MaxPrice, StockOf(good) + barrelsAlreadySold, TargetOf(good));
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
            weekLog.StartDay();

            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                double stockBefore = stocks[i];
                double stock = stockBefore;

                if (stock < good.Ceiling)
                {
                    stock = Math.Min(good.Ceiling, stock + good.DailyProduction);
                }

                stocks[i] = Math.Max(0d, stock - good.DailyConsumption);
                weekLog.Record(i, stock - stockBefore, stock - stocks[i]);
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
