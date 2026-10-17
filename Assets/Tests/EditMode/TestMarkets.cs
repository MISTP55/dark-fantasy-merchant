using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>The thresholds and prices of the markets the tests build.</summary>
    public static class TestMarkets
    {
        /// <summary>
        /// For a good the city does not produce: T0 at 10 days of consumption, T1 at 30, T2 at
        /// T1 and T3 at 40. A market that starts with 30 days of a good is at its threshold T1.
        /// </summary>
        public static ThresholdRule Thresholds => new ThresholdRule(10, 20, 10, 5, 10, 15, 20);

        /// <summary>
        /// Patrician III's hiring, and a starting workforce large enough that every workshop
        /// of a test market starts with its maximum of workers.
        /// </summary>
        public static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 1000);

        /// <summary>Patrician III's labour, as the economy's defaults.</summary>
        public static LabourRule Labour => new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 0.02, 30);

        /// <summary>The factors at which the player buys in Patrician III.</summary>
        public static PriceCurve BuyCurve => new PriceCurve(4, 1.5, 1, 0.8, 0.6);

        /// <summary>The factors at which the player sells in Patrician III, at the middle setting.</summary>
        public static PriceCurve SellCurve => new PriceCurve(2, 1.4, 1, 0.7, 0.5);

        public static MarketPricing Pricing => new MarketPricing(BuyCurve, SellCurve);
    }
}
