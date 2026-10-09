using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of a good that cities trade, in barrels. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "Good", menuName = "Dark Fantasy Merchant/Good")]
    public sealed class GoodDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("Gold coins a barrel costs in a city that holds its target stock.")]
        [SerializeField, Min(1)] private long basePrice = 10;

        [Tooltip("Barrels that 1 000 inhabitants consume in a day.")]
        [SerializeField, Min(0f)] private double consumptionPerThousand = 1d;

        public string DisplayName => displayName;

        public long BasePrice => basePrice;

        public double ConsumptionPerThousand => consumptionPerThousand;
    }
}
