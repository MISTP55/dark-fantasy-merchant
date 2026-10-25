using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Tells when a ship has arrived, and in which port. A ship arrives when it docks at
    /// its berth (or in a port without a harbour), or when it reaches the end of a route
    /// at sea. A ship whose route ends at a port's anchorage and that enters the harbour's
    /// queue or grid has not arrived yet: it does when it docks. Arrivals, and ships that
    /// entered a queue, are held until <see cref="Report"/>, which is called once the
    /// docking is up to date, so that the port of each one is known.
    /// </summary>
    public sealed class ShipArrivals<TPort> where TPort : class
    {
        private readonly ShipDocking<TPort> docking;

        // Ships that have arrived since the last report, each one once.
        private readonly List<Ship> arrivedShips = new List<Ship>();

        // Ships that have entered a queue since the last report, each one once.
        private readonly List<Ship> queuedShips = new List<Ship>();

        // Ships whose arrivals are reported.
        private readonly HashSet<Ship> trackedShips = new HashSet<Ship>();

        public ShipArrivals(ShipDocking<TPort> docking)
        {
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
            docking.Docked += OnDocked;
            docking.CityStateChanged += OnCityStateChanged;
        }

        /// <summary>
        /// Raised by <see cref="Report"/> with a ship that has arrived and the port it
        /// lies in, or null when it stopped at sea.
        /// </summary>
        public event Action<Ship, TPort> Arrived;

        /// <summary>
        /// Raised by <see cref="Report"/> with a tracked ship that has entered a city's queue,
        /// and is still in it, and that city.
        /// </summary>
        public event Action<Ship, TPort> Queued;

        /// <summary>Starts reporting the arrivals of a ship.</summary>
        public void Track(Ship ship)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            ship.Arrived += Add;
            trackedShips.Add(ship);
        }

        /// <summary>
        /// Stops reporting the arrivals of a ship, the one it has not reported yet included.
        /// A ship that the docking still knows is reported again if it enters a port:
        /// remove it from the docking first.
        /// </summary>
        public void Forget(Ship ship)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            ship.Arrived -= Add;
            trackedShips.Remove(ship);
            arrivedShips.Remove(ship);
            queuedShips.Remove(ship);
        }

        /// <summary>Reports the arrivals since the last call. Call after the docking's update.</summary>
        public void Report()
        {
            // By index: a listener may cause another arrival, which is reported too.
            for (int i = 0; i < arrivedShips.Count; i++)
            {
                Ship ship = arrivedShips[i];

                // Its route ended at the anchorage, but it is not at its berth yet.
                if (docking.IsInCity(ship) && !docking.IsDocked(ship))
                {
                    continue;
                }

                Arrived?.Invoke(ship, docking.PortOf(ship));
            }

            arrivedShips.Clear();

            for (int i = 0; i < queuedShips.Count; i++)
            {
                Ship ship = queuedShips[i];

                if (docking.StateOf(ship) == HarbourState.Waiting)
                {
                    Queued?.Invoke(ship, docking.CityOf(ship));
                }
            }

            queuedShips.Clear();
        }

        private void OnCityStateChanged(Ship ship, TPort city)
        {
            if (trackedShips.Contains(ship)
                && docking.StateOf(ship) == HarbourState.Waiting
                && !queuedShips.Contains(ship))
            {
                queuedShips.Add(ship);
            }
        }

        // A ship that was at its anchorage when it was ordered to the port sails no route.
        private void OnDocked(Ship ship, TPort port)
        {
            Add(ship);
        }

        private void Add(Ship ship)
        {
            if (!arrivedShips.Contains(ship))
            {
                arrivedShips.Add(ship);
            }
        }
    }
}
