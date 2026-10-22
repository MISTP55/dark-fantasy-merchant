using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What the inhabitants of a city needed of each good in its last day, and what they consumed,
    /// and what its workshops needed and took of it as inputs, for the debug tools. Filled by <see cref="CityMarket.StartDay"/>, which it changes nothing
    /// of; all zeros before the first day.
    /// </summary>
    public sealed class CityDayReport
    {
        private readonly double[] needed;
        private readonly double[] consumed;
        private readonly double[] inputNeeded;
        private readonly double[] inputsConsumed;

        public CityDayReport(int goodCount)
        {
            if (goodCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goodCount));
            }

            needed = new double[goodCount];
            consumed = new double[goodCount];
            inputNeeded = new double[goodCount];
            inputsConsumed = new double[goodCount];
        }

        /// <summary>Units the inhabitants needed in the day.</summary>
        public double NeededOf(int good)
        {
            return needed[good];
        }

        /// <summary>Units the inhabitants consumed: less than they needed when the stock ran out.</summary>
        public double ConsumedOf(int good)
        {
            return consumed[good];
        }

        /// <summary>Units the inhabitants needed and did not get.</summary>
        public double MissingOf(int good)
        {
            return Math.Max(0d, needed[good] - consumed[good]);
        }

        /// <summary>Units the workshops needed as their inputs in the day, with no shortage.</summary>
        public double InputNeedOf(int good)
        {
            return inputNeeded[good];
        }

        /// <summary>Units the workshops took as their inputs: less than they needed when the stock was short.</summary>
        public double InputsConsumedOf(int good)
        {
            return inputsConsumed[good];
        }

        internal void Clear()
        {
            Array.Clear(needed, 0, needed.Length);
            Array.Clear(consumed, 0, consumed.Length);
            Array.Clear(inputNeeded, 0, inputNeeded.Length);
            Array.Clear(inputsConsumed, 0, inputsConsumed.Length);
        }

        internal void RecordConsumption(int good, double neededUnits, double consumedUnits)
        {
            needed[good] = neededUnits;
            consumed[good] = consumedUnits;
        }

        // Added up: several workshops can take the same good.
        internal void AddInputs(int good, double neededUnits, double consumedUnits)
        {
            inputNeeded[good] += neededUnits;
            inputsConsumed[good] += consumedUnits;
        }
    }
}
