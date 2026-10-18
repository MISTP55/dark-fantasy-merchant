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

        public long SailorWeeklyWage => sailorWeeklyWage;

        /// <summary>
        /// The billing of these expenses to a treasury. Throws when the wage or the days
        /// of a week are unusable: the Min attributes only constrain the Inspector, not
        /// the file.
        /// </summary>
        /// <param name="sailorCount">The sailors aboard all the player's ships.</param>
        public WeeklyBilling CreateBilling(Treasury treasury, int daysPerWeek, Func<int> sailorCount)
        {
            return new WeeklyBilling(treasury, daysPerWeek, sailorWeeklyWage, sailorCount);
        }
    }
}
