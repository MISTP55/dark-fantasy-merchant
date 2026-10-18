namespace DarkFantasyMerchant.Core
{
    /// <summary>What a purchase or a sale came to: it can be less than what was asked for.</summary>
    public readonly struct TradeResult
    {
        public TradeResult(int units, long gold)
        {
            Units = units;
            Gold = gold;
        }

        /// <summary>Units of the good that changed hands, barrels or bushels; zero when nothing could be traded.</summary>
        public int Units { get; }

        /// <summary>Gold paid for a purchase, or received for a sale.</summary>
        public long Gold { get; }
    }
}
