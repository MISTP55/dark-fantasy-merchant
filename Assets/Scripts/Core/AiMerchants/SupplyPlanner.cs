using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Plans how the AI merchants supply the ports: a good is carried from a port where it is
    /// plentiful to one where it will be short, by the quantity <see cref="ShipmentQuantity"/>
    /// gives and for the gap of the factors of the two prices times the weight of the good. From that
    /// it chooses a ship's final destination from its home, a stopover on its way back, and a cargo.
    /// The length of a trip divides nothing. Ports are indices into the markets it was given, goods
    /// are indices into the catalogue.
    /// </summary>
    public sealed class SupplyPlanner
    {
        /// <summary>A stopover may lengthen the way back to less than this multiple of the direct trip.</summary>
        public const double DetourLimit = 1.2;

        // The margin CityMarket uses: days of fractional consumption leave a whole quantity a hair short.
        private const double WholeUnitMargin = 1e-9;

        private readonly IReadOnlyList<CityMarket> markets;
        private readonly double[,] travelDays;
        private readonly GoodUnits goodUnits;
        private readonly int[] weights;

        // Reused by Load: the score and quantity of each good, and the goods in order of loading.
        private readonly double[] scores;
        private readonly int[] quantities;
        private readonly int[] order;

        /// <param name="travelDays">
        /// Days from a port to another, by index; infinity when the ship cannot sail there.
        /// Read on every call, so it can still be filled in after the planner is made.
        /// </param>
        /// <param name="units">The room a unit of each good takes in a hold. Without it, every unit takes one barrel.</param>
        /// <param name="weights">
        /// How much each good counts, by good; 0 for a good never carried. A good beyond the list,
        /// or every good without it, weighs 1.
        /// </param>
        public SupplyPlanner(
            IReadOnlyList<CityMarket> markets,
            double[,] travelDays,
            GoodUnits units = null,
            IReadOnlyList<int> weights = null)
        {
            this.markets = markets ?? throw new ArgumentNullException(nameof(markets));
            this.travelDays = travelDays ?? throw new ArgumentNullException(nameof(travelDays));

            if (travelDays.GetLength(0) != markets.Count || travelDays.GetLength(1) != markets.Count)
            {
                throw new ArgumentException("The travel days must have a row and a column per market.", nameof(travelDays));
            }

            goodUnits = units ?? GoodUnits.AllBarrels;

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
            this.weights = new int[goodCount];

            for (int good = 0; good < goodCount; good++)
            {
                int weight = weights != null && good < weights.Count ? weights[good] : 1;

                if (weight < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(weights), "A weight is not negative.");
                }

                this.weights[good] = weight;
            }

            scores = new double[goodCount];
            quantities = new int[goodCount];
            order = new int[goodCount];
        }

        public int PortCount => markets.Count;

        public int GoodCount { get; }

        /// <summary>Whether a ship can sail from a port to another: a finite days count, not negative.</summary>
        public bool IsReachable(int from, int to)
        {
            RequirePort(from, nameof(from));
            RequirePort(to, nameof(to));

            return Reachable(from, to);
        }

        /// <summary>
        /// The stock a port will hold after the days of a trip: what it has, what is under way to it,
        /// and what it will produce less what it will consume, never below nothing.
        /// </summary>
        public double ProjectedStockOf(int port, int good, double travelDays, int underWay)
        {
            RequirePort(port, nameof(port));
            RequireGood(good);

            CityMarket market = markets[port];
            double net = market.DailyProductionOf(good) - market.DailyConsumptionOf(good);

            return Math.Max(0d, market.StockOf(good) + underWay + net * travelDays);
        }

        /// <summary>
        /// Whether a good is a transport from a port to another, and if so how many units and what
        /// it scores: the factor the destination sells at once the trip is over, less the factor the
        /// source buys at, times the weight. Not a transport when it is the same port, the destination
        /// cannot be reached, the weight is 0, the whole quantity is under the smallest lot or the score
        /// is not above 0.
        /// </summary>
        /// <param name="underWay">Units other ships are carrying to each port, by port then good. Null for none.</param>
        public bool TryEvaluate(int from, int to, int good, int[,] underWay, out int units, out double score)
        {
            RequirePort(from, nameof(from));
            RequirePort(to, nameof(to));
            RequireGood(good);
            RequireUnderWay(underWay);

            return Evaluate(from, to, good, underWay, out units, out score);
        }

        /// <summary>
        /// The port a ship from <paramref name="home"/> sails to: the one where what it can carry there
        /// or bring back scores the most, -1 when there is none. For each port, the goods home can send
        /// there count, or else the goods it can send back.
        /// </summary>
        public int ChooseDestination(int home, int[,] underWay)
        {
            RequirePort(home, nameof(home));
            RequireUnderWay(underWay);

            int best = -1;
            double bestTotal = 0d;

            for (int port = 0; port < PortCount; port++)
            {
                if (port == home || !Reachable(home, port))
                {
                    continue;
                }

                double total = 0d;

                for (int good = 0; good < GoodCount; good++)
                {
                    if (Evaluate(home, port, good, underWay, out _, out double score)
                        || Evaluate(port, home, good, underWay, out _, out score))
                    {
                        total += score;
                    }
                }

                if (total > bestTotal)
                {
                    bestTotal = total;
                    best = port;
                }
            }

            return best;
        }

        /// <summary>
        /// The port where a ship on its way from <paramref name="port"/> back to <paramref name="home"/>
        /// stops, -1 when there is none: one that lengthens the trip to less than <see cref="DetourLimit"/>
        /// times the direct one and where the goods it carries there score the most; on a tie, the
        /// shortest detour, then the lowest index.
        /// </summary>
        /// <param name="excluded">Ports that are no stopover, by port. Null for none.</param>
        public int ChooseStopover(int port, int home, int[,] underWay, IReadOnlyList<bool> excluded)
        {
            RequirePort(port, nameof(port));
            RequirePort(home, nameof(home));
            RequireUnderWay(underWay);

            if (excluded != null && excluded.Count < PortCount)
            {
                throw new ArgumentException("The excluded ports must have an entry per port.", nameof(excluded));
            }

            int best = -1;
            double bestValue = 0d;
            double bestDetour = 0d;

            for (int candidate = 0; candidate < PortCount; candidate++)
            {
                if (candidate == port || candidate == home || (excluded != null && excluded[candidate])
                    || !Reachable(port, candidate) || !Reachable(candidate, home) || !Reachable(port, home))
                {
                    continue;
                }

                double detour = travelDays[port, candidate] + travelDays[candidate, home];

                // The negated comparison also rejects NaN.
                if (!(detour < DetourLimit * travelDays[port, home]))
                {
                    continue;
                }

                double value = 0d;

                for (int good = 0; good < GoodCount; good++)
                {
                    if (Evaluate(port, candidate, good, underWay, out _, out double score))
                    {
                        value += score;
                    }
                }

                if (value > bestValue || (value > 0d && value == bestValue && detour < bestDetour))
                {
                    bestValue = value;
                    bestDetour = detour;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Loads a ship in <paramref name="from"/> for <paramref name="to"/>: every good that is a transport,
        /// by decreasing score (the lowest index first on a tie), each as many units as the quantity,
        /// the stock and the room allow. Once a good is chosen, any whole unit that fits is taken, even
        /// under the smallest lot.
        /// </summary>
        /// <param name="room">Barrels of room the ship has.</param>
        /// <param name="units">Receives the units of each good, cleared first; at least <see cref="GoodCount"/> long.</param>
        /// <returns>The barrels of room the cargo takes.</returns>
        public int Load(int from, int to, int room, int[,] underWay, int[] units)
        {
            RequirePort(from, nameof(from));
            RequirePort(to, nameof(to));

            if (room < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(room));
            }

            RequireUnderWay(underWay);

            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            if (units.Length < GoodCount)
            {
                throw new ArgumentException("The units must have an entry per good.", nameof(units));
            }

            Array.Clear(units, 0, units.Length);

            // The goods that are a transport, put in order by insertion.
            int count = 0;

            for (int good = 0; good < GoodCount; good++)
            {
                if (!Evaluate(from, to, good, underWay, out int quantity, out double score))
                {
                    continue;
                }

                scores[good] = score;
                quantities[good] = quantity;

                int position = count++;

                while (position > 0 && scores[order[position - 1]] < score)
                {
                    order[position] = order[position - 1];
                    position--;
                }

                order[position] = good;
            }

            CityMarket source = markets[from];
            int taken = 0;

            for (int i = 0; i < count; i++)
            {
                int good = order[i];
                int unitRoom = goodUnits.RoomOf(good);
                int loaded = Math.Min(quantities[good], Math.Min(source.AvailableOf(good), (room - taken) / unitRoom));

                if (loaded > 0)
                {
                    units[good] = loaded;
                    taken += loaded * unitRoom;
                }
            }

            return taken;
        }

        private bool Evaluate(int from, int to, int good, int[,] underWay, out int units, out double score)
        {
            units = 0;
            score = 0d;

            if (from == to || !Reachable(from, to) || weights[good] == 0)
            {
                return false;
            }

            CityMarket source = markets[from];
            CityMarket destination = markets[to];
            double sourceStock = source.StockOf(good);
            double projected = ProjectedStockOf(to, good, travelDays[from, to], underWay != null ? underWay[to, good] : 0);

            double quantity = Math.Floor(
                ShipmentQuantity.Of(sourceStock, source.ThresholdsOf(good), projected, destination.ThresholdsOf(good)) + WholeUnitMargin);

            // The negated comparison also rejects NaN.
            if (!(quantity >= ShipmentQuantity.SmallestLotFor(goodUnits.RoomOf(good))))
            {
                return false;
            }

            double gap = (destination.SellFactorAt(good, projected) - source.BuyFactorAt(good, sourceStock)) * weights[good];

            if (!(gap > 0d))
            {
                return false;
            }

            units = (int)Math.Min(int.MaxValue, quantity);
            score = gap;

            return true;
        }

        // False for NaN too.
        private bool Reachable(int from, int to)
        {
            double days = travelDays[from, to];

            return days >= 0d && !double.IsInfinity(days);
        }

        private void RequirePort(int port, string name)
        {
            if (port < 0 || port >= PortCount)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private void RequireGood(int good)
        {
            if (good < 0 || good >= GoodCount)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }
        }

        private void RequireUnderWay(int[,] underWay)
        {
            if (underWay != null && (underWay.GetLength(0) < PortCount || underWay.GetLength(1) < GoodCount))
            {
                throw new ArgumentException("The units under way must have a row per port and a column per good.", nameof(underWay));
            }
        }
    }
}
