using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Every ship of the map, the player's and the others, with its name, and which ports they lie in: a step of the
    /// world's time sails the ships at sea, then updates the docking, then reports the arrivals. A ship in a city
    /// keeps its route for when it is back at sea and is not on the map.
    /// </summary>
    public sealed class ShipFleet<TPort> where TPort : class
    {
        // What CityPicker skips: a ship in a city cannot be picked on the map.
        private static readonly Vector2 NotOnMap = new Vector2(float.NaN, float.NaN);

        private readonly ShipArrivals<TPort> arrivals;

        // By index, in the order the ships were added: a ship is added to and removed from all three at once.
        private readonly List<Ship> ships = new List<Ship>();
        private readonly List<Vector2> worldPositions = new List<Vector2>();
        private readonly List<string> names = new List<string>();

        private readonly List<Ship> playerShips = new List<Ship>();

        public ShipFleet(ShipDocking<TPort> docking)
        {
            Docking = docking ?? throw new ArgumentNullException(nameof(docking));
            arrivals = new ShipArrivals<TPort>(docking);
        }

        /// <summary>Ships in the cities, in their harbours and in port. Orders go through it.</summary>
        public ShipDocking<TPort> Docking { get; }

        public IReadOnlyList<Ship> Ships => ships;

        /// <summary>The ships of <see cref="Ships"/> that the player owns and orders about.</summary>
        public IReadOnlyList<Ship> PlayerShips => playerShips;

        /// <summary>
        /// World position of each entry of <see cref="Ships"/>, in the same order, as of the last
        /// <see cref="UpdatePositions"/> or <see cref="UpdatePositionOf"/>. NaN for a ship in a city.
        /// </summary>
        public IReadOnlyList<Vector2> WorldPositions => worldPositions;

        /// <summary>Raised by <see cref="Step"/> with a ship that has arrived and its port, or null when it stopped at sea.</summary>
        public event Action<Ship, TPort> Arrived
        {
            add => arrivals.Arrived += value;
            remove => arrivals.Arrived -= value;
        }

        /// <summary>Raised by <see cref="Step"/> with a ship that has entered a city's queue, and that city.</summary>
        public event Action<Ship, TPort> Queued
        {
            add => arrivals.Queued += value;
            remove => arrivals.Queued -= value;
        }

        /// <summary>
        /// Raised by <see cref="Step"/> with its seconds, before the ships sail it: what gives ships their orders for
        /// the step listens to it.
        /// </summary>
        public event Action<float> StepStarted;

        /// <summary>Adds a ship, whose arrivals are reported from now on.</summary>
        /// <param name="isPlayerShip">Whether the player owns it.</param>
        /// <exception cref="ArgumentException">The ship is in the fleet already.</exception>
        public void Add(Ship ship, string displayName, bool isPlayerShip)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (ships.Contains(ship))
            {
                throw new ArgumentException("The ship is in the fleet already.", nameof(ship));
            }

            arrivals.Track(ship);
            ships.Add(ship);
            worldPositions.Add(ship.WorldPosition);
            names.Add(displayName);

            if (isPlayerShip)
            {
                playerShips.Add(ship);
            }
        }

        /// <summary>
        /// Takes a ship out for good: it leaves its city, while still listed, so that whoever shows it there is told,
        /// and its arrivals are no longer reported.
        /// </summary>
        /// <returns>False for a ship that is not in the fleet.</returns>
        public bool Remove(Ship ship)
        {
            int index = ship != null ? ships.IndexOf(ship) : -1;

            if (index < 0)
            {
                return false;
            }

            Docking.Remove(ship);
            arrivals.Forget(ship);

            // Found again: a listener of the docking may have changed the fleet.
            index = ships.IndexOf(ship);

            if (index >= 0)
            {
                ships.RemoveAt(index);
                worldPositions.RemoveAt(index);
                names.RemoveAt(index);
            }

            playerShips.Remove(ship);
            return true;
        }

        /// <returns>The name of the ship, or null for a ship that is not in the fleet.</returns>
        public string NameOf(Ship ship)
        {
            int index = ship != null ? ships.IndexOf(ship) : -1;

            return index >= 0 ? names[index] : null;
        }

        public bool IsPlayerShip(Ship ship)
        {
            return ship != null && playerShips.Contains(ship);
        }

        /// <returns>Whether the ship is in the fleet: false once it was removed.</returns>
        public bool Contains(Ship ship)
        {
            return ship != null && ships.Contains(ship);
        }

        /// <summary>
        /// One step of the world's time: <see cref="StepStarted"/>, then every ship at sea sails it, then the docking
        /// lets into the cities the ships that have reached their anchorage and advances the harbours, then the
        /// arrivals are reported, their ports known.
        /// </summary>
        public void Step(float deltaTime)
        {
            StepStarted?.Invoke(deltaTime);

            // A ship in a city keeps its route for when it is back at sea: it does not sail it there.
            foreach (Ship ship in ships)
            {
                if (!Docking.IsInCity(ship))
                {
                    ship.Advance(deltaTime);
                }
            }

            Docking.Update(deltaTime);
            arrivals.Report();
        }

        /// <summary>Brings <see cref="WorldPositions"/> up to date, once all the steps of a frame are sailed.</summary>
        public void UpdatePositions()
        {
            for (int i = 0; i < ships.Count; i++)
            {
                worldPositions[i] = PositionOf(ships[i]);
            }
        }

        /// <summary>Brings the position of one ship up to date, as one that enters or leaves a city needs at once.</summary>
        public void UpdatePositionOf(Ship ship)
        {
            int index = ship != null ? ships.IndexOf(ship) : -1;

            if (index >= 0)
            {
                worldPositions[index] = PositionOf(ship);
            }
        }

        private Vector2 PositionOf(Ship ship)
        {
            return Docking.IsInCity(ship) ? NotOnMap : ship.WorldPosition;
        }
    }
}
