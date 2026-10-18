using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// When a city builds a house of its own: once its housed inhabitants are above a share of
    /// its room, and its market holds the materials of a house, which it takes from its stock
    /// even down to nothing. A city pays no gold. Stateless: the population is passed in.
    /// </summary>
    public sealed class CityHousingRule
    {
        /// <param name="occupancy">Share of the room above which the city builds, between 0 and 1 (0.95).</param>
        /// <param name="room">Places of a house the city builds, at least 1.</param>
        /// <param name="materials">Units of goods a house takes from the market, each good at most once; none is a house built from nothing.</param>
        public CityHousingRule(double occupancy, int room, IReadOnlyList<MaterialCost> materials)
        {
            // The negated comparison also rejects NaN.
            if (!(occupancy >= 0d && occupancy <= 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(occupancy), "The occupancy is between 0 and 1.");
            }

            if (room < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(room), "A house shelters at least one inhabitant.");
            }

            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            var seen = new HashSet<int>();
            var copy = new List<MaterialCost>(materials.Count);

            foreach (MaterialCost material in materials)
            {
                if (!seen.Add(material.Good))
                {
                    throw new ArgumentException("A good appears twice among the materials.", nameof(materials));
                }

                copy.Add(material);
            }

            Occupancy = occupancy;
            Room = room;
            Materials = copy;
        }

        /// <summary>Share of the room above which the city builds.</summary>
        public double Occupancy { get; }

        /// <summary>Places of a house the city builds.</summary>
        public int Room { get; }

        /// <summary>Units of goods a house takes from the market.</summary>
        public IReadOnlyList<MaterialCost> Materials { get; }

        /// <summary>Whether the housed inhabitants are above the occupancy of the room; the beggars do not count.</summary>
        public bool IsCrowded(CityPopulation population)
        {
            if (population == null)
            {
                throw new ArgumentNullException(nameof(population));
            }

            return population.Housed > Occupancy * population.Room;
        }
    }
}
