using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Turns a direction of travel into one of the eight headings.</summary>
    public static class CompassHeading
    {
        private const int DirectionCount = 8;
        private const float SectorDegrees = 360f / DirectionCount;

        /// <returns>
        /// The heading whose 45-degree sector contains <paramref name="direction"/>, or
        /// <paramref name="fallback"/> for a zero or NaN vector.
        /// </returns>
        public static CompassDirection FromVector(Vector2 direction, CompassDirection fallback)
        {
            // Compared per component: Vector2 equality is approximate and would treat a
            // very short vector as zero.
            if (direction.x == 0f && direction.y == 0f)
            {
                return fallback;
            }

            // Clockwise angle from north, hence x before y.
            float degrees = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

            if (float.IsNaN(degrees))
            {
                return fallback;
            }

            int sector = Mathf.RoundToInt(degrees / SectorDegrees);
            return (CompassDirection)((sector % DirectionCount + DirectionCount) % DirectionCount);
        }
    }
}
