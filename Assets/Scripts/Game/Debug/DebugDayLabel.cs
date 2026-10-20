using System;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The date of a day of the history, as the hover of a debug chart writes it.</summary>
    public static class DebugDayLabel
    {
        /// <summary>
        /// The date of the day at an index, or a dash past the history: a chart drawn before the
        /// horizon was cut can be hovered before it is drawn again.
        /// </summary>
        public static string Of(SimulationHistory history, int index, Func<int, string> formatDay)
        {
            if (history == null)
            {
                throw new ArgumentNullException(nameof(history));
            }

            if (formatDay == null)
            {
                throw new ArgumentNullException(nameof(formatDay));
            }

            return index >= 0 && index < history.DayCount ? formatDay(history.DayAt(index)) : DebugCitiesTable.NoFigure;
        }
    }
}
