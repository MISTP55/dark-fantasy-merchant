using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of one good in one city: its base price, what the city consumes of it
    /// for the inhabitants it has, and the workshop that produces it there, if any.
    /// </summary>
    public readonly struct MarketGood
    {
        /// <param name="basePrice">Gold per barrel that the factors of the price curves multiply, at least 1.</param>
        /// <param name="dailyConsumptionPerThousand">Barrels 1 000 inhabitants consume in a day; it can be a fraction.</param>
        /// <param name="maxWorkers">Most workers of the city's workshop for the good; 0 for a good the city does not produce.</param>
        /// <param name="dailyOutputPerWorker">Barrels a worker of that workshop makes in a day, its efficiency included.</param>
        /// <param name="isFood">Whether the stock of the good feeds the city: only food makes its population change.</param>
        public MarketGood(
            long basePrice,
            double dailyConsumptionPerThousand,
            int maxWorkers,
            double dailyOutputPerWorker,
            bool isFood = false)
        {
            if (basePrice < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice));
            }

            if (maxWorkers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWorkers));
            }

            RequireNotNegative(dailyConsumptionPerThousand, nameof(dailyConsumptionPerThousand));
            RequireNotNegative(dailyOutputPerWorker, nameof(dailyOutputPerWorker));

            BasePrice = basePrice;
            DailyConsumptionPerThousand = dailyConsumptionPerThousand;
            MaxWorkers = maxWorkers;
            DailyOutputPerWorker = dailyOutputPerWorker;
            IsFood = isFood;
        }

        public long BasePrice { get; }

        public double DailyConsumptionPerThousand { get; }

        public int MaxWorkers { get; }

        public double DailyOutputPerWorker { get; }

        public bool IsFood { get; }

        public bool IsProduced => MaxWorkers > 0 && DailyOutputPerWorker > 0d;

        /// <summary>Barrels the workshop makes in a day with all its workers: it does not depend on the city's size.</summary>
        public double DailyCapacity => MaxWorkers * DailyOutputPerWorker;

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
