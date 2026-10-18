using System;
using System.Collections.Generic;
using System.Globalization;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The alphabetical order of what the player reads in a list: goods, ships, materials.
    /// Accents and case aside, so that "Étoffe" comes between "Cuir" and "Fer"; equal names
    /// keep their order.
    /// </summary>
    public static class NameOrder
    {
        private const CompareOptions NameComparison = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        /// <summary>Compares two names in alphabetical order, accents and case aside.</summary>
        public static int Compare(string first, string second)
        {
            return CultureInfo.InvariantCulture.CompareInfo.Compare(first, second, NameComparison);
        }

        /// <summary>The indices of the names, in the alphabetical order of the names.</summary>
        public static int[] Of(IReadOnlyList<string> names)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            var order = new int[names.Count];

            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            // Array.Sort is not stable: names that compare equal keep their order.
            Array.Sort(order, (first, second) =>
            {
                int byName = Compare(names[first], names[second]);
                return byName != 0 ? byName : first.CompareTo(second);
            });

            return order;
        }

        /// <summary>Sorts the items in the alphabetical order of their names, ties in their order.</summary>
        public static void Sort<T>(List<T> items, Func<T, string> nameOf)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (nameOf == null)
            {
                throw new ArgumentNullException(nameof(nameOf));
            }

            var names = new string[items.Count];

            for (int i = 0; i < names.Length; i++)
            {
                names[i] = nameOf(items[i]);
            }

            var sorted = new List<T>(items.Count);

            foreach (int index in Of(names))
            {
                sorted.Add(items[index]);
            }

            items.Clear();
            items.AddRange(sorted);
        }
    }
}
