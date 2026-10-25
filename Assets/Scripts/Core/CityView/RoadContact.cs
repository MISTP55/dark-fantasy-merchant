namespace DarkFantasyMerchant.Core
{
    /// <summary>When a road touches a building or another road: two cells touch by a side, and maybe by a corner.</summary>
    public enum RoadContact
    {
        /// <summary>The 8 neighbours of a cell touch it.</summary>
        SidesAndCorners,

        /// <summary>Only the 4 cells that share a side with a cell touch it.</summary>
        SidesOnly,
    }
}
