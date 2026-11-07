using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of one good in one city: its base price, what the housed inhabitants consume
    /// of it, and the workshop that produces it there, if any, with the inputs it takes.
    /// </summary>
    public readonly struct MarketGood
    {
        // Null for a good without recipe, as the default good has.
        private readonly MarketInput[] inputs;

        /// <param name="basePrice">Gold per unit that the factors of the price curves multiply, above 0; it can be a fraction.</param>
        /// <param name="dailyConsumptionPerThousand">Units of the good 1 000 housed inhabitants consume in a day, the beggars consuming nothing; it can be a fraction.</param>
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
            inputs = null;
        }

        /// <summary>What the workshop takes for every unit it makes; empty for a good that needs nothing.</summary>
        public IReadOnlyList<MarketInput> Inputs => inputs ?? Array.Empty<MarketInput>();

        /// <summary>The same good, whose workshop takes these inputs; null or empty for none. A good is listed once at most.</summary>
        public MarketGood WithInputs(IReadOnlyList<MarketInput> recipe)
        {
            var copy = new MarketInput[recipe?.Count ?? 0];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = recipe[i];

                for (int j = 0; j < i; j++)
                {
                    if (copy[j].Good == copy[i].Good)
                    {
                        throw new ArgumentException($"The good {copy[i].Good} is listed twice in the recipe.", nameof(recipe));
                    }
                }
            }

            return new MarketGood(this, copy);
        }

        private MarketGood(MarketGood good, MarketInput[] inputs)
        {
            BasePrice = good.BasePrice;
            DailyConsumptionPerThousand = good.DailyConsumptionPerThousand;
            MaxWorkers = good.MaxWorkers;
            DailyOutputPerWorker = good.DailyOutputPerWorker;
            MerchantDailyOutputPerWorker = good.MerchantDailyOutputPerWorker;
            this.inputs = inputs;
        }

        public double BasePrice { get; }

        /// <summary>Units 1 000 housed inhabitants consume in a day.</summary>
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
