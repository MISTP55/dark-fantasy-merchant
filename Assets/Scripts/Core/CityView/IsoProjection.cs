using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The isometric projection between grid cells and world positions: cell (0, 0) at the bottom, x towards the
    /// upper right, y towards the upper left. A grid seen from above with x to the east and y to the north is thus
    /// shown turned an eighth of a turn anticlockwise: its north is at the upper left, its east at the upper right.
    /// </summary>
    public static class IsoProjection
    {
        /// <summary>World units across a cell's diamond.</summary>
        public const float CellWidth = 1f;

        /// <summary>World units from the top to the bottom of a cell's diamond.</summary>
        public const float CellHeight = 0.5f;

        public static Vector2 CenterOf(Vector2Int cell)
        {
            return new Vector2((cell.x - cell.y) * CellWidth * 0.5f, (cell.x + cell.y) * CellHeight * 0.5f);
        }

        /// <summary>
        /// The world position of a point of the grid, between cells too: cell (x, y)'s centre is the point (x, y),
        /// so on whole points it is <see cref="CenterOf(Vector2Int)"/>.
        /// </summary>
        public static Vector2 PointOf(Vector2 gridPoint)
        {
            return new Vector2((gridPoint.x - gridPoint.y) * CellWidth * 0.5f, (gridPoint.x + gridPoint.y) * CellHeight * 0.5f);
        }

        /// <summary>The point of the grid at this world position: the exact inverse of <see cref="PointOf"/>.</summary>
        public static Vector2 GridPointAt(Vector2 world)
        {
            var difference = world.x / (CellWidth * 0.5f);
            var sum = world.y / (CellHeight * 0.5f);
            return new Vector2((sum + difference) * 0.5f, (sum - difference) * 0.5f);
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
            var sum = world.y / (CellHeight * 0.5f);
            return new Vector2Int(
                Mathf.FloorToInt((sum + difference) * 0.5f + 0.5f),
                Mathf.FloorToInt((sum - difference) * 0.5f + 0.5f));
        }

        /// <summary>
        /// The key that orders buildings for drawing: a larger key is drawn later. It is not a sortingOrder itself
        /// (it does not fit one): the city view ranks the placed buildings by this key and gives each sprite its rank.
        /// The nearer block is the lower on screen, of smaller x and y. The key is lexicographic: the centre of the block
        /// (twice the sum of the origin plus the sum of the size), the smaller the later, then, for same-shape
        /// neighbours that tie on the centre, the side of the diagonal the far corner is on, seen from the long side
        /// of the block (a square has no long side). Good for grids up to 1000 by 1000.
        /// </summary>
        /// <param name="size">The covered size, already turned (<see cref="Footprint.SizeFor"/>).</param>
        public static long DrawKeyOf(Vector2Int origin, Vector2Int size)
        {
            long centre = MaxCentre - (2 * (origin.x + origin.y) + size.x + size.y);
            long tieBreak = (origin.x + size.x - origin.y - size.y) * Mathf.Clamp(size.x - size.y, -1, 1);
            return centre * TieBreakSpan + tieBreak + TieBreakSpan / 2;
        }

        private const long TieBreakSpan = 1_000_000;

        // The largest centre of a block on a 1000 by 1000 grid: the keys stay positive.
        private const long MaxCentre = 4 * 1000;
    }
}
