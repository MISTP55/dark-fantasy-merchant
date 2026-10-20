using System;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Which events the debug journal shows: of a city or all, of a good or all, and of which
    /// types. Every type is shown at first but the threshold crossings, which are many.
    /// </summary>
    public sealed class DebugEventFilter
    {
        private readonly bool[] shown = new bool[Enum.GetValues(typeof(SimulationEventType)).Length];

        public DebugEventFilter()
        {
            for (int i = 0; i < shown.Length; i++)
            {
                shown[i] = true;
            }

            shown[(int)SimulationEventType.ThresholdCrossing] = false;
        }

        /// <summary>The city's index in the record; -1 for all.</summary>
        public int City { get; set; } = -1;

        /// <summary>The good's index; -1 for all. A good hides the events of the whole city.</summary>
        public int Good { get; set; } = -1;

        public bool IsShown(SimulationEventType type)
        {
            return shown[(int)type];
        }

        public void SetShown(SimulationEventType type, bool isShown)
        {
            shown[(int)type] = isShown;
        }

        public bool Matches(SimulationEvent simulationEvent)
        {
            return shown[(int)simulationEvent.Type]
                && (City < 0 || simulationEvent.City == City)
                && (Good < 0 || simulationEvent.Good == Good);
        }
    }
}
