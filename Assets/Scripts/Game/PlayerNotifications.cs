using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the player's <see cref="NotificationFeed"/>, created before the components
    /// that post to it or show it, and ages it in real time.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerNotifications : MonoBehaviour
    {
        [Tooltip("Real seconds a notification is displayed before it fades out.")]
        [SerializeField] private float displaySeconds = 5f;

        [Tooltip("Real seconds the fade out lasts.")]
        [SerializeField] private float fadeSeconds = 1f;

        /// <summary>Null when the durations are invalid.</summary>
        public NotificationFeed Feed { get; private set; }

        public bool IsReady => Feed != null;

        // Awake, not Start: the other components look for the feed in their OnEnable.
        private void Awake()
        {
            // The negated comparisons also reject NaN.
            if (!(displaySeconds > 0f) || float.IsInfinity(displaySeconds)
                || !(fadeSeconds >= 0f) || float.IsInfinity(fadeSeconds))
            {
                Debug.LogError(
                    $"PlayerNotifications has invalid durations (display {displaySeconds}, fade {fadeSeconds}).",
                    this);
                return;
            }

            Feed = new NotificationFeed(displaySeconds, fadeSeconds);
        }

        private void Update()
        {
            // Real time, not the world clock's: a notification is read by the player, in
            // fast forward too.
            Feed?.Advance(Time.deltaTime);
        }
    }
}
