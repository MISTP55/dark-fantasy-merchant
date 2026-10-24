namespace DarkFantasyMerchant.Game
{
    /// <summary>What a building being placed would be, which sets the colour of its ghost in the city view.</summary>
    public enum GhostState
    {
        /// <summary>It can stand and would be connected to the road network: green.</summary>
        Connected,

        /// <summary>It can stand but would not be connected: yellow.</summary>
        Unconnected,

        /// <summary>It cannot stand, or cannot be paid: red.</summary>
        Impossible,
    }
}
