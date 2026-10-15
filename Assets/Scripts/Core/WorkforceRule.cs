using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// How the workshops of a city hire and lay off, as in Patrician III: every day a
    /// workshop hires more workers the lower the stock of its good stands against the
    /// thresholds, up to its maximum, and keeps a few workers above T3, where it produces
    /// nothing. A stock equal to a threshold counts as under it. It also spreads the
    /// workers every city starts with over its workshops.
    /// </summary>
    public sealed class WorkforceRule
    {
        /// <param name="hiredUpToT0">Workers a workshop hires in a day while the stock is at T0 or under.</param>
        /// <param name="hiredUpToT1">Workers hired in a day while the stock is above T0, up to T1.</param>
        /// <param name="hiredUpToT2">Workers hired in a day while the stock is above T1, up to T2.</param>
        /// <param name="hiredUpToT3">Workers hired in a day while the stock is above T2, up to T3.</param>
        /// <param name="workersAboveT3">Workers a workshop keeps above T3, where it produces nothing.</param>
        /// <param name="startingWorkforce">Workers every city starts with, spread evenly over its workshops.</param>
        public WorkforceRule(
            int hiredUpToT0,
            int hiredUpToT1,
            int hiredUpToT2,
            int hiredUpToT3,
            int workersAboveT3,
            int startingWorkforce)
        {
            RequireNotNegative(hiredUpToT0, nameof(hiredUpToT0));
            RequireNotNegative(hiredUpToT1, nameof(hiredUpToT1));
            RequireNotNegative(hiredUpToT2, nameof(hiredUpToT2));
            RequireNotNegative(hiredUpToT3, nameof(hiredUpToT3));
            RequireNotNegative(workersAboveT3, nameof(workersAboveT3));
            RequireNotNegative(startingWorkforce, nameof(startingWorkforce));

            HiredUpToT0 = hiredUpToT0;
            HiredUpToT1 = hiredUpToT1;
            HiredUpToT2 = hiredUpToT2;
            HiredUpToT3 = hiredUpToT3;
            WorkersAboveT3 = workersAboveT3;
            StartingWorkforce = startingWorkforce;
        }

        public int HiredUpToT0 { get; }

        public int HiredUpToT1 { get; }

        public int HiredUpToT2 { get; }

        public int HiredUpToT3 { get; }

        public int WorkersAboveT3 { get; }

        public int StartingWorkforce { get; }

        /// <summary>Whether a workshop produces at this stock: up to T3, not above.</summary>
        public static bool ProducesAt(double stock, StockThresholds thresholds)
        {
            return !(stock > thresholds.T3);
        }

        /// <summary>The workers of a workshop for the day, from the stock its good has left.</summary>
        public int NextWorkers(int workers, int maxWorkers, double stock, StockThresholds thresholds)
        {
            RequireNotNegative(workers, nameof(workers));
            RequireNotNegative(maxWorkers, nameof(maxWorkers));

            if (!ProducesAt(stock, thresholds))
            {
                return Math.Min(WorkersAboveT3, maxWorkers);
            }

            int hired = stock > thresholds.T2 ? HiredUpToT3
                : stock > thresholds.T1 ? HiredUpToT2
                : stock > thresholds.T0 ? HiredUpToT1
                : HiredUpToT0;

            return (int)Math.Min(maxWorkers, (long)workers + hired);
        }

        /// <summary>
        /// The workers a workshop starts with: the starting workforce over the city's
        /// workshops, rounded up as in Patrician III, and never above the workshop's maximum.
        /// </summary>
        public int StartingWorkersOf(int maxWorkers, int workshops)
        {
            RequireNotNegative(maxWorkers, nameof(maxWorkers));
            RequireNotNegative(workshops, nameof(workshops));

            if (workshops == 0)
            {
                return 0;
            }

            long share = ((long)StartingWorkforce + workshops - 1) / workshops;

            return (int)Math.Min(share, maxWorkers);
        }

        private static void RequireNotNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, "Workers are not negative.");
            }
        }
    }
}
