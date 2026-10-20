namespace DarkFantasyMerchant.Core
{
    /// <summary>How fast the world's time passes. Any speed but <see cref="Normal"/> is a fast forward.</summary>
    public enum GameSpeed
    {
        Normal,
        FastForward,

        /// <summary>The debug tools' fast forward, much faster than the player's.</summary>
        Turbo,
    }
}
