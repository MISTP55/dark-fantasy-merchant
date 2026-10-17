using System;
using System.Collections.Generic;
using System.Globalization;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The table of the cities debug panel: one row per city with its population,
    /// its beggars over the number they tend to, its housed inhabitants over its room, how much it has grown or declined since the game started and, for every good, its stock and what it produces, consumes and
    /// gains in a week, the nominal figure first and the one of the last days in brackets,
    /// the workers of its workshop over their maximum and its reserve,
    /// and a last row with the total of every column. Follows the markets while it is shown.
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
        private const string BeggarsCellClass = "debug-cities__cell--beggars";
        private const string HousingCellClass = "debug-cities__cell--housing";
        private const string GrowthSinceStartCellClass = "debug-cities__cell--growth-since-start";
        private const string StockCellClass = "debug-cities__cell--stock";
        private const string FigureCellClass = "debug-cities__cell--figure";
        private const string WorkersCellClass = "debug-cities__cell--workers";
        private const string CityGroupClass = "debug-cities__cell--city-group";
        private const string GoodGroupClass = "debug-cities__cell--good-group";

        // Not the French culture: its separators and its minus sign depend on the platform.
        private static readonly NumberFormatInfo QuantityFormat = new NumberFormatInfo
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
            public Label Workers;
        }

        private sealed class Row
        {
            public Label Population;
            public Label Beggars;
            public Label Housing;
            public Label GrowthSinceStart;
            public CityMarket Market;
            public GoodCells[] Goods;
            public Action Refresh;
        }

        private readonly VisualElement rowContainer;
        private readonly int goodCount;
        private readonly List<Row> rows = new List<Row>();
        private readonly Label totalPopulation;
        private readonly Label totalBeggars;
        private readonly Label totalHousing;
        private readonly Label totalGrowthSinceStart;
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
            totalBeggars = AddLabel(totalRow, BeggarsCellClass);
            totalHousing = AddLabel(totalRow, HousingCellClass);
            totalGrowthSinceStart = AddLabel(totalRow, GrowthSinceStartCellClass);
            totalGoods = AddGoodCells(totalRow, goodCount);
            totalContainer.Add(totalRow);
            WriteTotals();
        }

        /// <summary>A quantity as the table writes it, in the unit of its good: "1 234,6", one decimal, the French way.</summary>
        public static string FormatQuantity(double quantity)
        {
            return Rounded(quantity).ToString("N1", QuantityFormat);
        }

        /// <summary>A net gain as the table writes it: "+42,0" or "-5,6", coloured, or a plain "0,0".</summary>
        public static string FormatNet(double quantity)
        {
            double rounded = Rounded(quantity);
            string text = rounded.ToString("N1", QuantityFormat);

            if (rounded > 0d)
            {
                return $"<color={PositiveColor}>+{text}</color>";
            }

            return rounded < 0d ? $"<color={NegativeColor}>{text}</color>" : text;
        }

        /// <summary>
        /// Workers as the table writes them: "45/60", followed by the reserve ("45/60 +10") when there is one,
        /// or a dash where no workshop can have any.
        /// </summary>
        public static string FormatWorkers(long workers, long maxWorkers, long reserve = 0)
        {
            if (maxWorkers <= 0)
            {
                return NoFigure;
            }

            return reserve > 0 ? $"{workers}/{maxWorkers} +{reserve}" : $"{workers}/{maxWorkers}";
        }

        /// <summary>Beggars as the table writes them, with the number they tend to: "59 (62)".</summary>
        public static string FormatBeggars(long beggars, long target)
        {
            return $"{beggars} ({target})";
        }

        /// <summary>The housed inhabitants over the room of the houses: "1 008 / 1 100".</summary>
        public static string FormatHousing(long housed, long room)
        {
            return $"{TreasuryHudController.FormatNumber(housed)} / {TreasuryHudController.FormatNumber(room)}";
        }

        /// <summary>A rate as the table writes it: "+2,5 %" or "-10,0 %", coloured, or a plain "0,0 %".</summary>
        /// <param name="rate">A share: 0.05 is +5 %.</param>
        public static string FormatRate(double rate)
        {
            double rounded = Rounded(rate * 100d);
            string text = $"{rounded.ToString("N1", QuantityFormat)}\u00A0%";

            if (rounded > 0d)
            {
                return $"<color={PositiveColor}>+{text}</color>";
            }

            return rounded < 0d ? $"<color={NegativeColor}>{text}</color>" : text;
        }

        /// <summary>Adds the row of a city. It is written at once, and then follows the market while the table is shown.</summary>
        public void AddCity(string cityName, CityMarket market)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            var element = new VisualElement();
            element.AddToClassList(RowClass);

            AddLabel(element, NameCellClass).text = cityName;

            var row = new Row
            {
                Population = AddLabel(element, PopulationCellClass),
                Beggars = AddLabel(element, BeggarsCellClass),
                Housing = AddLabel(element, HousingCellClass),
                GrowthSinceStart = AddLabel(element, GrowthSinceStartCellClass),
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
        private static double Rounded(double value)
        {
            double rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);

            return rounded == 0d ? 0d : rounded;
        }

        private static string FormatFigure(double nominal, double recent)
        {
            return $"{FormatQuantity(nominal)} ({FormatQuantity(recent)})";
        }

        private void Write(Row row)
        {
            CityMarket market = row.Market;

            row.Population.text = TreasuryHudController.FormatNumber(market.Population.Inhabitants);
            row.Beggars.text = FormatBeggars(market.Population.Beggars, market.BeggarTarget);
            row.Housing.text = FormatHousing(market.Population.Housed, market.Population.Room);
            row.GrowthSinceStart.text = FormatRate(market.Population.GrowthSinceStart);

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
                    cells.Workers.text = NoFigure;
                    continue;
                }

                Write(
                    cells,
                    market.AvailableOf(i),
                    market.NominalWeeklyProductionOf(i),
                    market.NominalWeeklyConsumptionOf(i),
                    market.RecentProductionOf(i),
                    market.RecentConsumptionOf(i),
                    market.WorkersOf(i),
                    market.MaxWorkersOf(i),
                    market.ReserveOf(i));
            }
        }

        // Summed again on every change: a handful of cities, once a day each.
        private void WriteTotals()
        {
            long inhabitants = 0;
            long startingInhabitants = 0;
            long beggars = 0;
            long beggarTargets = 0;
            long housed = 0;
            long room = 0;

            foreach (Row row in rows)
            {
                beggars += row.Market.Population.Beggars;
                beggarTargets += row.Market.BeggarTarget;
                housed += row.Market.Population.Housed;
                room += row.Market.Population.Room;
                inhabitants += row.Market.Population.Inhabitants;
                startingInhabitants += row.Market.Population.StartingInhabitants;
            }

            totalPopulation.text = TreasuryHudController.FormatNumber(inhabitants);
            totalBeggars.text = FormatBeggars(beggars, beggarTargets);
            totalHousing.text = FormatHousing(housed, room);

            // The growth of the world: all its inhabitants together, against all those it started with.
            totalGrowthSinceStart.text = FormatRate(
                startingInhabitants > 0 ? (double)inhabitants / startingInhabitants - 1d : 0d);

            for (int i = 0; i < totalGoods.Length; i++)
            {
                long stock = 0;
                double nominalProduction = 0d;
                double nominalConsumption = 0d;
                double recentProduction = 0d;
                double recentConsumption = 0d;
                long workers = 0;
                long maxWorkers = 0;
                long reserve = 0;

                foreach (Row row in rows)
                {
                    CityMarket market = row.Market;

                    // A market that does not have the good adds nothing to its totals.
                    if (i >= market.GoodCount)
                    {
                        continue;
                    }

                    // The whole units, as the rows write them.
                    stock += market.AvailableOf(i);
                    nominalProduction += market.NominalWeeklyProductionOf(i);
                    nominalConsumption += market.NominalWeeklyConsumptionOf(i);
                    recentProduction += market.RecentProductionOf(i);
                    recentConsumption += market.RecentConsumptionOf(i);
                    workers += market.WorkersOf(i);
                    maxWorkers += market.MaxWorkersOf(i);
                    reserve += market.ReserveOf(i);
                }

                Write(
                    totalGoods[i],
                    stock,
                    nominalProduction,
                    nominalConsumption,
                    recentProduction,
                    recentConsumption,
                    workers,
                    maxWorkers,
                    reserve);
            }
        }

        private static void Write(
            GoodCells cells,
            long stock,
            double nominalProduction,
            double nominalConsumption,
            double recentProduction,
            double recentConsumption,
            long workers,
            long maxWorkers,
            long reserve)
        {
            cells.Stock.text = TreasuryHudController.FormatNumber(stock);
            cells.Production.text = FormatFigure(nominalProduction, recentProduction);
            cells.Consumption.text = FormatFigure(nominalConsumption, recentConsumption);
            cells.Net.text = $"{FormatNet(nominalProduction - nominalConsumption)} "
                + $"({FormatNet(recentProduction - recentConsumption)})";
            cells.Workers.text = FormatWorkers(workers, maxWorkers, reserve);
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
                    Workers = AddLabel(row, WorkersCellClass),
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
            AddLabel(columns, BeggarsCellClass).text = "Mendiants";
            AddLabel(columns, HousingCellClass).text = "Logements";
            AddLabel(columns, GrowthSinceStartCellClass).text = "Croiss. totale";

            foreach (string goodName in goodNames)
            {
                AddLabel(groups, GoodGroupClass).text = goodName;
                AddLabel(columns, StockCellClass).text = "Stock";
                AddLabel(columns, FigureCellClass).text = "Prod.";
                AddLabel(columns, FigureCellClass).text = "Conso.";
                AddLabel(columns, FigureCellClass).text = "Net";
                AddLabel(columns, WorkersCellClass).text = "Ouv.";
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
