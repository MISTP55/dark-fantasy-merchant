using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Tooltip("Barrels of room a bushel takes in a hold: 10.")]
        [SerializeField, Min(1)] private int barrelsPerBushel = 10;

        [Header("Workshops (a good's maximum of workers and output per worker are the good's own)")]
        [Tooltip("Share of a good's output per worker in a city that produces it inefficiently: 0.75.")]
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

        [Header("Merchants' workshops (the player's)")]
        [Tooltip("Workshops of a good in a city from which a merchant's workers make more: 3.")]
        [SerializeField, Min(1)] private int merchantFirstStep = 3;

        [Tooltip("Factor of their output from the first step: 1.03.")]
        [SerializeField, Min(0f)] private double merchantFirstBonus = 1.03d;

        [Tooltip("Workshops from which the second bonus applies: 6.")]
        [SerializeField, Min(1)] private int merchantSecondStep = 6;

        [Tooltip("Factor of their output from the second step: 1.06.")]
        [SerializeField, Min(0f)] private double merchantSecondBonus = 1.06d;

        [Tooltip("Workshops from which the third bonus applies: 9.")]
        [SerializeField, Min(1)] private int merchantThirdStep = 9;

        [Tooltip("Factor of their output from the third step: 1.1.")]
        [SerializeField, Min(0f)] private double merchantThirdBonus = 1.1d;

        [Header("Start of a game")]
        [Tooltip("Workers every city starts with, spread evenly over its workshops: 250.")]
        [SerializeField, Min(0)] private int startingWorkforce = 250;

        [Tooltip("Inhabitants housed per starting worker: 4.")]
        [SerializeField, Min(0)] private int inhabitantsPerWorker = 4;

        [Header("Labour")]
        [Tooltip("Beggars per starting worker, rounded down for the city: 4 / 17.")]
        [SerializeField, Min(0f)] private double startingBeggarsPerWorker = 4d / 17d;

        [Tooltip("How satisfied the beggars must be for the city to attract them: 50.")]
        [SerializeField, Min(0f)] private double beggarSatisfaction = 50d;

        [Tooltip("The beggars' target is the root of (inhabitants x satisfaction / 2) divided by this, plus the minimum.")]
        [SerializeField, Min(1)] private int beggarTargetDivisor = 3;

        [Tooltip("Beggars a city keeps whatever its size.")]
        [SerializeField, Min(0)] private int beggarTargetMinimum = 8;

        [Tooltip("Beggars arrive each day, under their target, at the root of the inhabitants divided by this, rounded up.")]
        [SerializeField, Min(1)] private int beggarArrivalDivisor = 5;

        [Tooltip("Beggars leave each day, above their target, at the root of the inhabitants divided by this, rounded up.")]
        [SerializeField, Min(1)] private int beggarDepartureDivisor = 10;

        [Tooltip("Beggars a city needs for the player's workshops to hire.")]
        [SerializeField, Min(0)] private int vacancyMinimumBeggars = 5;

        [Tooltip("Free room a city needs, more than this, for the player's workshops to hire.")]
        [SerializeField, Min(0)] private int vacancyMinimumFreeRoom = 3;

        [Tooltip("Share of a workshop's reserve that leaves for the beggars at every layoff.")]
        [SerializeField, Range(0f, 1f)] private double reserveLeakShare = 0.02d;

        [Tooltip("The reserve leaks only while the workers and the reserve of the workshop stay under this.")]
        [SerializeField, Min(0)] private int reserveLeakCeiling = 30;

        [Header("Satisfaction and classes")]
        [Tooltip("Satisfaction of every class when the game starts: 50.")]
        [SerializeField] private int startingSatisfaction = 50;

        [Tooltip("Base of the rich's target satisfaction: -13.")]
        [SerializeField] private int richSatisfactionBase = -13;

        [Tooltip("Base of the wealthy's target satisfaction: -18.")]
        [SerializeField] private int wealthySatisfactionBase = -18;

        [Tooltip("Base of the poor's target satisfaction: -27.")]
        [SerializeField] private int poorSatisfactionBase = -27;

        [Tooltip("Points a satisfaction rises in a day towards twice its target: 2.")]
        [SerializeField, Min(0)] private int satisfactionRise = 2;

        [Tooltip("Points a satisfaction falls in a day towards twice its target: 1.")]
        [SerializeField, Min(0)] private int satisfactionFall = 1;

        [Tooltip("The term every class's target shares: 9, as there are no public buildings nor events yet to add to it.")]
        [SerializeField] private int commonSatisfaction = 9;

        [Tooltip("The lowest a satisfaction falls: -40.")]
        [SerializeField] private int lowestSatisfaction = -40;

        [Tooltip("The highest a satisfaction rises: 80.")]
        [SerializeField] private int highestSatisfaction = 80;

        [Tooltip("Added to a class's satisfaction for its share of the poor: 40.")]
        [SerializeField] private int classTargetOffset = 40;

        [Tooltip("The rich a city tends to are (satisfaction + offset) x poor / this: 320.")]
        [SerializeField, Min(1)] private int richTargetDivisor = 320;

        [Tooltip("The wealthy a city tends to are (satisfaction + offset) x poor / this: 160.")]
        [SerializeField, Min(1)] private int wealthyTargetDivisor = 160;

        [Tooltip("A class short of room still takes up to this many: 10.")]
        [SerializeField, Min(0)] private int classRoomFloor = 10;

        [Tooltip("Unhappy poor leave each day at their number divided by this: 100, a hundredth.")]
        [SerializeField, Min(1)] private int poorLeavingDivisor = 100;

        [Tooltip("Workers of a city's workshop the leaving poor spare while other jobs can be taken: 30.")]
        [SerializeField, Min(0)] private int protectedWorkers = 30;

        [Header("Cities' houses")]
        [Tooltip("A city builds a rich house of its own, with the materials of the player's, once its rich are above this share of their room.")]
        [SerializeField, Range(0f, 1f)] private double cityRichHouseOccupancy = 0.97d;

        [Tooltip("A city builds a wealthy house once its wealthy are above this share of their room.")]
        [SerializeField, Range(0f, 1f)] private double cityWealthyHouseOccupancy = 0.96d;

        [Tooltip("A city builds a poor house once its poor are above this share of their room.")]
        [FormerlySerializedAs("cityHouseOccupancy")]
        [SerializeField, Range(0f, 1f)] private double cityPoorHouseOccupancy = 0.95d;

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

        public int MerchantFirstStep => merchantFirstStep;

        public double MerchantFirstBonus => merchantFirstBonus;

        public int MerchantSecondStep => merchantSecondStep;

        public double MerchantSecondBonus => merchantSecondBonus;

        public int MerchantThirdStep => merchantThirdStep;

        public double MerchantThirdBonus => merchantThirdBonus;

        public int StartingWorkforce => startingWorkforce;

        public int InhabitantsPerWorker => inhabitantsPerWorker;

        /// <summary>
        /// The starting workforce times the inhabitants per worker: the population of a market
        /// made without labour only. It is not that of the game's cities, whose markets have
        /// labour and start with <see cref="CityPopulation.AtStart"/>, from the workers they
        /// actually start with.
        /// </summary>
        public int StartingPopulation => startingWorkforce * inhabitantsPerWorker;

        public double StartingBeggarsPerWorker => startingBeggarsPerWorker;

        public double T0Days => t0Days;

        public double T1AddedDays => t1AddedDays;

        public double T2ProductionDays => t2ProductionDays;

        public double T0Floor => t0Floor;

        public double T1Floor => t1Floor;

        public double T2Floor => t2Floor;

        public double T3Floor => t3Floor;

        public PriceFactors BuyFactors => buyFactors;

        public PriceFactors SellFactors => sellFactors;

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
        /// or not, at least one active, and the recipe of every good, active or not, made of goods of
        /// the catalogue other than itself, each once and above 0.
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

            foreach (CatalogueGood entry in goods)
            {
                if (!TryValidateRecipe(entry.Good, out problem))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryValidateRecipe(GoodDefinition good, out string problem)
        {
            problem = null;
            var seen = new HashSet<GoodDefinition>();

            foreach (GoodDefinition.InputAmount input in good.Inputs)
            {
                if (input == null || input.Good == null)
                {
                    problem = $"'{good.name}' has an input with no good";
                }
                else if (input.Good == good)
                {
                    problem = $"'{good.name}' is its own input";
                }
                else if (!IsInCatalogue(input.Good))
                {
                    problem = $"'{good.name}' takes '{input.Good.name}', which is not in the catalogue";
                }
                else if (!seen.Add(input.Good))
                {
                    problem = $"'{good.name}' takes '{input.Good.name}' twice";
                }
                // The negated comparison also rejects NaN.
                else if (!(input.UnitsPerUnit > 0d) || double.IsInfinity(input.UnitsPerUnit))
                {
                    problem = $"'{good.name}' takes '{input.Good.name}' at {input.UnitsPerUnit}, which is not above 0";
                }

                if (problem != null)
                {
                    return false;
                }
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
                ?? ProblemOf(() => CreateMerchantOutputRule(), "the merchants' workshops")
                ?? ProblemOf(() => new CityPopulation(StartingPopulation), "the starting population (workforce times inhabitants per worker)")
                ?? ProblemOf(() => CreateLabourRule(), "the labour")
                ?? ProblemOf(() => CreateSocialRule(), "the satisfaction and classes")
                ?? ProblemOf(() => CreateHousingRule(SocialClass.Rich, 1, Array.Empty<MaterialCost>()), "the cities' houses")
                ?? ProblemOf(() => CreateHousingRule(SocialClass.Wealthy, 1, Array.Empty<MaterialCost>()), "the cities' houses")
                ?? ProblemOf(() => CreateHousingRule(SocialClass.Poor, 1, Array.Empty<MaterialCost>()), "the cities' houses")
                ?? ProblemOf(() => CityPopulation.AtStart(0, inhabitantsPerWorker, startingBeggarsPerWorker, PerClass<int>.All(1)), "the starting beggars per worker");

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

        /// <summary>What the merchants' workshops make, from these settings. Throws when one of them is unusable.</summary>
        public MerchantOutputRule CreateMerchantOutputRule()
        {
            return new MerchantOutputRule(
                merchantFirstStep,
                merchantFirstBonus,
                merchantSecondStep,
                merchantSecondBonus,
                merchantThirdStep,
                merchantThirdBonus);
        }

        /// <summary>How the workers are hired and the beggars come and go, from these settings. Throws when one of them is unusable.</summary>
        public LabourRule CreateLabourRule()
        {
            return new LabourRule(
                inhabitantsPerWorker,
                beggarSatisfaction,
                beggarTargetDivisor,
                beggarTargetMinimum,
                beggarArrivalDivisor,
                beggarDepartureDivisor,
                vacancyMinimumBeggars,
                vacancyMinimumFreeRoom,
                reserveLeakShare,
                reserveLeakCeiling);
        }

        /// <summary>How the classes are satisfied and move, from these settings. Throws when one of them is unusable.</summary>
        public SocialRule CreateSocialRule()
        {
            return new SocialRule(
                startingSatisfaction,
                new PerClass<int>(richSatisfactionBase, wealthySatisfactionBase, poorSatisfactionBase),
                satisfactionRise,
                satisfactionFall,
                commonSatisfaction,
                lowestSatisfaction,
                highestSatisfaction,
                classTargetOffset,
                richTargetDivisor,
                wealthyTargetDivisor,
                classRoomFloor,
                poorLeavingDivisor,
                protectedWorkers);
        }

        /// <summary>
        /// When a city builds a house of its own for a class, from these settings. Throws when the
        /// occupancy or the room is unusable.
        /// </summary>
        /// <param name="room">Places of a house of the class, the player's.</param>
        /// <param name="materials">What a house takes from the market (<see cref="BuildingDefinition.MaterialsOf"/>).</param>
        public CityHousingRule CreateHousingRule(SocialClass housedClass, int room, IReadOnlyList<MaterialCost> materials)
        {
            double occupancy = housedClass == SocialClass.Rich
                ? cityRichHouseOccupancy
                : housedClass == SocialClass.Wealthy ? cityWealthyHouseOccupancy : cityPoorHouseOccupancy;

            return new CityHousingRule(housedClass, occupancy, room, materials);
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
        /// The market of a city at the start of the game, with one stock per active good and its
        /// population: the starting population, the same in every city, or, with a labour rule,
        /// the one of a new game (housed families, beggars, houses). Call
        /// <see cref="TryValidate"/> first. Throws when a setting or a good's figure is unusable.
        /// </summary>
        /// <param name="daysPerWeek">Days of the calendar's week: goods give their consumption per week.</param>
        /// <param name="labour">The labour of the city, or null to keep the immediate hiring.</param>
        /// <param name="roomPerHouse">Places of a house of each class, each at least 1 with a labour rule: the city starts with as many houses as each class needs.</param>
        /// <param name="housing">When the city builds a house of its own for each class, only with a labour rule; null for never.</param>
        /// <param name="social">The satisfaction and moves of the classes, only with a labour rule; null for classes that never move.</param>
        public CityMarket CreateMarket(
            CityDefinition city,
            MarketPricing pricing,
            int daysPerWeek,
            LabourRule labour = null,
            PerClass<int> roomPerHouse = default,
            IReadOnlyList<CityHousingRule> housing = null,
            SocialRule social = null)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (daysPerWeek < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerWeek));
            }

            if (labour != null && (roomPerHouse.Rich < 1 || roomPerHouse.Wealthy < 1 || roomPerHouse.Poor < 1))
            {
                throw new ArgumentOutOfRangeException(nameof(roomPerHouse), "A house shelters at least one inhabitant.");
            }

            var marketGoods = new MarketGood[Goods.Count];

            for (int i = 0; i < Goods.Count; i++)
            {
                GoodDefinition good = Goods[i];
                int maxWorkers = 0;
                double outputPerWorker = 0d;
                double merchantOutputPerWorker = 0d;

                switch (city.ProductionOf(good))
                {
                    case GoodProduction.Efficient:
                        maxWorkers = good.MaxWorkers;
                        outputPerWorker = good.DailyOutputPerWorker;
                        merchantOutputPerWorker = good.MerchantDailyOutputPerWorker;
                        break;
                    case GoodProduction.Inefficient:
                        maxWorkers = good.MaxWorkers;
                        outputPerWorker = good.DailyOutputPerWorker * inefficientEfficiency;
                        merchantOutputPerWorker = good.MerchantDailyOutputPerWorker * inefficientEfficiency;
                        break;
                }

                PerClass<double> weekly = good.WeeklyConsumptionPerThousand;

                marketGoods[i] = new MarketGood(
                    good.BasePrice,
                    new PerClass<double>(weekly.Rich / daysPerWeek, weekly.Wealthy / daysPerWeek, weekly.Poor / daysPerWeek),
                    maxWorkers,
                    outputPerWorker,
                    merchantOutputPerWorker,
                    good.SatisfactionWeights)
                    .WithInputs(InputsOf(good));
            }

            WorkforceRule workforce = CreateWorkforceRule();

            // The labour relies on housed = inhabitants per worker x workers: only the start gives that.
            CityPopulation population = labour == null
                ? new CityPopulation(StartingPopulation)
                : CityPopulation.AtStart(
                    CityMarket.StartingWorkforceOf(marketGoods, workforce),
                    inhabitantsPerWorker,
                    startingBeggarsPerWorker,
                    roomPerHouse);

            return new CityMarket(
                marketGoods,
                CreateThresholdRule(),
                workforce,
                startingStockDays,
                pricing,
                daysPerWeek,
                population,
                labour,
                housing,
                social);
        }

        // The recipe of a good by the indices of its inputs among the active goods; an inactive input is left out.
        private List<MarketInput> InputsOf(GoodDefinition good)
        {
            var inputs = new List<MarketInput>();

            foreach (GoodDefinition.InputAmount input in good.Inputs)
            {
                int index = input == null ? -1 : IndexOf(input.Good);

                if (index >= 0)
                {
                    inputs.Add(new MarketInput(index, input.UnitsPerUnit));
                }
            }

            return inputs;
        }
    }
}
