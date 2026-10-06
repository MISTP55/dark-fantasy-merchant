using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Finds which map position, if any, is under a point.</summary>
    public static class CityPicker
    {
        /// <returns>
        /// Index of the position nearest to <paramref name="point"/> within
        /// <paramref name="radius"/> (inclusive), or -1. Ties resolve to the lowest index.
        /// </returns>
        public static int PickNearest(IReadOnlyList<Vector2> positions, Vector2 point, float radius)
        {
            // The negated comparison also rejects a NaN radius.
            if (positions == null || !(radius >= 0f))
            {
                return -1;
            }

            int bestIndex = -1;
            float bestSqrDistance = radius * radius;

            for (int i = 0; i < positions.Count; i++)
            {
                float sqrDistance = (positions[i] - point).sqrMagnitude;

                // A NaN distance fails both comparisons and is skipped.
                bool isBetter = bestIndex < 0
                    ? sqrDistance <= bestSqrDistance
                    : sqrDistance < bestSqrDistance;

                if (isBetter)
                {
                    bestIndex = i;
                    bestSqrDistance = sqrDistance;
                }
            }

            return bestIndex;
        }
    }
}
