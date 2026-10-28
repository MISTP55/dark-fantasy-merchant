using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What the player owes for one week, one amount per kind of recurring expense: the
    /// wages of the sailors and the rent of the trading posts above their capacity.
    /// </summary>
    public readonly struct WeeklyBill
    {
        /// <param name="sailorCount">Sailors aboard all the player's ships.</param>
        /// <param name="sailorWeeklyWage">Gold coins one sailor is paid for a week.</param>
        /// <param name="warehouseRent">Gold coins the trading posts cost for the week.</param>
        public WeeklyBill(int sailorCount, long sailorWeeklyWage, long warehouseRent = 0)
        {
            if (sailorCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sailorCount));
            }

            if (sailorWeeklyWage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sailorWeeklyWage));
            }

            if (warehouseRent < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warehouseRent));
            }

            SailorWages = sailorCount * sailorWeeklyWage;
            WarehouseRent = warehouseRent;
        }

        public long SailorWages { get; }

        public long WarehouseRent { get; }

        /// <summary>The sum of every expense of the week.</summary>
        public long Total => SailorWages + WarehouseRent;
    }
}
