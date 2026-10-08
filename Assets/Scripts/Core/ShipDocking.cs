using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Which ships lie in which port, and which are sailing to one. A ship enters a port
    /// only when it was ordered to it and has arrived; it leaves on its next order.
    /// Orders to ships that can dock go through here rather than straight to the ship.
    /// </summary>
    public sealed class ShipDocking<TPort> where TPort : class
    {
        // Far below the size of a navigation cell, in normalized map units. Not zero:
        // a ship skips a waypoint it is on in world space, so it can lie a rounding
        // error away from its anchorage.
        private const float SamePointTolerance = 1e-6f;

        // Port each ship under way is sailing to.
        private readonly Dictionary<Ship, TPort> destinationPorts = new Dictionary<Ship, TPort>();

        private readonly Dictionary<Ship, TPort> ports = new Dictionary<Ship, TPort>();

        // Docked ships, the first to have arrived first.
        private readonly List<Ship> dockedShips = new List<Ship>();

        // Reused by every update.
        private readonly List<Ship> arrivedShips = new List<Ship>();

        public event Action<Ship, TPort> Docked;

        public event Action<Ship, TPort> Undocked;

        public bool IsDocked(Ship ship)
        {
            return ship != null && ports.ContainsKey(ship);
        }

        /// <returns>The port the ship lies in, or null when it is at sea.</returns>
        public TPort PortOf(Ship ship)
        {
            return ship != null && ports.TryGetValue(ship, out TPort port) ? port : null;
        }

        /// <returns>The port the ship will enter when it arrives, or null.</returns>
        public TPort DestinationPortOf(Ship ship)
        {
            return ship != null && destinationPorts.TryGetValue(ship, out TPort port) ? port : null;
        }

        /// <param name="results">
        /// Cleared, then filled with the ships in the port, the first to have arrived first.
        /// </param>
        public void GetShipsIn(TPort port, List<Ship> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            foreach (Ship ship in dockedShips)
            {
                if (ReferenceEquals(ports[ship], port))
                {
                    results.Add(ship);
                }
            }
        }

        /// <summary>
        /// Orders a ship to a port, which it enters when it arrives. A ship in another
        /// port leaves it; a ship already in this one stays there. When the ship cannot
        /// sail to the port, it sails as near as it can and stays at sea.
        /// </summary>
        /// <param name="portPosition">Normalized map position of the port.</param>
        /// <returns>Whether the ship is in the port or on its way to enter it.</returns>
        public bool OrderToPort(Ship ship, TPort port, Vector2 portPosition)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (port == null)
            {
                throw new ArgumentNullException(nameof(port));
            }

            if (ReferenceEquals(PortOf(ship), port))
            {
                return true;
            }

            // An order the ship ignores changes nothing, not even where it is docked.
            if (!ship.SetDestination(portPosition))
            {
                return false;
            }

            destinationPorts.Remove(ship);
            Undock(ship);

            // A route to a port in another sea ends on the nearest shore of this one.
            Vector2 routeEnd = ship.Destination ?? ship.Position;

            if (!IsSamePoint(routeEnd, ship.AnchorageAt(portPosition)))
            {
                return false;
            }

            destinationPorts[ship] = port;
            return true;
        }

        /// <summary>
        /// Orders a ship to a point of the map. It will not enter a port, even one that
        /// is at that point, and it leaves the port it is in.
        /// </summary>
        /// <returns>False when the ship ignored the order, which changes nothing.</returns>
        public bool OrderToPoint(Ship ship, Vector2 destination)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (!ship.SetDestination(destination))
            {
                return false;
            }

            destinationPorts.Remove(ship);
            Undock(ship);
            return true;
        }

        /// <summary>Docks the ships that have arrived at their port. Call after the ships have advanced.</summary>
        public void Update()
        {
            arrivedShips.Clear();

            foreach (KeyValuePair<Ship, TPort> pair in destinationPorts)
            {
                if (!pair.Key.IsMoving)
                {
                    arrivedShips.Add(pair.Key);
                }
            }

            for (int i = 0; i < arrivedShips.Count; i++)
            {
                Ship ship = arrivedShips[i];

                // A listener of an earlier ship's event may have given this one an order.
                if (ship.IsMoving || !destinationPorts.TryGetValue(ship, out TPort port))
                {
                    continue;
                }

                destinationPorts.Remove(ship);
                ports[ship] = port;
                dockedShips.Add(ship);
                Docked?.Invoke(ship, port);
            }
        }

        private void Undock(Ship ship)
        {
            if (!ports.TryGetValue(ship, out TPort port))
            {
                return;
            }

            ports.Remove(ship);
            dockedShips.Remove(ship);
            Undocked?.Invoke(ship, port);
        }

        private static bool IsSamePoint(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) <= SamePointTolerance && Mathf.Abs(a.y - b.y) <= SamePointTolerance;
        }
    }
}
