using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The stocks of goods of one city. Every day the city produces some goods and
    /// consumes all of them; the price of a good follows its stock against thresholds that follow its consumption and production.
    /// There is no limit to what a city can store. Goods are indices into the catalogue.
    /// It also remembers what the days of the last week actually produced and consumed.
    /// What it consumes and produces follows its population, which the food it holds
    /// makes grow or decline every day.
    /// </summary>
    public sealed class CityMarket
    {
        private const double WholeBarrelMargin = 1e-9;

        private readonly MarketGood[] goods;
        private readonly double[] stocks;
        private readonly ThresholdRule thresholdRule;
        private readonly MarketPricing pricing;
        private readonly int daysPerWeek;
        private readonly MarketWeekLog weekLog;
        private readonly FoodGrowth foodGrowth;

        /// <param name="thresholds">How the stock thresholds of a good, at which its prices are given, follow what the city consumes and produces of it.</param>
        /// <param name="startingDays">Days of consumption the city holds of every good when the game starts.</param>
        /// <param name="daysPerWeek">Days of the calendar's week, over which the weekly figures are given.</param>
        /// <param name="population">The inhabitants of the city, who set what it consumes.</param>
        /// <param name="foodGrowth">How food changes the population. Without it, the population stays what it is.</param>
        public CityMarket(
            IReadOnlyList<MarketGood> goods,
            ThresholdRule thresholds,
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

            thresholdRule = thresholds ?? throw new ArgumentNullException(nameof(thresholds));

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

            for (int i = 0; i < goods.Count; i++)
            {
                this.goods[i] = goods[i];

                // The same for every good, produced or not: producers fill up as the days pass.
                stocks[i] = startingDays * DailyConsumptionOf(i);
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
        /// when barrels were taken or put.
        /// </summary>
        public event Action Changed;

        /// <summary>Barrels the inhabitants consume in a day while the city has them.</summary>
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

        /// <summary>The stocks at which the prices of a good are given, for the population of the moment.</summary>
        public StockThresholds ThresholdsOf(int good)
        {
            RequireGood(good);

            double consumption = DailyConsumptionOf(good);

            return thresholdRule.ThresholdsFor(consumption, consumption * goods[good].ProductionRate);
        }

        /// <summary>Barrels the city produces in a week while its stock is under the ceiling.</summary>
        public double NominalWeeklyProductionOf(int good)
        {
            return DailyConsumptionOf(good) * goods[good].ProductionRate * daysPerWeek;
        }

        /// <summary>Barrels the city consumes in a week while it has them.</summary>
        public double NominalWeeklyConsumptionOf(int good)
        {
            return DailyConsumptionOf(good) * daysPerWeek;
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

            return pricing.BuyPrice(goods[good].BasePrice, StockOf(good) - barrelsAlreadyBought, ThresholdsOf(good));
        }

        /// <summary>What the player is paid for the next barrel, or for a later one of the same sale.</summary>
        /// <param name="barrelsAlreadySold">Barrels of this sale that come before it.</param>
        public long SellPriceOf(int good, int barrelsAlreadySold = 0)
        {
            RequireGood(good);
            RequireNotNegative(barrelsAlreadySold, nameof(barrelsAlreadySold));

            return pricing.SellPrice(goods[good].BasePrice, StockOf(good) + barrelsAlreadySold, ThresholdsOf(good));
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
        /// ceiling is only consumed. The food that is left then changes the population.
        /// </summary>
        public void StartDay()
        {
            weekLog.StartDay();

            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                double consumption = DailyConsumptionOf(i);
                double ceiling = consumption * good.CeilingDays;
                double stockBefore = stocks[i];
                double stock = stockBefore;

                if (stock < ceiling)
                {
                    stock = Math.Min(ceiling, stock + consumption * good.ProductionRate);
                }

                stocks[i] = Math.Max(0d, stock - consumption);
                weekLog.Record(i, stock - stockBefore, stock - stocks[i]);
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
