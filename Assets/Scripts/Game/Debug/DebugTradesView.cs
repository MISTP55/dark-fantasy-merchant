using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The trades tab of the debug panel: the flows of a good between the cities, what the AI
    /// merchants and the player sold to each and bought from each over the history kept.
    /// </summary>
    public sealed class DebugTradesView : IDebugView
    {
        private readonly SimulationRecorder recorder;
        private readonly List<string> cityNames = new List<string>();
        private readonly int[] goodOrder;
        private readonly DropdownField goodField;
        private readonly DebugFlowsTable flows;

        // The flows of every city, reused.
        private readonly long[] received;
        private readonly long[] shipped;

        private bool isShown;

        /// <param name="page">The page the view fills.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        public DebugTradesView(VisualElement page, SimulationRecorder recorder, IReadOnlyList<string> goodNames)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (goodNames == null)
            {
                throw new ArgumentNullException(nameof(goodNames));
            }

            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));

            foreach (CityDefinition city in recorder.Cities)
            {
                cityNames.Add(city.DisplayName);
            }

            goodOrder = NameOrder.Of(goodNames);
            received = new long[cityNames.Count];
            shipped = new long[cityNames.Count];

            page.Clear();
            var toolbar = new VisualElement();
            toolbar.AddToClassList("debug-curves__toolbar");
            page.Add(toolbar);

            var goodChoices = new List<string>();

            foreach (int good in goodOrder)
            {
                goodChoices.Add(goodNames[good]);
            }

            goodField = new DropdownField("Marchandise", goodChoices, 0) { focusable = false };
            goodField.AddToClassList("debug-panel__dropdown");
            goodField.AddToClassList("debug-panel__dropdown--labelled");
            goodField.RegisterValueChangedCallback(change => Refresh());
            toolbar.Add(goodField);

            var legend = new Label("Unités vendues à la ville (reçu) et achetées à la ville (expédié) par les marchands IA et le joueur, sur l'historique gardé.");
            legend.AddToClassList("debug-cities__legend");
            page.Add(legend);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("debug-cities__scroll");
            page.Add(scroll);
            var header = new VisualElement();
            var rows = new VisualElement();
            var totals = new VisualElement();
            scroll.Add(header);
            scroll.Add(rows);
            scroll.Add(totals);
            flows = new DebugFlowsTable(header, rows, totals);
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

        public void Refresh()
        {
            if (!isShown)
            {
                return;
            }

            // Every city in one pass over the trades: this runs on most frames at x600.
            recorder.Record.FlowsOf(goodOrder[Math.Max(0, goodField.index)], received, shipped);
            flows.Write(cityNames, city => (received[city], shipped[city]));
        }
    }
}
