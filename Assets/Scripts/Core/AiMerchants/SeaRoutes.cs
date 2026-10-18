using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>How long the route between two points of the map is, and whether it gets there.</summary>
    public static class SeaRoutes
    {
        /// <summary>
        /// Measures the route a ship sails from one point to another: over water with a
        /// pathfinder, otherwise in a straight line.
        /// </summary>
        /// <param name="navigation">Null for a map without a navigation mask.</param>
        /// <param name="waypoints">Working list, cleared and filled with the route.</param>
        /// <param name="length">The length of the route in world units.</param>
        /// <returns>
        /// False when the route does not end on <paramref name="to"/>: a point in another
        /// sea, a NaN point or a map without water.
        /// </returns>
        public static bool TryMeasure(
            NavigationPathfinder navigation,
            MapProjection projection,
            Vector2 from,
            Vector2 to,
            List<Vector2> waypoints,
            out float length)
        {
            if (projection == null)
            {
                throw new ArgumentNullException(nameof(projection));
            }

            if (waypoints == null)
            {
                throw new ArgumentNullException(nameof(waypoints));
            }

            length = 0f;
            waypoints.Clear();

            if (IsNaN(from) || IsNaN(to))
            {
                return false;
            }

            from = ClampToMap(from);
            to = ClampToMap(to);

            if (navigation == null)
            {
                waypoints.Add(to);
            }
            else if (!navigation.TryFindPath(from, to, waypoints))
            {
                return false;
            }

            // A route to another sea ends on the nearest shore of this one. Compared per
            // component: the pathfinder ends a route it can complete on the point itself.
            Vector2 end = waypoints.Count > 0 ? waypoints[waypoints.Count - 1] : from;

            if (end.x != to.x || end.y != to.y)
            {
                return false;
            }

            Vector2 previous = projection.NormalizedToWorld(from);

            foreach (Vector2 waypoint in waypoints)
            {
                Vector2 next = projection.NormalizedToWorld(waypoint);
                length += (next - previous).magnitude;
                previous = next;
            }

            return true;
        }

        private static Vector2 ClampToMap(Vector2 point)
        {
            return new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
        }

        private static bool IsNaN(Vector2 value)
        {
            return float.IsNaN(value.x) || float.IsNaN(value.y);
        }
    }
}
