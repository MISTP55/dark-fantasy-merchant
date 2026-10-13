using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The inhabitants of one city. Their number changes a little every day, so it is
    /// not whole; what is shown is the nearest whole number.
    /// </summary>
    public sealed class CityPopulation
    {
        public CityPopulation(double count)
        {
            // The negated comparison also rejects NaN.
            if (!(count >= 0d) || double.IsInfinity(count))
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Count = count;
        }

        public double Count { get; private set; }

        /// <summary>The inhabitants as they are shown.</summary>
        public int Inhabitants => (int)Math.Min(int.MaxValue, Math.Round(Count, MidpointRounding.AwayFromZero));

        /// <summary>
        /// One day at a monthly rate: the days of a month at the same rate change the
        /// population by exactly that rate.
        /// </summary>
        /// <param name="monthlyRate">The change over a month as a share of the population: 0.05 is +5 %. Not under -1.</param>
        public void GrowForADay(double monthlyRate, int daysPerMonth)
        {
            // The negated comparison also rejects NaN.
            if (!(monthlyRate >= -1d) || double.IsInfinity(monthlyRate))
            {
                throw new ArgumentOutOfRangeException(nameof(monthlyRate));
            }

            if (daysPerMonth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerMonth));
            }

            Count *= Math.Pow(1d + monthlyRate, 1d / daysPerMonth);
        }
    }
}
