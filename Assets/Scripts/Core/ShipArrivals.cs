using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Tells when a ship has arrived, and in which port. A ship arrives when it reaches
    /// the end of its route, or when it enters a port without sailing because it was
    /// already at its anchorage. Arrivals are held until <see cref="Report"/>, which is
    /// called once the docking is up to date, so that the port of each one is known.
    /// </summary>
    public sealed class ShipArrivals<TPort> where TPort : class
    {
        private readonly ShipDocking<TPort> docking;

        // Ships that have arrived since the last report, each one once.
        private readonly List<Ship> arrivedShips = new List<Ship>();

        public ShipArrivals(ShipDocking<TPort> docking)
        {
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
            docking.Docked += OnDocked;
        }

        /// <summary>
        /// Raised by <see cref="Report"/> with a ship that has arrived and the port it
        /// lies in, or null when it stopped at sea.
        /// </summary>
        public event Action<Ship, TPort> Arrived;

        /// <summary>Starts reporting the arrivals of a ship.</summary>
        public void Track(Ship ship)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            ship.Arrived += Add;
        }

        /// <summary>Reports the arrivals since the last call. Call after the docking's update.</summary>
        public void Report()
        {
            // By index: a listener may cause another arrival, which is reported too.
            for (int i = 0; i < arrivedShips.Count; i++)
            {
                Ship ship = arrivedShips[i];
                Arrived?.Invoke(ship, docking.PortOf(ship));
            }

            arrivedShips.Clear();
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
