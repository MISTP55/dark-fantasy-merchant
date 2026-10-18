using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the world's calendar and of the pace of its time. Holds no
    /// runtime state: the current date is in a <see cref="GameClock"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Calendar", menuName = "Dark Fantasy Merchant/Calendar")]
    public sealed class CalendarDefinition : ScriptableObject
    {
        [Tooltip("Year of the first day of the game.")]
        [SerializeField] private int startYear = 932;

        [Tooltip("One name per month, in order. Their number is the number of months of a year.")]
        [SerializeField] private string[] monthNames =
        {
            "Janvis", "Févrin", "Marsis", "Avrilis", "Maïus", "Juinis",
            "Juillis", "Aoûtis", "Septem", "Octem", "Novem", "Décem",
        };

        [SerializeField, Min(1)] private int daysPerMonth = 30;

        [Tooltip("Days between two payments of the recurring expenses. Weeks are counted from the first day of the game, not within a month.")]
        [SerializeField, Min(1)] private int daysPerWeek = 7;

        [Tooltip("Real seconds a day lasts at normal speed.")]
        [SerializeField, Min(0.01f)] private float secondsPerDay = 30f;

        [Tooltip("How many times faster the world runs in fast forward.")]
        [SerializeField, Min(1f)] private float fastForwardMultiplier = 60f;

        public int StartYear => startYear;

        public int MonthsPerYear => monthNames != null ? monthNames.Length : 0;

        public int DaysPerMonth => daysPerMonth;

        public int DaysPerWeek => daysPerWeek;

        public float SecondsPerDay => secondsPerDay;

        public float FastForwardMultiplier => fastForwardMultiplier;

        /// <summary>
        /// A clock on the first day of the start year. Throws when the values of the asset
        /// are unusable: the Min attributes only constrain the Inspector, not the file.
        /// </summary>
        public GameClock CreateClock()
        {
            return new GameClock(secondsPerDay, fastForwardMultiplier, startYear, MonthsPerYear, daysPerMonth);
        }

        /// <param name="month">Month of the year, from 1.</param>
        /// <returns>The month's name, or its number when it has no usable name.</returns>
        public string MonthName(int month)
        {
            int index = month - 1;
            string monthName = index >= 0 && index < MonthsPerYear ? monthNames[index] : null;

            return string.IsNullOrWhiteSpace(monthName) ? $"Mois {month}" : monthName;
        }

        public string Format(GameDate date)
        {
            return $"{date.Day} {MonthName(date.Month)} {date.Year}";
        }
    }
}
