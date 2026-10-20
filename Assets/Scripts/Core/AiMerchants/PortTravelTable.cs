using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The days a ship takes between every pair of ports, and the routes it sails between
    /// them, kept so that a ship that leaves a port does not look for its route again.
    /// Measured one pair at a time, so that the searches can be spread over several frames.
    /// </summary>
    public sealed class PortTravelTable
    {
        private readonly IReadOnlyList<Vector2> anchorages;
        private readonly NavigationPathfinder navigation;
        private readonly MapProjection projection;
        private readonly double worldUnitsPerDay;

        // Reused by every measure.
        private readonly List<Vector2> waypoints = new List<Vector2>();

        // The route from a port to another, not including the first port's anchorage. Null
        // for a port that cannot be reached, a pair not measured yet and a port to itself.
        private readonly Vector2[,][] routes;

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
            routes = new Vector2[count, count][];

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
                routes[first, second] = waypoints.ToArray();
                routes[second, first] = Reversed(waypoints, anchorages[first]);
            }

            second++;

            if (second >= PortCount)
            {
                first++;
                second = first + 1;
            }
        }

        /// <summary>
        /// The route a ship sails from a port's anchorage to another's, as
        /// <see cref="Ship.FollowRoute"/> takes it: the points to sail through in order,
        /// not including the anchorage it leaves. The same route both ways.
        /// </summary>
        /// <returns>
        /// Null for a port that cannot be reached, for a pair that is not measured yet and
        /// from a port to itself.
        /// </returns>
        public IReadOnlyList<Vector2> RouteBetween(int from, int to)
        {
            if (from < 0 || from >= PortCount)
            {
                throw new ArgumentOutOfRangeException(nameof(from));
            }

            if (to < 0 || to >= PortCount)
            {
                throw new ArgumentOutOfRangeException(nameof(to));
            }

            return routes[from, to];
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

        // The way back: the waypoints before the last in the other order, then the start.
        private static Vector2[] Reversed(List<Vector2> route, Vector2 start)
        {
            // Two ports on the same anchorage.
            if (route.Count == 0)
            {
                return new Vector2[0];
            }

            var reversed = new Vector2[route.Count];

            for (int i = 0; i < route.Count - 1; i++)
            {
                reversed[i] = route[route.Count - 2 - i];
            }

            reversed[route.Count - 1] = start;
            return reversed;
        }
    }
}
