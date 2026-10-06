using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Runtime state of one ship: where it is, where it is going and which way it faces.
    /// Sails in a straight line at constant speed.
    /// </summary>
    public sealed class Ship
    {
        private readonly MapProjection projection;
        private readonly float speed;
        private Vector2 destination;

        /// <param name="position">Normalized map position; clamped to the map.</param>
        /// <param name="speed">World units per second.</param>
        public Ship(MapProjection projection, Vector2 position, float speed)
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
            Position = ClampToMap(position);
            Heading = CompassDirection.S;
        }

        /// <summary>Normalized map position.</summary>
        public Vector2 Position { get; private set; }

        public Vector2 WorldPosition => projection.NormalizedToWorld(Position);

        /// <summary>Normalized map position the ship is sailing to, or null when idle.</summary>
        public Vector2? Destination => IsMoving ? destination : (Vector2?)null;

        public bool IsMoving { get; private set; }

        public CompassDirection Heading { get; private set; }

        /// <summary>
        /// Orders the ship to a normalized map position, replacing any order in progress.
        /// The point is clamped to the map; a NaN point is ignored.
        /// </summary>
        public void SetDestination(Vector2 destination)
        {
            if (IsNaN(destination))
            {
                return;
            }

            Vector2 clamped = ClampToMap(destination);
            Vector2 toDestination = projection.NormalizedToWorld(clamped) - WorldPosition;

            // Compared per component: Vector2 equality is approximate and would drop a
            // very short trip.
            if (toDestination.x == 0f && toDestination.y == 0f)
            {
                IsMoving = false;
                return;
            }

            this.destination = clamped;
            IsMoving = true;
            Heading = CompassHeading.FromVector(toDestination, Heading);
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
            Vector2 toDestination = projection.NormalizedToWorld(destination) - worldPosition;
            float distance = toDestination.magnitude;
            float step = speed * deltaTime;

            // Also taken when the remaining distance underflows to zero.
            if (!(step < distance))
            {
                Position = destination;
                IsMoving = false;
                return;
            }

            Position = projection.WorldToNormalized(worldPosition + toDestination * (step / distance));
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
