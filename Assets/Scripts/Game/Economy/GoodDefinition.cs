using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of a good that cities trade, in its unit. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "Good", menuName = "Dark Fantasy Merchant/Good")]
    public sealed class GoodDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("Shown before the good's name in the city's market. None: an empty place.")]
        [SerializeField] private Sprite icon;

        [Tooltip("What the good is for.")]
        [SerializeField] private GoodCategory category;

        [Tooltip("What the good is counted, priced and stocked in. A bushel takes the economy's barrels per bushel of room in a hold.")]
        [SerializeField] private GoodUnit unit;

        [Tooltip("Gold coins a unit costs, before the factor of the city's stock (see the economy's price factors). It can be a fraction: prices are rounded to whole coins.")]
        [SerializeField, Min(0.01f)] private double basePrice = 10d;

        [Header("Consumption per class, in a week, as in Patrician III (the beggars consume nothing)")]
        [Tooltip("Units of the good that 1 000 rich consume in a week.")]
        [SerializeField, Min(0f)] private double richWeeklyConsumptionPerThousand = 7d;

        [Tooltip("Units of the good that 1 000 wealthy consume in a week.")]
        [SerializeField, Min(0f)] private double wealthyWeeklyConsumptionPerThousand = 7d;

        [Tooltip("Units of the good that 1 000 poor consume in a week.")]
        [SerializeField, Min(0f)] private double poorWeeklyConsumptionPerThousand = 7d;

        [Header("Weight of the stock in the satisfaction, as in Patrician III (0: none)")]
        [Tooltip("How much the stock of the good weighs in the satisfaction of the rich.")]
        [SerializeField, Min(0)] private int richSatisfactionWeight;

        [Tooltip("How much the stock of the good weighs in the satisfaction of the wealthy.")]
        [SerializeField, Min(0)] private int wealthySatisfactionWeight;

        [Tooltip("How much the stock of the good weighs in the satisfaction of the poor.")]
        [SerializeField, Min(0)] private int poorSatisfactionWeight;

        [Tooltip("Most workers a city's workshop for the good can have. The same in every city.")]
        [SerializeField, Min(0)] private int maxWorkers = 60;

        [Tooltip("Units of the good a worker makes in a day in a city that produces the good efficiently.")]
        [SerializeField, Min(0f)] private double dailyOutputPerWorker = 0.1d;

        [Tooltip("Units of the good a worker of a merchant's workshop (the player's) makes in a day in a city that produces the good efficiently, before the bonus for several workshops. Patrician III's lacks the coefficient of the city's own workshop.")]
        [SerializeField, Min(0f)] private double merchantDailyOutputPerWorker = 0.1d;

        [Tooltip("How much the AI merchants care to carry the good, as in Patrician III: the gap of price factors between two cities is multiplied by it. 0: they never carry it.")]
        [SerializeField, Min(0)] private int aiWeight = 9;

        public string DisplayName => displayName;

        public Sprite Icon => icon;

        public GoodCategory Category => category;

        public GoodUnit Unit => unit;

        public double BasePrice => basePrice;

        /// <summary>Units 1 000 inhabitants of each class consume in a week.</summary>
        public PerClass<double> WeeklyConsumptionPerThousand => new PerClass<double>(
            richWeeklyConsumptionPerThousand, wealthyWeeklyConsumptionPerThousand, poorWeeklyConsumptionPerThousand);

        /// <summary>How much the stock weighs in the satisfaction of each class.</summary>
        public PerClass<int> SatisfactionWeights => new PerClass<int>(richSatisfactionWeight, wealthySatisfactionWeight, poorSatisfactionWeight);

        public int MaxWorkers => maxWorkers;

        public double DailyOutputPerWorker => dailyOutputPerWorker;

        public double MerchantDailyOutputPerWorker => merchantDailyOutputPerWorker;

        public int AiWeight => aiWeight;
    }
}
