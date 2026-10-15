using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Chooses what a merchant ship buys in the port it is in and where it sells it: the
    /// cargo and the destination that earn the most per day. Prices follow stocks, so
    /// that is a good in surplus here carried to a port that is short of it. Ports are
    /// indices into the markets it was given.
    /// </summary>
    public sealed class TradeRoutePlanner
    {
        // A trip and a stopover of no time would give a profit per day of infinity.
        private const double ShortestTripDays = 1e-6;

        private readonly IReadOnlyList<CityMarket> markets;
        private readonly double[,] travelDays;
        private readonly double stopoverDays;
        private readonly GoodUnits units;

        // Units of each good in the cargo being worked out.
        private readonly int[] loaded;

        /// <param name="travelDays">
        /// Days from a port to another, by index; infinity when the ship cannot sail there.
        /// Read on every plan, so it can still be filled in after the planner is made.
        /// </param>
        /// <param name="stopoverDays">Days a ship stays in a port, counted in the length of a trade.</param>
        /// <param name="units">The room a unit of each good takes in a hold. Without it, every unit takes one barrel.</param>
        public TradeRoutePlanner(
            IReadOnlyList<CityMarket> markets, double[,] travelDays, double stopoverDays, GoodUnits units = null)
        {
            this.markets = markets ?? throw new ArgumentNullException(nameof(markets));
            this.travelDays = travelDays ?? throw new ArgumentNullException(nameof(travelDays));

            if (travelDays.GetLength(0) != markets.Count || travelDays.GetLength(1) != markets.Count)
            {
                throw new ArgumentException("The travel days must have a row and a column per market.", nameof(travelDays));
            }

            // The negated comparison also rejects NaN.
            if (!(stopoverDays >= 0d) || double.IsInfinity(stopoverDays))
            {
                throw new ArgumentOutOfRangeException(nameof(stopoverDays));
            }

            this.stopoverDays = stopoverDays;
            this.units = units ?? GoodUnits.AllBarrels;

            // Markets made from different catalogues only trade the goods they all have.
            int goodCount = markets.Count > 0 ? int.MaxValue : 0;

            foreach (CityMarket market in markets)
            {
                if (market == null)
                {
                    throw new ArgumentException("A market is missing.", nameof(markets));
                }

                goodCount = Math.Min(goodCount, market.GoodCount);
            }

            GoodCount = goodCount;
            loaded = new int[goodCount];
        }

        public int PortCount => markets.Count;

        public int GoodCount { get; }

        /// <summary>The trade from a port that earns the most per day of trip and stopover.</summary>
        /// <param name="capacity">Barrels of room the ship has.</param>
        /// <param name="incoming">
        /// Units other ships are carrying to each port, by port then good: they count as
        /// already delivered there. Null for none.
        /// </param>
        /// <returns>False when no destination gives a profit.</returns>
        public bool TryPlanTrade(int from, int capacity, int[,] incoming, out TradePlan plan)
        {
            Require(from, capacity, incoming);

            plan = default;

            int bestDestination = -1;
            long bestProfit = 0;
            double bestScore = 0d;
            int[] bestUnits = null;

            for (int to = 0; to < PortCount; to++)
            {
                if (to == from || !IsReachable(from, to))
                {
                    continue;
                }

                long profit = Load(from, to, capacity, incoming);

                if (profit <= 0)
                {
                    continue;
                }

                double score = profit / TripDays(from, to);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDestination = to;
                    bestProfit = profit;

                    if (bestUnits == null)
                    {
                        bestUnits = new int[GoodCount];
                    }

                    Array.Copy(loaded, bestUnits, GoodCount);
                }
            }

            if (bestDestination < 0)
            {
                return false;
            }

            plan = new TradePlan(bestDestination, bestUnits, bestProfit, bestScore);
            return true;
        }

        /// <summary>
        /// Where to sail empty when there is nothing to trade from a port: the port from
        /// which the best trade starts, counting the days to get there.
        /// </summary>
        /// <param name="claimed">
        /// Ports other empty ships are already sailing to, by port: they are left to them,
        /// so that the ships with nothing to trade do not all go to the same one. Null for none.
        /// </param>
        /// <returns>False when no port the ship can reach has a trade that pays.</returns>
        public bool TryPlanRelocation(int from, int capacity, int[,] incoming, bool[] claimed, out int destination)
        {
            return TryPlanRelocation(from, capacity, incoming, claimed, out destination, out _);
        }

        /// <param name="profitPerDay">
        /// What the trade from that port earns per day, counting the days to sail there
        /// empty: to compare with the <see cref="TradePlan.ProfitPerDay"/> of a trade from here.
        /// </param>
        public bool TryPlanRelocation(
            int from, int capacity, int[,] incoming, bool[] claimed, out int destination, out double profitPerDay)
        {
            Require(from, capacity, incoming);

            if (claimed != null && claimed.Length < PortCount)
            {
                throw new ArgumentException("The claimed ports must have an entry per port.", nameof(claimed));
            }

            destination = -1;
            double bestScore = 0d;

            for (int source = 0; source < PortCount; source++)
            {
                if (source == from || !IsReachable(from, source) || (claimed != null && claimed[source]))
                {
                    continue;
                }

                if (!TryPlanTrade(source, capacity, incoming, out TradePlan trade))
                {
                    continue;
                }

                double score = trade.Profit / (TripDays(from, source) + TripDays(source, trade.Destination));

                if (score > bestScore)
                {
                    bestScore = score;
                    destination = source;
                }
            }

            profitPerDay = bestScore;
            return destination >= 0;
        }

        /// <summary>
        /// Fills <see cref="loaded"/> with the cargo that earns the most between two ports:
        /// unit by unit, the good whose next unit pays the most per barrel of room, among
        /// those that still fit, while one pays. The margin of a good only falls as it is
        /// loaded: with units of one size this is the best cargo, with bushels a close one.
        /// </summary>
        /// <returns>What the cargo earns.</returns>
        private long Load(int from, int to, int capacity, int[,] incoming)
        {
            CityMarket source = markets[from];
            CityMarket destination = markets[to];
            long profit = 0;
            int room = capacity;

            Array.Clear(loaded, 0, loaded.Length);

            while (true)
            {
                int bestGood = -1;
                long bestMargin = 0;
                double bestMarginPerBarrel = 0d;

                for (int good = 0; good < GoodCount; good++)
                {
                    int unitRoom = units.RoomOf(good);

                    if (unitRoom > room || loaded[good] >= source.AvailableOf(good))
                    {
                        continue;
                    }

                    int delivered = (incoming != null ? incoming[to, good] : 0) + loaded[good];
                    long margin = destination.SellPriceOf(good, delivered) - source.BuyPriceOf(good, loaded[good]);
                    double marginPerBarrel = (double)margin / unitRoom;

                    if (margin > 0 && marginPerBarrel > bestMarginPerBarrel)
                    {
                        bestMarginPerBarrel = marginPerBarrel;
                        bestMargin = margin;
                        bestGood = good;
                    }
                }

                if (bestGood < 0)
                {
                    break;
                }

                loaded[bestGood]++;
                room -= units.RoomOf(bestGood);
                profit += bestMargin;
            }

            return profit;
        }

        private double TripDays(int from, int to)
        {
            return Math.Max(ShortestTripDays, travelDays[from, to] + stopoverDays);
        }

        // False for NaN too.
        private bool IsReachable(int from, int to)
        {
            double days = travelDays[from, to];

            return days >= 0d && !double.IsInfinity(days);
        }

        private void Require(int from, int capacity, int[,] incoming)
        {
            if (from < 0 || from >= PortCount)
            {
                throw new ArgumentOutOfRangeException(nameof(from));
            }

            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (incoming != null && (incoming.GetLength(0) < PortCount || incoming.GetLength(1) < GoodCount))
            {
                throw new ArgumentException("The incoming units must have a row per port and a column per good.", nameof(incoming));
            }
        }
    }
}
