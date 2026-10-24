using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Buildings go where they need the fewest roads to reach the road network, with equal chance among those.</summary>
    [CreateAssetMenu(fileName = "RoadsidePlacement", menuName = "Dark Fantasy Merchant/Placement/Roadside")]
    public sealed class RoadsidePlacementDefinition : PlacementStrategyDefinition
    {
        public override IPlacementStrategy CreateStrategy(System.Random random)
        {
            return new RoadsidePlacement(random);
        }
    }
}
