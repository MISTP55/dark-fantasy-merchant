using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What a city actually produced and consumed of each good on each of the last days,
    /// a week of them at most: the oldest day is forgotten when a new one starts. What its
    /// inhabitants consumed and what its workshops took as inputs are kept apart.
    /// </summary>
    public sealed class MarketWeekLog
    {
        // Indexed by day, then by good. The days are a ring: the newest is at currentDay.
        private readonly double[][] produced;
        private readonly double[][] consumed;
        private readonly double[][] inputs;
        private readonly int goodCount;

        private int currentDay = -1;

        /// <param name="days">Days the log remembers: those of a week.</param>
        public MarketWeekLog(int goodCount, int days)
        {
            if (goodCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goodCount));
            }

            if (days < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(days));
            }

            this.goodCount = goodCount;
            produced = new double[days][];
            consumed = new double[days][];
            inputs = new double[days][];

            for (int day = 0; day < days; day++)
            {
                produced[day] = new double[goodCount];
                consumed[day] = new double[goodCount];
                inputs[day] = new double[goodCount];
            }
        }

        /// <summary>Opens a day with nothing produced or consumed, in the place of the oldest one.</summary>
        public void StartDay()
        {
            currentDay = (currentDay + 1) % produced.Length;
            Array.Clear(produced[currentDay], 0, goodCount);
            Array.Clear(consumed[currentDay], 0, goodCount);
            Array.Clear(inputs[currentDay], 0, goodCount);
        }

        /// <summary>Writes what the day that last started produced and consumed of a good.</summary>
        public void Record(int good, double producedUnits, double consumedUnits)
        {
            RequireGood(good);
            RequireDay();

            produced[currentDay][good] = producedUnits;
            consumed[currentDay][good] = consumedUnits;
        }

        /// <summary>Adds units of a good that a workshop took as its input in the day that last started.</summary>
        public void AddInputs(int good, double units)
        {
            RequireGood(good);
            RequireDay();

            inputs[currentDay][good] += units;
        }

        /// <summary>Units produced over the days remembered.</summary>
        public double ProducedOf(int good)
        {
            return Sum(produced, good);
        }

        /// <summary>Units consumed over the days remembered.</summary>
        public double ConsumedOf(int good)
        {
            return Sum(consumed, good);
        }

        /// <summary>Units the workshops took as inputs over the days remembered.</summary>
        public double InputsOf(int good)
        {
            return Sum(inputs, good);
        }

        // Summed on demand, not kept up to date: a running total would drift as days leave it.
        private double Sum(double[][] days, int good)
        {
            RequireGood(good);

            double total = 0d;

            foreach (double[] day in days)
            {
                total += day[good];
            }

            return total;
        }

        private void RequireDay()
        {
            if (currentDay < 0)
            {
                throw new InvalidOperationException("No day has started.");
            }
        }

        private void RequireGood(int good)
        {
            if (good < 0 || good >= goodCount)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }
        }
    }
}
