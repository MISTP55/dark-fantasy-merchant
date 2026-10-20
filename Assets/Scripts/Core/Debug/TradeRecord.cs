namespace DarkFantasyMerchant.Core
{
    public enum Trader
    {
        AiMerchant,
        Player,
    }

    /// <summary>A purchase or a sale in a city's market, for the debug tools.</summary>
    public readonly struct TradeRecord
    {
        public TradeRecord(int day, int city, int good, Trader trader, Ship ship, PlayerTradeKind kind, int units, long gold, long? profit)
        {
            Day = day;
            City = city;
            Good = good;
            Trader = trader;
            Ship = ship;
            Kind = kind;
            Units = units;
            Gold = gold;
            Profit = profit;
        }

        public int Day { get; }

        /// <summary>The city's index in the record.</summary>
        public int City { get; }

        public int Good { get; }

        public Trader Trader { get; }

        /// <summary>The AI ship that traded; null for the player.</summary>
        public Ship Ship { get; }

        /// <summary>A purchase from the market or a sale to it; an AI merchant never builds.</summary>
        public PlayerTradeKind Kind { get; }

        public int Units { get; }

        public long Gold { get; }

        /// <summary>The profit of an AI sale; null otherwise.</summary>
        public long? Profit { get; }
    }
}
