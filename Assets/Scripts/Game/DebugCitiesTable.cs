using System;
using System.Collections.Generic;
using System.Globalization;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The table of the cities debug panel: one row per city with its population and, for
    /// every good, its stock and what it produces, consumes and gains in a week, the
    /// nominal figure first and the one of the last days in brackets, and a last row with
    /// the total of every column. Follows the markets while it is shown.
    /// </summary>
    public sealed class DebugCitiesTable
    {
        /// <summary>Written in the cells of a good that a city's market does not have.</summary>
        public const string NoFigure = "—";

        /// <summary>Rich text colour of a net gain.</summary>
        public const string PositiveColor = "#8FCB7A";

        /// <summary>Rich text colour of a net loss.</summary>
        public const string NegativeColor = "#E0705A";

        private const string RowClass = "debug-cities__row";
        private const string HeaderRowClass = "debug-cities__row--header";
        private const string TotalRowClass = "debug-cities__row--total";
        private const string CellClass = "debug-cities__cell";
        private const string NameCellClass = "debug-cities__cell--name";
        private const string PopulationCellClass = "debug-cities__cell--population";
        private const string StockCellClass = "debug-cities__cell--stock";
        private const string FigureCellClass = "debug-cities__cell--figure";
        private const string CityGroupClass = "debug-cities__cell--city-group";
        private const string GoodGroupClass = "debug-cities__cell--good-group";

        // Not the French culture: its separators and its minus sign depend on the platform.
        private static readonly NumberFormatInfo BarrelFormat = new NumberFormatInfo
        {
            NumberGroupSeparator = "\u00A0",
            NumberDecimalSeparator = ",",
        };

        private sealed class GoodCells
        {
            public Label Stock;
            public Label Production;
            public Label Consumption;
            public Label Net;
        }

        private sealed class Row
        {
            public int Population;
            public CityMarket Market;
            public GoodCells[] Goods;
            public Action Refresh;
        }

        private readonly VisualElement rowContainer;
        private readonly int goodCount;
        private readonly List<Row> rows = new List<Row>();
        private readonly Label totalPopulation;
        private readonly GoodCells[] totalGoods;

        private bool isShown;

        /// <param name="header">Where the two header rows are built.</param>
        /// <param name="rowContainer">Where the rows are built, one per city added.</param>
        /// <param name="totalContainer">Where the row of the totals is built.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        public DebugCitiesTable(
            VisualElement header,
            VisualElement rowContainer,
            VisualElement totalContainer,
            IReadOnlyList<string> goodNames)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (totalContainer == null)
            {
                throw new ArgumentNullException(nameof(totalContainer));
            }

            if (goodNames == null)
            {
                throw new ArgumentNullException(nameof(goodNames));
            }

            this.rowContainer = rowContainer ?? throw new ArgumentNullException(nameof(rowContainer));
            goodCount = goodNames.Count;

            header.Clear();
            rowContainer.Clear();
            totalContainer.Clear();
            BuildHeader(header, goodNames);

            var totalRow = new VisualElement();
            totalRow.AddToClassList(RowClass);
            totalRow.AddToClassList(TotalRowClass);
            AddLabel(totalRow, NameCellClass).text = "Total";
            totalPopulation = AddLabel(totalRow, PopulationCellClass);
            totalGoods = AddGoodCells(totalRow, goodCount);
            totalContainer.Add(totalRow);
            WriteTotals();
        }

        /// <summary>Barrels as the table writes them: "1 234,6", one decimal, the French way.</summary>
        public static string FormatBarrels(double barrels)
        {
            return Rounded(barrels).ToString("N1", BarrelFormat);
        }

        /// <summary>A net gain as the table writes it: "+42,0" or "-5,6", coloured, or a plain "0,0".</summary>
        public static string FormatNet(double barrels)
        {
            double rounded = Rounded(barrels);
            string text = rounded.ToString("N1", BarrelFormat);

            if (rounded > 0d)
            {
                return $"<color={PositiveColor}>+{text}</color>";
            }

            return rounded < 0d ? $"<color={NegativeColor}>{text}</color>" : text;
        }

        /// <summary>Adds the row of a city. It is written at once, and then follows the market while the table is shown.</summary>
        public void AddCity(string cityName, int population, CityMarket market)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            var element = new VisualElement();
            element.AddToClassList(RowClass);

            AddLabel(element, NameCellClass).text = cityName;
            AddLabel(element, PopulationCellClass).text = TreasuryHudController.FormatNumber(population);

            var row = new Row
            {
                Population = population,
                Market = market,
                Goods = AddGoodCells(element, goodCount),
            };

            // A market that changes changes the totals too.
            row.Refresh = () =>
            {
                Write(row);
                WriteTotals();
            };

            rows.Add(row);
            rowContainer.Add(element);
            row.Refresh();

            if (isShown)
            {
                market.Changed += row.Refresh;
            }
        }

        /// <summary>Rewrites every row and follows the markets from now on.</summary>
        public void Show()
        {
            foreach (Row row in rows)
            {
                if (!isShown)
                {
                    row.Market.Changed += row.Refresh;
                }

                Write(row);
            }

            WriteTotals();
            isShown = true;
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

        // A value that rounds to nothing is written without a sign.
        private static double Rounded(double barrels)
        {
            double rounded = Math.Round(barrels, 1, MidpointRounding.AwayFromZero);

            return rounded == 0d ? 0d : rounded;
        }

        private static string FormatFigure(double nominal, double recent)
        {
            return $"{FormatBarrels(nominal)} ({FormatBarrels(recent)})";
        }

        private void Write(Row row)
        {
            CityMarket market = row.Market;

            for (int i = 0; i < row.Goods.Length; i++)
            {
                GoodCells cells = row.Goods[i];

                // A market made from another catalogue than the columns: nothing to show there.
                if (i >= market.GoodCount)
                {
                    cells.Stock.text = NoFigure;
                    cells.Production.text = NoFigure;
                    cells.Consumption.text = NoFigure;
                    cells.Net.text = NoFigure;
                    continue;
                }

                Write(
                    cells,
                    market.AvailableOf(i),
                    market.NominalWeeklyProductionOf(i),
                    market.NominalWeeklyConsumptionOf(i),
                    market.RecentProductionOf(i),
                    market.RecentConsumptionOf(i));
            }
        }

        // Summed again on every change: a handful of cities, once a day each.
        private void WriteTotals()
        {
            long population = 0;

            foreach (Row row in rows)
            {
                population += row.Population;
            }

            totalPopulation.text = TreasuryHudController.FormatNumber(population);

            for (int i = 0; i < totalGoods.Length; i++)
            {
                long stock = 0;
                double nominalProduction = 0d;
                double nominalConsumption = 0d;
                double recentProduction = 0d;
                double recentConsumption = 0d;

                foreach (Row row in rows)
                {
                    CityMarket market = row.Market;

                    // A market that does not have the good adds nothing to its totals.
                    if (i >= market.GoodCount)
                    {
                        continue;
                    }

                    // The whole barrels, as the rows write them.
                    stock += market.AvailableOf(i);
                    nominalProduction += market.NominalWeeklyProductionOf(i);
                    nominalConsumption += market.NominalWeeklyConsumptionOf(i);
                    recentProduction += market.RecentProductionOf(i);
                    recentConsumption += market.RecentConsumptionOf(i);
                }

                Write(totalGoods[i], stock, nominalProduction, nominalConsumption, recentProduction, recentConsumption);
            }
        }

        private static void Write(
            GoodCells cells,
            long stock,
            double nominalProduction,
            double nominalConsumption,
            double recentProduction,
            double recentConsumption)
        {
            cells.Stock.text = TreasuryHudController.FormatNumber(stock);
            cells.Production.text = FormatFigure(nominalProduction, recentProduction);
            cells.Consumption.text = FormatFigure(nominalConsumption, recentConsumption);
            cells.Net.text = $"{FormatNet(nominalProduction - nominalConsumption)} "
                + $"({FormatNet(recentProduction - recentConsumption)})";
        }

        private static GoodCells[] AddGoodCells(VisualElement row, int goodCount)
        {
            var goods = new GoodCells[goodCount];

            for (int i = 0; i < goodCount; i++)
            {
                goods[i] = new GoodCells
                {
                    Stock = AddLabel(row, StockCellClass),
                    Production = AddLabel(row, FigureCellClass),
                    Consumption = AddLabel(row, FigureCellClass),
                    Net = AddLabel(row, FigureCellClass),
                };
            }

            return goods;
        }

        // Two rows: the goods over their columns, then the name of every column.
        private static void BuildHeader(VisualElement header, IReadOnlyList<string> goodNames)
        {
            var groups = new VisualElement();
            groups.AddToClassList(RowClass);
            groups.AddToClassList(HeaderRowClass);
            AddLabel(groups, CityGroupClass);

            var columns = new VisualElement();
            columns.AddToClassList(RowClass);
            columns.AddToClassList(HeaderRowClass);
            AddLabel(columns, NameCellClass).text = "Ville";
            AddLabel(columns, PopulationCellClass).text = "Population";

            foreach (string goodName in goodNames)
            {
                AddLabel(groups, GoodGroupClass).text = goodName;
                AddLabel(columns, StockCellClass).text = "Stock";
                AddLabel(columns, FigureCellClass).text = "Prod.";
                AddLabel(columns, FigureCellClass).text = "Conso.";
                AddLabel(columns, FigureCellClass).text = "Net";
            }

            header.Add(groups);
            header.Add(columns);
        }

        private static Label AddLabel(VisualElement parent, string cellClass)
        {
            var label = new Label();
            label.AddToClassList(CellClass);
            label.AddToClassList(cellClass);
            parent.Add(label);
            return label;
        }
    }
}
