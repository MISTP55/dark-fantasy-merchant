using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Which ships lie in which port, which are in a city's harbour on their way to or from
    /// a berth, and which are sailing to one. A ship that reaches the anchorage of the port it
    /// was ordered to enters the city's harbour, if it has one, and docks only at its berth;
    /// without a harbour it docks at once. On its next order it leaves, back by the edge of
    /// the harbour, before it sails at sea. Orders to ships that can dock go through here
    /// rather than straight to the ship.
    /// </summary>
    public sealed class ShipDocking<TPort> where TPort : class
    {
        // A ship in a city: its harbour's queue or grid, or docked at once without a harbour.
        private sealed class Stay
        {
            public TPort City;

            // Null for a city without a harbour: the ship is then docked.
            public Harbour Harbour;
        }

        // Far below the size of a navigation cell, in normalized map units. Not zero:
        // a ship skips a waypoint it is on in world space, so it can lie a rounding
        // error away from its anchorage.
        private const float SamePointTolerance = 1e-6f;

        private readonly Func<TPort, Harbour> harbourOf;

        // Port each ship at sea, or leaving a city, is sailing to.
        private readonly Dictionary<Ship, TPort> destinationPorts = new Dictionary<Ship, TPort>();

        private readonly Dictionary<Ship, TPort> ports = new Dictionary<Ship, TPort>();

        // Docked ships, the first to have arrived first.
        private readonly List<Ship> dockedShips = new List<Ship>();

        private readonly Dictionary<Ship, Stay> stays = new Dictionary<Ship, Stay>();

        // Ships in a city, the first to have come first.
        private readonly List<Ship> cityShips = new List<Ship>();

        // The harbours whose events are listened to, and the city of each.
        private readonly Dictionary<Harbour, TPort> harbourCities = new Dictionary<Harbour, TPort>();

        // Reused by every update.
        private readonly List<Ship> arrivedShips = new List<Ship>();
        private readonly List<Harbour> busyHarbours = new List<Harbour>();

        /// <param name="harbourOf">
        /// The harbour of a port, or null for a port without one, which a ship enters at once.
        /// Null when no port has a harbour. A port must keep the same harbour.
        /// </param>
        public ShipDocking(Func<TPort, Harbour> harbourOf = null)
        {
            this.harbourOf = harbourOf;
        }

        /// <summary>Raised when a ship docks at its berth, or in a port without a harbour.</summary>
        public event Action<Ship, TPort> Docked;

        /// <summary>Raised when a docked ship is no longer at its berth: it leaves, or is removed.</summary>
        public event Action<Ship, TPort> Undocked;

        /// <summary>
        /// Raised after every change of <see cref="StateOf"/> of a ship in a city, or one that
        /// has just left it, with that city: queued, entered the grid, docked, started leaving,
        /// turned back, exited, withdrawn from the queue, removed.
        /// </summary>
        public event Action<Ship, TPort> CityStateChanged;

        /// <summary>Whether the ship is docked at its berth (or in a port without a harbour).</summary>
        public bool IsDocked(Ship ship)
        {
            return ship != null && ports.ContainsKey(ship);
        }

        /// <returns>The port the ship is docked in, or null when it is not docked.</returns>
        public TPort PortOf(Ship ship)
        {
            return ship != null && ports.TryGetValue(ship, out TPort port) ? port : null;
        }

        /// <returns>The port the ship will enter when it arrives, or null.</returns>
        public TPort DestinationPortOf(Ship ship)
        {
            return ship != null && destinationPorts.TryGetValue(ship, out TPort port) ? port : null;
        }

        /// <returns>Where the ship stands in its city; at sea when it is in none.</returns>
        public HarbourState StateOf(Ship ship)
        {
            if (ship == null || !stays.TryGetValue(ship, out Stay stay))
            {
                return HarbourState.AtSea;
            }

            return stay.Harbour == null ? HarbourState.Docked : stay.Harbour.StateOf(ship);
        }

        /// <returns>The city whose queue or grid the ship is in, docked included, or null at sea.</returns>
        public TPort CityOf(Ship ship)
        {
            return ship != null && stays.TryGetValue(ship, out Stay stay) ? stay.City : null;
        }

        /// <summary>Whether the ship is in a city: it is then not on the map and does not sail its route.</summary>
        public bool IsInCity(Ship ship)
        {
            return ship != null && stays.ContainsKey(ship);
        }

        /// <summary>Whether the ship is crossing a harbour, or sailing at sea.</summary>
        public bool IsUnderWay(Ship ship)
        {
            if (ship == null)
            {
                return false;
            }

            HarbourState state = StateOf(ship);
            return state == HarbourState.Arriving
                || state == HarbourState.Leaving
                || (state == HarbourState.AtSea && ship.IsMoving);
        }

        /// <param name="results">
        /// Cleared, then filled with the ships docked in the port, the first to have arrived first.
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

        /// <param name="results">
        /// Cleared, then filled with the ships in the city in every state, the first to have come first.
        /// </param>
        public void GetShipsInCity(TPort city, List<Ship> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            foreach (Ship ship in cityShips)
            {
                if (ReferenceEquals(stays[ship].City, city))
                {
                    results.Add(ship);
                }
            }
        }

        /// <summary>
        /// Orders a ship to a port, which it enters when it arrives. A ship in another
        /// city leaves it; a ship already in this one stays there, and one leaving it turns
        /// back to its berth. When the ship cannot sail to the port, it sails as near as it
        /// can and stays at sea.
        /// </summary>
        /// <param name="portPosition">Normalized map position of the port.</param>
        /// <returns>Whether the ship is in the port or on its way to enter it.</returns>
        public bool OrderToPort(Ship ship, TPort port, Vector2 portPosition)
        {
            return OrderToPort(ship, port, portPosition, null);
        }

        /// <summary>
        /// Orders a ship to a port like <see cref="OrderToPort(Ship, TPort, Vector2)"/>, along
        /// a route worked out beforehand rather than one the ship's pathfinder looks for.
        /// </summary>
        /// <param name="route">
        /// The route from where the ship is to the port's anchorage, as
        /// <see cref="Ship.FollowRoute"/> takes it. Null to let the pathfinder find one.
        /// </param>
        public bool OrderToPort(Ship ship, TPort port, Vector2 portPosition, IReadOnlyList<Vector2> route)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (port == null)
            {
                throw new ArgumentNullException(nameof(port));
            }

            if (stays.TryGetValue(ship, out Stay stay) && ReferenceEquals(stay.City, port))
            {
                if (stay.Harbour != null && stay.Harbour.StateOf(ship) == HarbourState.Leaving)
                {
                    stay.Harbour.Return(ship);
                    ship.Stop();
                    destinationPorts.Remove(ship);
                    CityStateChanged?.Invoke(ship, port);
                }

                return true;
            }

            // An order the ship ignores changes nothing, not even where it is docked.
            if (route != null ? !ship.FollowRoute(route) : !ship.SetDestination(portPosition))
            {
                return false;
            }

            destinationPorts.Remove(ship);
            LeaveCity(ship);

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
        /// is at that point, and it leaves the city it is in.
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
            LeaveCity(ship);
            return true;
        }

        /// <summary>
        /// Puts a ship that starts in a port straight at a berth, docked: <see cref="CityStateChanged"/> is raised,
        /// but not <see cref="Docked"/>, since the ship has not come in. The ship must lie at the port's anchorage,
        /// out of every city and with no order to a port.
        /// </summary>
        /// <param name="portPosition">Normalized map position of the port.</param>
        /// <returns>False, the ship staying at sea, when it is not at the anchorage or no berth is free.</returns>
        public bool PlaceInPort(Ship ship, TPort port, Vector2 portPosition)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (port == null)
            {
                throw new ArgumentNullException(nameof(port));
            }

            if (stays.ContainsKey(ship) || destinationPorts.ContainsKey(ship))
            {
                throw new InvalidOperationException("Only a ship at sea with no order to a port can be placed in one.");
            }

            if (ship.IsMoving || !IsSamePoint(ship.Position, ship.AnchorageAt(portPosition)))
            {
                return false;
            }

            Harbour harbour = harbourOf?.Invoke(port);

            if (harbour != null && !harbour.Place(ship))
            {
                return false;
            }

            if (harbour != null)
            {
                Listen(harbour, port);
            }

            stays[ship] = new Stay { City = port, Harbour = harbour };
            cityShips.Add(ship);
            ports[ship] = port;
            dockedShips.Add(ship);
            CityStateChanged?.Invoke(ship, port);
            return true;
        }

        /// <summary>
        /// Forgets a ship that leaves the map: it is no longer in a city, which it leaves
        /// at once whatever its state, nor on its way to one.
        /// </summary>
        public void Remove(Ship ship)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            destinationPorts.Remove(ship);

            if (!stays.TryGetValue(ship, out Stay stay))
            {
                return;
            }

            Forget(ship);
            Undock(ship);
            CityStateChanged?.Invoke(ship, stay.City);

            // Last: the harbour lets the queue in, which raises its own changes.
            if (stay.Harbour != null && stay.Harbour.Contains(ship))
            {
                stay.Harbour.Remove(ship);
            }
        }

        /// <summary>
        /// Advances the harbours, then lets the ships that have arrived at their port's
        /// anchorage into it. Call after the ships have advanced.
        /// </summary>
        /// <param name="deltaTime">Game seconds the harbours' traffic advances by.</param>
        public void Update(float deltaTime)
        {
            AdvanceHarbours(deltaTime);

            arrivedShips.Clear();

            foreach (KeyValuePair<Ship, TPort> pair in destinationPorts)
            {
                if (!pair.Key.IsMoving && !stays.ContainsKey(pair.Key))
                {
                    arrivedShips.Add(pair.Key);
                }
            }

            for (int i = 0; i < arrivedShips.Count; i++)
            {
                Ship ship = arrivedShips[i];

                // A listener of an earlier ship's event may have given this one an order.
                if (ship.IsMoving || stays.ContainsKey(ship) || !destinationPorts.TryGetValue(ship, out TPort port))
                {
                    continue;
                }

                destinationPorts.Remove(ship);
                Enter(ship, port);
            }
        }

        private void AdvanceHarbours(float deltaTime)
        {
            busyHarbours.Clear();

            foreach (Stay stay in stays.Values)
            {
                if (stay.Harbour != null && !busyHarbours.Contains(stay.Harbour))
                {
                    busyHarbours.Add(stay.Harbour);
                }
            }

            for (int i = 0; i < busyHarbours.Count; i++)
            {
                busyHarbours[i].Advance(deltaTime);
            }
        }

        private void Enter(Ship ship, TPort port)
        {
            Harbour harbour = harbourOf?.Invoke(port);
            stays[ship] = new Stay { City = port, Harbour = harbour };
            cityShips.Add(ship);

            if (harbour == null)
            {
                Dock(ship, port);
                return;
            }

            Listen(harbour, port);
            harbour.Admit(ship);
            CityStateChanged?.Invoke(ship, port);
        }

        // A ship in a city goes back to sea along the route it has just been given:
        // at once from the queue or a port without a harbour, by the edge of the grid otherwise.
        private void LeaveCity(Ship ship)
        {
            if (!stays.TryGetValue(ship, out Stay stay))
            {
                return;
            }

            HarbourState state = StateOf(ship);

            if (stay.Harbour == null || state == HarbourState.Waiting)
            {
                stay.Harbour?.Withdraw(ship);
                Forget(ship);
                Undock(ship);
                CityStateChanged?.Invoke(ship, stay.City);
                return;
            }

            if (state == HarbourState.Leaving)
            {
                return;
            }

            stay.Harbour.Leave(ship);
            Undock(ship);
            CityStateChanged?.Invoke(ship, stay.City);
        }

        private void Listen(Harbour harbour, TPort port)
        {
            if (harbourCities.ContainsKey(harbour))
            {
                return;
            }

            harbourCities.Add(harbour, port);
            harbour.Entered += ship => CityStateChanged?.Invoke(ship, port);
            harbour.Berthed += ship => Dock(ship, port);
            harbour.Exited += ship => OnExited(ship, port);
        }

        // The ship is back at sea, at its city's anchorage, with the route it was given.
        private void OnExited(Ship ship, TPort port)
        {
            Forget(ship);
            CityStateChanged?.Invoke(ship, port);
        }

        private void Dock(Ship ship, TPort port)
        {
            ports[ship] = port;
            dockedShips.Add(ship);
            CityStateChanged?.Invoke(ship, port);
            Docked?.Invoke(ship, port);
        }

        private void Forget(Ship ship)
        {
            stays.Remove(ship);
            cityShips.Remove(ship);
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
