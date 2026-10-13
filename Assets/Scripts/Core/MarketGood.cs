using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of one good in one city: what it costs, and what the city consumes and
    /// produces of it for the inhabitants it has.
    /// </summary>
    public readonly struct MarketGood
    {
        /// <param name="minPrice">Gold per barrel at twice the target stock and above, from 1 to the base price.</param>
        /// <param name="basePrice">Gold per barrel at the target stock.</param>
        /// <param name="maxPrice">Gold per barrel at an empty stock, the base price or more.</param>
        /// <param name="dailyConsumptionPerThousand">Barrels 1 000 inhabitants consume in a day; it can be a fraction.</param>
        /// <param name="productionRate">Production as a share of the consumption; 0 for a good the city does not produce.</param>
        /// <param name="ceilingDays">Days of consumption above which the city's production stops raising the stock.</param>
        /// <param name="isFood">Whether the stock of the good feeds the city: only food makes its population change.</param>
        public MarketGood(
            long minPrice,
            long basePrice,
            long maxPrice,
            double dailyConsumptionPerThousand,
            double productionRate,
            double ceilingDays,
            bool isFood = false)
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

            RequireNotNegative(dailyConsumptionPerThousand, nameof(dailyConsumptionPerThousand));
            RequireNotNegative(productionRate, nameof(productionRate));
            RequireNotNegative(ceilingDays, nameof(ceilingDays));

            MinPrice = minPrice;
            BasePrice = basePrice;
            MaxPrice = maxPrice;
            DailyConsumptionPerThousand = dailyConsumptionPerThousand;
            ProductionRate = productionRate;
            CeilingDays = ceilingDays;
            IsFood = isFood;
        }

        /// <summary>The floor of the price, however much the city holds.</summary>
        public long MinPrice { get; }

        public long BasePrice { get; }

        /// <summary>The ceiling of the price, however little the city holds.</summary>
        public long MaxPrice { get; }

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
