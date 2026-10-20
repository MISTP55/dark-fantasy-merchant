using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// What every city received and shipped of a good through the trades kept, for the debug
    /// panel, with the difference and a last row with the totals. The cities are in alphabetical order.
    /// </summary>
    public sealed class DebugFlowsTable
    {
        private const string RowClass = "debug-cities__row";
        private const string CellClass = "debug-cities__cell";

        private static readonly string[] CellClasses =
        {
            "debug-cities__cell--name", "debug-cities__cell--population", "debug-cities__cell--population", "debug-cities__cell--population",
        };

        private readonly VisualElement rowContainer;
        private readonly Label[] totals;

        public DebugFlowsTable(VisualElement header, VisualElement rowContainer, VisualElement totalContainer)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (totalContainer == null)
            {
                throw new ArgumentNullException(nameof(totalContainer));
            }

            this.rowContainer = rowContainer ?? throw new ArgumentNullException(nameof(rowContainer));
            header.Clear();
            rowContainer.Clear();
            totalContainer.Clear();

            VisualElement headerRow = NewRow("debug-cities__row--header");
            Label[] titles = AddCells(headerRow);
            titles[0].text = "Ville";
            titles[1].text = "Reçu";
            titles[2].text = "Expédié";
            titles[3].text = "Net";
            header.Add(headerRow);

            VisualElement totalRow = NewRow("debug-cities__row--total");
            totals = AddCells(totalRow);
            totals[0].text = "Total";
            totalContainer.Add(totalRow);
        }

        /// <param name="cityNames">The cities, in the order of their indices.</param>
        /// <param name="flowOf">The units a city received and shipped, by its index.</param>
        public void Write(IReadOnlyList<string> cityNames, Func<int, (long received, long shipped)> flowOf)
        {
            if (cityNames == null)
            {
                throw new ArgumentNullException(nameof(cityNames));
            }

            if (flowOf == null)
            {
                throw new ArgumentNullException(nameof(flowOf));
            }

            rowContainer.Clear();
            long received = 0;
            long shipped = 0;

            foreach (int city in NameOrder.Of(cityNames))
            {
                (long cityReceived, long cityShipped) = flowOf(city);
                VisualElement row = NewRow();
                Label[] cells = AddCells(row);
                cells[0].text = cityNames[city];
                WriteFigures(cells, cityReceived, cityShipped);
                rowContainer.Add(row);
                received += cityReceived;
                shipped += cityShipped;
            }

            ListStripes.Apply(rowContainer);
            WriteFigures(totals, received, shipped);
        }

        private static void WriteFigures(Label[] cells, long received, long shipped)
        {
            cells[1].text = TreasuryHudController.FormatNumber(received);
            cells[2].text = TreasuryHudController.FormatNumber(shipped);
            cells[3].text = DebugTradeText.SignedGold(received - shipped);
        }

        private static Label[] AddCells(VisualElement row)
        {
            var cells = new Label[CellClasses.Length];

            for (int i = 0; i < CellClasses.Length; i++)
            {
                cells[i] = new Label();
                cells[i].AddToClassList(CellClass);
                cells[i].AddToClassList(CellClasses[i]);
                row.Add(cells[i]);
            }

            return cells;
        }

        private static VisualElement NewRow(string rowClass = null)
        {
            var row = new VisualElement();
            row.AddToClassList(RowClass);

            if (rowClass != null)
            {
                row.AddToClassList(rowClass);
            }

            return row;
        }
    }
}
