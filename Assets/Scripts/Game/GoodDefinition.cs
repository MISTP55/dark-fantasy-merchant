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

        [Tooltip("Gold coins a barrel costs, before the factor of the city's stock (see the economy's price factors).")]
        [SerializeField, Min(1)] private long basePrice = 10;

        [Tooltip("Barrels that 1 000 inhabitants consume in a week.")]
        [FormerlySerializedAs("consumptionPerThousand")]
        [SerializeField, Min(0f)] private double weeklyConsumptionPerThousand = 7d;

        public string DisplayName => displayName;

        public GoodCategory Category => category;

        public long BasePrice => basePrice;

        public double WeeklyConsumptionPerThousand => weeklyConsumptionPerThousand;
    }
}
