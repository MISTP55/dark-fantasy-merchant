using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The figures of one good in one city: what it costs, and what the city consumes and produces of it.</summary>
    public readonly struct MarketGood
    {
        /// <param name="minPrice">Gold per barrel at twice the target stock and above, from 1 to the base price.</param>
        /// <param name="basePrice">Gold per barrel at the target stock.</param>
        /// <param name="maxPrice">Gold per barrel at an empty stock, the base price or more.</param>
        /// <param name="dailyConsumption">Barrels the city consumes in a day; it can be a fraction.</param>
        /// <param name="productionRate">Production as a share of the consumption; 0 for a good the city does not produce.</param>
        /// <param name="ceilingDays">Days of consumption above which the city's production stops raising the stock.</param>
        public MarketGood(
            long minPrice,
            long basePrice,
            long maxPrice,
            double dailyConsumption,
            double productionRate,
            double ceilingDays)
        {
            if (minPrice < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minPrice));
            }

            if (basePrice < minPrice)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice));
            }

            if (maxPrice < basePrice)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPrice));
            }

            RequireNotNegative(dailyConsumption, nameof(dailyConsumption));
            RequireNotNegative(productionRate, nameof(productionRate));
            RequireNotNegative(ceilingDays, nameof(ceilingDays));

            MinPrice = minPrice;
            BasePrice = basePrice;
            MaxPrice = maxPrice;
            DailyConsumption = dailyConsumption;
            ProductionRate = productionRate;
            CeilingDays = ceilingDays;
        }

        /// <summary>The floor of the price, however much the city holds.</summary>
        public long MinPrice { get; }

        public long BasePrice { get; }

        /// <summary>The ceiling of the price, however little the city holds.</summary>
        public long MaxPrice { get; }

        public double DailyConsumption { get; }

        public double ProductionRate { get; }

        public double CeilingDays { get; }

        public bool IsProduced => ProductionRate > 0d;

        /// <summary>Barrels the city produces in a day, while its stock is under the ceiling.</summary>
        public double DailyProduction => DailyConsumption * ProductionRate;

        /// <summary>The stock, in barrels, that the city's production does not exceed.</summary>
        public double Ceiling => DailyConsumption * CeilingDays;

        private static void RequireNotNegative(double value, string name)
        {
            // The negated comparison also rejects NaN.
            if (!(value >= 0d) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
