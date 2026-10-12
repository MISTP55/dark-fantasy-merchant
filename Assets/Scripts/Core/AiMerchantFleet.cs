using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The merchant ships that trade on their own. A ship sells its whole hold when it
    /// enters a port, stays there for a stopover, then buys the cargo that earns the most
    /// and sails to where it sells; with nothing to trade it sails empty to a better
    /// port, or waits. Their gold is not limited: only their profit is counted.
    /// </summary>
    public sealed class AiMerchantFleet<TPort> where TPort : class
    {
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
        }

        private readonly ShipDocking<TPort> docking;
        private readonly IReadOnlyList<TPort> ports;
        private readonly IReadOnlyList<Vector2> portPositions;
        private readonly IReadOnlyList<CityMarket> markets;
        private readonly TradeRoutePlanner planner;
        private readonly double stopoverSeconds;

        private readonly Dictionary<TPort, int> portIndices = new Dictionary<TPort, int>();
        private readonly Dictionary<Ship, Merchant> merchantsByShip = new Dictionary<Ship, Merchant>();
        private readonly List<Merchant> merchants = new List<Merchant>();

        // Barrels under way to each port, by port then good. Rebuilt for every decision.
        private readonly int[,] incoming;

        /// <param name="docking">The docking of the map: the ships are ordered through it.</param>
        /// <param name="ports">The ports the ships trade between, in the planner's order.</param>
        /// <param name="portPositions">Normalized map position of each port, as an order to it takes.</param>
        /// <param name="markets">The market of each port.</param>
        /// <param name="stopoverSeconds">Seconds of the world a ship stays in a port.</param>
        public AiMerchantFleet(
            ShipDocking<TPort> docking,
            IReadOnlyList<TPort> ports,
            IReadOnlyList<Vector2> portPositions,
            IReadOnlyList<CityMarket> markets,
            TradeRoutePlanner planner,
            double stopoverSeconds)
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

            // The negated comparison also rejects NaN.
            if (!(stopoverSeconds >= 0d) || double.IsInfinity(stopoverSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(stopoverSeconds));
            }

            this.stopoverSeconds = stopoverSeconds;
            incoming = new int[planner.PortCount, planner.GoodCount];

            for (int i = 0; i < ports.Count; i++)
            {
                portIndices[ports[i]] = i;
            }

            docking.Docked += OnDocked;
        }

        public int Count => merchants.Count;

        public bool Contains(Ship ship)
        {
            return ship != null && merchantsByShip.ContainsKey(ship);
        }

        /// <returns>Gold the ship's sales brought minus what its purchases cost; 0 for a ship that is not of the fleet.</returns>
        public long ProfitOf(Ship ship)
        {
            return ship != null && merchantsByShip.TryGetValue(ship, out Merchant merchant) ? merchant.Profit : 0;
        }

        /// <summary>Takes a ship into the fleet and orders it into the port it starts beside.</summary>
        public void Add(Ship ship, int startPort)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (startPort < 0 || startPort >= ports.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startPort));
            }

            if (merchantsByShip.ContainsKey(ship))
            {
                throw new ArgumentException("The ship is already in the fleet.", nameof(ship));
            }

            var merchant = new Merchant { Ship = ship, LastPort = startPort, Wait = stopoverSeconds };
            merchants.Add(merchant);
            merchantsByShip.Add(ship, merchant);
            SendTo(merchant, startPort);
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

            // The whole hold, whatever the prices have become since it was bought.
            CityMarket market = markets[index];

            for (int good = 0; good < market.GoodCount; good++)
            {
                int barrels = ship.Cargo.BarrelsOf(good);

                if (barrels > 0)
                {
                    merchant.Profit += Trade.Sell(market, ship.Cargo, good, barrels).Gold;
                }
            }
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

            CountIncoming(merchant);

            int capacity = ship.Cargo.Free;

            if (planner.TryPlanTrade(from, capacity, incoming, out TradePlan plan))
            {
                for (int good = 0; good < planner.GoodCount; good++)
                {
                    int barrels = plan.BarrelsOf(good);

                    if (barrels > 0)
                    {
                        merchant.Profit -= Trade.Buy(markets[from], ship.Cargo, good, barrels).Gold;
                    }
                }

                SendTo(merchant, plan.Destination);
            }
            else if (planner.TryPlanRelocation(from, capacity, incoming, out int destination))
            {
                SendTo(merchant, destination);
            }
            else
            {
                merchant.Wait = stopoverSeconds;
            }
        }

        /// <summary>What the other ships of the fleet are carrying to each port.</summary>
        private void CountIncoming(Merchant deciding)
        {
            Array.Clear(incoming, 0, incoming.Length);

            foreach (Merchant other in merchants)
            {
                if (ReferenceEquals(other, deciding))
                {
                    continue;
                }

                TPort destination = docking.DestinationPortOf(other.Ship);

                if (destination == null || !portIndices.TryGetValue(destination, out int port))
                {
                    continue;
                }

                for (int good = 0; good < planner.GoodCount; good++)
                {
                    incoming[port, good] += other.Ship.Cargo.BarrelsOf(good);
                }
            }
        }

        private bool SendTo(Merchant merchant, int port)
        {
            return docking.OrderToPort(merchant.Ship, ports[port], portPositions[port]);
        }
    }
}
