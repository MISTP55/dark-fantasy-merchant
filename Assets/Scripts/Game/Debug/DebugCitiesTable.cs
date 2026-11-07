using System;
using System.Collections.Generic;
using System.Globalization;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The table of the cities debug panel: one row per city with its population,
    /// its beggars over the number they tend to, its housed over the room of the houses, how much it has grown or declined since the game started and, for every good, its stock and what it produces, consumes, takes as inputs and
    /// gains in a week, the nominal figure first and the one of the last days in brackets,
    /// the workers of its workshop over their maximum and its reserve,
    /// and a last row with the total of every column. Follows the markets while it is shown, writing what changed at most once a frame.
    /// The cities and the goods are in alphabetical order.
    /// The city's columns and the goods' are in two containers, so that only the goods' scroll sideways:
    /// UI Toolkit has no sticky position. The rows have one height, so that the two sides line up.
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
        private const string HousedCellClass = "debug-cities__cell--housed";
        private const string GrowthSinceStartCellClass = "debug-cities__cell--growth-since-start";
        private const string StockCellClass = "debug-cities__cell--stock";
        private const string FigureCellClass = "debug-cities__cell--figure";
        private const string WorkersCellClass = "debug-cities__cell--workers";
        private const string CityGroupClass = "debug-cities__cell--city-group";
        private const string GoodGroupClass = "debug-cities__cell--good-group";

        // Accents and case aside, as a French reader sorts: "Édimbourg" between "Cologne" and "Gdansk".

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
            public Label Inputs;
            public Label Net;
            public Label Workers;
        }

        private sealed class Row
        {
            public string Name;
            public Label Population;
            public Label Beggars;
            public Label Housed;
            public Label GrowthSinceStart;
            public City City;
            public GoodCells[] Goods;
            public Action Refresh;
            public bool IsDirty;
        }

        private readonly VisualElement rowContainer;
        private readonly VisualElement goodRowContainer;
        private readonly int goodCount;

        // The good of every column of goods: their indices in the catalogue's order, sorted by name.
        private readonly int[] goodOrder;
        private readonly List<Row> rows = new List<Row>();
        private readonly Label totalPopulation;
        private readonly Label totalBeggars;
        private readonly Label totalHoused;
        private readonly Label totalGrowthSinceStart;
        private readonly GoodCells[] totalGoods;

        private bool isShown;

        // A row has changed since it was last written.
        private bool hasChanges;

        /// <param name="header">Where the two header rows of the city's columns are built.</param>
        /// <param name="rowContainer">Where the city's columns of the rows are built, one row per city added.</param>
        /// <param name="totalContainer">Where the city's columns of the row of the totals are built.</param>
        /// <param name="goodHeader">Where the two header rows of the goods' columns are built.</param>
        /// <param name="goodRowContainer">Where the goods' columns of the rows are built, one row per city added.</param>
        /// <param name="goodTotalContainer">Where the goods' columns of the row of the totals are built.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order; their columns are in alphabetical order.</param>
        public DebugCitiesTable(
            VisualElement header,
            VisualElement rowContainer,
            VisualElement totalContainer,
            VisualElement goodHeader,
            VisualElement goodRowContainer,
            VisualElement goodTotalContainer,
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

            if (goodHeader == null)
            {
                throw new ArgumentNullException(nameof(goodHeader));
            }

            if (goodTotalContainer == null)
            {
                throw new ArgumentNullException(nameof(goodTotalContainer));
            }

            if (goodNames == null)
            {
                throw new ArgumentNullException(nameof(goodNames));
            }

            this.rowContainer = rowContainer ?? throw new ArgumentNullException(nameof(rowContainer));
            this.goodRowContainer = goodRowContainer ?? throw new ArgumentNullException(nameof(goodRowContainer));
            goodCount = goodNames.Count;
            goodOrder = NameOrder.Of(goodNames);

            header.Clear();
            rowContainer.Clear();
            totalContainer.Clear();
            goodHeader.Clear();
            goodRowContainer.Clear();
            goodTotalContainer.Clear();
            BuildHeader(header, goodHeader, goodNames, goodOrder);

            VisualElement totalRow = NewRow(TotalRowClass);
            AddLabel(totalRow, NameCellClass).text = "Total";
            totalPopulation = AddLabel(totalRow, PopulationCellClass);
            totalBeggars = AddLabel(totalRow, BeggarsCellClass);
            totalHoused = AddLabel(totalRow, HousedCellClass);
            totalGrowthSinceStart = AddLabel(totalRow, GrowthSinceStartCellClass);
            totalContainer.Add(totalRow);

            VisualElement goodTotalRow = NewRow(TotalRowClass);
            totalGoods = AddGoodCells(goodTotalRow, goodCount);
            goodTotalContainer.Add(goodTotalRow);
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
            return $"{NumberText.Format(beggars)} ({NumberText.Format(target)})";
        }

        /// <summary>
        /// The housed over the room of the houses: "110 / 120", or "1 108 / 1 400" for a total.
        /// </summary>
        public static string FormatHoused(long count, long room)
        {
            return $"{NumberText.Format(count)} / {NumberText.Format(room)}";
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

        /// <summary>
        /// Adds the row of a city, among the others in alphabetical order. It is written at once,
        /// and then follows the market while the table is shown.
        /// </summary>
        public void AddCity(string cityName, City city)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            VisualElement element = NewRow();
            VisualElement goodElement = NewRow();

            AddLabel(element, NameCellClass).text = cityName;

            var row = new Row
            {
                Name = cityName,
                Population = AddLabel(element, PopulationCellClass),
                Beggars = AddLabel(element, BeggarsCellClass),
                Housed = AddLabel(element, HousedCellClass),
                GrowthSinceStart = AddLabel(element, GrowthSinceStartCellClass),
                City = city,
                Goods = AddGoodCells(goodElement, goodCount),
            };

            // Marked only: a market changes at every trade, many times a frame at high speed, and the row and the
            // totals are written once a frame (WriteChanges).
            row.Refresh = () =>
            {
                row.IsDirty = true;
                hasChanges = true;
            };

            // After the cities of the same name, before the first that comes after it.
            int index = rows.FindIndex(other => NameOrder.Compare(other.Name, cityName) > 0);

            if (index < 0)
            {
                index = rows.Count;
            }

            rows.Insert(index, row);
            rowContainer.Insert(index, element);
            goodRowContainer.Insert(index, goodElement);

            // The two sides of a row take the same stripe.
            ListStripes.Apply(rowContainer);
            ListStripes.Apply(goodRowContainer);
            Write(row);
            WriteTotals();

            if (isShown)
            {
                city.Changed += row.Refresh;
            }
        }

        /// <summary>Rewrites every row and follows the markets from now on.</summary>
        public void Show()
        {
            foreach (Row row in rows)
            {
                if (!isShown)
                {
                    row.City.Changed += row.Refresh;
                }

                Write(row);
                row.IsDirty = false;
            }

            WriteTotals();
            hasChanges = false;
            isShown = true;
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

        /// <summary>Stops following the markets: a hidden table costs nothing.</summary>
        public void Hide()
        {
            if (!isShown)
            {
                return;
            }

            foreach (Row row in rows)
            {
                row.City.Changed -= row.Refresh;
            }

            isShown = false;
        }

        // A value that rounds to nothing is written without a sign.
        private static double Rounded(double value)
        {
            double rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);

            return rounded == 0d ? 0d : rounded;
        }

        /// <summary>A weekly figure as the table writes it: the nominal one, then that of the last days in brackets.</summary>
        public static string FormatFigure(double nominal, double recent)
        {
            return $"{FormatQuantity(nominal)} ({FormatQuantity(recent)})";
        }

        private void Write(Row row)
        {
            City city = row.City;

            row.Population.text = NumberText.Format(city.Population.Inhabitants);
            row.Beggars.text = FormatBeggars(city.Population.Beggars, city.BeggarTarget);
            row.Housed.text = FormatHoused(city.Population.Housed, city.Population.Room);
            row.GrowthSinceStart.text = FormatRate(city.Population.GrowthSinceStart);

            for (int column = 0; column < row.Goods.Length; column++)
            {
                GoodCells cells = row.Goods[column];
                int i = goodOrder[column];

                // A market made from another catalogue than the columns: nothing to show there.
                if (i >= city.Market.GoodCount)
                {
                    cells.Stock.text = NoFigure;
                    cells.Production.text = NoFigure;
                    cells.Consumption.text = NoFigure;
                    cells.Inputs.text = NoFigure;
                    cells.Net.text = NoFigure;
                    cells.Workers.text = NoFigure;
                    continue;
                }

                Write(
                    cells,
                    city.Market.UnitsOf(i),
                    city.NominalWeeklyProductionOf(i),
                    city.NominalWeeklyConsumptionOf(i),
                    city.NominalWeeklyInputNeedOf(i),
                    city.RecentProductionOf(i),
                    city.RecentConsumptionOf(i),
                    city.RecentInputsOf(i),
                    city.WorkersOf(i),
                    city.MaxWorkersOf(i),
                    city.ReserveOf(i));
            }
        }

        // The housed of every city together.
        private string FormatHousedTotal()
        {
            long count = 0;
            long room = 0;

            foreach (Row row in rows)
            {
                count += row.City.Population.Housed;
                room += row.City.Population.Room;
            }

            return FormatHoused(count, room);
        }

        // Summed again at most once a frame: a handful of cities.
        private void WriteTotals()
        {
            totalHoused.text = FormatHousedTotal();

            long inhabitants = 0;
            long startingInhabitants = 0;
            long beggars = 0;
            long beggarTargets = 0;

            foreach (Row row in rows)
            {
                beggars += row.City.Population.Beggars;
                beggarTargets += row.City.BeggarTarget;
                inhabitants += row.City.Population.Inhabitants;
                startingInhabitants += row.City.Population.StartingInhabitants;
            }

            totalPopulation.text = NumberText.Format(inhabitants);
            totalBeggars.text = FormatBeggars(beggars, beggarTargets);

            // The growth of the world: all its inhabitants together, against all those it started with.
            totalGrowthSinceStart.text = FormatRate(
                startingInhabitants > 0 ? (double)inhabitants / startingInhabitants - 1d : 0d);

            for (int column = 0; column < totalGoods.Length; column++)
            {
                int i = goodOrder[column];
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

                foreach (Row row in rows)
                {
                    City city = row.City;

                    // A market that does not have the good adds nothing to its totals.
                    if (i >= city.Market.GoodCount)
                    {
                        continue;
                    }

                    // The whole units, as the rows write them.
                    stock += city.Market.UnitsOf(i);
                    nominalProduction += city.NominalWeeklyProductionOf(i);
                    nominalConsumption += city.NominalWeeklyConsumptionOf(i);
                    nominalInputs += city.NominalWeeklyInputNeedOf(i);
                    recentProduction += city.RecentProductionOf(i);
                    recentConsumption += city.RecentConsumptionOf(i);
                    recentInputs += city.RecentInputsOf(i);
                    workers += city.WorkersOf(i);
                    maxWorkers += city.MaxWorkersOf(i);
                    reserve += city.ReserveOf(i);
                }

                Write(
                    totalGoods[column],
                    stock,
                    nominalProduction,
                    nominalConsumption,
                    nominalInputs,
                    recentProduction,
                    recentConsumption,
                    recentInputs,
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
            double nominalInputs,
            double recentProduction,
            double recentConsumption,
            double recentInputs,
            long workers,
            long maxWorkers,
            long reserve)
        {
            cells.Stock.text = NumberText.Format(stock);
            cells.Production.text = FormatFigure(nominalProduction, recentProduction);
            cells.Consumption.text = FormatFigure(nominalConsumption, recentConsumption);
            cells.Inputs.text = FormatFigure(nominalInputs, recentInputs);

            // What the inhabitants consume and what the workshops take both leave the stock.
            cells.Net.text = $"{FormatNet(nominalProduction - nominalConsumption - nominalInputs)} "
                + $"({FormatNet(recentProduction - recentConsumption - recentInputs)})";
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
                    Inputs = AddLabel(row, FigureCellClass),
                    Net = AddLabel(row, FigureCellClass),
                    Workers = AddLabel(row, WorkersCellClass),
                };
            }

            return goods;
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

        // Two rows on each side: the goods over their columns (a blank over the city's), then the name of every column.
        private static void BuildHeader(
            VisualElement header, VisualElement goodHeader, IReadOnlyList<string> goodNames, int[] goodOrder)
        {
            VisualElement groups = NewRow(HeaderRowClass);
            AddLabel(groups, CityGroupClass);

            VisualElement columns = NewRow(HeaderRowClass);
            AddLabel(columns, NameCellClass).text = "Ville";
            AddLabel(columns, PopulationCellClass).text = "Population";
            AddLabel(columns, BeggarsCellClass).text = "Mendiants";
            AddLabel(columns, HousedCellClass).text = "Ouvriers logés";
            AddLabel(columns, GrowthSinceStartCellClass).text = "Croiss. totale";

            VisualElement goodGroups = NewRow(HeaderRowClass);
            VisualElement goodColumns = NewRow(HeaderRowClass);

            foreach (int good in goodOrder)
            {
                AddLabel(goodGroups, GoodGroupClass).text = goodNames[good];
                AddLabel(goodColumns, StockCellClass).text = "Stock";
                AddLabel(goodColumns, FigureCellClass).text = "Prod.";
                AddLabel(goodColumns, FigureCellClass).text = "Conso.";
                AddLabel(goodColumns, FigureCellClass).text = "Intr.";
                AddLabel(goodColumns, FigureCellClass).text = "Net";
                AddLabel(goodColumns, WorkersCellClass).text = "Ouv.";
            }

            header.Add(groups);
            header.Add(columns);
            goodHeader.Add(goodGroups);
            goodHeader.Add(goodColumns);
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
