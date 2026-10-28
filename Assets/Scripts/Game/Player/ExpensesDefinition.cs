using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of what the player's recurring expenses cost. Holds no runtime
    /// state: what is owed and when is in a <see cref="WeeklyBilling"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Expenses", menuName = "Dark Fantasy Merchant/Expenses")]
    public sealed class ExpensesDefinition : ScriptableObject
    {
        [Tooltip("Gold coins one sailor is paid for a week.")]
        [SerializeField, Min(0)] private long sailorWeeklyWage = 10;

        [Tooltip("Share of the base value of what a player's workshop makes in a week, at its workers of the moment, that its workers are paid.")]
        [SerializeField, Min(0f)] private double workshopWageShare = 0.8d;

        public long SailorWeeklyWage => sailorWeeklyWage;

        public double WorkshopWageShare => workshopWageShare;

        /// <summary>
        /// The billing of these expenses to a treasury. Throws when the wage or the days
        /// of a week are unusable: the Min attributes only constrain the Inspector, not
        /// the file.
        /// </summary>
        /// <param name="sailorCount">The sailors aboard all the player's ships.</param>
        /// <param name="warehouseRent">The rent of the player's trading posts; null for none.</param>
        /// <param name="workshopRunningCosts">The running cost of the player's workshops; null for none.</param>
        /// <param name="workshopWages">The wages of the player's workshops; null for none.</param>
        public WeeklyBilling CreateBilling(
            Treasury treasury, int daysPerWeek, Func<int> sailorCount, Func<long> warehouseRent = null,
            Func<long> workshopRunningCosts = null, Func<long> workshopWages = null)
        {
            return new WeeklyBilling(
                treasury, daysPerWeek, sailorWeeklyWage, sailorCount, warehouseRent, workshopRunningCosts, workshopWages);
        }
    }
}
