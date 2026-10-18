using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Runtime state of one ship: where it is, the route it follows, which way it
    /// faces, its crew and its cargo. Sails at constant speed, the share of its own that its crew allows: over
    /// water only when it was given a pathfinder, otherwise in a straight line.
    /// </summary>
    public sealed class Ship
    {
        private readonly MapProjection projection;
        private readonly float speed;
        private readonly NavigationPathfinder navigation;

        // Waypoints not reached yet, the next one first.
        private readonly List<Vector2> waypoints = new List<Vector2>();

        // Where the current route started, then the waypoints reached since.
        private readonly List<Vector2> sailedWaypoints = new List<Vector2>();

        // Where an order is worked out, so that an order that fails leaves the route alone.
        private readonly List<Vector2> orderedRoute = new List<Vector2>();

        /// <param name="position">
        /// Normalized map position; clamped to the map, then moved to the nearest water
        /// when the ship has a pathfinder.
        /// </param>
        /// <param name="speed">World units per second, with a crew that sails it at full speed.</param>
        /// <param name="crew">The sailors aboard; the ship does not move without one.</param>
        /// <param name="navigation">
        /// Where the ship can sail. Null for a map without a navigation mask: the ship
        /// then sails in a straight line.
        /// </param>
        /// <param name="cargo">The ship's hold. Null for a ship that carries nothing.</param>
        public Ship(
            MapProjection projection,
            Vector2 position,
            float speed,
            ShipCrew crew,
            NavigationPathfinder navigation = null,
            CargoHold cargo = null)
        {
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));

            if (IsNaN(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            // The negated comparison also rejects NaN.
            if (!(speed > 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }

            Crew = crew ?? throw new ArgumentNullException(nameof(crew));
            Cargo = cargo ?? new CargoHold(0);

            this.speed = speed;
            this.navigation = navigation;
            Position = ClampToMap(position);
            Heading = CompassDirection.S;

            // Cities are on land: a ship that starts on one starts on the water beside it.
            if (navigation != null && navigation.TryGetNearestNavigable(Position, out Vector2 onWater))
            {
                Position = onWater;
            }
        }

        /// <summary>The sailors aboard, who set how much of its speed the ship sails at.</summary>
        public ShipCrew Crew { get; }

        /// <summary>The goods aboard. A ship that was given no hold has one without room.</summary>
        public CargoHold Cargo { get; }

        /// <summary>Normalized map position.</summary>
        public Vector2 Position { get; private set; }

        public Vector2 WorldPosition => projection.NormalizedToWorld(Position);

        /// <summary>
        /// Normalized map position where the ship's route ends, or null when idle. With a
        /// pathfinder it can differ from the point that was ordered.
        /// </summary>
        public Vector2? Destination => IsMoving ? waypoints[waypoints.Count - 1] : (Vector2?)null;

        /// <summary>
        /// Normalized map positions the ship has yet to sail through, the next one first.
        /// Empty when idle.
        /// </summary>
        public IReadOnlyList<Vector2> RemainingWaypoints => waypoints;

        /// <summary>
        /// Normalized map positions the ship has sailed through on its current route:
        /// where it was when it was ordered, then the waypoints it reached. The ship's
        /// own position is not in it. Empty when idle, and started again by every order.
        /// </summary>
        public IReadOnlyList<Vector2> SailedWaypoints => sailedWaypoints;

        public bool IsMoving => waypoints.Count > 0;

        public CompassDirection Heading { get; private set; }

        /// <summary>
        /// Raised with the ship when it reaches the end of its route; it is idle by then.
        /// An order that replaces the route does not raise it.
        /// </summary>
        public event Action<Ship> Arrived;

        /// <summary>
        /// Orders the ship to a normalized map position, replacing any order in progress.
        /// The point is clamped to the map; with a pathfinder, a point the ship cannot
        /// sail to is replaced by the nearest one it can. A NaN point is ignored, and so
        /// is an order for which there is no route.
        /// </summary>
        /// <returns>False when the order was ignored, which leaves the route as it was.</returns>
        public bool SetDestination(Vector2 destination)
        {
            if (IsNaN(destination))
            {
                return false;
            }

            Vector2 clamped = ClampToMap(destination);

            orderedRoute.Clear();

            if (navigation == null)
            {
                orderedRoute.Add(clamped);
            }
            else if (!navigation.TryFindPath(Position, clamped, orderedRoute))
            {
                return false;
            }

            waypoints.Clear();
            waypoints.AddRange(orderedRoute);
            SkipWaypointsAlreadyReached();
            FaceNextWaypoint();

            sailedWaypoints.Clear();

            if (IsMoving)
            {
                sailedWaypoints.Add(Position);
            }

            return true;
        }

        /// <summary>
        /// Where a ship lies when it is at a point of the map, a city for instance: the
        /// point itself, clamped to the map, or the nearest water when the ship has a
        /// pathfinder. That water can be in a sea this ship cannot sail to.
        /// </summary>
        public Vector2 AnchorageAt(Vector2 point)
        {
            Vector2 clamped = ClampToMap(point);

            return navigation != null && navigation.TryGetNearestNavigable(clamped, out Vector2 onWater)
                ? onWater
                : clamped;
        }

        public void Advance(float deltaTime)
        {
            // The negated comparison also rejects NaN.
            if (!IsMoving || !(deltaTime > 0f))
            {
                return;
            }

            float step = speed * Crew.SpeedFactor * deltaTime;

            // A ship without a sailor does not move, and keeps its route. The negated
            // comparison also rejects the NaN of an infinite step that nobody sails.
            if (!(step > 0f))
            {
                return;
            }

            // Steps are measured in world space: on a map that is not square, a
            // normalized step would be faster along one axis than the other.
            Vector2 worldPosition = WorldPosition;

            while (waypoints.Count > 0)
            {
                Vector2 toWaypoint = projection.NormalizedToWorld(waypoints[0]) - worldPosition;
                float distance = toWaypoint.magnitude;

                if (step < distance)
                {
                    Position = projection.WorldToNormalized(worldPosition + toWaypoint * (step / distance));
                    return;
                }

                // Also taken when the remaining distance underflows to zero. What is
                // left of the step is spent on the next leg, so that the ship does not
                // slow down at a turn.
                Position = waypoints[0];
                worldPosition = WorldPosition;
                step -= distance;
                sailedWaypoints.Add(waypoints[0]);
                waypoints.RemoveAt(0);
                FaceNextWaypoint();
            }

            // The route is over: an idle ship has none, sailed or not.
            sailedWaypoints.Clear();
            Arrived?.Invoke(this);
        }

        private void SkipWaypointsAlreadyReached()
        {
            while (waypoints.Count > 0)
            {
                Vector2 toWaypoint = projection.NormalizedToWorld(waypoints[0]) - WorldPosition;

                // Compared per component: Vector2 equality is approximate and would drop
                // a very short trip.
                if (toWaypoint.x != 0f || toWaypoint.y != 0f)
                {
                    return;
                }

                waypoints.RemoveAt(0);
            }
        }

        /// <summary>Turns the ship towards its next waypoint; keeps its heading when idle.</summary>
        private void FaceNextWaypoint()
        {
            if (waypoints.Count > 0)
            {
                Vector2 toWaypoint = projection.NormalizedToWorld(waypoints[0]) - WorldPosition;
                Heading = CompassHeading.FromVector(toWaypoint, Heading);
            }
        }

        private static Vector2 ClampToMap(Vector2 normalized)
        {
            return new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        }

        private static bool IsNaN(Vector2 value)
        {
            return float.IsNaN(value.x) || float.IsNaN(value.y);
        }
    }
}
