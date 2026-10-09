namespace DarkFantasyMerchant.Game
{
    /// <summary>How a city produces a good, against what it consumes of it.</summary>
    public enum GoodProduction
    {
        /// <summary>Not at all: the city depends on trade for it.</summary>
        None,

        /// <summary>Barely more than it consumes.</summary>
        Inefficient,

        /// <summary>More than it consumes, with a good margin.</summary>
        Efficient,
    }
}
