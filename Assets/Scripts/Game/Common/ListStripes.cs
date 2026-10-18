using System;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Stripes the rows of a list: every other row, from the second, takes a slightly lighter
    /// background (<see cref="AlternateClass"/>, in InfoPanel.uss). USS has no :nth-child, so the
    /// class is set here, again whenever rows are added, removed or moved.
    /// </summary>
    public static class ListStripes
    {
        public const string AlternateClass = "list-row--alternate";

        /// <summary>Sets the class on the odd children of the container, in their order, and clears it on the others.</summary>
        public static void Apply(VisualElement rowContainer)
        {
            if (rowContainer == null)
            {
                throw new ArgumentNullException(nameof(rowContainer));
            }

            for (int i = 0; i < rowContainer.childCount; i++)
            {
                rowContainer[i].EnableInClassList(AlternateClass, i % 2 == 1);
            }
        }
    }
}
