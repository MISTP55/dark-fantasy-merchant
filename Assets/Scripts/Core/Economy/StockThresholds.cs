using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The four stocks of a good in a city at which its price factors are given, in units of the good,
    /// from the lowest to the highest. Two of them can be equal.
    /// </summary>
    public readonly struct StockThresholds
    {
        public StockThresholds(double t0, double t1, double t2, double t3)
        {
            // The negated comparisons also reject NaN.
            if (!(t0 >= 0d) || !(t1 >= t0) || !(t2 >= t1) || !(t3 >= t2) || double.IsInfinity(t3))
            {
                throw new ArgumentOutOfRangeException(nameof(t0), "Thresholds are finite, not negative and in order.");
            }

            T0 = t0;
            T1 = t1;
            T2 = t2;
            T3 = t3;
        }

        public double T0 { get; }

        public double T1 { get; }

        public double T2 { get; }

        public double T3 { get; }
    }
}
