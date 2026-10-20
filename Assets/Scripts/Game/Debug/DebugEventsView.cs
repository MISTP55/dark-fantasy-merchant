using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The journal of the cities' events, newest first, filtered by city, good and type. Frozen,
    /// it keeps the rows it shows, whatever the days bring, and catches up once it thaws.
    /// </summary>
    public sealed class DebugEventsView : IDebugView
    {
        private const string All = "Toutes";
        private const float RowHeight = 22f;

        private static readonly string[] CellClasses =
        {
            "debug-events__cell--date", "debug-events__cell--city", "debug-events__cell--type", "debug-events__cell--detail",
        };

        private readonly SimulationRecorder recorder;
        private readonly IReadOnlyList<string> goodNames;
        private readonly List<string> cityNames = new List<string>();
        private readonly DebugEventFilter filter = new DebugEventFilter();
        private readonly int[] cityOrder;
        private readonly int[] goodOrder;
        private readonly Toggle frozen;
        private readonly ListView list;

        // The events shown, oldest first: copies, so that a frozen list keeps them once the record forgets them.
        private readonly List<SimulationEvent> matched = new List<SimulationEvent>();

        // The position, counted from the start of the game, of the first event not looked at yet.
        private long next;
        private bool isShown;

        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        public DebugEventsView(VisualElement page, SimulationRecorder recorder, IReadOnlyList<string> goodNames)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            this.goodNames = goodNames ?? throw new ArgumentNullException(nameof(goodNames));
            page.Clear();

            foreach (CityDefinition city in recorder.Cities)
            {
                cityNames.Add(city.DisplayName);
            }

            cityOrder = NameOrder.Of(cityNames);
            goodOrder = NameOrder.Of(goodNames);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("debug-curves__toolbar");
            page.Add(toolbar);

            DropdownField cityField = Dropdown(toolbar, "Ville", WithAll(cityNames, cityOrder));
            cityField.RegisterValueChangedCallback(change =>
            {
                filter.City = cityField.index > 0 ? cityOrder[cityField.index - 1] : -1;
                Rebuild();
            });

            DropdownField goodField = Dropdown(toolbar, "Marchandise", WithAll(goodNames, goodOrder));
            goodField.RegisterValueChangedCallback(change =>
            {
                filter.Good = goodField.index > 0 ? goodOrder[goodField.index - 1] : -1;
                Rebuild();
            });

            frozen = new Toggle { text = "Figer", focusable = false };
            frozen.AddToClassList("debug-panel__toggle");
            frozen.RegisterValueChangedCallback(change => Refresh());
            toolbar.Add(frozen);

            // One toggle per name of type, in the order of the types: the beggars who come and go share one.
            var types = new VisualElement();
            types.AddToClassList("debug-curves__toolbar");
            page.Add(types);
            var named = new List<string>();

            foreach (SimulationEventType type in Enum.GetValues(typeof(SimulationEventType)))
            {
                string name = DebugEventText.TypeName(type);

                if (named.Contains(name))
                {
                    continue;
                }

                named.Add(name);
                var toggle = new Toggle { text = name, focusable = false, value = filter.IsShown(type) };
                toggle.AddToClassList("debug-panel__toggle");
                toggle.RegisterValueChangedCallback(change =>
                {
                    foreach (SimulationEventType other in Enum.GetValues(typeof(SimulationEventType)))
                    {
                        if (DebugEventText.TypeName(other) == name)
                        {
                            filter.SetShown(other, change.newValue);
                        }
                    }

                    Rebuild();
                });
                types.Add(toggle);
            }

            VisualElement header = Row();
            string[] titles = { "Date", "Ville", "Type", "Détail" };

            for (int i = 0; i < titles.Length; i++)
            {
                ((Label)header[i]).text = titles[i];
            }

            header.AddToClassList("debug-cities__row--header");
            page.Add(header);

            list = new ListView(matched, RowHeight, Row, Bind)
            {
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                focusable = false,
            };
            list.AddToClassList("debug-journal");
            page.Add(list);
        }

        public void Show()
        {
            isShown = true;
            Refresh();
        }

        public void Hide()
        {
            isShown = false;
        }

        // Forgets what the record forgot and adds what it has found since, unless frozen.
        public void Refresh()
        {
            if (!isShown || frozen.value)
            {
                return;
            }

            SimulationRecord record = recorder.Record;
            int firstDay = record.History.FirstDay;
            int forgotten = 0;

            while (forgotten < matched.Count && matched[forgotten].Day < firstDay)
            {
                forgotten++;
            }

            matched.RemoveRange(0, forgotten);

            IReadOnlyList<SimulationEvent> events = record.Events;
            long start = Math.Max(next, record.EventsTrimmed);

            for (long position = start; position < record.EventsTrimmed + events.Count; position++)
            {
                SimulationEvent simulationEvent = events[(int)(position - record.EventsTrimmed)];

                if (filter.Matches(simulationEvent))
                {
                    matched.Add(simulationEvent);
                }
            }

            next = record.EventsTrimmed + events.Count;
            list.RefreshItems();
        }

        // Another filter: every event kept is looked at again.
        private void Rebuild()
        {
            matched.Clear();
            next = 0;
            Refresh();
        }

        // Newest first: the last of the list at the top.
        private void Bind(VisualElement element, int index)
        {
            SimulationEvent simulationEvent = matched[matched.Count - 1 - index];
            ((Label)element[0]).text = recorder.FormatDay(simulationEvent.Day);
            ((Label)element[1]).text = cityNames[simulationEvent.City];
            ((Label)element[2]).text = DebugEventText.TypeName(simulationEvent.Type);
            ((Label)element[3]).text = DebugEventText.Detail(simulationEvent, goodNames);
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.AddToClassList("debug-cities__row");

            foreach (string cellClass in CellClasses)
            {
                var label = new Label();
                label.AddToClassList("debug-cities__cell");
                label.AddToClassList(cellClass);
                row.Add(label);
            }

            return row;
        }

        private static List<string> WithAll(IReadOnlyList<string> names, int[] order)
        {
            var choices = new List<string> { All };

            foreach (int index in order)
            {
                choices.Add(names[index]);
            }

            return choices;
        }

        private static DropdownField Dropdown(VisualElement toolbar, string label, List<string> choices)
        {
            var field = new DropdownField(label, choices, 0) { focusable = false };
            field.AddToClassList("debug-panel__dropdown");
            field.AddToClassList("debug-panel__dropdown--labelled");
            toolbar.Add(field);
            return field;
        }
    }
}
