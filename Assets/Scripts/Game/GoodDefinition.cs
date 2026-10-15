using UnityEngine;
using UnityEngine.Serialization;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of a good that cities trade, in its unit. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "Good", menuName = "Dark Fantasy Merchant/Good")]
    public sealed class GoodDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("What the good is for. Only the stock of food makes the population of a city grow or decline.")]
        [SerializeField] private GoodCategory category;

        [Tooltip("What the good is counted, priced and stocked in. A bushel takes the economy's barrels per bushel of room in a hold.")]
        [SerializeField] private GoodUnit unit;

        [Tooltip("Gold coins a unit costs, before the factor of the city's stock (see the economy's price factors). It can be a fraction: prices are rounded to whole coins.")]
        [SerializeField, Min(0.01f)] private double basePrice = 10d;

        [Tooltip("Units of the good that 1 000 inhabitants consume in a week.")]
        [FormerlySerializedAs("consumptionPerThousand")]
        [SerializeField, Min(0f)] private double weeklyConsumptionPerThousand = 7d;

        [Tooltip("Most workers a city's workshop for the good can have. The same in every city.")]
        [SerializeField, Min(0)] private int maxWorkers = 60;

        [Tooltip("Units of the good a worker makes in a day in a city that produces the good efficiently.")]
        [SerializeField, Min(0f)] private double dailyOutputPerWorker = 0.1d;

        public string DisplayName => displayName;

        public GoodCategory Category => category;

        public GoodUnit Unit => unit;

        public double BasePrice => basePrice;

        public double WeeklyConsumptionPerThousand => weeklyConsumptionPerThousand;

        public int MaxWorkers => maxWorkers;

        public double DailyOutputPerWorker => dailyOutputPerWorker;
    }
}
