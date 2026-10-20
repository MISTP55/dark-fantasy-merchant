using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The trades tab of the debug panel, in two sub-tabs: the journal of every purchase and sale
    /// of the AI merchants and of the player, newest first, and the flows of a good between the cities.
    /// </summary>
    public sealed class DebugTradesView : IDebugView
    {
        private const int JournalTab = 0;
        private const float RowHeight = 22f;

        private static readonly string[] Titles =
        {
            "Date", "Navire", "Ville", "Opération", "Marchandise", "Qté", "Prix moyen", "Total", "Bénéfice",
        };

        private static readonly string[] CellClasses =
        {
            "debug-events__cell--date", "debug-trades__cell--ship", "debug-events__cell--city", "debug-trades__cell--operation",
            "debug-trades__cell--good", "debug-trades__cell--number", "debug-trades__cell--number", "debug-trades__cell--number",
            "debug-trades__cell--number",
        };

        private readonly DebugTabs subtabs;
        private readonly SimulationRecorder recorder;
        private readonly Func<Ship, string> shipName;
        private readonly IReadOnlyList<string> goodNames;
        private readonly List<string> cityNames = new List<string>();
        private readonly int[] goodOrder;
        private readonly ListView list;
        private readonly DropdownField flowGoodField;
        private readonly DebugFlowsTable flows;

        // The flows of every city, reused.
        private readonly long[] received;
        private readonly long[] shipped;

        // The trades shown, oldest first, and the position of the first one not looked at yet.
        private readonly List<TradeRecord> shown = new List<TradeRecord>();
        private long next;
        private bool isShown;

        /// <param name="journalPage">The sub-page of the journal.</param>
        /// <param name="flowsPage">The sub-page of the flows.</param>
        /// <param name="shipName">The name of an AI ship, as the map gives it.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        public DebugTradesView(
            DebugTabs subtabs,
            VisualElement journalPage,
            VisualElement flowsPage,
            SimulationRecorder recorder,
            Func<Ship, string> shipName,
            IReadOnlyList<string> goodNames)
        {
            if (journalPage == null)
            {
                throw new ArgumentNullException(nameof(journalPage));
            }

            if (flowsPage == null)
            {
                throw new ArgumentNullException(nameof(flowsPage));
            }

            this.subtabs = subtabs ?? throw new ArgumentNullException(nameof(subtabs));
            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            this.shipName = shipName;
            this.goodNames = goodNames ?? throw new ArgumentNullException(nameof(goodNames));

            foreach (CityDefinition city in recorder.Cities)
            {
                cityNames.Add(city.DisplayName);
            }

            goodOrder = NameOrder.Of(goodNames);
            received = new long[cityNames.Count];
            shipped = new long[cityNames.Count];

            journalPage.Clear();
            var legend = new Label("Les achats et ventes des marchands IA et du joueur, et les matériaux des constructions du joueur. Le bénéfice d'une vente IA est son prix moins le coût moyen des unités vendues.");
            legend.AddToClassList("debug-cities__legend");
            journalPage.Add(legend);

            VisualElement header = Row();

            for (int i = 0; i < Titles.Length; i++)
            {
                ((Label)header[i]).text = Titles[i];
            }

            header.AddToClassList("debug-cities__row--header");
            journalPage.Add(header);

            list = new ListView(shown, RowHeight, Row, Bind)
            {
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                focusable = false,
            };
            list.AddToClassList("debug-journal");
            journalPage.Add(list);

            flowsPage.Clear();
            var toolbar = new VisualElement();
            toolbar.AddToClassList("debug-curves__toolbar");
            flowsPage.Add(toolbar);

            var goodChoices = new List<string>();

            foreach (int good in goodOrder)
            {
                goodChoices.Add(goodNames[good]);
            }

            flowGoodField = new DropdownField("Marchandise", goodChoices, 0) { focusable = false };
            flowGoodField.AddToClassList("debug-panel__dropdown");
            flowGoodField.AddToClassList("debug-panel__dropdown--labelled");
            flowGoodField.RegisterValueChangedCallback(change => Refresh());
            toolbar.Add(flowGoodField);

            var flowsLegend = new Label("Unités vendues à la ville (reçu) et achetées à la ville (expédié) par les marchands IA et le joueur, sur l'historique gardé.");
            flowsLegend.AddToClassList("debug-cities__legend");
            flowsPage.Add(flowsLegend);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("debug-cities__scroll");
            flowsPage.Add(scroll);
            var flowsHeader = new VisualElement();
            var flowsRows = new VisualElement();
            var flowsTotals = new VisualElement();
            scroll.Add(flowsHeader);
            scroll.Add(flowsRows);
            scroll.Add(flowsTotals);
            flows = new DebugFlowsTable(flowsHeader, flowsRows, flowsTotals);

            subtabs.Changed += index => Refresh();

            if (subtabs.Selected < 0)
            {
                subtabs.Select(JournalTab);
            }
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

            if (subtabs.Selected == JournalTab)
            {
                RefreshJournal();
            }
            else
            {
                // Every city in one pass over the trades: this runs on most frames at x600.
                recorder.Record.FlowsOf(goodOrder[Math.Max(0, flowGoodField.index)], received, shipped);
                flows.Write(cityNames, city => (received[city], shipped[city]));
            }
        }

        // Forgets what the record forgot and adds what it has kept since.
        private void RefreshJournal()
        {
            SimulationRecord record = recorder.Record;
            int firstDay = record.History.FirstDay;
            int forgotten = 0;

            while (forgotten < shown.Count && shown[forgotten].Day < firstDay)
            {
                forgotten++;
            }

            shown.RemoveRange(0, forgotten);

            IReadOnlyList<TradeRecord> trades = record.Trades;

            for (long position = Math.Max(next, record.TradesTrimmed); position < record.TradesTrimmed + trades.Count; position++)
            {
                shown.Add(trades[(int)(position - record.TradesTrimmed)]);
            }

            next = record.TradesTrimmed + trades.Count;
            list.RefreshItems();
        }

        // Newest first: the last of the list at the top.
        private void Bind(VisualElement element, int index)
        {
            TradeRecord trade = shown[shown.Count - 1 - index];
            ((Label)element[0]).text = recorder.FormatDay(trade.Day);
            ((Label)element[1]).text = DebugTradeText.Trader(trade, shipName);
            ((Label)element[2]).text = cityNames[trade.City];
            ((Label)element[3]).text = DebugTradeText.Operation(trade);
            ((Label)element[4]).text = trade.Good < goodNames.Count ? goodNames[trade.Good] : DebugCitiesTable.NoFigure;
            ((Label)element[5]).text = TreasuryHudController.FormatNumber(trade.Units);
            ((Label)element[6]).text = DebugTradeText.UnitPrice(trade);
            ((Label)element[7]).text = TreasuryHudController.FormatNumber(trade.Gold);
            ((Label)element[8]).text = DebugTradeText.Profit(trade);
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
    }
}
