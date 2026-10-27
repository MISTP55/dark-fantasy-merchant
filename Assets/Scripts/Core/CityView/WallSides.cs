using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The sides of a wall's cell on which another wall stands. North is +y, east +x.</summary>
    [Flags]
    public enum WallSides
    {
        None = 0,
        East = 1,
        North = 2,
        West = 4,
        South = 8,
    }

    /// <summary>
    /// How a wall looks from its neighbours: a straight section between two walls on opposite sides, a tower
    /// otherwise (alone, at an end, a corner or a crossing), joined to each wall beside it.
    /// </summary>
    public static class WallShapes
    {
        /// <summary>Every combination of sides: a <see cref="WallSides"/> value is below it.</summary>
        public const int Count = 16;

        /// <summary>The sides of the cell on which a wall stands; corners never count.</summary>
        public static WallSides SidesOf(CityGrid grid, Vector2Int cell)
        {
            WallSides sides = WallSides.None;

            if (IsWall(grid, cell + Vector2Int.right))
            {
                sides |= WallSides.East;
            }

            if (IsWall(grid, cell + Vector2Int.up))
            {
                sides |= WallSides.North;
            }

            if (IsWall(grid, cell + Vector2Int.left))
            {
                sides |= WallSides.West;
            }

            if (IsWall(grid, cell + Vector2Int.down))
            {
                sides |= WallSides.South;
            }

            return sides;
        }

        /// <summary>Whether a wall with these neighbours is a straight section rather than a tower.</summary>
        public static bool IsStraight(WallSides sides)
        {
            return sides == (WallSides.East | WallSides.West) || sides == (WallSides.North | WallSides.South);
        }

        private static bool IsWall(CityGrid grid, Vector2Int cell)
        {
            PlacedBuilding building = grid.BuildingAt(cell);
            return building != null && building.Kind.IsWall;
        }
    }
}
