using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The stocks of goods of one city, their thresholds and their prices: the price of a good follows its stock
    /// against its thresholds, which follow what the city needs of it and can produce. There is no limit to what a
    /// city can store. Goods are indices into the catalogue. What moves the stocks in a day, and what the city needs,
    /// is its <see cref="City"/>'s.
    /// </summary>
    public sealed class CityMarket
    {
        private const double WholeUnitMargin = 1e-9;

        private readonly MarketGood[] goods;
        private readonly double[] stocks;
        private readonly StockThresholds[] thresholds;
        private readonly ThresholdRule thresholdRule;
        private readonly MarketPricing pricing;
        private readonly Func<int, double> dailyDemandOf;

        /// <param name="thresholdRule">How the stock thresholds of a good, at which its prices are given, follow what the city needs (its inhabitants and its workshops' inputs) and produces of it.</param>
        /// <param name="dailyDemandOf">What the city needs of a good in a day, by the good's index: read at once and whenever the thresholds are worked out.</param>
        /// <param name="startingDays">Days of its demand the city holds of every good when the game starts.</param>
        public CityMarket(
            IReadOnlyList<MarketGood> goods,
            ThresholdRule thresholdRule,
            MarketPricing pricing,
            Func<int, double> dailyDemandOf,
            double startingDays)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            this.thresholdRule = thresholdRule ?? throw new ArgumentNullException(nameof(thresholdRule));
            this.pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
            this.dailyDemandOf = dailyDemandOf ?? throw new ArgumentNullException(nameof(dailyDemandOf));

            // The negated comparisons also reject NaN.
            if (!(startingDays >= 0d) || double.IsInfinity(startingDays))
            {
                throw new ArgumentOutOfRangeException(nameof(startingDays));
            }

            this.goods = new MarketGood[goods.Count];
            stocks = new double[goods.Count];
            thresholds = new StockThresholds[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                this.goods[i] = goods[i];

                // The same days for every good, produced or not: producers fill up as the days pass.
                stocks[i] = startingDays * dailyDemandOf(i);
            }

            UpdateThresholds();
        }

        public int GoodCount => goods.Length;

        /// <summary>Raised once when units were taken or put, and once by the end of a day.</summary>
        public event Action Changed;

        /// <summary>Units in stock; a city consumes fractions of a unit, so it is not whole.</summary>
        public double StockOf(int good)
        {
            RequireGood(good);

            return stocks[good];
        }

        /// <summary>Whole units in stock: what is shown and what can be bought.</summary>
        public int AvailableOf(int good)
        {
            // Days of fractional consumption leave a whole stock a hair short of itself.
            return (int)Math.Min(int.MaxValue, Math.Floor(StockOf(good) + WholeUnitMargin));
        }

        /// <summary>
        /// The stocks at which the prices of a good are given and its workshop hires, as the city
        /// keeps them: worked out at the start and at the end of every day, for the population
        /// of that moment, and unchanged in between, so that every workshop of a day decides on the
        /// same thresholds.
        /// </summary>
        public StockThresholds ThresholdsOf(int good)
        {
            RequireGood(good);

            return thresholds[good];
        }

        /// <summary>
        /// The factor at which the city sells the good to a buyer when its stock is <paramref name="stock"/>,
        /// as the AI merchants weigh it: one point of the curve, no integral.
        /// </summary>
        public double BuyFactorAt(int good, double stock)
        {
            RequireGood(good);

            return pricing.BuyCurve.FactorAt(stock, ThresholdsOf(good));
        }

        /// <summary>
        /// The factor at which the city buys the good from a seller when its stock is <paramref name="stock"/>,
        /// as the AI merchants weigh it: one point of the curve, no integral.
        /// </summary>
        public double SellFactorAt(int good, double stock)
        {
            RequireGood(good);

            return pricing.SellCurve.FactorAt(stock, ThresholdsOf(good));
        }

        /// <summary>What the player pays for the next unit, or for a later one of the same purchase.</summary>
        /// <param name="unitsAlreadyBought">Units of this purchase that come before it.</param>
        public long BuyPriceOf(int good, int unitsAlreadyBought = 0)
        {
            RequireGood(good);
            RequireNotNegative(unitsAlreadyBought, nameof(unitsAlreadyBought));

            return pricing.BuyPrice(goods[good].BasePrice, StockOf(good) - unitsAlreadyBought, ThresholdsOf(good));
        }

        /// <summary>What the player is paid for the next unit, or for a later one of the same sale.</summary>
        /// <param name="unitsAlreadySold">Units of this sale that come before it.</param>
        public long SellPriceOf(int good, int unitsAlreadySold = 0)
        {
            RequireGood(good);
            RequireNotNegative(unitsAlreadySold, nameof(unitsAlreadySold));

            return pricing.SellPrice(goods[good].BasePrice, StockOf(good) + unitsAlreadySold, ThresholdsOf(good));
        }

        /// <summary>Takes whole units out of the stock. Throws when fewer are available.</summary>
        public void Take(int good, int units)
        {
            RequirePositive(units);

            if (units > AvailableOf(good))
            {
                throw new ArgumentOutOfRangeException(nameof(units));
            }

            // Not below nothing: the last unit can be that hair short.
            Withdraw(good, units);
            Changed?.Invoke();
        }

        /// <summary>Adds whole units to the stock, whatever it already holds.</summary>
        public void Put(int good, int units)
        {
            RequireGood(good);
            RequirePositive(units);

            stocks[good] += units;
            Changed?.Invoke();
        }

        /// <summary>
        /// Takes up to these units out of the stock, fractions included, for the day of the city: nothing is told
        /// before the day ends.
        /// </summary>
        /// <returns>The units taken: the whole stock when it holds fewer.</returns>
        internal double Withdraw(int good, double units)
        {
            double taken = Math.Min(stocks[good], units);
            stocks[good] -= taken;

            return taken;
        }

        /// <summary>Adds units to the stock, fractions included, for the day of the city: nothing is told before the day ends.</summary>
        internal void Deposit(int good, double units)
        {
            stocks[good] += units;
        }

        /// <summary>Ends the day of the city: the next one starts on the thresholds of the demand this one leaves.</summary>
        internal void EndDay()
        {
            UpdateThresholds();
            Changed?.Invoke();
        }

        private void UpdateThresholds()
        {
            for (int i = 0; i < goods.Length; i++)
            {
                thresholds[i] = thresholdRule.ThresholdsFor(dailyDemandOf(i), goods[i].DailyCapacity);
            }
        }

        private void RequireGood(int good)
        {
            if (good < 0 || good >= goods.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }
        }

        private static void RequirePositive(int units)
        {
            if (units < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(units));
            }
        }

        private static void RequireNotNegative(int units, string name)
        {
            if (units < 0)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
