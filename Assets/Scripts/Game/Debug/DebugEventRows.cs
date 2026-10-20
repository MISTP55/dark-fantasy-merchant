using System;
using System.Collections;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The rows of the debug events journal: the events of the record a filter lets through,
    /// copied, so that a frozen journal keeps them once the record forgets them. A refresh adds
    /// only the events found since the last one; a new filter looks at every event again, frozen or not.
    /// </summary>
    public sealed class DebugEventRows
    {
        private readonly DebugEventFilter filter;

        // Oldest first.
        private readonly List<SimulationEvent> matched = new List<SimulationEvent>();

        // The position, counted from the start of the game, of the first event not looked at yet.
        private long next;

        public DebugEventRows(DebugEventFilter filter)
        {
            this.filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        /// <summary>The rows, for a list view: read them with <see cref="NewestAt"/>.</summary>
        public IList Items => matched;

        public int Count => matched.Count;

        /// <summary>A frozen journal does not change on a refresh.</summary>
        public bool IsFrozen { get; set; }

        /// <param name="index">From 0, the newest row.</param>
        public SimulationEvent NewestAt(int index)
        {
            return matched[matched.Count - 1 - index];
        }

        /// <summary>Forgets what the record forgot and adds what it has found since, unless frozen.</summary>
        public void Refresh(SimulationRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (IsFrozen)
            {
                return;
            }

            int firstDay = record.History.FirstDay;
            int forgotten = 0;

            while (forgotten < matched.Count && matched[forgotten].Day < firstDay)
            {
                forgotten++;
            }

            matched.RemoveRange(0, forgotten);

            IReadOnlyList<SimulationEvent> events = record.Events;

            for (long position = Math.Max(next, record.EventsTrimmed); position < record.EventsTrimmed + events.Count; position++)
            {
                SimulationEvent simulationEvent = events[(int)(position - record.EventsTrimmed)];

                if (filter.Matches(simulationEvent))
                {
                    matched.Add(simulationEvent);
                }
            }

            next = record.EventsTrimmed + events.Count;
        }

        /// <summary>Another filter: every event the record keeps is looked at again, even while frozen.</summary>
        public void Rebuild(SimulationRecord record)
        {
            bool frozen = IsFrozen;
            matched.Clear();
            next = 0;
            IsFrozen = false;
            Refresh(record);
            IsFrozen = frozen;
        }
    }
}
