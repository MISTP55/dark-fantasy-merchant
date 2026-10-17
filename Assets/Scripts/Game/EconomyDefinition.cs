using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the economy: the catalogue of goods, in the order they are
    /// listed everywhere, and the settings of production and prices. Holds no runtime
    /// state: the stocks are in a <see cref="CityMarket"/> per city.
    /// </summary>
    [CreateAssetMenu(fileName = "Economy", menuName = "Dark Fantasy Merchant/Economy")]
    public sealed class EconomyDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        [Tooltip("The goods of the game. Their order is their order on screen. A good that is not active does not exist in the game.")]
        [SerializeField] private List<CatalogueGood> goods = new List<CatalogueGood>();

        // The active goods, worked out on first use and again after every change of the file.
        private List<GoodDefinition> activeGoods;

        [Tooltip("Days of its consumption a city holds of every good when the game starts, whether it produces it or not.")]
        [SerializeField, Min(0f)] private double startingStockDays = 7d;

        [Tooltip("Barrels of room a bushel takes in a hold: 10 is Patrician III's load.")]
        [SerializeField, Min(1)] private int barrelsPerBushel = 10;

        [Header("Workshops (a good's maximum of workers and output per worker are the good's own)")]
        [Tooltip("Share of a good's output per worker in a city that produces it inefficiently: 0.75 is Patrician III's 768 / 1024.")]
        [SerializeField, Range(0f, 1f)] private double inefficientEfficiency = 0.75d;

        [Tooltip("Workers a workshop hires in a day while the stock of its good is at T0 or under.")]
        [SerializeField, Min(0)] private int hiredUpToT0 = 16;

        [Tooltip("Workers hired in a day while the stock is above T0, up to T1.")]
        [SerializeField, Min(0)] private int hiredUpToT1 = 8;

        [Tooltip("Workers hired in a day while the stock is above T1, up to T2.")]
        [SerializeField, Min(0)] private int hiredUpToT2 = 4;

        [Tooltip("Workers hired in a day while the stock is above T2, up to T3.")]
        [SerializeField, Min(0)] private int hiredUpToT3 = 1;

        [Tooltip("Workers a workshop keeps above T3, where it produces nothing.")]
        [SerializeField, Min(0)] private int workersAboveT3 = 1;

        [Header("Start of a game")]
        [Tooltip("Workers every city starts with, spread evenly over its workshops: 250 is Patrician III's.")]
        [SerializeField, Min(0)] private int startingWorkforce = 250;

        [Tooltip("Inhabitants per starting worker: 72 / 17 is Patrician III's 4, plus a seventeenth of beggars.")]
        [SerializeField, Min(0f)] private double inhabitantsPerWorker = 72d / 17d;

        [Header("Stock thresholds T0 to T3 of a good in a city, at which its prices are given")]
        [Tooltip("T0: days of the city's consumption.")]
        [SerializeField, Min(0f)] private double t0Days = 7d;

        [Tooltip("T1: T0 plus these days of consumption.")]
        [SerializeField, Min(0f)] private double t1AddedDays = 14d;

        [Tooltip("T2: T1 plus these days of the city's production; T2 is T1 for a good the city does not produce. T3 is T2 plus T0.")]
        [SerializeField, Min(0f)] private double t2ProductionDays = 10d;

        [Tooltip("The lowest T0, in units of the good. Above 0.")]
        [SerializeField, Min(0f)] private double t0Floor = 5d;

        [Tooltip("The lowest T1, in units of the good. At least the T0 floor.")]
        [SerializeField, Min(0f)] private double t1Floor = 10d;

        [Tooltip("The lowest T2, in units of the good. At least the T1 floor.")]
        [SerializeField, Min(0f)] private double t2Floor = 15d;

        [Tooltip("The lowest T3, in units of the good. At least the T2 floor.")]
        [SerializeField, Min(0f)] private double t3Floor = 20d;

        [Header("Prices, as factors of a good's base price")]
        [Tooltip("What the player pays when buying from a city.")]
        [SerializeField] private PriceFactors buyFactors = new PriceFactors(4d, 1.5d, 1d, 0.8d, 0.6d);

        [Tooltip("What the player is paid when selling to a city. At no point above the buy factors.")]
        [SerializeField] private PriceFactors sellFactors = new PriceFactors(2d, 1.4d, 1d, 0.7d, 0.5d);

        [Header("Population, by the days of its consumption a city holds of food (the average of its foods)")]
        [Tooltip("Days of food at which the population does not change. Under them it declines.")]
        [SerializeField, Min(0f)] private double stableFoodDays = 7d;

        [Tooltip("Days of food at which the population grows the fastest. A food is not counted for more.")]
        [SerializeField, Min(0f)] private double abundantFoodDays = 14d;

        [Tooltip("Growth of the population over a month at the abundant days, as a share: 0.05 is +5 %.")]
        [SerializeField, Min(0f)] private double maxMonthlyGrowth = 0.05d;

        [Tooltip("Decline of the population over a month with no food at all, as a share: 0.1 is -10 %.")]
        [SerializeField, Range(0f, 1f)] private double maxMonthlyDecline = 0.1d;

        /// <summary>The active goods of the catalogue, in its order: the only goods of the game.</summary>
        public IReadOnlyList<GoodDefinition> Goods => activeGoods ??= ActiveGoodsOfTheCatalogue();

        /// <summary>Every good of the catalogue, active or not, for the Editor and the content checks.</summary>
        public IReadOnlyList<CatalogueGood> Catalogue => goods;

        public double StartingStockDays => startingStockDays;

        public int BarrelsPerBushel => barrelsPerBushel;

        public double InefficientEfficiency => inefficientEfficiency;

        public int HiredUpToT0 => hiredUpToT0;

        public int HiredUpToT1 => hiredUpToT1;

        public int HiredUpToT2 => hiredUpToT2;

        public int HiredUpToT3 => hiredUpToT3;

        public int WorkersAboveT3 => workersAboveT3;

        public int StartingWorkforce => startingWorkforce;

        public double InhabitantsPerWorker => inhabitantsPerWorker;

        /// <summary>The inhabitants of every city when the game starts: the starting workforce, times the inhabitants per worker.</summary>
        public double StartingPopulation => startingWorkforce * inhabitantsPerWorker;

        public double T0Days => t0Days;

        public double T1AddedDays => t1AddedDays;

        public double T2ProductionDays => t2ProductionDays;

        public double T0Floor => t0Floor;

        public double T1Floor => t1Floor;

        public double T2Floor => t2Floor;

        public double T3Floor => t3Floor;

        public PriceFactors BuyFactors => buyFactors;

        public PriceFactors SellFactors => sellFactors;

        public double StableFoodDays => stableFoodDays;

        public double AbundantFoodDays => abundantFoodDays;

        public double MaxMonthlyGrowth => maxMonthlyGrowth;

        public double MaxMonthlyDecline => maxMonthlyDecline;

        /// <returns>
        /// The index of a good among the active goods, which is what Core knows it by, or -1,
        /// also for a good of the catalogue that is not active.
        /// </returns>
        public int IndexOf(GoodDefinition good)
        {
            return good != null ? ((List<GoodDefinition>)Goods).IndexOf(good) : -1;
        }

        /// <summary>Whether the good is listed in the catalogue, active or not.</summary>
        public bool IsInCatalogue(GoodDefinition good)
        {
            return good != null && goods.Exists(entry => entry.Good == good);
        }

        /// <summary>
        /// Whether the catalogue can be used: at least one good, none missing, none twice, active
        /// or not, and at least one active.
        /// </summary>
        /// <param name="problem">What is wrong, for the console; null when nothing is.</param>
        public bool TryValidate(out string problem)
        {
            problem = null;

            if (goods.Count == 0)
            {
                problem = "it has no good";
                return false;
            }

            for (int i = 0; i < goods.Count; i++)
            {
                GoodDefinition good = goods[i].Good;

                if (good == null)
                {
                    problem = $"its good {i} is empty";
                    return false;
                }

                if (goods.FindIndex(entry => entry.Good == good) != i)
                {
                    problem = $"'{good.name}' is listed twice";
                    return false;
                }
            }

            if (Goods.Count == 0)
            {
                problem = "none of its goods is active";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Whether the price factors, the stock thresholds and the workforce can be used: the
        /// Inspector lets through values that the Create methods refuse.
        /// </summary>
        /// <param name="problem">Which setting is wrong and why, for the console; null when none is.</param>
        public bool TryValidateSettings(out string problem)
        {
            problem = ProblemOf(() => CreateGoodUnits(), "the barrels per bushel")
                ?? ProblemOf(() => CreateAiWeights(), "the AI weights")
                ?? ProblemOf(() => buyFactors.ToCurve(), "the buy factors")
                ?? ProblemOf(() => sellFactors.ToCurve(), "the sell factors")
                ?? ProblemOf(() => CreatePricing(), "the sell factors against the buy factors")
                ?? ProblemOf(() => CreateThresholdRule(), "the stock thresholds")
                ?? ProblemOf(() => CreateWorkforceRule(), "the workforce")
                ?? ProblemOf(() => new CityPopulation(StartingPopulation), "the starting population (workforce times inhabitants per worker)");

            return problem == null;
        }

        private static string ProblemOf(Action create, string settings)
        {
            try
            {
                create();
                return null;
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return $"{settings}: {exception.Message}";
            }
        }

        // Says at once, in the Editor, what would leave every city without a market in Play mode.
        private void OnValidate()
        {
            activeGoods = null;

            if (!TryValidateSettings(out string problem))
            {
                Debug.LogWarning($"EconomyDefinition '{name}', {problem}\nCities will have no market in Play mode.", this);
            }
            else if (goods.Count > 0 && !TryValidate(out problem))
            {
                // An empty catalogue is a new asset, not yet filled in: no need to say it.
                Debug.LogWarning($"EconomyDefinition '{name}': {problem}.\nCities will have no market in Play mode.", this);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        // A change made through a SerializedObject (the Inspector, an undo, the tests) comes in here.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            activeGoods = null;
        }

        private List<GoodDefinition> ActiveGoodsOfTheCatalogue()
        {
            var active = new List<GoodDefinition>(goods.Count);

            foreach (CatalogueGood entry in goods)
            {
                // An empty entry stays in, active: TryValidate refuses it rather than hide it.
                if (entry.Active)
                {
                    active.Add(entry.Good);
                }
            }

            return active;
        }

        /// <summary>
        /// The pricing of these settings. Throws when one of them is unusable: the
        /// attributes only constrain the Inspector, not the file.
        /// </summary>
        public MarketPricing CreatePricing()
        {
            return new MarketPricing(buyFactors.ToCurve(), sellFactors.ToCurve());
        }

        /// <summary>
        /// How the stock thresholds of a good follow a city's consumption and production, from
        /// these settings. Throws when one of them is unusable.
        /// </summary>
        public ThresholdRule CreateThresholdRule()
        {
            return new ThresholdRule(t0Days, t1AddedDays, t2ProductionDays, t0Floor, t1Floor, t2Floor, t3Floor);
        }

        /// <summary>How the workshops hire, lay off and start, from these settings. Throws when one of them is unusable.</summary>
        public WorkforceRule CreateWorkforceRule()
        {
            return new WorkforceRule(hiredUpToT0, hiredUpToT1, hiredUpToT2, hiredUpToT3, workersAboveT3, startingWorkforce);
        }

        /// <summary>Barrels of room a unit of a good takes in a hold: one, or the barrels per bushel.</summary>
        public int RoomOf(GoodDefinition good)
        {
            return good != null && good.Unit == GoodUnit.Bushel ? barrelsPerBushel : 1;
        }

        /// <summary>The room a unit of each active good takes, by index. Throws when the barrels per bushel are under 1.</summary>
        public GoodUnits CreateGoodUnits()
        {
            // Checked even for a catalogue without bushels: the setting is wrong all the same.
            if (barrelsPerBushel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(barrelsPerBushel), "A bushel takes at least one barrel of room.");
            }

            var rooms = new int[Goods.Count];

            for (int i = 0; i < Goods.Count; i++)
            {
                rooms[i] = RoomOf(Goods[i]);
            }

            return new GoodUnits(rooms);
        }

        /// <summary>The weight the AI merchants give each active good, by index. Throws when a good has a negative weight.</summary>
        public int[] CreateAiWeights()
        {
            var weights = new int[Goods.Count];

            for (int i = 0; i < Goods.Count; i++)
            {
                GoodDefinition good = Goods[i];

                // An empty entry is refused by TryValidate; here it is a good no one carries.
                if (good == null)
                {
                    continue;
                }

                if (good.AiWeight < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(goods), $"'{good.name}' has a negative AI weight.");
                }

                weights[i] = good.AiWeight;
            }

            return weights;
        }

        /// <summary>
        /// How food changes the population of a city, from these settings. Throws when one
        /// of them is unusable.
        /// </summary>
        /// <param name="daysPerMonth">Days of the calendar's month: the rates are given per month.</param>
        public FoodGrowth CreateFoodGrowth(int daysPerMonth)
        {
            return new FoodGrowth(stableFoodDays, abundantFoodDays, maxMonthlyGrowth, maxMonthlyDecline, daysPerMonth);
        }

        /// <summary>
        /// The market of a city at the start of the game, with the starting population,
        /// the same in every city, and one stock per active good. Call
        /// <see cref="TryValidate"/> first. Throws when a setting or a good's figure is unusable.
        /// </summary>
        /// <param name="daysPerWeek">Days of the calendar's week: goods give their consumption per week.</param>
        /// <param name="foodGrowth">How food changes the population. Without it, the population stays what it is.</param>
        public CityMarket CreateMarket(
            CityDefinition city,
            MarketPricing pricing,
            int daysPerWeek,
            FoodGrowth foodGrowth = null)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (daysPerWeek < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerWeek));
            }

            var marketGoods = new MarketGood[Goods.Count];

            for (int i = 0; i < Goods.Count; i++)
            {
                GoodDefinition good = Goods[i];
                int maxWorkers = 0;
                double outputPerWorker = 0d;

                switch (city.ProductionOf(good))
                {
                    case GoodProduction.Efficient:
                        maxWorkers = good.MaxWorkers;
                        outputPerWorker = good.DailyOutputPerWorker;
                        break;
                    case GoodProduction.Inefficient:
                        maxWorkers = good.MaxWorkers;
                        outputPerWorker = good.DailyOutputPerWorker * inefficientEfficiency;
                        break;
                }

                marketGoods[i] = new MarketGood(
                    good.BasePrice,
                    good.WeeklyConsumptionPerThousand / daysPerWeek,
                    maxWorkers,
                    outputPerWorker,
                    good.Category == GoodCategory.Food);
            }

            return new CityMarket(
                marketGoods,
                CreateThresholdRule(),
                CreateWorkforceRule(),
                startingStockDays,
                pricing,
                daysPerWeek,
                new CityPopulation(StartingPopulation),
                foodGrowth);
        }
    }
}
