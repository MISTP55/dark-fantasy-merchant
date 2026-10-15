using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of one good in one city: its base price, and what the city consumes and
    /// produces of it for the inhabitants it has.
    /// </summary>
    public readonly struct MarketGood
    {
        /// <param name="basePrice">Gold per barrel that the factors of the price curves multiply, at least 1.</param>
        /// <param name="dailyConsumptionPerThousand">Barrels 1 000 inhabitants consume in a day; it can be a fraction.</param>
        /// <param name="productionRate">Production as a share of the consumption; 0 for a good the city does not produce.</param>
        /// <param name="ceilingDays">Days of consumption above which the city's production stops raising the stock.</param>
        /// <param name="isFood">Whether the stock of the good feeds the city: only food makes its population change.</param>
        public MarketGood(
            long basePrice,
            double dailyConsumptionPerThousand,
            double productionRate,
            double ceilingDays,
            bool isFood = false)
        {
            if (basePrice < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice));
            }

            RequireNotNegative(dailyConsumptionPerThousand, nameof(dailyConsumptionPerThousand));
            RequireNotNegative(productionRate, nameof(productionRate));
            RequireNotNegative(ceilingDays, nameof(ceilingDays));

            BasePrice = basePrice;
            DailyConsumptionPerThousand = dailyConsumptionPerThousand;
            ProductionRate = productionRate;
            CeilingDays = ceilingDays;
            IsFood = isFood;
        }

        public long BasePrice { get; }

        public double DailyConsumptionPerThousand { get; }

        public double ProductionRate { get; }

        public double CeilingDays { get; }

        public bool IsFood { get; }

        public bool IsProduced => ProductionRate > 0d;

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
