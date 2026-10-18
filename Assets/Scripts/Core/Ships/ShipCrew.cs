using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The sailors aboard one ship, out of those it has room for. A ship needs sailors to
    /// sail: none and it does not move, few and it is slow, and half of its capacity is
    /// enough for its full speed.
    /// </summary>
    public sealed class ShipCrew
    {
        /// <summary>Share of the capacity up to which the ship is at its slowest.</summary>
        public const float SlowestOccupancy = 0.1f;

        /// <summary>Share of the capacity from which the ship sails at full speed.</summary>
        public const float FullSpeedOccupancy = 0.5f;

        /// <summary>Share of its speed a ship keeps at its slowest.</summary>
        public const float SlowestSpeedFactor = 0.5f;

        /// <param name="capacity">Sailors the ship has room for, at least one.</param>
        /// <param name="count">Sailors aboard, from none to the capacity.</param>
        public ShipCrew(int capacity, int count)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (count < 0 || count > capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Capacity = capacity;
            Count = count;
        }

        public int Capacity { get; }

        public int Count { get; }

        /// <summary>Share of the capacity that is manned, from 0 to 1.</summary>
        public float Occupancy => (float)Count / Capacity;

        /// <summary>
        /// Share of its speed the ship sails at: 0 without a sailor, the slowest up to
        /// <see cref="SlowestOccupancy"/>, then rising in a straight line to 1 at
        /// <see cref="FullSpeedOccupancy"/>.
        /// </summary>
        public float SpeedFactor
        {
            get
            {
                if (Count == 0)
                {
                    return 0f;
                }

                // InverseLerp clamps: the slowest below the range, full speed above it.
                float manned = Mathf.InverseLerp(SlowestOccupancy, FullSpeedOccupancy, Occupancy);

                return Mathf.Lerp(SlowestSpeedFactor, 1f, manned);
            }
        }
    }
}
