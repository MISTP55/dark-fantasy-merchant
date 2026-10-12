using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The days a ship takes between every pair of ports. Measured one pair at a time,
    /// so that the searches can be spread over several frames.
    /// </summary>
    public sealed class PortTravelTable
    {
        private readonly IReadOnlyList<Vector2> anchorages;
        private readonly NavigationPathfinder navigation;
        private readonly MapProjection projection;
        private readonly double worldUnitsPerDay;

        // Reused by every measure.
        private readonly List<Vector2> waypoints = new List<Vector2>();

        // The next pair to measure: first is below second.
        private int first;
        private int second = 1;

        /// <param name="anchorages">Where a ship lies at each port, in normalized map positions.</param>
        /// <param name="navigation">Null for a map without a navigation mask.</param>
        /// <param name="worldUnitsPerDay">What the ships sail in a day.</param>
        public PortTravelTable(
            IReadOnlyList<Vector2> anchorages,
            NavigationPathfinder navigation,
            MapProjection projection,
            double worldUnitsPerDay)
        {
            this.anchorages = anchorages ?? throw new ArgumentNullException(nameof(anchorages));
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));

            // The negated comparison also rejects NaN.
            if (!(worldUnitsPerDay > 0d) || double.IsInfinity(worldUnitsPerDay))
            {
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerDay));
            }

            this.navigation = navigation;
            this.worldUnitsPerDay = worldUnitsPerDay;

            int count = anchorages.Count;
            Days = new double[count, count];

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    Days[i, j] = i == j ? 0d : double.PositiveInfinity;
                }
            }
        }

        public int PortCount => anchorages.Count;

        /// <summary>
        /// Days from a port to another, the same both ways. Infinity for a port that
        /// cannot be reached, and for a pair that is not measured yet.
        /// </summary>
        public double[,] Days { get; }

        public bool IsComplete => first >= PortCount - 1;

        /// <summary>Measures one more pair of ports. Does nothing once complete.</summary>
        public void MeasureNext()
        {
            if (IsComplete)
            {
                return;
            }

            if (SeaRoutes.TryMeasure(navigation, projection, anchorages[first], anchorages[second], waypoints, out float length))
            {
                double days = length / worldUnitsPerDay;
                Days[first, second] = days;
                Days[second, first] = days;
            }

            second++;

            if (second >= PortCount)
            {
                first++;
                second = first + 1;
            }
        }

        /// <returns>Whether a ship can sail from this port to at least one other, among the pairs measured.</returns>
        public bool ReachesAnother(int port)
        {
            if (port < 0 || port >= PortCount)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            for (int other = 0; other < PortCount; other++)
            {
                if (other != port && !double.IsInfinity(Days[port, other]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
