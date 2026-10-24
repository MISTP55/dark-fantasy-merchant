using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// When a city builds a house of its own for one class: once that class is above a share of
    /// its room, and its market holds the materials of a house, which it takes from its stock
    /// even down to nothing. A city pays no gold. Stateless: the population is passed in.
    /// </summary>
    public sealed class CityHousingRule
    {
        /// <param name="housedClass">The class the house shelters.</param>
        /// <param name="occupancy">Share of the class's room above which the city builds, between 0 and 1 (0.95 for the poor).</param>
        /// <param name="room">Places of a house the city builds, at least 1.</param>
        /// <param name="materials">Units of goods a house takes from the market, each good at most once; none is a house built from nothing.</param>
        /// <param name="footprint">The cells a house covers on the city grid; required only when the market has a grid.</param>
        public CityHousingRule(SocialClass housedClass, double occupancy, int room, IReadOnlyList<MaterialCost> materials, Footprint? footprint = null)
        {
            if (housedClass < SocialClass.Rich || housedClass > SocialClass.Poor)
            {
                throw new ArgumentOutOfRangeException(nameof(housedClass));
            }

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

            HousedClass = housedClass;
            Occupancy = occupancy;
            Room = room;
            Materials = copy;
            Footprint = footprint;
        }

        /// <summary>The class the house shelters.</summary>
        public SocialClass HousedClass { get; }

        /// <summary>Share of the class's room above which the city builds.</summary>
        public double Occupancy { get; }

        /// <summary>Places of a house the city builds.</summary>
        public int Room { get; }

        /// <summary>The cells a house covers on the city grid; null when the market has no grid.</summary>
        public Footprint? Footprint { get; }

        /// <summary>Units of goods a house takes from the market.</summary>
        public IReadOnlyList<MaterialCost> Materials { get; }

        /// <summary>Whether the class is above the occupancy of its room; the beggars and the other classes do not count.</summary>
        public bool IsCrowded(CityPopulation population)
        {
            if (population == null)
            {
                throw new ArgumentNullException(nameof(population));
            }

            return population.CountOf(HousedClass) > Occupancy * population.RoomOf(HousedClass);
        }
    }
}
