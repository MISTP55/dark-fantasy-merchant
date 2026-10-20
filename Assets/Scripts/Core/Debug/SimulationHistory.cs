using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of every city at the end of each day, for the debug tools, kept over a horizon of
    /// days or over the whole game: a day beyond the horizon is forgotten, never the last one.
    /// Days are counted from the start of the game and recorded in order.
    /// </summary>
    public sealed class SimulationHistory
    {
        private readonly int cityCount;
        private readonly int rowSize;
        private readonly List<int> days = new List<int>();
        private readonly List<float[][]> rows = new List<float[][]>();

        // The rows of forgotten days, made again for the next ones rather than left to the collector.
        private readonly Stack<float[][]> spare = new Stack<float[][]>();

        /// <param name="horizonDays">Days kept, 1 or more; null for the whole game.</param>
        public SimulationHistory(int cityCount, int goodCount, int? horizonDays)
        {
            if (cityCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cityCount));
            }

            if (goodCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goodCount));
            }

            RequireHorizon(horizonDays);
            this.cityCount = cityCount;
            rowSize = CitySnapshot.SizeFor(goodCount);
            HorizonDays = horizonDays;
        }

        /// <summary>Days kept; null for the whole game.</summary>
        public int? HorizonDays { get; private set; }

        public int DayCount => days.Count;

        /// <summary>The oldest day kept, or -1 before the first one.</summary>
        public int FirstDay => days.Count > 0 ? days[0] : -1;

        /// <summary>The last day recorded, or -1 before the first one.</summary>
        public int LastDay => days.Count > 0 ? days[days.Count - 1] : -1;

        /// <param name="index">From 0, the oldest day kept, to <see cref="DayCount"/> - 1.</param>
        public int DayAt(int index)
        {
            return days[index];
        }

        /// <summary>The row of a city on a day kept, for the debug tools that read many figures of it.</summary>
        public float[] RowOf(int index, int city)
        {
            return rows[index][city];
        }

        public float ValueOf(int index, int city, CityIndicator indicator)
        {
            return rows[index][city][CitySnapshot.IndexOf(indicator)];
        }

        public float ValueOf(int index, int city, GoodIndicator indicator, int good)
        {
            return rows[index][city][CitySnapshot.IndexOf(indicator, good)];
        }

        /// <summary>
        /// Opens a day after the last one and forgets those beyond the horizon.
        /// </summary>
        /// <returns>One row per city to fill, of zeros.</returns>
        public float[][] BeginDay(int day)
        {
            if (days.Count > 0 && day <= LastDay)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "A day is recorded after the last one.");
            }

            float[][] dayRows = spare.Count > 0 ? spare.Pop() : NewRows();

            foreach (float[] row in dayRows)
            {
                Array.Clear(row, 0, row.Length);
            }

            days.Add(day);
            rows.Add(dayRows);
            Trim();
            return dayRows;
        }

        /// <summary>Changes the days kept, forgetting at once those beyond a shorter horizon.</summary>
        /// <param name="horizonDays">1 or more; null for the whole game.</param>
        public void SetHorizon(int? horizonDays)
        {
            RequireHorizon(horizonDays);
            HorizonDays = horizonDays;
            Trim();
        }

        private static void RequireHorizon(int? horizonDays)
        {
            if (horizonDays.HasValue && horizonDays.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(horizonDays));
            }
        }

        private float[][] NewRows()
        {
            var dayRows = new float[cityCount][];

            for (int city = 0; city < cityCount; city++)
            {
                dayRows[city] = new float[rowSize];
            }

            return dayRows;
        }

        // The days after the last one minus the horizon.
        private void Trim()
        {
            if (!HorizonDays.HasValue || days.Count == 0)
            {
                return;
            }

            int oldestKept = LastDay - HorizonDays.Value + 1;
            int forgotten = 0;

            while (forgotten < days.Count - 1 && days[forgotten] < oldestKept)
            {
                spare.Push(rows[forgotten]);
                forgotten++;
            }

            days.RemoveRange(0, forgotten);
            rows.RemoveRange(0, forgotten);
        }
    }
}
