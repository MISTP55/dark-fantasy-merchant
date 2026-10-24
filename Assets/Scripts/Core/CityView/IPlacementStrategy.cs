using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Chooses where a building goes on the grid.</summary>
    public interface IPlacementStrategy
    {
        /// <param name="roads">Cleared, then filled with the roads to lay with the building, if any.</param>
        /// <returns>False when the footprint fits nowhere.</returns>
        bool TryChoose(CityGrid grid, Footprint footprint, out GridPlace place, List<Vector2Int> roads);
    }
}
