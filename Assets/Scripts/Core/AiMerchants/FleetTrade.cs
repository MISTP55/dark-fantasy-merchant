namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// A purchase or a sale an AI ship made in a market, for the debug tools: the units, the gold
    /// they cost or brought and, for a sale, its profit over what those units cost.
    /// </summary>
    public readonly struct FleetTrade
    {
        public FleetTrade(Ship ship, CityMarket market, int good, int units, long gold, bool isSale, long? profit)
        {
            Ship = ship;
            Market = market;
            Good = good;
            Units = units;
            Gold = gold;
            IsSale = isSale;
            Profit = profit;
        }

        public Ship Ship { get; }

        public CityMarket Market { get; }

        public int Good { get; }

        public int Units { get; }

        public long Gold { get; }

        public bool IsSale { get; }

        /// <summary>The sale's gold minus the share of the purchases of its good it sold; null for a purchase.</summary>
        public long? Profit { get; }
    }
}
