using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The cells a port covers, split by depth: its front rows, on the side it faces, stand on water, the others
    /// on land.
    /// </summary>
    public readonly struct PortFootprint
    {
        /// <param name="waterRows">The front rows on water, at least 1 and under the depth: a port has land behind.</param>
        public PortFootprint(Footprint footprint, int waterRows)
        {
            if (waterRows < 1 || waterRows >= footprint.Depth)
            {
                throw new ArgumentOutOfRangeException(nameof(waterRows));
            }

            Footprint = footprint;
            WaterRows = waterRows;
        }

        public Footprint Footprint { get; }

        public int WaterRows { get; }

        /// <summary>Whether a cell covered by the port at this site must be water; false for a land cell.</summary>
        public bool IsWater(PortSite site, Vector2Int cell)
        {
            Vector2Int size = Footprint.SizeFor(site.Place.Orientation);
            Vector2Int offset = cell - site.Origin;

            switch (site.Facing)
            {
                case PortFacing.North:
                    return offset.y >= size.y - WaterRows;
                case PortFacing.South:
                    return offset.y < WaterRows;
                case PortFacing.East:
                    return offset.x >= size.x - WaterRows;
                default:
                    return offset.x < WaterRows;
            }
        }
    }
}
