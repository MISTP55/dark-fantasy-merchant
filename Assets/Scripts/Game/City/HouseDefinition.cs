using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The house the player can build: how many it shelters. It is also the one the cities build
    /// of their own, without its gold.
    /// </summary>
    [CreateAssetMenu(fileName = "House", menuName = "Dark Fantasy Merchant/House")]
    public sealed class HouseDefinition : BuildingDefinition
    {
        [Tooltip("Inhabitants the house shelters: 140.")]
        [SerializeField, Min(1)] private int room = 140;

        public int Room => room;

        public override bool TryValidate(out string problem)
        {
            if (!base.TryValidate(out problem))
            {
                return false;
            }

            if (room < 1)
            {
                problem = "it has no room";
            }

            return problem == null;
        }
    }
}
