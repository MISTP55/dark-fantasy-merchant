namespace DarkFantasyMerchant.Core
{
    /// <summary>Where a stock stands against the thresholds of its good.</summary>
    public enum StockBand
    {
        BelowT0,
        T0ToT1,
        T1ToT2,
        T2ToT3,
        AboveT3,
    }

    public static class StockBands
    {
        /// <summary>The band of a stock; a stock at a threshold is in the band above it.</summary>
        public static StockBand Of(double stock, StockThresholds thresholds)
        {
            return Of(stock, thresholds.T0, thresholds.T1, thresholds.T2, thresholds.T3);
        }

        public static StockBand Of(double stock, double t0, double t1, double t2, double t3)
        {
            if (stock >= t3)
            {
                return StockBand.AboveT3;
            }

            if (stock >= t2)
            {
                return StockBand.T2ToT3;
            }

            if (stock >= t1)
            {
                return StockBand.T1ToT2;
            }

            return stock >= t0 ? StockBand.T0ToT1 : StockBand.BelowT0;
        }
    }
}
