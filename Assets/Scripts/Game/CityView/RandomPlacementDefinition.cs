using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Buildings take any place where they fit, with equal chance.</summary>
    [CreateAssetMenu(fileName = "RandomPlacement", menuName = "Dark Fantasy Merchant/Placement/Random")]
    public sealed class RandomPlacementDefinition : PlacementStrategyDefinition
    {
        public override IPlacementStrategy CreateStrategy(System.Random random)
        {
            return new RandomPlacement(random);
        }
    }
}
