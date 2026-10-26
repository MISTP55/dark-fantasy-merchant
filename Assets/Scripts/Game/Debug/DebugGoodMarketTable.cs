using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// One good in every city's market, for the debug panel: its stock, its thresholds, its prices,
    /// what the city produces, consumes, takes as inputs and gains of it in a week (nominal, then the last days in
    /// brackets), its workers and what the AI ships carry there, and a last row with the totals.
    /// The cities are in alphabetical order. Follows the markets while it is shown, writing what changed at most once a frame.
    /// </summary>
    public sealed class DebugGoodMarketTable
    {
        private const string RowClass = "debug-cities__row";
        private const string HeaderRowClass = "debug-cities__row--header";
        private const string TotalRowClass = "debug-cities__row--total";
        private const string CellClass = "debug-cities__cell";

        private static readonly string[] Titles =
        {
            "Ville", "Stock", "T0", "T1", "T2", "T3", "Achat", "Vente", "Prod.", "Conso.", "Intr.", "Net", "Ouv.", "En route (IA)",
        };

        private static readonly string[] CellClasses =
        {
            "debug-cities__cell--name", "debug-cities__cell--stock", "debug-cities__cell--threshold",
            "debug-cities__cell--threshold", "debug-cities__cell--threshold", "debug-cities__cell--threshold",
            "debug-cities__cell--price", "debug-cities__cell--price", "debug-cities__cell--figure",
            "debug-cities__cell--figure", "debug-cities__cell--figure", "debug-cities__cell--figure", "debug-cities__cell--workers",
            "debug-cities__cell--under-way",
        };

        private sealed class Row
        {
            public string Name;
            public CityMarket Market;
            public Func<int, int> UnderWay;
            public Label[] Cells;
            public Action Refresh;
            public bool IsDirty;
        }

        private readonly VisualElement rowContainer;
        private readonly List<Row> rows = new List<Row>();
        private readonly Label[] totals;

        private int good;
        private bool isShown;

        // A row has changed since it was last written.
        private bool hasChanges;

        public DebugGoodMarketTable(VisualElement header, VisualElement rowContainer, VisualElement totalContainer)
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

            VisualElement headerRow = NewRow(HeaderRowClass);
            Label[] titleCells = AddCells(headerRow);

            for (int i = 0; i < Titles.Length; i++)
            {
                titleCells[i].text = Titles[i];
            }

            header.Add(headerRow);

            VisualElement totalRow = NewRow(TotalRowClass);
            totals = AddCells(totalRow);
            totals[0].text = "Total";
            totalContainer.Add(totalRow);
        }

        /// <param name="underWay">Units of a good the AI ships carry to the city.</param>
        public void AddCity(string cityName, CityMarket market, Func<int, int> underWay)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            VisualElement element = NewRow();
            var row = new Row { Name = cityName, Market = market, UnderWay = underWay ?? (_ => 0), Cells = AddCells(element) };
            row.Cells[0].text = cityName;

            // Marked only: a market changes at every trade, many times a frame at high speed, and the row and the
            // totals are written once a frame (WriteChanges).
            row.Refresh = () =>
            {
                row.IsDirty = true;
                hasChanges = true;
            };

            int index = rows.FindIndex(other => NameOrder.Compare(other.Name, cityName) > 0);

            if (index < 0)
            {
                index = rows.Count;
            }

            rows.Insert(index, row);
            rowContainer.Insert(index, element);
            ListStripes.Apply(rowContainer);
            Write(row);
            WriteTotals();

            if (isShown)
            {
                market.Changed += row.Refresh;
            }
        }

        /// <summary>Shows another good, its index in the catalogue.</summary>
        public void SetGood(int shownGood)
        {
            good = shownGood;
            WriteAll();
        }

        /// <summary>Rewrites every row and follows the markets from now on.</summary>
        public void Show()
        {
            if (!isShown)
            {
                foreach (Row row in rows)
                {
                    row.Market.Changed += row.Refresh;
                }
            }

            isShown = true;
            WriteAll();
        }

        /// <summary>Stops following the markets: a hidden table costs nothing.</summary>
        public void Hide()
        {
            if (!isShown)
            {
                return;
            }

            foreach (Row row in rows)
            {
                row.Market.Changed -= row.Refresh;
            }

            isShown = false;
        }

        /// <summary>Rewrites every row: what is under way changes without the markets.</summary>
        public void WriteAll()
        {
            foreach (Row row in rows)
            {
                Write(row);
                row.IsDirty = false;
            }

            WriteTotals();
            hasChanges = false;
        }

        /// <summary>
        /// Rewrites the rows whose market changed since they were last written, and the totals when one did. Called
        /// once a frame while the table is shown.
        /// </summary>
        public void WriteChanges()
        {
            if (!hasChanges)
            {
                return;
            }

            foreach (Row row in rows)
            {
                if (row.IsDirty)
                {
                    Write(row);
                    row.IsDirty = false;
                }
            }

            WriteTotals();
            hasChanges = false;
        }

        private void Write(Row row)
        {
            Label[] cells = row.Cells;
            CityMarket market = row.Market;

            // A market made from another catalogue than the table's: nothing to show there.
            if (good >= market.GoodCount)
            {
                for (int i = 1; i < cells.Length; i++)
                {
                    cells[i].text = DebugCitiesTable.NoFigure;
                }

                return;
            }

            StockThresholds thresholds = market.ThresholdsOf(good);
            cells[1].text = TreasuryHudController.FormatNumber(market.AvailableOf(good));
            cells[2].text = DebugCitiesTable.FormatQuantity(thresholds.T0);
            cells[3].text = DebugCitiesTable.FormatQuantity(thresholds.T1);
            cells[4].text = DebugCitiesTable.FormatQuantity(thresholds.T2);
            cells[5].text = DebugCitiesTable.FormatQuantity(thresholds.T3);
            cells[6].text = TreasuryHudController.FormatNumber(market.BuyPriceOf(good));
            cells[7].text = TreasuryHudController.FormatNumber(market.SellPriceOf(good));
            WriteFigures(
                cells,
                market.NominalWeeklyProductionOf(good),
                market.NominalWeeklyConsumptionOf(good),
                market.NominalWeeklyInputNeedOf(good),
                market.RecentProductionOf(good),
                market.RecentConsumptionOf(good),
                market.RecentInputsOf(good),
                market.WorkersOf(good),
                market.MaxWorkersOf(good),
                market.ReserveOf(good),
                row.UnderWay(good));
        }

        private void WriteTotals()
        {
            long stock = 0;
            double nominalProduction = 0d;
            double nominalConsumption = 0d;
            double nominalInputs = 0d;
            double recentProduction = 0d;
            double recentConsumption = 0d;
            double recentInputs = 0d;
            long workers = 0;
            long maxWorkers = 0;
            long reserve = 0;
            long underWay = 0;

            foreach (Row row in rows)
            {
                CityMarket market = row.Market;

                // A market that does not have the good adds nothing to its totals.
                if (good >= market.GoodCount)
                {
                    continue;
                }

                stock += market.AvailableOf(good);
                nominalProduction += market.NominalWeeklyProductionOf(good);
                nominalConsumption += market.NominalWeeklyConsumptionOf(good);
                nominalInputs += market.NominalWeeklyInputNeedOf(good);
                recentProduction += market.RecentProductionOf(good);
                recentConsumption += market.RecentConsumptionOf(good);
                recentInputs += market.RecentInputsOf(good);
                workers += market.WorkersOf(good);
                maxWorkers += market.MaxWorkersOf(good);
                reserve += market.ReserveOf(good);
                underWay += row.UnderWay(good);
            }

            totals[1].text = TreasuryHudController.FormatNumber(stock);

            // Thresholds and prices are a city's own: they do not add up.
            for (int i = 2; i <= 7; i++)
            {
                totals[i].text = DebugCitiesTable.NoFigure;
            }

            WriteFigures(
                totals,
                nominalProduction,
                nominalConsumption,
                nominalInputs,
                recentProduction,
                recentConsumption,
                recentInputs,
                workers,
                maxWorkers,
                reserve,
                underWay);
        }

        private static void WriteFigures(
            Label[] cells,
            double nominalProduction,
            double nominalConsumption,
            double nominalInputs,
            double recentProduction,
            double recentConsumption,
            double recentInputs,
            long workers,
            long maxWorkers,
            long reserve,
            long underWay)
        {
            cells[8].text = DebugCitiesTable.FormatFigure(nominalProduction, recentProduction);
            cells[9].text = DebugCitiesTable.FormatFigure(nominalConsumption, recentConsumption);
            cells[10].text = DebugCitiesTable.FormatFigure(nominalInputs, recentInputs);
            cells[11].text = $"{DebugCitiesTable.FormatNet(nominalProduction - nominalConsumption - nominalInputs)} "
                + $"({DebugCitiesTable.FormatNet(recentProduction - recentConsumption - recentInputs)})";
            cells[12].text = DebugCitiesTable.FormatWorkers(workers, maxWorkers, reserve);
            cells[13].text = TreasuryHudController.FormatNumber(underWay);
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
