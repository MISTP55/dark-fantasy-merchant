namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// A purchase or a sale an AI ship made in a market, for the debug tools: the units, and the
    /// gold they cost or brought.
    /// </summary>
    public readonly struct FleetTrade
    {
        public FleetTrade(Ship ship, CityMarket market, int good, int units, long gold, bool isSale)
        {
            Ship = ship;
            Market = market;
            Good = good;
            Units = units;
            Gold = gold;
            IsSale = isSale;
        }

        public Ship Ship { get; }

        public CityMarket Market { get; }

        public int Good { get; }

        public int Units { get; }

        public long Gold { get; }

        public bool IsSale { get; }
    }
}
