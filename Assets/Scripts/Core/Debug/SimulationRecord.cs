using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What the debug tools keep of the simulation: the figures of every city at the end of each
    /// day and the trades in their markets, both over the same horizon. It reads the markets and
    /// changes nothing of them.
    /// </summary>
    public sealed class SimulationRecord
    {
        private readonly IReadOnlyList<CityMarket> markets;
        private readonly Dictionary<CityMarket, int> cityIndices = new Dictionary<CityMarket, int>();
        private readonly TrimmedList<TradeRecord> trades = new TrimmedList<TradeRecord>();

        /// <param name="markets">The cities recorded, in the order of their indices.</param>
        /// <param name="horizonDays">Days kept, 1 or more; null for the whole game.</param>
        public SimulationRecord(IReadOnlyList<CityMarket> markets, int goodCount, int? horizonDays)
        {
            this.markets = markets ?? throw new ArgumentNullException(nameof(markets));
            History = new SimulationHistory(markets.Count, goodCount, horizonDays);

            for (int city = 0; city < markets.Count; city++)
            {
                cityIndices[markets[city] ?? throw new ArgumentNullException(nameof(markets))] = city;
            }
        }

        public SimulationHistory History { get; }

        /// <summary>The trades kept, oldest first.</summary>
        public IReadOnlyList<TradeRecord> Trades => trades;

        /// <summary>Raised after a day is recorded and after the horizon changes.</summary>
        public event Action Recorded;

        /// <summary>Records every city at the end of a day, after its <see cref="CityMarket.StartDay"/>.</summary>
        /// <param name="underWay">Units of a good the AI merchants carry to a city: (city, good).</param>
        public void RecordDay(int day, Func<int, int, int> underWay)
        {
            if (underWay == null)
            {
                throw new ArgumentNullException(nameof(underWay));
            }

            float[][] rows = History.BeginDay(day);

            for (int city = 0; city < markets.Count; city++)
            {
                int cityIndex = city;
                CitySnapshot.Capture(markets[city], good => underWay(cityIndex, good), rows[city]);
            }

            Trim();
            Recorded?.Invoke();
        }

        /// <summary>Records a purchase or a sale of an AI ship; nothing for a market not recorded.</summary>
        public void AddTrade(int day, CityMarket market, FleetTrade trade)
        {
            if (market != null && cityIndices.TryGetValue(market, out int city))
            {
                trades.Add(new TradeRecord(day, city, trade.Good, trade.IsSale ? PlayerTradeKind.Sell : PlayerTradeKind.Buy, trade.Units));
            }
        }

        /// <summary>Records a purchase, a sale or materials of a building of the player; nothing for a market not recorded.</summary>
        public void AddPlayerTrade(int day, CityMarket market, int good, int units, PlayerTradeKind kind)
        {
            if (market != null && cityIndices.TryGetValue(market, out int city))
            {
                trades.Add(new TradeRecord(day, city, good, kind, units));
            }
        }

        /// <param name="horizonDays">1 or more; null for the whole game.</param>
        public void SetHorizon(int? horizonDays)
        {
            History.SetHorizon(horizonDays);
            Trim();
            Recorded?.Invoke();
        }

        /// <summary>Units of a good sold into a city and bought from it, buildings' materials included, over the trades kept.</summary>
        public void FlowOf(int city, int good, out long received, out long shipped)
        {
            received = 0;
            shipped = 0;

            for (int i = 0; i < trades.Count; i++)
            {
                TradeRecord trade = trades[i];

                if (trade.City == city && trade.Good == good)
                {
                    AddFlow(trade, ref received, ref shipped);
                }
            }
        }

        /// <summary><see cref="FlowOf"/> for every city at once, in one pass over the trades kept.</summary>
        /// <param name="received">One entry per city, overwritten.</param>
        /// <param name="shipped">One entry per city, overwritten.</param>
        public void FlowsOf(int good, long[] received, long[] shipped)
        {
            if (received == null)
            {
                throw new ArgumentNullException(nameof(received));
            }

            if (shipped == null)
            {
                throw new ArgumentNullException(nameof(shipped));
            }

            Array.Clear(received, 0, received.Length);
            Array.Clear(shipped, 0, shipped.Length);

            for (int i = 0; i < trades.Count; i++)
            {
                TradeRecord trade = trades[i];

                if (trade.Good == good && trade.City < received.Length && trade.City < shipped.Length)
                {
                    AddFlow(trade, ref received[trade.City], ref shipped[trade.City]);
                }
            }
        }

        private static void AddFlow(TradeRecord trade, ref long received, ref long shipped)
        {
            if (trade.Kind == PlayerTradeKind.Sell)
            {
                received += trade.Units;
            }
            else
            {
                shipped += trade.Units;
            }
        }

        // Down to the oldest day the history keeps.
        private void Trim()
        {
            int firstDay = History.FirstDay;
            int forgotten = 0;

            while (forgotten < trades.Count && trades[forgotten].Day < firstDay)
            {
                forgotten++;
            }

            trades.RemoveFirst(forgotten);
        }
    }
}
