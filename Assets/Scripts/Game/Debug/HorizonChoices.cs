using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Game
{
    /// <summary>How far back the debug tools remember the simulation: a number of months, or the whole game.</summary>
    public static class HorizonChoices
    {
        private static readonly int[] Months = { 3, 6, 12, 24, 48 };

        /// <summary>The choices as the debug panel writes them, the whole game last.</summary>
        public static IReadOnlyList<string> Labels { get; } = new[] { "3 mois", "6 mois", "12 mois", "24 mois", "48 mois", "Toute la partie" };

        /// <summary>Twelve months.</summary>
        public const int DefaultIndex = 2;

        /// <returns>The days of a choice; null for the whole game.</returns>
        public static int? DaysOf(int index, int daysPerMonth)
        {
            if (index < 0 || index >= Labels.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return index < Months.Length ? Months[index] * daysPerMonth : (int?)null;
        }
    }
}
