using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The stocks of goods of one city. Every day the city consumes its goods, then its
    /// workshops hire or lay off by where each stock stands against its thresholds, and
    /// produce; the price of a good follows its stock against the same thresholds. There is
    /// no limit to what a city can store. Goods are indices into the catalogue. It also
    /// remembers what the days of the last week actually produced and consumed. What it
    /// consumes follows its population, which the food it holds makes grow or decline every day.
    /// </summary>
    public sealed class CityMarket
    {
        private const double WholeUnitMargin = 1e-9;

        private readonly MarketGood[] goods;
        private readonly double[] stocks;
        private readonly int[] workers;
        private readonly ThresholdRule thresholdRule;
        private readonly WorkforceRule workforce;
        private readonly MarketPricing pricing;
        private readonly int daysPerWeek;
        private readonly MarketWeekLog weekLog;
        private readonly FoodGrowth foodGrowth;

        /// <param name="thresholdRule">How the stock thresholds of a good, at which its prices are given, follow what the city consumes and produces of it.</param>
        /// <param name="workforce">How the workshops hire, lay off and start.</param>
        /// <param name="startingDays">Days of consumption the city holds of every good when the game starts.</param>
        /// <param name="daysPerWeek">Days of the calendar's week, over which the weekly figures are given.</param>
        /// <param name="population">The inhabitants of the city, who set what it consumes.</param>
        /// <param name="foodGrowth">How food changes the population. Without it, the population stays what it is.</param>
        public CityMarket(
            IReadOnlyList<MarketGood> goods,
            ThresholdRule thresholdRule,
            WorkforceRule workforce,
            double startingDays,
            MarketPricing pricing,
            int daysPerWeek,
            CityPopulation population,
            FoodGrowth foodGrowth = null)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            this.thresholdRule = thresholdRule ?? throw new ArgumentNullException(nameof(thresholdRule));
            this.workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));

            // The negated comparisons also reject NaN.
            if (!(startingDays >= 0d) || double.IsInfinity(startingDays))
            {
                throw new ArgumentOutOfRangeException(nameof(startingDays));
            }

            if (daysPerWeek < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerWeek));
            }

            this.pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
            Population = population ?? throw new ArgumentNullException(nameof(population));
            this.foodGrowth = foodGrowth;
            this.daysPerWeek = daysPerWeek;
            weekLog = new MarketWeekLog(goods.Count, daysPerWeek);
            this.goods = new MarketGood[goods.Count];
            stocks = new double[goods.Count];
            workers = new int[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                this.goods[i] = goods[i];

                // The same for every good, produced or not: producers fill up as the days pass.
                stocks[i] = startingDays * DailyConsumptionOf(i);
            }

            int workshops = Array.FindAll(this.goods, good => good.IsProduced).Length;

            for (int i = 0; i < this.goods.Length; i++)
            {
                workers[i] = this.goods[i].IsProduced
                    ? workforce.StartingWorkersOf(this.goods[i].MaxWorkers, workshops)
                    : 0;
            }
        }

        public int GoodCount => goods.Length;

        public CityPopulation Population { get; }

        /// <summary>
        /// The change of the population over a month, as a share of it, that the food in
        /// stock gives at this moment; 0 for a city that eats nothing.
        /// </summary>
        public double MonthlyGrowthRate =>
            foodGrowth != null && TryGetFoodDays(foodGrowth.AbundantDays, out double foodDays)
                ? foodGrowth.MonthlyRateAt(foodDays)
                : 0d;

        /// <summary>
        /// Raised once by a day that starts, after the population has changed, and once
        /// when units were taken or put.
        /// </summary>
        public event Action Changed;

        /// <summary>Units the inhabitants consume in a day while the city has them.</summary>
        public double DailyConsumptionOf(int good)
        {
            RequireGood(good);

            return goods[good].DailyConsumptionPerThousand * Population.Count / 1000d;
        }

        /// <summary>
        /// The days of its consumption the city holds of food: the average of its foods,
        /// each counted up to a number of days, so that plenty of one does not make up
        /// for the lack of another.
        /// </summary>
        /// <param name="daysCountedPerFood">The most days one food is counted for.</param>
        /// <returns>False for a city that consumes no food.</returns>
        public bool TryGetFoodDays(double daysCountedPerFood, out double foodDays)
        {
            double sum = 0d;
            int foods = 0;

            for (int i = 0; i < goods.Length; i++)
            {
                double consumption = DailyConsumptionOf(i);

                if (goods[i].IsFood && consumption > 0d)
                {
                    sum += Math.Min(daysCountedPerFood, stocks[i] / consumption);
                    foods++;
                }
            }

            foodDays = foods > 0 ? sum / foods : 0d;
            return foods > 0;
        }

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

        /// <summary>The stocks at which the prices of a good are given, for the population of the moment.</summary>
        public StockThresholds ThresholdsOf(int good)
        {
            RequireGood(good);

            return thresholdRule.ThresholdsFor(DailyConsumptionOf(good), goods[good].DailyCapacity);
        }

        /// <summary>Workers of the city's workshop for a good: 0 for a good it does not produce.</summary>
        public int WorkersOf(int good)
        {
            RequireGood(good);

            return workers[good];
        }

        /// <summary>The most workers the city's workshop for a good can have: 0 for a good it does not produce.</summary>
        public int MaxWorkersOf(int good)
        {
            RequireGood(good);

            return goods[good].IsProduced ? goods[good].MaxWorkers : 0;
        }

        /// <summary>Units the workshop makes in a week with the workers of the moment, while the stock is not above T3.</summary>
        public double NominalWeeklyProductionOf(int good)
        {
            return workers[good] * goods[good].DailyOutputPerWorker * daysPerWeek;
        }

        /// <summary>Units the city consumes in a week while it has them.</summary>
        public double NominalWeeklyConsumptionOf(int good)
        {
            return DailyConsumptionOf(good) * daysPerWeek;
        }

        /// <summary>
        /// Units the city actually produced over the last days, a week of them at most:
        /// less than the nominal figure on the days the stock was above T3.
        /// </summary>
        public double RecentProductionOf(int good)
        {
            return weekLog.ProducedOf(good);
        }

        /// <summary>
        /// Units the city actually consumed over the last days, a week of them at most:
        /// less than the nominal figure when the stock ran out. What the player buys is not counted.
        /// </summary>
        public double RecentConsumptionOf(int good)
        {
            return weekLog.ConsumedOf(good);
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
            stocks[good] = Math.Max(0d, stocks[good] - units);
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
        /// One day of the city, as in Patrician III: the inhabitants consume every good, down
        /// to nothing; then every workshop hires or lays off by where the stock left stands
        /// against the thresholds, and produces, unless that stock is above T3. The food that
        /// is left then changes the population.
        /// </summary>
        public void StartDay()
        {
            weekLog.StartDay();

            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                double consumed = Math.Min(stocks[i], DailyConsumptionOf(i));
                double produced = 0d;
                stocks[i] -= consumed;

                if (good.IsProduced)
                {
                    StockThresholds thresholds = ThresholdsOf(i);
                    workers[i] = workforce.NextWorkers(workers[i], good.MaxWorkers, stocks[i], thresholds);

                    if (WorkforceRule.ProducesAt(stocks[i], thresholds))
                    {
                        produced = workers[i] * good.DailyOutputPerWorker;
                        stocks[i] += produced;
                    }
                }

                weekLog.Record(i, produced, consumed);
            }

            if (foodGrowth != null)
            {
                Population.GrowForADay(MonthlyGrowthRate, foodGrowth.DaysPerMonth);
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
