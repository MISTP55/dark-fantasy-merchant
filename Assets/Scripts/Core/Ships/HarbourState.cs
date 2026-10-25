namespace DarkFantasyMerchant.Core
{
    /// <summary>Where a ship stands in the traffic of a city's harbour.</summary>
    public enum HarbourState
    {
        /// <summary>Not in the harbour.</summary>
        AtSea,

        /// <summary>Queued for a berth, off the grid.</summary>
        Waiting,

        /// <summary>On the grid, sailing from the edge to its berth.</summary>
        Arriving,

        /// <summary>At its berth.</summary>
        Docked,

        /// <summary>On the grid, sailing from its berth to the edge.</summary>
        Leaving,
    }
}
