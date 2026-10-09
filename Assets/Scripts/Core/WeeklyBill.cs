using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What the player owes for one week, one amount per kind of recurring expense. So
    /// far the only one is the wages of the sailors.
    /// </summary>
    public readonly struct WeeklyBill
    {
        /// <param name="sailorCount">Sailors aboard all the player's ships.</param>
        /// <param name="sailorWeeklyWage">Gold coins one sailor is paid for a week.</param>
        public WeeklyBill(int sailorCount, long sailorWeeklyWage)
        {
            if (sailorCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sailorCount));
            }

            if (sailorWeeklyWage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sailorWeeklyWage));
            }

            SailorWages = sailorCount * sailorWeeklyWage;
        }

        public long SailorWages { get; }

        /// <summary>The sum of every expense of the week.</summary>
        public long Total => SailorWages;
    }
}
