using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// How the buildings choose their place on a city's grid. Each kind of strategy is an asset
    /// that makes the Core strategy; the catalogue of buildings holds the one the world uses.
    /// </summary>
    public abstract class PlacementStrategyDefinition : ScriptableObject
    {
        /// <param name="random">The source of chance of the world.</param>
        public abstract IPlacementStrategy CreateStrategy(System.Random random);
    }
}
