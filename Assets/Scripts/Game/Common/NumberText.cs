using System.Globalization;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Numbers as everything the player reads writes them, the French way.</summary>
    public static class NumberText
    {
        // Not the French culture: its separator and its minus sign depend on the platform.
        private static readonly NumberFormatInfo FrenchFormat = new NumberFormatInfo { NumberGroupSeparator = " " };

        /// <summary>A whole number: "6 000", thousands separated by a no-break space.</summary>
        public static string Format(long value)
        {
            return value.ToString("N0", FrenchFormat);
        }

        /// <summary>Gold: "10 000 or", or "-250 or" for a debt.</summary>
        public static string FormatGold(long gold)
        {
            return Format(gold) + " or";
        }
    }
}
