using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>A good a workshop takes to make its own, and how much of it for every unit it makes.</summary>
    public readonly struct MarketInput
    {
        /// <param name="good">The input's index among the market's goods.</param>
        /// <param name="unitsPerUnit">Units of the input, in its own unit, for every unit made; above 0.</param>
        public MarketInput(int good, double unitsPerUnit)
        {
            if (good < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }

            // The negated comparison also rejects NaN.
            if (!(unitsPerUnit > 0d) || double.IsInfinity(unitsPerUnit))
            {
                throw new ArgumentOutOfRangeException(nameof(unitsPerUnit), "An input is above 0 and finite.");
            }

            Good = good;
            UnitsPerUnit = unitsPerUnit;
        }

        public int Good { get; }

        public double UnitsPerUnit { get; }
    }
}
