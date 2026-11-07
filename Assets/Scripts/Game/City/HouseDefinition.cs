using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// A house the player can build: the class it shelters and how many of them. The house of a
    /// class is also the one the cities build of their own, without its gold.
    /// </summary>
    [CreateAssetMenu(fileName = "House", menuName = "Dark Fantasy Merchant/House")]
    public sealed class HouseDefinition : BuildingDefinition
    {
        [Tooltip("The class the house shelters. A class has exactly one house.")]
        [SerializeField] private SocialClass socialClass = SocialClass.Poor;

        [Tooltip("Inhabitants of the class the house shelters: 40 rich, 70 wealthy or 140 poor.")]
        [SerializeField, Min(1)] private int room = 140;

        public SocialClass SocialClass => socialClass;

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
