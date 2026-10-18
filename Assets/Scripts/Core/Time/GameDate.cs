using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// A day of the game's calendar. Months and days are counted from 1. Holds no month
    /// name: names are content.
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>
    {
        public GameDate(int year, int month, int day)
        {
            Year = year;
            Month = month;
            Day = day;
        }

        public int Year { get; }

        public int Month { get; }

        public int Day { get; }

        /// <param name="elapsedDays">Whole days since the first day of <paramref name="startYear"/>.</param>
        public static GameDate FromElapsedDays(int elapsedDays, int startYear, int monthsPerYear, int daysPerMonth)
        {
            if (elapsedDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedDays));
            }

            if (monthsPerYear < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(monthsPerYear));
            }

            if (daysPerMonth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerMonth));
            }

            int daysPerYear = monthsPerYear * daysPerMonth;
            int dayOfYear = elapsedDays % daysPerYear;

            return new GameDate(
                startYear + elapsedDays / daysPerYear,
                dayOfYear / daysPerMonth + 1,
                dayOfYear % daysPerMonth + 1);
        }

        public bool Equals(GameDate other)
        {
            return Year == other.Year && Month == other.Month && Day == other.Day;
        }

        public override bool Equals(object obj)
        {
            return obj is GameDate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Year * 397 ^ Month) * 397 ^ Day;
        }

        public override string ToString()
        {
            return $"{Year}-{Month}-{Day}";
        }

        public static bool operator ==(GameDate left, GameDate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameDate left, GameDate right)
        {
            return !left.Equals(right);
        }
    }
}
