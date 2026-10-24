using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The cells a building covers, in its Along orientation.</summary>
    public readonly struct Footprint
    {
        public Footprint(int width, int depth)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (depth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(depth));
            }

            Width = width;
            Depth = depth;
        }

        public int Width { get; }

        public int Depth { get; }

        /// <summary>A square looks the same in both orientations.</summary>
        public bool IsSquare => Width == Depth;

        /// <summary>The cells covered on x and y once turned.</summary>
        public Vector2Int SizeFor(BuildingOrientation orientation)
        {
            return orientation == BuildingOrientation.Along
                ? new Vector2Int(Width, Depth)
                : new Vector2Int(Depth, Width);
        }
    }
}
