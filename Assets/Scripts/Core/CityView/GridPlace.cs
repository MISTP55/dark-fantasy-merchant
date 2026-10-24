using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Where and how a footprint lies on the grid.</summary>
    public readonly struct GridPlace
    {
        public GridPlace(Vector2Int origin, BuildingOrientation orientation)
        {
            Origin = origin;
            Orientation = orientation;
        }

        /// <summary>The covered cell of smallest x and y.</summary>
        public Vector2Int Origin { get; }

        public BuildingOrientation Orientation { get; }
    }
}
