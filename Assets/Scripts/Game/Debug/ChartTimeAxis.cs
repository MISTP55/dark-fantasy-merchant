using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    public enum ChartTimeUnit
    {
        Day,
        Month,
        Year,
    }

    /// <summary>A mark of the time axis of a debug chart: a day counted from the start of the game, and its label.</summary>
    public readonly struct ChartTick
    {
        public ChartTick(int day, string label)
        {
            Day = day;
            Label = label;
        }

        public int Day { get; }

        public string Label { get; }
    }

    /// <summary>
    /// The marks of the time axis of the debug charts: in days for a chart of two months at most,
    /// in months up to three years, in years beyond; as many as fit, on round days.
    /// </summary>
    public static class ChartTimeAxis
    {
        private static readonly int[] DaySteps = { 1, 2, 5, 10, 15, 30 };
        private static readonly int[] MonthSteps = { 1, 2, 3, 6, 12 };
        private static readonly int[] YearSteps = { 1, 2, 5, 10, 20, 50, 100 };

        public static ChartTimeUnit UnitOf(int spanDays, int daysPerMonth, int monthsPerYear)
        {
            if (spanDays <= 2 * daysPerMonth)
            {
                return ChartTimeUnit.Day;
            }

            return spanDays <= 3 * daysPerMonth * monthsPerYear ? ChartTimeUnit.Month : ChartTimeUnit.Year;
        }

        /// <summary>The marks from one day to another, both included, at most <paramref name="maxTicks"/> of them and never none but for want of a round day.</summary>
        /// <param name="into">Cleared, then filled in the order of the days.</param>
        public static void Ticks(int firstDay, int lastDay, int maxTicks, ChartCalendar calendar, List<ChartTick> into)
        {
            if (calendar == null)
            {
                throw new ArgumentNullException(nameof(calendar));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();

            if (lastDay < firstDay)
            {
                return;
            }

            maxTicks = Math.Max(1, maxTicks);
            ChartTimeUnit unit = UnitOf(lastDay - firstDay, calendar.DaysPerMonth, calendar.MonthsPerYear);
            int[] steps = unit == ChartTimeUnit.Day ? DaySteps : unit == ChartTimeUnit.Month ? MonthSteps : YearSteps;
            int step = steps[steps.Length - 1];

            foreach (int candidate in steps)
            {
                if (Count(firstDay, lastDay, unit, candidate, calendar) <= maxTicks)
                {
                    step = candidate;
                    break;
                }
            }

            int count = Count(firstDay, lastDay, unit, step, calendar);

            // Still too many with the largest step: every other one, or fewer.
            int keepEvery = (count + maxTicks - 1) / maxTicks;
            int index = 0;

            for (int day = firstDay; day <= lastDay; day++)
            {
                if (IsTick(day, unit, step, calendar))
                {
                    if (index % keepEvery == 0)
                    {
                        into.Add(new ChartTick(day, LabelOf(day, unit, calendar)));
                    }

                    index++;
                }
            }
        }

        private static int Count(int firstDay, int lastDay, ChartTimeUnit unit, int step, ChartCalendar calendar)
        {
            int count = 0;

            for (int day = firstDay; day <= lastDay; day++)
            {
                if (IsTick(day, unit, step, calendar))
                {
                    count++;
                }
            }

            return count;
        }

        // Every step-th day of a month from its first, every step-th month from the first of the
        // game, every step-th year from the first.
        private static bool IsTick(int day, ChartTimeUnit unit, int step, ChartCalendar calendar)
        {
            switch (unit)
            {
                case ChartTimeUnit.Day:
                    return day % calendar.DaysPerMonth % step == 0;
                case ChartTimeUnit.Month:
                    return day % calendar.DaysPerMonth == 0 && day / calendar.DaysPerMonth % step == 0;
                default:
                    return day % calendar.DaysPerYear == 0 && day / calendar.DaysPerYear % step == 0;
            }
        }

        // "6 Févrin"; "Févrin", or "Janvis 933" for the first of a year; "933".
        private static string LabelOf(int day, ChartTimeUnit unit, ChartCalendar calendar)
        {
            GameDate date = calendar.DateOf(day);

            switch (unit)
            {
                case ChartTimeUnit.Day:
                    return $"{date.Day} {calendar.MonthName(date.Month)}";
                case ChartTimeUnit.Month:
                    return date.Month == 1 ? $"{calendar.MonthName(date.Month)} {date.Year}" : calendar.MonthName(date.Month);
                default:
                    return date.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
