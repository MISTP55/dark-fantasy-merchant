namespace DarkFantasyMerchant.Core
{
    public enum SimulationEventType
    {
        /// <summary>The inhabitants did not get all they needed of a good.</summary>
        Shortage,

        /// <summary>The stock of a good moved into another band of its thresholds.</summary>
        ThresholdCrossing,
        Hired,
        LaidOff,

        /// <summary>A workshop stopped producing, its stock above T3.</summary>
        ProductionHalted,
        ProductionResumed,
        PoorLeft,
        ClassChange,
        BeggarsArrived,
        BeggarsLeft,
        CityHouseBuilt,
    }

    /// <summary>Something that happened in a city's day, for the debug tools.</summary>
    public readonly struct SimulationEvent
    {
        public SimulationEvent(int day, int city, int good, SimulationEventType type, double amount, int from = 0, int to = 0)
        {
            Day = day;
            City = city;
            Good = good;
            Type = type;
            Amount = amount;
            From = from;
            To = to;
        }

        public int Day { get; }

        /// <summary>The city's index in the record.</summary>
        public int City { get; }

        /// <summary>The good's index; -1 for an event of the whole city.</summary>
        public int Good { get; }

        public SimulationEventType Type { get; }

        /// <summary>
        /// The units missing, the workers hired or laid off, the poor who left, the inhabitants a class
        /// took (below 0: gave back), the beggars who came or left, or the houses built.
        /// </summary>
        public double Amount { get; }

        /// <summary>The <see cref="StockBand"/> a crossing left.</summary>
        public int From { get; }

        /// <summary>
        /// The <see cref="StockBand"/> a crossing entered, the <see cref="SocialClass"/> of a class
        /// change or of a house, or the workers the leaving poor took.
        /// </summary>
        public int To { get; }
    }
}
