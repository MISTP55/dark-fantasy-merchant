namespace DarkFantasyMerchant.Core
{
    /// <summary>A trade a ship can make from the port it is in: what to buy and where to sell it.</summary>
    public readonly struct TradePlan
    {
        private readonly int[] units;

        /// <param name="units">Units to buy of each good, by index. Kept, not copied.</param>
        /// <param name="profitPerDay">The profit over the days of the trip and of the stopover.</param>
        public TradePlan(int destination, int[] units, long profit, double profitPerDay)
        {
            Destination = destination;
            this.units = units;
            Profit = profit;
            ProfitPerDay = profitPerDay;
        }

        /// <summary>Index of the port where the cargo is sold.</summary>
        public int Destination { get; }

        /// <summary>What the cargo is sold for minus what it costs, if no price changes meanwhile.</summary>
        public long Profit { get; }

        /// <summary>What the trade earns per day it takes: what plans are compared by.</summary>
        public double ProfitPerDay { get; }

        /// <summary>Units to buy, all goods together, barrels and bushels alike.</summary>
        public int TotalUnits
        {
            get
            {
                int total = 0;

                if (units != null)
                {
                    foreach (int count in units)
                    {
                        total += count;
                    }
                }

                return total;
            }
        }

        /// <returns>Units to buy of a good, barrels or bushels; none for a good the plan does not know.</returns>
        public int UnitsOf(int good)
        {
            return units != null && good >= 0 && good < units.Length ? units[good] : 0;
        }
    }
}
