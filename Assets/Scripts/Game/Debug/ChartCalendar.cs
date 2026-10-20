using System;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The calendar of the debug charts: the dates of the days counted from the start of the game.</summary>
    public sealed class ChartCalendar
    {
        private readonly Func<int, string> monthName;

        /// <param name="monthName">The name of a month of the year, from 1.</param>
        public ChartCalendar(int startYear, int monthsPerYear, int daysPerMonth, Func<int, string> monthName)
        {
            if (monthsPerYear < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(monthsPerYear));
            }

            if (daysPerMonth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerMonth));
            }

            StartYear = startYear;
            MonthsPerYear = monthsPerYear;
            DaysPerMonth = daysPerMonth;
            this.monthName = monthName ?? throw new ArgumentNullException(nameof(monthName));
        }

        public int StartYear { get; }

        public int MonthsPerYear { get; }

        public int DaysPerMonth { get; }

        public int DaysPerYear => MonthsPerYear * DaysPerMonth;

        public GameDate DateOf(int day)
        {
            return GameDate.FromElapsedDays(day, StartYear, MonthsPerYear, DaysPerMonth);
        }

        public string MonthName(int month)
        {
            return monthName(month);
        }

        /// <summary>As the calendar writes a date: "6 Févrin 933".</summary>
        public string FormatDay(int day)
        {
            GameDate date = DateOf(day);

            return $"{date.Day} {MonthName(date.Month)} {date.Year}";
        }
    }
}
