using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Tells a click from a drag for one pointer button: a press that moves farther than
    /// the threshold before release is a drag, otherwise it is a click.
    /// </summary>
    public sealed class PointerGesture
    {
        private readonly float sqrDragThreshold;
        private Vector2 pressPosition;
        private Vector2 lastPosition;

        public PointerGesture(float dragThresholdPixels)
        {
            // The negated comparison also rejects NaN.
            if (!(dragThresholdPixels >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(dragThresholdPixels));
            }

            sqrDragThreshold = dragThresholdPixels * dragThresholdPixels;
        }

        public bool IsPressed { get; private set; }

        public bool IsDragging { get; private set; }

        public void Press(Vector2 screenPosition)
        {
            IsPressed = true;
            IsDragging = false;
            pressPosition = screenPosition;
            lastPosition = screenPosition;
        }

        /// <returns>Screen-pixel delta to apply as a drag; zero while not dragging.</returns>
        public Vector2 Move(Vector2 screenPosition)
        {
            if (!IsPressed)
            {
                return Vector2.zero;
            }

            if (!IsDragging)
            {
                if ((screenPosition - pressPosition).sqrMagnitude <= sqrDragThreshold)
                {
                    return Vector2.zero;
                }

                IsDragging = true;
            }

            Vector2 delta = screenPosition - lastPosition;
            lastPosition = screenPosition;
            return delta;
        }

        /// <returns>True when the gesture was a click.</returns>
        public bool Release()
        {
            bool isClick = IsPressed && !IsDragging;
            Cancel();
            return isClick;
        }

        public void Cancel()
        {
            IsPressed = false;
            IsDragging = false;
        }
    }
}
