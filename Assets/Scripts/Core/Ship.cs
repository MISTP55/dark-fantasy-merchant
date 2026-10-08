using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Runtime state of one ship: where it is, the route it follows and which way it
    /// faces. Sails at constant speed: over water only when it was given a pathfinder,
    /// otherwise in a straight line.
    /// </summary>
    public sealed class Ship
    {
        private readonly MapProjection projection;
        private readonly float speed;
        private readonly NavigationPathfinder navigation;

        // Waypoints not reached yet, the next one first.
        private readonly List<Vector2> waypoints = new List<Vector2>();

        // Where an order is worked out, so that an order that fails leaves the route alone.
        private readonly List<Vector2> orderedRoute = new List<Vector2>();

        /// <param name="position">
        /// Normalized map position; clamped to the map, then moved to the nearest water
        /// when the ship has a pathfinder.
        /// </param>
        /// <param name="speed">World units per second.</param>
        /// <param name="navigation">
        /// Where the ship can sail. Null for a map without a navigation mask: the ship
        /// then sails in a straight line.
        /// </param>
        public Ship(MapProjection projection, Vector2 position, float speed, NavigationPathfinder navigation = null)
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

        public bool IsMoving => waypoints.Count > 0;

        public CompassDirection Heading { get; private set; }

        /// <summary>
        /// Orders the ship to a normalized map position, replacing any order in progress.
        /// The point is clamped to the map; with a pathfinder, a point the ship cannot
        /// sail to is replaced by the nearest one it can. A NaN point is ignored, and so
        /// is an order for which there is no route.
        /// </summary>
        public void SetDestination(Vector2 destination)
        {
            if (IsNaN(destination))
            {
                return;
            }

            Vector2 clamped = ClampToMap(destination);

            orderedRoute.Clear();

            if (navigation == null)
            {
                orderedRoute.Add(clamped);
            }
            else if (!navigation.TryFindPath(Position, clamped, orderedRoute))
            {
                return;
            }

            waypoints.Clear();
            waypoints.AddRange(orderedRoute);
            SkipWaypointsAlreadyReached();
            FaceNextWaypoint();
        }

        public void Advance(float deltaTime)
        {
            // The negated comparison also rejects NaN.
            if (!IsMoving || !(deltaTime > 0f))
            {
                return;
            }

            // Steps are measured in world space: on a map that is not square, a
            // normalized step would be faster along one axis than the other.
            Vector2 worldPosition = WorldPosition;
            float step = speed * deltaTime;

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
                waypoints.RemoveAt(0);
                FaceNextWaypoint();
            }
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
