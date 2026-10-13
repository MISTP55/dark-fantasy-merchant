using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// How the food a city holds makes its population change. The rate follows the days
    /// of consumption in stock, in straight lines: the steepest decline at an empty
    /// stock, no change at the stable days, the fastest growth at the abundant days and above.
    /// </summary>
    public sealed class FoodGrowth
    {
        private readonly double stableDays;
        private readonly double maxMonthlyGrowth;
        private readonly double maxMonthlyDecline;

        /// <param name="stableDays">Days of food in stock at which the population does not change.</param>
        /// <param name="abundantDays">Days of food in stock at which it grows the fastest, the stable days or more.</param>
        /// <param name="maxMonthlyGrowth">Growth over a month at the abundant days, as a share: 0.05 is +5 %.</param>
        /// <param name="maxMonthlyDecline">Decline over a month at an empty stock, as a share from 0 to 1: 0.1 is -10 %.</param>
        public FoodGrowth(
            double stableDays,
            double abundantDays,
            double maxMonthlyGrowth,
            double maxMonthlyDecline,
            int daysPerMonth)
        {
            // The negated comparisons also reject NaN.
            if (!(stableDays >= 0d) || double.IsInfinity(stableDays))
            {
                throw new ArgumentOutOfRangeException(nameof(stableDays));
            }

            if (!(abundantDays >= stableDays) || double.IsInfinity(abundantDays))
            {
                throw new ArgumentOutOfRangeException(nameof(abundantDays));
            }

            if (!(maxMonthlyGrowth >= 0d) || double.IsInfinity(maxMonthlyGrowth))
            {
                throw new ArgumentOutOfRangeException(nameof(maxMonthlyGrowth));
            }

            if (!(maxMonthlyDecline >= 0d) || maxMonthlyDecline > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMonthlyDecline));
            }

            if (daysPerMonth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerMonth));
            }

            this.stableDays = stableDays;
            AbundantDays = abundantDays;
            this.maxMonthlyGrowth = maxMonthlyGrowth;
            this.maxMonthlyDecline = maxMonthlyDecline;
            DaysPerMonth = daysPerMonth;
        }

        /// <summary>The days of a food above which more of it changes nothing.</summary>
        public double AbundantDays { get; }

        public int DaysPerMonth { get; }

        /// <summary>The change of the population over a month, as a share of it, for the days of food in stock.</summary>
        public double MonthlyRateAt(double foodDays)
        {
            if (foodDays >= AbundantDays)
            {
                return maxMonthlyGrowth;
            }

            if (foodDays >= stableDays)
            {
                return maxMonthlyGrowth * (foodDays - stableDays) / (AbundantDays - stableDays);
            }

            // Under the stable days, which are therefore not 0. A stock is never negative.
            return -maxMonthlyDecline * (1d - Math.Max(0d, foodDays) / stableDays);
        }
    }
}
