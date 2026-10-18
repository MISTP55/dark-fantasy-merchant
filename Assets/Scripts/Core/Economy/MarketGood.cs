using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of one good in one city: its base price, what the city consumes of it
    /// for the inhabitants it has, and the workshop that produces it there, if any.
    /// </summary>
    public readonly struct MarketGood
    {
        /// <param name="basePrice">Gold per unit that the factors of the price curves multiply, above 0; it can be a fraction.</param>
        /// <param name="dailyConsumptionPerThousand">Units of the good 1 000 inhabitants consume in a day; it can be a fraction.</param>
        /// <param name="maxWorkers">Most workers of the city's workshop for the good; 0 for a good the city does not produce.</param>
        /// <param name="dailyOutputPerWorker">Units of the good a worker of that workshop makes in a day, its efficiency included.</param>
        /// <param name="merchantDailyOutputPerWorker">Units a worker of a merchant's workshop for the good (the player's) makes in a day, the city's efficiency included, before the bonus of <see cref="MerchantOutputRule"/>; 0 where the city does not produce the good.</param>
        public MarketGood(
            double basePrice,
            double dailyConsumptionPerThousand,
            int maxWorkers,
            double dailyOutputPerWorker,
            double merchantDailyOutputPerWorker = 0d)
        {
            // The negated comparison also rejects NaN.
            if (!(basePrice > 0d) || double.IsInfinity(basePrice))
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice));
            }

            if (maxWorkers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWorkers));
            }

            RequireNotNegative(dailyConsumptionPerThousand, nameof(dailyConsumptionPerThousand));
            RequireNotNegative(dailyOutputPerWorker, nameof(dailyOutputPerWorker));
            RequireNotNegative(merchantDailyOutputPerWorker, nameof(merchantDailyOutputPerWorker));

            BasePrice = basePrice;
            DailyConsumptionPerThousand = dailyConsumptionPerThousand;
            MaxWorkers = maxWorkers;
            DailyOutputPerWorker = dailyOutputPerWorker;
            MerchantDailyOutputPerWorker = merchantDailyOutputPerWorker;
        }

        public double BasePrice { get; }

        public double DailyConsumptionPerThousand { get; }

        public int MaxWorkers { get; }

        public double DailyOutputPerWorker { get; }

        public double MerchantDailyOutputPerWorker { get; }

        public bool IsProduced => MaxWorkers > 0 && DailyOutputPerWorker > 0d;

        /// <summary>Units the workshop makes in a day with all its workers: it does not depend on the city's size.</summary>
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
