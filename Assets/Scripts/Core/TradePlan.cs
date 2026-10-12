namespace DarkFantasyMerchant.Core
{
    /// <summary>A trade a ship can make from the port it is in: what to buy and where to sell it.</summary>
    public readonly struct TradePlan
    {
        private readonly int[] barrels;

        /// <param name="barrels">Barrels to buy of each good, by index. Kept, not copied.</param>
        public TradePlan(int destination, int[] barrels, long profit)
        {
            Destination = destination;
            this.barrels = barrels;
            Profit = profit;
        }

        /// <summary>Index of the port where the cargo is sold.</summary>
        public int Destination { get; }

        /// <summary>What the cargo is sold for minus what it costs, if no price changes meanwhile.</summary>
        public long Profit { get; }

        public int TotalBarrels
        {
            get
            {
                int total = 0;

                if (barrels != null)
                {
                    foreach (int count in barrels)
                    {
                        total += count;
                    }
                }

                return total;
            }
        }

        /// <returns>Barrels to buy of a good; none for a good the plan does not know.</returns>
        public int BarrelsOf(int good)
        {
            return barrels != null && good >= 0 && good < barrels.Length ? barrels[good] : 0;
        }
    }
}
