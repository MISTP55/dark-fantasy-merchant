using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What the player owes for one week, one amount per kind of recurring expense: the
    /// wages of the sailors, the rent of the trading posts above their capacity, and the
    /// running cost and the wages of the player's workshops.
    /// </summary>
    public readonly struct WeeklyBill
    {
        /// <param name="sailorCount">Sailors aboard all the player's ships.</param>
        /// <param name="sailorWeeklyWage">Gold coins one sailor is paid for a week.</param>
        /// <param name="warehouseRent">Gold coins the trading posts cost for the week.</param>
        /// <param name="workshopRunningCosts">Gold coins the player's workshops cost to run for the week.</param>
        /// <param name="workshopWages">Gold coins the workers of the player's workshops are paid for the week.</param>
        public WeeklyBill(
            int sailorCount, long sailorWeeklyWage, long warehouseRent = 0, long workshopRunningCosts = 0, long workshopWages = 0)
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

            if (workshopRunningCosts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workshopRunningCosts));
            }

            if (workshopWages < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workshopWages));
            }

            SailorWages = sailorCount * sailorWeeklyWage;
            WarehouseRent = warehouseRent;
            WorkshopRunningCosts = workshopRunningCosts;
            WorkshopWages = workshopWages;
        }

        public long SailorWages { get; }

        public long WarehouseRent { get; }

        public long WorkshopRunningCosts { get; }

        public long WorkshopWages { get; }

        /// <summary>The sum of every expense of the week.</summary>
        public long Total => SailorWages + WarehouseRent + WorkshopRunningCosts + WorkshopWages;
    }
}
