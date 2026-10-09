namespace DarkFantasyMerchant.Core
{
    /// <summary>What a purchase or a sale came to: it can be less than what was asked for.</summary>
    public readonly struct TradeResult
    {
        public TradeResult(int barrels, long gold)
        {
            Barrels = barrels;
            Gold = gold;
        }

        /// <summary>Barrels that changed hands; zero when nothing could be traded.</summary>
        public int Barrels { get; }

        /// <summary>Gold paid for a purchase, or received for a sale.</summary>
        public long Gold { get; }
    }
}
