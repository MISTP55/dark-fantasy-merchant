using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The isometric projection between grid cells and world positions: cell (0, 0) at the top, x towards the
    /// lower right, y towards the lower left.
    /// </summary>
    public static class IsoProjection
    {
        /// <summary>World units across a cell's diamond.</summary>
        public const float CellWidth = 1f;

        /// <summary>World units from the top to the bottom of a cell's diamond.</summary>
        public const float CellHeight = 0.5f;

        public static Vector2 CenterOf(Vector2Int cell)
        {
            return new Vector2((cell.x - cell.y) * CellWidth * 0.5f, -(cell.x + cell.y) * CellHeight * 0.5f);
        }

        /// <summary>
        /// The centre of the cells a footprint covers, from this origin.
        /// <paramref name="size"/> is the covered size, already turned (<see cref="Footprint.SizeFor"/>).
        /// </summary>
        public static Vector2 CenterOf(Vector2Int origin, Vector2Int size)
        {
            var far = origin + size - Vector2Int.one;
            return (CenterOf(origin) + CenterOf(far)) * 0.5f;
        }

        /// <summary>The cell whose diamond holds this world position.</summary>
        public static Vector2Int CellAt(Vector2 world)
        {
            var difference = world.x / (CellWidth * 0.5f);
            var sum = -world.y / (CellHeight * 0.5f);
            return new Vector2Int(
                Mathf.FloorToInt((sum + difference) * 0.5f + 0.5f),
                Mathf.FloorToInt((sum - difference) * 0.5f + 0.5f));
        }

        /// <summary>
        /// The sorting order of a building: the further down the screen its centre is, the later it is drawn.
        /// The centre alone ties for two blocks of the same shape side by side, one in front of the other, so the
        /// tie is broken by the side of the diagonal the origin is on, seen from the long side of the block (a
        /// square has no long side). The tie-break is clamped: it only matters between neighbours, and the order
        /// has to stay in Unity's sorting-order range (a short) on grids up to 200 by 200.
        /// </summary>
        /// <param name="size">The covered size, already turned (<see cref="Footprint.SizeFor"/>).</param>
        public static int SortingOrderOf(Vector2Int origin, Vector2Int size)
        {
            var centre = 2 * (origin.x + origin.y) + size.x + size.y;
            var tieBreak = Mathf.Clamp((origin.y - origin.x) * Mathf.Clamp(size.x - size.y, -1, 1), -TieBreakReach, TieBreakReach);
            return centre * (2 * TieBreakReach + 2) + tieBreak + TieBreakReach;
        }

        private const int TieBreakReach = 7;
    }
}
