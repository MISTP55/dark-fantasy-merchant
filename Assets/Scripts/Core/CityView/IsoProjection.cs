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

        /// <summary>The centre of the cells a footprint of this size covers, from this origin.</summary>
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

        /// <summary>The sorting order of a building: the further down the screen its far cell is, the later it is drawn.</summary>
        public static int SortingOrderOf(Vector2Int origin, Vector2Int size)
        {
            var far = origin + size - Vector2Int.one;
            return far.x + far.y;
        }
    }
}
