using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// A wall: one cell of the walls that close in, with the water, the space where a city's buildings stand. Placed by
    /// the city's layout; nobody builds it.
    /// </summary>
    [CreateAssetMenu(fileName = "Wall", menuName = "Dark Fantasy Merchant/Wall")]
    public sealed class WallDefinition : BuildingDefinition
    {
        public override bool TryValidate(out string problem)
        {
            if (!base.TryValidate(out problem))
            {
                return false;
            }

            if (FootprintSize != Vector2Int.one)
            {
                problem = $"its footprint is {FootprintSize.x} by {FootprintSize.y}, a wall covers one cell";
            }

            return problem == null;
        }
    }
}
