using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Converts between normalized map coordinates ((0,0) bottom-left, (1,1) top-right)
    /// and world space. The map is centered on the world origin.
    /// </summary>
    public sealed class MapProjection
    {
        /// <param name="worldWidth">Width of the map in world units.</param>
        /// <param name="aspectRatio">Map width divided by map height.</param>
        public MapProjection(float worldWidth, float aspectRatio)
        {
            if (!IsFinitePositive(worldWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(worldWidth));
            }

            if (!IsFinitePositive(aspectRatio))
            {
                throw new ArgumentOutOfRangeException(nameof(aspectRatio));
            }

            float worldHeight = worldWidth / aspectRatio;
            WorldRect = new Rect(-worldWidth * 0.5f, -worldHeight * 0.5f, worldWidth, worldHeight);
        }

        public Rect WorldRect { get; }

        public Vector2 NormalizedToWorld(Vector2 normalized)
        {
            return new Vector2(
                WorldRect.xMin + normalized.x * WorldRect.width,
                WorldRect.yMin + normalized.y * WorldRect.height);
        }

        public Vector2 WorldToNormalized(Vector2 world)
        {
            return new Vector2(
                (world.x - WorldRect.xMin) / WorldRect.width,
                (world.y - WorldRect.yMin) / WorldRect.height);
        }

        public static bool IsInsideMap(Vector2 normalized)
        {
            return normalized.x >= 0f && normalized.x <= 1f
                && normalized.y >= 0f && normalized.y <= 1f;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsInfinity(value);
        }
    }
}
