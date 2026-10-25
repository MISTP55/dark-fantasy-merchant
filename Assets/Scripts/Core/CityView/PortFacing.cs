namespace DarkFantasyMerchant.Core
{
    /// <summary>The side of a port its water rows are on: north is +y, east +x, south −y, west −x.</summary>
    public enum PortFacing
    {
        North,
        East,
        South,
        West,
    }

    public static class PortFacings
    {
        /// <summary>Facing north or south, a port lies Along (its width on x); east or west, Across.</summary>
        public static BuildingOrientation OrientationOf(PortFacing facing)
        {
            return facing == PortFacing.North || facing == PortFacing.South
                ? BuildingOrientation.Along
                : BuildingOrientation.Across;
        }

        /// <summary>A quarter turn: north, east, south, west, then north again.</summary>
        public static PortFacing Turned(PortFacing facing)
        {
            return (PortFacing)(((int)facing + 1) % 4);
        }
    }
}
