using UnityEngine;
using UnityEngine.Serialization;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of a good that cities trade, in barrels. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "Good", menuName = "Dark Fantasy Merchant/Good")]
    public sealed class GoodDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("What the good is for. Only the stock of food makes the population of a city grow or decline.")]
        [SerializeField] private GoodCategory category;

        [Tooltip("Floor of the price: gold coins a barrel costs in a city that holds twice its target stock or more. From 1 to the base price.")]
        [SerializeField, Min(1)] private long minPrice = 5;

        [Tooltip("Gold coins a barrel costs in a city that holds its target stock.")]
        [SerializeField, Min(1)] private long basePrice = 10;

        [Tooltip("Ceiling of the price: gold coins a barrel costs in a city that has none left. The base price or more.")]
        [SerializeField, Min(1)] private long maxPrice = 25;

        [Tooltip("Barrels that 1 000 inhabitants consume in a week.")]
        [FormerlySerializedAs("consumptionPerThousand")]
        [SerializeField, Min(0f)] private double weeklyConsumptionPerThousand = 7d;

        public string DisplayName => displayName;

        public GoodCategory Category => category;

        public long MinPrice => minPrice;

        public long BasePrice => basePrice;

        public long MaxPrice => maxPrice;

        public double WeeklyConsumptionPerThousand => weeklyConsumptionPerThousand;
    }
}
