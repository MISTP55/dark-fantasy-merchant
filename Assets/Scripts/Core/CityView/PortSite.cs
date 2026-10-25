using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Where a port stands and which side its water rows are on.</summary>
    public readonly struct PortSite
    {
        public PortSite(Vector2Int origin, PortFacing facing)
        {
            Origin = origin;
            Facing = facing;
        }

        /// <summary>The covered cell of smallest x and y.</summary>
        public Vector2Int Origin { get; }

        public PortFacing Facing { get; }

        /// <summary>The place on the grid, its orientation following the facing.</summary>
        public GridPlace Place => new GridPlace(Origin, PortFacings.OrientationOf(Facing));
    }
}
