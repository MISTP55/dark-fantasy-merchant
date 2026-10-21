using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// How many units of one good an AI ship carries from a source city to a destination city, from the
    /// stock thresholds of both: the source never gives below the middle of its T0 and T1, the
    /// destination never receives at or above its T1, and the rest is shared so that both
    /// end at the same relative position in their range. Rounding and the smallest lot are the planner's.
    /// </summary>
    public static class ShipmentQuantity
    {
        /// <summary>The smallest lot of a good counted in barrels, in units.</summary>
        public const int SmallestBarrelLot = 5;

        /// <summary>The smallest lot of a good counted in bushels, in units.</summary>
        public const int SmallestBushelLot = 3;

        /// <summary>
        /// The smallest lot of a good, given the room one of its units takes in a hold, in barrels:
        /// <see cref="SmallestBushelLot"/> when a unit takes more than one barrel, else <see cref="SmallestBarrelLot"/>.
        /// </summary>
        public static int SmallestLotFor(int unitRoom)
        {
            return unitRoom > 1 ? SmallestBushelLot : SmallestBarrelLot;
        }

        /// <summary>
        /// The units to carry from a source holding <paramref name="sourceStock"/> to a destination holding
        /// <paramref name="projectedStock"/>, not rounded, never negative and, for valid thresholds (<see cref="StockThresholds"/> rejects NaN and infinity), never NaN.
        /// </summary>
        public static double Of(double sourceStock, StockThresholds source, double projectedStock, StockThresholds destination)
        {
            double middleOfSource = (source.T0 + source.T1) / 2;

            // The negated comparisons also reject NaN.
            if (!(sourceStock > middleOfSource) || !(projectedStock < destination.T1))
            {
                return 0d;
            }

            double need = destination.T1 - projectedStock;
            if (sourceStock - source.T1 >= need)
            {
                return need;
            }

            // Step 1: the surplus above the source's T1.
            double surplus = Math.Max(0d, sourceStock - source.T1);

            // Step 2: up to the destination's target point, without going below the source's middle.
            double targetDestination = destination.T0 + 0.375 * (destination.T1 - destination.T0);
            double toTarget = Math.Max(0d, Math.Min(
                targetDestination - (projectedStock + surplus),
                (sourceStock - surplus) - middleOfSource));

            double sourceAfter = sourceStock - surplus - toTarget;
            double destinationAfter = projectedStock + surplus + toTarget;

            // Step 3: shared so that both end at the same relative position in their range.
            double sourceRange = source.T1 - middleOfSource;
            double destinationRange = destination.T1 - targetDestination;
            double shared = 0d;
            if (destinationAfter >= targetDestination && sourceAfter > middleOfSource && sourceRange + destinationRange > 0d)
            {
                shared = Math.Max(0d, ((sourceAfter - middleOfSource) * destinationRange
                    - (destinationAfter - targetDestination) * sourceRange) / (sourceRange + destinationRange));
            }

            return surplus + toTarget + shared;
        }
    }
}
