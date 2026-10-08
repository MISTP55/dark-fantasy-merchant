using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The messages shown to the player, the newest first. A notification is displayed
    /// for a while, fades out, then is removed.
    /// </summary>
    public sealed class NotificationFeed
    {
        private readonly float displaySeconds;
        private readonly float fadeSeconds;

        // The newest first, so the oldest, the next to be removed, is the last.
        private readonly List<Notification> notifications = new List<Notification>();

        /// <param name="displaySeconds">Seconds a notification is displayed before it fades out.</param>
        /// <param name="fadeSeconds">Seconds the fade out lasts. Zero removes it without a fade.</param>
        public NotificationFeed(float displaySeconds, float fadeSeconds)
        {
            // The negated comparisons also reject NaN.
            if (!(displaySeconds > 0f) || float.IsInfinity(displaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(displaySeconds));
            }

            if (!(fadeSeconds >= 0f) || float.IsInfinity(fadeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(fadeSeconds));
            }

            this.displaySeconds = displaySeconds;
            this.fadeSeconds = fadeSeconds;
        }

        /// <summary>The notifications on display, the newest first.</summary>
        public IReadOnlyList<Notification> Notifications => notifications;

        public event Action<Notification> Posted;

        /// <summary>Raised when a notification has faded out, the oldest first.</summary>
        public event Action<Notification> Removed;

        public Notification Post(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("A notification needs a text.", nameof(text));
            }

            var notification = new Notification(text);
            notifications.Insert(0, notification);
            Posted?.Invoke(notification);
            return notification;
        }

        public void Advance(float seconds)
        {
            // The negated comparison also rejects NaN.
            if (!(seconds > 0f))
            {
                return;
            }

            foreach (Notification notification in notifications)
            {
                notification.Age += seconds;
                notification.Opacity = OpacityAt(notification.Age);
            }

            // Read from the end on every turn: a listener may post a notification, which
            // goes to the front.
            while (notifications.Count > 0 && HasFadedOut(notifications[notifications.Count - 1]))
            {
                Notification oldest = notifications[notifications.Count - 1];
                notifications.RemoveAt(notifications.Count - 1);
                Removed?.Invoke(oldest);
            }
        }

        private bool HasFadedOut(Notification notification)
        {
            return notification.Age >= displaySeconds + fadeSeconds;
        }

        private float OpacityAt(float age)
        {
            if (age <= displaySeconds)
            {
                return 1f;
            }

            float fadedFor = age - displaySeconds;

            return fadedFor < fadeSeconds ? 1f - fadedFor / fadeSeconds : 0f;
        }
    }
}
