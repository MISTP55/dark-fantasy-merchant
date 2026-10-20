namespace DarkFantasyMerchant.Core
{
    /// <summary>Units of a good bought from a city's market or sold to it, for the flows of the debug tools.</summary>
    public readonly struct TradeRecord
    {
        public TradeRecord(int day, int city, int good, PlayerTradeKind kind, int units)
        {
            Day = day;
            City = city;
            Good = good;
            Kind = kind;
            Units = units;
        }

        public int Day { get; }

        /// <summary>The city's index in the record.</summary>
        public int City { get; }

        public int Good { get; }

        /// <summary>A purchase from the market, a sale to it, or materials of a building of the player.</summary>
        public PlayerTradeKind Kind { get; }

        public int Units { get; }
    }
}
