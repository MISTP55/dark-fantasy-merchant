namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// One message of a <see cref="NotificationFeed"/>, which ages it and fades it out.
    /// </summary>
    public sealed class Notification
    {
        internal Notification(string text)
        {
            Text = text;
            Opacity = 1f;
        }

        public string Text { get; }

        /// <summary>1 while the notification is displayed, then down to 0 as it fades out.</summary>
        public float Opacity { get; internal set; }

        /// <summary>Seconds since it was posted.</summary>
        internal float Age { get; set; }
    }
}
