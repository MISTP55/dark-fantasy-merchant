using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The figures of one good in one city: what it costs, and what the city consumes and produces of it.</summary>
    public readonly struct MarketGood
    {
        /// <param name="basePrice">Gold per barrel at the target stock, at least 1.</param>
        /// <param name="dailyConsumption">Barrels the city consumes in a day; it can be a fraction.</param>
        /// <param name="productionRate">Production as a share of the consumption; 0 for a good the city does not produce.</param>
        /// <param name="ceilingDays">Days of consumption above which the city's production stops raising the stock.</param>
        public MarketGood(long basePrice, double dailyConsumption, double productionRate, double ceilingDays)
        {
            if (basePrice < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice));
            }

            RequireNotNegative(dailyConsumption, nameof(dailyConsumption));
            RequireNotNegative(productionRate, nameof(productionRate));
            RequireNotNegative(ceilingDays, nameof(ceilingDays));

            BasePrice = basePrice;
            DailyConsumption = dailyConsumption;
            ProductionRate = productionRate;
            CeilingDays = ceilingDays;
        }

        public long BasePrice { get; }

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
