using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Logical state of the world map camera: center position and orthographic size,
    /// always kept inside the map rectangle.
    /// </summary>
    public sealed class MapCameraModel
    {
        private readonly Rect mapRect;
        private readonly float maxZoomInOrthographicSize;
        private float aspect;

        /// <param name="mapRect">Map rectangle in world units.</param>
        /// <param name="aspect">Viewport width divided by viewport height.</param>
        /// <param name="maxZoomInOrthographicSize">Smallest orthographic size allowed.</param>
        public MapCameraModel(Rect mapRect, float aspect, float maxZoomInOrthographicSize)
        {
            if (!IsFinitePositive(mapRect.width) || !IsFinitePositive(mapRect.height))
            {
                throw new ArgumentException("Map rectangle must have a positive size.", nameof(mapRect));
            }

            if (!IsFinitePositive(aspect))
            {
                throw new ArgumentOutOfRangeException(nameof(aspect));
            }

            if (!IsFinitePositive(maxZoomInOrthographicSize))
            {
                throw new ArgumentOutOfRangeException(nameof(maxZoomInOrthographicSize));
            }

            this.mapRect = mapRect;
            this.aspect = aspect;
            this.maxZoomInOrthographicSize = maxZoomInOrthographicSize;

            Position = mapRect.center;
            OrthographicSize = MaxOrthographicSize;
        }

        public Vector2 Position { get; private set; }

        public float OrthographicSize { get; private set; }

        /// <summary>Largest size at which the view still fits inside the map on both axes.</summary>
        public float MaxOrthographicSize
        {
            get { return Mathf.Min(mapRect.height * 0.5f, mapRect.width / (2f * aspect)); }
        }

        public float MinOrthographicSize
        {
            get { return Mathf.Min(maxZoomInOrthographicSize, MaxOrthographicSize); }
        }

        public void SetAspect(float newAspect)
        {
            if (!IsFinitePositive(newAspect))
            {
                throw new ArgumentOutOfRangeException(nameof(newAspect));
            }

            aspect = newAspect;
            OrthographicSize = Mathf.Clamp(OrthographicSize, MinOrthographicSize, MaxOrthographicSize);
            ClampPosition();
        }

        public void Pan(Vector2 worldDelta)
        {
            if (!IsFinite(worldDelta))
            {
                return;
            }

            Position += worldDelta;
            ClampPosition();
        }

        /// <summary>
        /// Multiplies the orthographic size by <paramref name="factor"/> (below 1 zooms in),
        /// keeping <paramref name="anchorWorld"/> at the same place in the viewport.
        /// </summary>
        public void Zoom(float factor, Vector2 anchorWorld)
        {
            if (!IsFinitePositive(factor) || !IsFinite(anchorWorld))
            {
                return;
            }

            float newSize = Mathf.Clamp(OrthographicSize * factor, MinOrthographicSize, MaxOrthographicSize);
            float ratio = newSize / OrthographicSize;

            Position = anchorWorld + (Position - anchorWorld) * ratio;
            OrthographicSize = newSize;
            ClampPosition();
        }

        /// <summary>Shows as much of the map as the view can.</summary>
        public void ZoomOutFully()
        {
            OrthographicSize = MaxOrthographicSize;
            ClampPosition();
        }

        /// <summary>
        /// Goes to a view, clamped to the size limits and to the map: the nearest valid
        /// view when the given one is not. An invalid view is ignored.
        /// </summary>
        public void SetView(Vector2 position, float orthographicSize)
        {
            if (!IsFinite(position) || !IsFinitePositive(orthographicSize))
            {
                return;
            }

            OrthographicSize = Mathf.Clamp(orthographicSize, MinOrthographicSize, MaxOrthographicSize);
            Position = position;
            ClampPosition();
        }

        /// <summary>
        /// Goes to the closest view of a point: centered on it as far as the map allows.
        /// An invalid position is ignored.
        /// </summary>
        public void ZoomInFullyOn(Vector2 position)
        {
            SetView(position, MinOrthographicSize);
        }

        /// <summary>
        /// World position of a viewport point, where (0,0) is the bottom-left corner
        /// of the view and (1,1) the top-right.
        /// </summary>
        public Vector2 ViewportToWorld(Vector2 viewportPoint)
        {
            return Position + new Vector2(
                (viewportPoint.x - 0.5f) * 2f * OrthographicSize * aspect,
                (viewportPoint.y - 0.5f) * 2f * OrthographicSize);
        }

        public float WorldUnitsPerPixel(float screenHeightPixels)
        {
            if (!IsFinitePositive(screenHeightPixels))
            {
                throw new ArgumentOutOfRangeException(nameof(screenHeightPixels));
            }

            return 2f * OrthographicSize / screenHeightPixels;
        }

        private void ClampPosition()
        {
            float halfHeight = OrthographicSize;
            float halfWidth = OrthographicSize * aspect;

            Position = new Vector2(
                ClampAxis(Position.x, mapRect.xMin + halfWidth, mapRect.xMax - halfWidth),
                ClampAxis(Position.y, mapRect.yMin + halfHeight, mapRect.yMax - halfHeight));
        }

        // When the view is as large as the map on an axis, rounding can make min exceed max.
        private static float ClampAxis(float value, float min, float max)
        {
            return min >= max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }
    }
}
