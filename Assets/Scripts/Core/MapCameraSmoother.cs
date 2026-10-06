using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Displayed state of the world map camera, eased towards a target state.
    /// Position and size share one easing factor, which keeps a zoom anchor fixed
    /// in the viewport for the whole transition and keeps the view inside the map
    /// whenever both the start and the target are.
    /// </summary>
    public sealed class MapCameraSmoother
    {
        // Close enough to the target to stop easing and land on it exactly.
        private const float SnapDistance = 1e-4f;

        public MapCameraSmoother(Vector2 position, float orthographicSize)
        {
            Position = position;
            OrthographicSize = orthographicSize;
        }

        public Vector2 Position { get; private set; }

        public float OrthographicSize { get; private set; }

        public void SnapTo(Vector2 position, float orthographicSize)
        {
            Position = position;
            OrthographicSize = orthographicSize;
        }

        /// <summary>
        /// Moves the displayed state towards the target, independently of frame rate.
        /// </summary>
        /// <param name="smoothTime">
        /// Time, in seconds, to cover about 63% of the remaining distance. Zero or less disables easing.
        /// </param>
        public void Advance(Vector2 targetPosition, float targetOrthographicSize, float deltaTime, float smoothTime)
        {
            if (!IsFinite(targetPosition.x) || !IsFinite(targetPosition.y) || !IsFinite(targetOrthographicSize))
            {
                return;
            }

            // The negated comparison also rejects NaN.
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
            {
                return;
            }

            if (!(smoothTime > 0f))
            {
                SnapTo(targetPosition, targetOrthographicSize);
                return;
            }

            float factor = 1f - Mathf.Exp(-deltaTime / smoothTime);
            Vector2 position = Vector2.LerpUnclamped(Position, targetPosition, factor);
            float size = Mathf.LerpUnclamped(OrthographicSize, targetOrthographicSize, factor);

            bool hasArrived = (targetPosition - position).sqrMagnitude < SnapDistance * SnapDistance
                && Mathf.Abs(targetOrthographicSize - size) < SnapDistance;

            if (hasArrived)
            {
                SnapTo(targetPosition, targetOrthographicSize);
                return;
            }

            Position = position;
            OrthographicSize = size;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
