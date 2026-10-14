using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The factors of a good's base price at an empty stock and at the four thresholds of a
    /// city's stock, in straight lines between them and constant above the last. A curve never
    /// rises as the stock rises. Two equal thresholds make a step.
    /// </summary>
    public sealed class PriceCurve
    {
        public PriceCurve(double empty, double atT0, double atT1, double atT2, double atT3)
        {
            RequireFactor(empty, nameof(empty));
            RequireFactor(atT0, nameof(atT0));
            RequireFactor(atT1, nameof(atT1));
            RequireFactor(atT2, nameof(atT2));
            RequireFactor(atT3, nameof(atT3));

            if (atT0 > empty || atT1 > atT0 || atT2 > atT1 || atT3 > atT2)
            {
                throw new ArgumentOutOfRangeException(nameof(atT0), "A price curve never rises with the stock.");
            }

            Empty = empty;
            AtT0 = atT0;
            AtT1 = atT1;
            AtT2 = atT2;
            AtT3 = atT3;
        }

        /// <summary>The factor at an empty stock, and below.</summary>
        public double Empty { get; }

        public double AtT0 { get; }

        public double AtT1 { get; }

        public double AtT2 { get; }

        /// <summary>The factor at T3, and above.</summary>
        public double AtT3 { get; }

        /// <summary>
        /// The factor at a stock. A stock of 0 or below is an empty one; at a step, the factor
        /// is the one before it.
        /// </summary>
        public double FactorAt(double stock, StockThresholds thresholds)
        {
            if (stock <= 0d)
            {
                return Empty;
            }

            if (stock <= thresholds.T0)
            {
                return Interpolate(stock, 0d, Empty, thresholds.T0, AtT0);
            }

            if (stock <= thresholds.T1)
            {
                return Interpolate(stock, thresholds.T0, AtT0, thresholds.T1, AtT1);
            }

            if (stock <= thresholds.T2)
            {
                return Interpolate(stock, thresholds.T1, AtT1, thresholds.T2, AtT2);
            }

            if (stock <= thresholds.T3)
            {
                return Interpolate(stock, thresholds.T2, AtT2, thresholds.T3, AtT3);
            }

            return AtT3;
        }

        /// <summary>
        /// The average factor over a range of stock: the integral of the curve over it,
        /// divided by its width. Below an empty stock the factor is the empty one.
        /// </summary>
        public double AverageFactor(double from, double to, StockThresholds thresholds)
        {
            // The negated comparison also rejects NaN.
            if (!(to > from) || double.IsInfinity(from) || double.IsInfinity(to))
            {
                throw new ArgumentOutOfRangeException(nameof(to));
            }

            // Segment by segment rather than as a difference of integrals from 0: a range far
            // above T3 then gives the last factor exactly.
            double area = Overlap(from, to, double.NegativeInfinity, 0d) * Empty
                + AreaOfSegment(from, to, 0d, Empty, thresholds.T0, AtT0)
                + AreaOfSegment(from, to, thresholds.T0, AtT0, thresholds.T1, AtT1)
                + AreaOfSegment(from, to, thresholds.T1, AtT1, thresholds.T2, AtT2)
                + AreaOfSegment(from, to, thresholds.T2, AtT2, thresholds.T3, AtT3)
                + Overlap(from, to, thresholds.T3, double.PositiveInfinity) * AtT3;

            return area / (to - from);
        }

        private static double Overlap(double from, double to, double start, double end)
        {
            return Math.Max(0d, Math.Min(to, end) - Math.Max(from, start));
        }

        /// <summary>The area under one straight segment, over the part of the range it covers; none for a step.</summary>
        private static double AreaOfSegment(
            double from, double to, double start, double startFactor, double end, double endFactor)
        {
            double low = Math.Max(from, start);
            double high = Math.Min(to, end);

            if (!(high > low))
            {
                return 0d;
            }

            double lowFactor = Interpolate(low, start, startFactor, end, endFactor);
            double highFactor = Interpolate(high, start, startFactor, end, endFactor);

            return (high - low) * (lowFactor + highFactor) / 2d;
        }

        private static double Interpolate(double stock, double start, double startFactor, double end, double endFactor)
        {
            return startFactor + (endFactor - startFactor) * (stock - start) / (end - start);
        }

        private static void RequireFactor(double value, string name)
        {
            // The negated comparison also rejects NaN.
            if (!(value > 0d) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
