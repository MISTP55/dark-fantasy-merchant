using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// How the four stock thresholds of a good in a city follow what the city consumes and
    /// produces of it, as in Patrician III: T0 is days of consumption, T1 adds days of
    /// consumption, T2 adds days of production, T3 adds T0 again. Each is at least its floor,
    /// in units of the good, so that a village or a good little consumed keeps a range of prices.
    /// </summary>
    public sealed class ThresholdRule
    {
        /// <param name="t0Days">Days of consumption up to T0.</param>
        /// <param name="t1AddedDays">Days of consumption from T0 to T1.</param>
        /// <param name="t2ProductionDays">Days of production from T1 to T2: none for a good the city does not produce.</param>
        /// <param name="t0Floor">The lowest T0, in units of the good, above 0.</param>
        /// <param name="t1Floor">The lowest T1, at least the T0 floor.</param>
        /// <param name="t2Floor">The lowest T2, at least the T1 floor.</param>
        /// <param name="t3Floor">The lowest T3, at least the T2 floor.</param>
        public ThresholdRule(
            double t0Days,
            double t1AddedDays,
            double t2ProductionDays,
            double t0Floor,
            double t1Floor,
            double t2Floor,
            double t3Floor)
        {
            RequireNotNegative(t0Days, nameof(t0Days));
            RequireNotNegative(t1AddedDays, nameof(t1AddedDays));
            RequireNotNegative(t2ProductionDays, nameof(t2ProductionDays));

            // The negated comparison also rejects NaN.
            if (!(t0Floor > 0d) || double.IsInfinity(t0Floor))
            {
                throw new ArgumentOutOfRangeException(nameof(t0Floor), "The floor of T0 is above 0 and finite.");
            }

            RequireFloor(t1Floor, nameof(t1Floor), "T1", t0Floor, "T0");
            RequireFloor(t2Floor, nameof(t2Floor), "T2", t1Floor, "T1");
            RequireFloor(t3Floor, nameof(t3Floor), "T3", t2Floor, "T2");

            T0Days = t0Days;
            T1AddedDays = t1AddedDays;
            T2ProductionDays = t2ProductionDays;
            T0Floor = t0Floor;
            T1Floor = t1Floor;
            T2Floor = t2Floor;
            T3Floor = t3Floor;
        }

        public double T0Days { get; }

        public double T1AddedDays { get; }

        public double T2ProductionDays { get; }

        public double T0Floor { get; }

        public double T1Floor { get; }

        public double T2Floor { get; }

        public double T3Floor { get; }

        /// <summary>The thresholds of a good the city consumes and produces at these daily figures, in units of the good.</summary>
        public StockThresholds ThresholdsFor(double dailyConsumption, double dailyProduction)
        {
            RequireNotNegative(dailyConsumption, nameof(dailyConsumption));
            RequireNotNegative(dailyProduction, nameof(dailyProduction));

            double t0 = Math.Max(T0Days * dailyConsumption, T0Floor);
            double t1 = Math.Max(t0 + T1AddedDays * dailyConsumption, T1Floor);
            double t2 = Math.Max(t1 + T2ProductionDays * dailyProduction, T2Floor);
            double t3 = Math.Max(t2 + t0, T3Floor);

            return new StockThresholds(t0, t1, t2, t3);
        }

        private static void RequireFloor(double floor, string name, string threshold, double previous, string previousThreshold)
        {
            // The negated comparison also rejects NaN.
            if (!(floor >= previous) || double.IsInfinity(floor))
            {
                throw new ArgumentOutOfRangeException(
                    name, $"The floor of {threshold} is finite and at least the floor of {previousThreshold}.");
            }
        }

        private static void RequireNotNegative(double value, string name)
        {
            // The negated comparison also rejects NaN.
            if (!(value >= 0d) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
