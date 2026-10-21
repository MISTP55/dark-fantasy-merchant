namespace DarkFantasyMerchant.Game
{
    /// <summary>What a good is counted, priced and stocked in.</summary>
    public enum GoodUnit
    {
        // The first, so that a new good is counted in barrels until it is said otherwise.
        Barrel,

        /// <summary>The larger load: it takes the economy's barrels per bushel of room in a hold.</summary>
        Bushel,
    }
}
