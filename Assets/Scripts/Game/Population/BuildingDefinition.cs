using System;
using System.Collections.Generic;
using System.Text;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of a building: its name and what it costs. Not every building is the player's: the
    /// town hall is placed by the city's layout and costs nothing. Each
    /// kind of building adds what it gives. Holds no runtime state: what a city has built is in its
    /// <see cref="CityMarket"/>.
    /// </summary>
    public abstract class BuildingDefinition : ScriptableObject
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

        [Tooltip("The name the player reads, in French.")]
        [SerializeField] private string displayName;

        [Tooltip("Gold coins the building costs, besides its materials.")]
        [SerializeField, Min(0)] private long gold;

        [Tooltip("The materials of the building, a good once at most.")]
        [SerializeField] private List<MaterialAmount> materials = new List<MaterialAmount>();

        [Tooltip("The cells the building covers on the city grid, in its Along orientation: width on x, depth on y. Each at least 1.")]
        [SerializeField] private Vector2Int footprint;

        /// <summary>What the cost of a building that cannot be built reads.</summary>
        public const string UnavailableText = "indisponible";

        public string DisplayName => displayName;

        public long Gold => gold;

        /// <summary>The footprint as set, usable or not (<see cref="TryValidate"/>).</summary>
        public Vector2Int FootprintSize => footprint;

        /// <summary>The cells the building covers on the grid. Throws for a footprint under 1: only for a building that <see cref="TryValidate"/> accepts.</summary>
        public Footprint Footprint => new Footprint(footprint.x, footprint.y);

        public IReadOnlyList<MaterialAmount> Materials => materials;

        /// <summary>
        /// The materials of the building, the player's or one a city builds of its own, by their
        /// index among the active goods. Nothing makes them unavailable: an empty or inactive
        /// material, or one under 1 unit, is left out, and a good listed twice is added up, so a
        /// building with none of its materials active takes nothing.
        /// </summary>
        public IReadOnlyList<MaterialCost> MaterialsOf(EconomyDefinition economy)
        {
            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            var units = new List<(int good, int units)>();

            foreach (MaterialAmount material in materials)
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
        /// The cost of the building in Core's terms, its materials known by their index among the
        /// economy's active goods. It takes only its materials that are active
        /// (<see cref="MaterialsOf"/>): it is never unavailable for its materials, and costs only
        /// its gold when none is active.
        /// </summary>
        /// <param name="problem">What is wrong, naming the building; null when nothing is.</param>
        public bool TryCreateCost(EconomyDefinition economy, out BuildingCost cost, out string problem)
        {
            cost = null;
            problem = null;

            if (economy == null)
            {
                problem = $"'{name}' has no economy to be costed in";
                return false;
            }

            if (gold < 0)
            {
                problem = $"'{name}' costs a negative gold";
                return false;
            }

            cost = new BuildingCost(gold, MaterialsOf(economy));
            return true;
        }

        /// <summary>
        /// What the building costs, for the player: "10 000 or, 10 bois, 30 métal", the materials it
        /// takes in their alphabetical order, or "indisponible" when its cost cannot be worked out.
        /// A building whose materials are all switched off reads "5 000 or".
        /// </summary>
        public string CostText(EconomyDefinition economy)
        {
            if (!TryCreateCost(economy, out BuildingCost cost, out _))
            {
                return UnavailableText;
            }

            var text = new StringBuilder(TreasuryHudController.FormatGold(cost.Gold));

            // In the alphabetical order of the materials, as every list the player reads.
            var costs = new List<MaterialCost>(cost.Materials);
            NameOrder.Sort(costs, material => economy.Goods[material.Good].DisplayName);

            foreach (MaterialCost material in costs)
            {
                text.Append(", ").Append(material.Units).Append(' ').Append(economy.Goods[material.Good].DisplayName.ToLowerInvariant());
            }

            return text.ToString();
        }

        /// <summary>
        /// Whether the building can be used at all: it has a name, and what its kind adds is
        /// usable. Its cost does not count here: a cost that cannot be worked out only makes the
        /// building unavailable (<see cref="TryCreateCost"/>).
        /// </summary>
        /// <param name="problem">What is wrong, for the console; null when nothing is.</param>
        public virtual bool TryValidate(out string problem)
        {
            problem = string.IsNullOrWhiteSpace(displayName) ? "it has no name" : null;

            if (problem == null && (footprint.x < 1 || footprint.y < 1))
            {
                problem = $"its footprint is {footprint.x} by {footprint.y}, a side is under 1";
            }

            return problem == null;
        }
    }
}
