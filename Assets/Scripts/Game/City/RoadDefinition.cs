using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>A road: one cell of the road network. The cost is the one of a single cell.</summary>
    [CreateAssetMenu(fileName = "Road", menuName = "Dark Fantasy Merchant/Road")]
    public sealed class RoadDefinition : BuildingDefinition
    {
        public override bool TryValidate(out string problem)
        {
            if (!base.TryValidate(out problem))
            {
                return false;
            }

            if (FootprintSize != Vector2Int.one)
            {
                problem = $"its footprint is {FootprintSize.x} by {FootprintSize.y}, a road covers one cell";
            }

            return problem == null;
        }
    }
}
