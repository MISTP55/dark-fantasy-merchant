using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The trading post: the one building the player builds on the site reserved in each city, where goods are
    /// stored against a rent.
    /// </summary>
    [CreateAssetMenu(fileName = "TradingPost", menuName = "Dark Fantasy Merchant/Trading Post")]
    public sealed class TradingPostDefinition : BuildingDefinition
    {
        [Tooltip("Barrels of goods the trading post can store.")]
        [SerializeField, Min(0)] private int capacity = 2000;

        [Tooltip("Gold coins the player pays for each barrel stored, each week.")]
        [SerializeField, Min(0)] private long rentPerBarrel = 1;

        public int Capacity => capacity;

        public long RentPerBarrel => rentPerBarrel;

        public override bool TryValidate(out string problem)
        {
            if (!base.TryValidate(out problem))
            {
                return false;
            }

            if (capacity < 0)
            {
                problem = $"its capacity is {capacity}, under 0";
                return false;
            }

            if (rentPerBarrel < 0)
            {
                problem = $"its rent is {rentPerBarrel}, under 0";
                return false;
            }

            return true;
        }
    }
}
