using System;
using System.Collections.Generic;
using System.Text;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the buildings the player can build in a city: what each costs and
    /// what it gives (jobs for a workshop, room for a house). Holds no runtime state: what a city
    /// has built is in its <see cref="CityMarket"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Buildings", menuName = "Dark Fantasy Merchant/Buildings")]
    public sealed class BuildingsDefinition : ScriptableObject
    {
        /// <summary>Units of a good that a building takes.</summary>
        [Serializable]
        public sealed class MaterialAmount
        {
            [Tooltip("The good, one of the economy's active goods.")]
            [SerializeField] private GoodDefinition good;

            [Tooltip("Units of the good (barrels or bushels), at least 1.")]
            [SerializeField, Min(1)] private int units = 1;

            public GoodDefinition Good => good;

            public int Units => units;
        }

        /// <summary>What a kind of building costs.</summary>
        [Serializable]
        public sealed class BuildingType
        {
            [Tooltip("Gold coins the building costs, besides its materials.")]
            [SerializeField, Min(0)] private long gold;

            [Tooltip("The materials of the building, a good once at most.")]
            [SerializeField] private List<MaterialAmount> materials = new List<MaterialAmount>();

            public long Gold => gold;

            public IReadOnlyList<MaterialAmount> Materials => materials;
        }

        [Header("Workshop")]
        [SerializeField] private BuildingType workshop = new BuildingType();

        [Tooltip("Workers a workshop employs at most.")]
        [SerializeField, Min(1)] private int workshopJobs = 30;

        [Header("Houses, one per class")]
        [FormerlySerializedAs("house")]
        [SerializeField] private BuildingType poorHouse = new BuildingType();

        [Tooltip("Poor a poor house shelters: 140.")]
        [FormerlySerializedAs("houseRoom")]
        [SerializeField, Min(1)] private int poorHouseRoom = 140;

        [SerializeField] private BuildingType wealthyHouse = new BuildingType();

        [Tooltip("Wealthy a wealthy house shelters: 70.")]
        [SerializeField, Min(1)] private int wealthyHouseRoom = 70;

        [SerializeField] private BuildingType richHouse = new BuildingType();

        [Tooltip("Rich a rich house shelters: 40.")]
        [SerializeField, Min(1)] private int richHouseRoom = 40;

        /// <summary>What the costs line writes for a building that cannot be built.</summary>
        public const string UnavailableText = "indisponible";

        public BuildingType Workshop => workshop;

        public int WorkshopJobs => workshopJobs;

        /// <summary>The house of a class.</summary>
        public BuildingType HouseOf(SocialClass housedClass)
        {
            switch (housedClass)
            {
                case SocialClass.Rich:
                    return richHouse;
                case SocialClass.Wealthy:
                    return wealthyHouse;
                case SocialClass.Poor:
                    return poorHouse;
                default:
                    throw new ArgumentOutOfRangeException(nameof(housedClass));
            }
        }

        /// <summary>The places of a house of each class.</summary>
        public PerClass<int> HouseRooms => new PerClass<int>(richHouseRoom, wealthyHouseRoom, poorHouseRoom);

        /// <summary>
        /// The cost of a building in Core's terms, its materials known by their index among the
        /// economy's active goods. A building takes only its materials that are active
        /// (<see cref="MaterialsOf"/>): it is never unavailable for its materials, and costs only
        /// its gold when none is active.
        /// </summary>
        /// <param name="type">The workshop or a house of this definition.</param>
        /// <param name="problem">What is wrong, naming the building; null when nothing is.</param>
        public bool TryCreateCost(BuildingType type, EconomyDefinition economy, out BuildingCost cost, out string problem)
        {
            cost = null;
            problem = null;

            if (type == null || economy == null)
            {
                problem = "there is no building or economy to cost";
                return false;
            }

            string building = type == richHouse ? "rich house"
                : type == wealthyHouse ? "wealthy house"
                : type == poorHouse ? "poor house"
                : "workshop";

            if (type.Gold < 0)
            {
                problem = $"the {building} costs a negative gold";
                return false;
            }

            cost = new BuildingCost(type.Gold, MaterialsOf(type, economy));
            return true;
        }

        /// <summary>
        /// The materials of a building, the player's or one a city builds of its own, by their
        /// index among the active goods. Nothing makes them unavailable: an empty or inactive
        /// material, or one under 1 unit, is left out, and a good listed twice is added up, so a
        /// building with none of its materials active takes nothing.
        /// </summary>
        /// <param name="type">The workshop or a house of this definition.</param>
        public IReadOnlyList<MaterialCost> MaterialsOf(BuildingType type, EconomyDefinition economy)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            var units = new List<(int good, int units)>();

            foreach (MaterialAmount material in type.Materials)
            {
                int index = material == null || material.Good == null ? -1 : economy.IndexOf(material.Good);

                if (index < 0 || material.Units < 1)
                {
                    continue;
                }

                int listed = units.FindIndex(entry => entry.good == index);

                if (listed < 0)
                {
                    units.Add((index, material.Units));
                }
                else
                {
                    units[listed] = (index, units[listed].units + material.Units);
                }
            }

            return units.ConvertAll(entry => new MaterialCost(entry.good, entry.units));
        }

        /// <summary>
        /// Whether the buildings can be used at all: jobs and rooms of at least 1. The room of the
        /// house of each class also sets the houses of every city at the start. A cost that cannot be worked
        /// out does not count here: it only makes its building unavailable
        /// (<see cref="TryCreateCost"/>).
        /// </summary>
        /// <param name="problem">What is wrong, for the console; null when nothing is.</param>
        public bool TryValidate(out string problem)
        {
            problem = null;

            if (workshopJobs < 1)
            {
                problem = "the workshop has no jobs";
                return false;
            }

            if (richHouseRoom < 1 || wealthyHouseRoom < 1 || poorHouseRoom < 1)
            {
                problem = "a house has no room";
                return false;
            }

            return true;
        }

        /// <summary>
        /// What the buildings cost, for the player: the houses in the alphabetical order of their
        /// names, then the workshop, "Maison d'aisés : 10 000 or, 15 bois, 20 métal · … · Atelier :
        /// 10 000 or, 10 bois, 30 métal", or "indisponible" for a building whose cost cannot be
        /// worked out. Only the materials the building takes are named: a building whose materials
        /// are all switched off reads "Maison de pauvres : 5 000 or".
        /// </summary>
        public string CostsText(EconomyDefinition economy)
        {
            var housedClasses = new List<SocialClass> { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };
            NameOrder.Sort(housedClasses, SocialClassNames.HouseOf);
            var text = new StringBuilder();

            foreach (SocialClass housedClass in housedClasses)
            {
                text.Append(SocialClassNames.HouseOf(housedClass)).Append(" : ").Append(CostText(HouseOf(housedClass), economy)).Append(" · ");
            }

            return text.Append("Atelier : ").Append(CostText(workshop, economy)).ToString();
        }

        private string CostText(BuildingType type, EconomyDefinition economy)
        {
            if (!TryCreateCost(type, economy, out BuildingCost cost, out _))
            {
                return UnavailableText;
            }

            var text = new StringBuilder(TreasuryHudController.FormatGold(cost.Gold));

            // In the alphabetical order of the materials, as every list the player reads.
            var materials = new List<MaterialCost>(cost.Materials);
            NameOrder.Sort(materials, material => economy.Goods[material.Good].DisplayName);

            foreach (MaterialCost material in materials)
            {
                text.Append(", ").Append(material.Units).Append(' ').Append(economy.Goods[material.Good].DisplayName.ToLowerInvariant());
            }

            return text.ToString();
        }
    }
}
