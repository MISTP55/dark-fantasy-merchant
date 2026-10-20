using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The merchant ships that trade on their own, on Patrician III's tour. Every ship has a home
    /// city. From home it sails straight to a final destination, with the cargo the planner loads
    /// for it, and sells everything there; then it comes back home, stopping on the way at the
    /// cities worth a short detour, where it sells only up to their T1 and keeps what home lacks;
    /// at home it sells everything and chooses the next final destination, or waits. In every port
    /// it stays for a stopover. What a ship loads for a port counts as under way to it, for every
    /// ship's decisions, until it enters that port. Their gold is not limited: only their profit is counted.
    /// </summary>
    public sealed class AiMerchantFleet<TPort> where TPort : class
    {
        // Rounding slack, as the planner's: a fractional lack a hair over a whole unit is that unit.
        private const double WholeUnitMargin = 1e-9;

        private sealed class Merchant
        {
            public Ship Ship;
            public long Profit;

            // Seconds left of its stopover, counted while it lies in port.
            public double Wait;

            // The port it last lay in, or the one it starts in: one it can sail to.
            public int LastPort;

            // It could not be sent back to that port: nothing more is tried for it at sea.
            public bool IsStranded;

            // The city its tours start from and end in.
            public int Home;

            // Where the tour goes: a city away from home on the way out, home on the way back; -1 for none.
            public int FinalDestination;

            // The port it sails to when its stopover is over: a stopover, or the final destination.
            public int NextPort;

            // The cities of this return trip, which are no stopover again before it enters home.
            public bool[] Visited;

            // Units it loaded for a port and has not delivered there, by port then good.
            public int[,] Records;
        }

        private readonly ShipDocking<TPort> docking;
        private readonly IReadOnlyList<TPort> ports;
        private readonly IReadOnlyList<Vector2> portPositions;
        private readonly IReadOnlyList<CityMarket> markets;
        private readonly SupplyPlanner planner;
        private readonly double stopoverSeconds;
        private readonly PortTravelTable routes;

        private readonly Dictionary<TPort, int> portIndices = new Dictionary<TPort, int>();
        private readonly Dictionary<Ship, Merchant> merchantsByShip = new Dictionary<Ship, Merchant>();
        private readonly List<Merchant> merchants = new List<Merchant>();

        // The sum of every ship's records, by port then good. Changed only with the records.
        private readonly int[,] underWay;

        // The units of each good the planner loads, reused.
        private readonly int[] cargo;

        /// <param name="docking">The docking of the map: the ships are ordered through it.</param>
        /// <param name="ports">The ports the ships trade between, in the planner's order.</param>
        /// <param name="portPositions">Normalized map position of each port, as an order to it takes.</param>
        /// <param name="markets">The market of each port.</param>
        /// <param name="planner">Chooses the destinations, the stopovers and the cargo, over the same ports.</param>
        /// <param name="stopoverSeconds">Seconds of the world a ship stays in a port.</param>
        /// <param name="routes">
        /// The routes between the ports' anchorages, over the same ports: a ship that leaves a port
        /// sails the one to its next port rather than looking for it. Null to look for every route.
        /// </param>
        public AiMerchantFleet(
            ShipDocking<TPort> docking,
            IReadOnlyList<TPort> ports,
            IReadOnlyList<Vector2> portPositions,
            IReadOnlyList<CityMarket> markets,
            SupplyPlanner planner,
            double stopoverSeconds,
            PortTravelTable routes = null)
        {
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
            this.ports = ports ?? throw new ArgumentNullException(nameof(ports));
            this.portPositions = portPositions ?? throw new ArgumentNullException(nameof(portPositions));
            this.markets = markets ?? throw new ArgumentNullException(nameof(markets));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));

            if (ports.Count != planner.PortCount || portPositions.Count != planner.PortCount
                || markets.Count != planner.PortCount)
            {
                throw new ArgumentException("There must be a port, a position and a market per port of the planner.");
            }

            if (routes != null && routes.PortCount != planner.PortCount)
            {
                throw new ArgumentException("There must be a route table over the ports of the planner.");
            }

            // The negated comparison also rejects NaN.
            if (!(stopoverSeconds >= 0d) || double.IsInfinity(stopoverSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(stopoverSeconds));
            }

            this.stopoverSeconds = stopoverSeconds;
            this.routes = routes;
            underWay = new int[planner.PortCount, planner.GoodCount];
            cargo = new int[planner.GoodCount];

            for (int i = 0; i < ports.Count; i++)
            {
                portIndices[ports[i]] = i;
            }

            docking.Docked += OnDocked;
        }

        public int Count => merchants.Count;

        /// <summary>Raised after every purchase and sale that moved at least one unit, for the debug tools.</summary>
        public event Action<FleetTrade> Traded;

        public bool Contains(Ship ship)
        {
            return ship != null && merchantsByShip.ContainsKey(ship);
        }

        /// <returns>Gold the ship's sales brought minus what its purchases cost; 0 for a ship that is not of the fleet.</returns>
        public long ProfitOf(Ship ship)
        {
            return ship != null && merchantsByShip.TryGetValue(ship, out Merchant merchant) ? merchant.Profit : 0;
        }

        /// <returns>The port of the ship's home; -1 for a ship that is not of the fleet.</returns>
        public int HomeOf(Ship ship)
        {
            return ship != null && merchantsByShip.TryGetValue(ship, out Merchant merchant) ? merchant.Home : -1;
        }

        /// <returns>The port the ship's tour goes to; -1 for none, and for a ship that is not of the fleet.</returns>
        public int FinalDestinationOf(Ship ship)
        {
            return ship != null && merchantsByShip.TryGetValue(ship, out Merchant merchant) ? merchant.FinalDestination : -1;
        }

        /// <summary>The ships of the fleet whose home is a port.</summary>
        public int ShipsHomedAt(int port)
        {
            int count = 0;

            foreach (Merchant merchant in merchants)
            {
                if (merchant.Home == port)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Units of a good the ships of the fleet have loaded for a port and not delivered there yet.</summary>
        public int UnderWayTo(int port, int good)
        {
            if (port < 0 || port >= planner.PortCount)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            if (good < 0 || good >= planner.GoodCount)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }

            return underWay[port, good];
        }

        /// <summary>Takes a ship into the fleet and orders it into its home, where its tours start.</summary>
        /// <param name="ship">A ship beside its home.</param>
        public void Add(Ship ship, int home)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (home < 0 || home >= ports.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(home));
            }

            if (merchantsByShip.ContainsKey(ship))
            {
                throw new ArgumentException("The ship is already in the fleet.", nameof(ship));
            }

            var merchant = new Merchant
            {
                Ship = ship,
                LastPort = home,
                Wait = stopoverSeconds,
                Home = home,
                FinalDestination = -1,
                NextPort = -1,
                Visited = new bool[planner.PortCount],
                Records = new int[planner.PortCount, planner.GoodCount],
            };

            merchants.Add(merchant);
            merchantsByShip.Add(ship, merchant);
            SendTo(merchant, home);
        }

        /// <summary>
        /// Lets a ship go: it no longer trades, its profit is forgotten and what it carries no
        /// longer counts as under way. The ship itself is left as it is, where it is, with what it carries.
        /// </summary>
        /// <returns>False for a ship that is not of the fleet.</returns>
        public bool Remove(Ship ship)
        {
            if (ship == null || !merchantsByShip.TryGetValue(ship, out Merchant merchant))
            {
                return false;
            }

            ClearRecords(merchant);
            merchantsByShip.Remove(ship);
            merchants.Remove(merchant);
            return true;
        }

        /// <summary>Counts the stopovers down and sends off the ships whose stopover is over.</summary>
        /// <param name="seconds">Seconds of the world since the last call.</param>
        public void Advance(double seconds)
        {
            // The negated comparison also rejects NaN.
            if (!(seconds > 0d))
            {
                return;
            }

            foreach (Merchant merchant in merchants)
            {
                Ship ship = merchant.Ship;

                if (docking.IsDocked(ship))
                {
                    merchant.Wait -= seconds;

                    if (merchant.Wait <= 0d)
                    {
                        Depart(merchant);
                    }
                }
                else if (!ship.IsMoving && docking.DestinationPortOf(ship) == null && !merchant.IsStranded)
                {
                    // Idle at sea, which its own orders never leave it: back to a port it knows.
                    merchant.IsStranded = !SendTo(merchant, merchant.LastPort);

                    if (merchant.IsStranded)
                    {
                        // What it carries will never arrive.
                        ClearRecords(merchant);
                    }
                }
            }
        }

        private void OnDocked(Ship ship, TPort port)
        {
            if (!merchantsByShip.TryGetValue(ship, out Merchant merchant) || !portIndices.TryGetValue(port, out int index))
            {
                return;
            }

            merchant.LastPort = index;
            merchant.IsStranded = false;
            merchant.Wait = stopoverSeconds;

            // Delivered, whatever it sells here.
            ClearRecordsFor(merchant, index);

            if (index == merchant.Home)
            {
                SellEverything(merchant, index);
                CutRecordsToCargo(merchant);

                // A new tour starts.
                Array.Clear(merchant.Visited, 0, merchant.Visited.Length);
                merchant.FinalDestination = planner.ChooseDestination(merchant.Home, underWay);
                merchant.NextPort = merchant.FinalDestination;
                return;
            }

            // Only a ship sent back from the sea enters a port away from home without a tour.
            if (merchant.FinalDestination < 0)
            {
                merchant.FinalDestination = merchant.Home;
            }

            if (index == merchant.FinalDestination)
            {
                SellEverything(merchant, index);
                merchant.FinalDestination = merchant.Home;
            }
            else
            {
                SellUpToT1(merchant, index);
            }

            merchant.Visited[index] = true;
            CutRecordsToCargo(merchant);

            int stopover = planner.ChooseStopover(index, merchant.Home, underWay, merchant.Visited);
            merchant.NextPort = stopover >= 0 ? stopover : merchant.Home;
        }

        private void Depart(Merchant merchant)
        {
            Ship ship = merchant.Ship;

            if (!portIndices.TryGetValue(docking.PortOf(ship), out int from))
            {
                // In a port the fleet does not trade in: it waits there.
                merchant.Wait = stopoverSeconds;
                return;
            }

            if (merchant.FinalDestination < 0)
            {
                merchant.FinalDestination = from == merchant.Home
                    ? planner.ChooseDestination(merchant.Home, underWay)
                    : merchant.Home;
                merchant.NextPort = merchant.FinalDestination;
            }

            if (merchant.FinalDestination < 0)
            {
                // Nothing worth a tour from home yet.
                merchant.Wait = stopoverSeconds;
                return;
            }

            if (merchant.NextPort < 0)
            {
                merchant.NextPort = merchant.FinalDestination;
            }

            LoadFor(merchant, from, merchant.FinalDestination);

            if (merchant.NextPort != merchant.FinalDestination)
            {
                // A stopover is worth its own cargo only with more than a quarter of the hold free.
                if (ship.Cargo.Free * 4 > ship.Cargo.Capacity)
                {
                    LoadFor(merchant, from, merchant.NextPort);
                }
                else
                {
                    merchant.NextPort = merchant.FinalDestination;
                }
            }

            SendTo(merchant, merchant.NextPort);
        }

        /// <summary>Buys in a port the cargo the planner loads for another, and records it as under way there.</summary>
        private void LoadFor(Merchant merchant, int from, int to)
        {
            CargoHold hold = merchant.Ship.Cargo;
            planner.Load(from, to, hold.Free, underWay, cargo);

            for (int good = 0; good < planner.GoodCount; good++)
            {
                if (cargo[good] <= 0)
                {
                    continue;
                }

                TradeResult bought = Trade.Buy(markets[from], hold, good, cargo[good]);
                merchant.Profit -= bought.Gold;
                Record(merchant, to, good, bought.Units);

                if (bought.Units > 0)
                {
                    Traded?.Invoke(new FleetTrade(merchant.Ship, markets[from], good, bought.Units, bought.Gold, false));
                }
            }
        }

        // The whole hold, whatever the prices have become since it was bought.
        private void SellEverything(Merchant merchant, int port)
        {
            CargoHold hold = merchant.Ship.Cargo;
            CityMarket market = markets[port];

            for (int good = 0; good < market.GoodCount; good++)
            {
                int units = hold.UnitsOf(good);

                if (units > 0)
                {
                    Sell(merchant, port, good, units);
                }
            }
        }

        // What brings the city to its T1, out of what home does not lack.
        private void SellUpToT1(Merchant merchant, int port)
        {
            CargoHold hold = merchant.Ship.Cargo;
            CityMarket market = markets[port];
            CityMarket home = markets[merchant.Home];

            for (int good = 0; good < planner.GoodCount; good++)
            {
                int aboard = hold.UnitsOf(good);

                if (aboard <= 0)
                {
                    continue;
                }

                double keep = Math.Ceiling(Math.Max(0d, home.ThresholdsOf(good).T1 - home.StockOf(good)) - WholeUnitMargin);
                double lack = Math.Max(0d, market.ThresholdsOf(good).T1 - market.StockOf(good));
                double units = Math.Floor(Math.Min(aboard - keep, lack) + WholeUnitMargin);

                if (units > 0d)
                {
                    Sell(merchant, port, good, (int)units);
                }
            }
        }

        private void Sell(Merchant merchant, int port, int good, int units)
        {
            CityMarket market = markets[port];
            TradeResult sold = Trade.Sell(market, merchant.Ship.Cargo, good, units);
            merchant.Profit += sold.Gold;

            if (sold.Units > 0)
            {
                Traded?.Invoke(new FleetTrade(merchant.Ship, market, good, sold.Units, sold.Gold, true));
            }
        }

        private void Record(Merchant merchant, int port, int good, int units)
        {
            merchant.Records[port, good] += units;
            underWay[port, good] += units;
        }

        // A record never exceeds what is aboard of its good.
        private void CutRecordsToCargo(Merchant merchant)
        {
            CargoHold hold = merchant.Ship.Cargo;

            for (int good = 0; good < planner.GoodCount; good++)
            {
                int aboard = hold.UnitsOf(good);

                for (int port = 0; port < planner.PortCount; port++)
                {
                    int excess = merchant.Records[port, good] - aboard;

                    if (excess > 0)
                    {
                        merchant.Records[port, good] -= excess;
                        underWay[port, good] -= excess;
                    }
                }
            }
        }

        private void ClearRecordsFor(Merchant merchant, int port)
        {
            for (int good = 0; good < planner.GoodCount; good++)
            {
                underWay[port, good] -= merchant.Records[port, good];
                merchant.Records[port, good] = 0;
            }
        }

        private void ClearRecords(Merchant merchant)
        {
            for (int port = 0; port < planner.PortCount; port++)
            {
                ClearRecordsFor(merchant, port);
            }
        }

        private bool SendTo(Merchant merchant, int port)
        {
            // Only a ship in port lies on the anchorage the route starts from; at sea, or for a
            // pair without a route, the pathfinder looks for one.
            bool inLastPort = ReferenceEquals(docking.PortOf(merchant.Ship), ports[merchant.LastPort]);
            IReadOnlyList<Vector2> route = routes != null && inLastPort ? routes.RouteBetween(merchant.LastPort, port) : null;

            return docking.OrderToPort(merchant.Ship, ports[port], portPositions[port], route);
        }
    }
}
