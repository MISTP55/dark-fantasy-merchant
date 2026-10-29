using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The population of every city, for the debug panel: its inhabitants, its beggars over the
    /// number they tend to, its rich, wealthy and poor over the room of their houses with their
    /// satisfaction, how much it has grown since the game started and the houses it built of its
    /// own, and a last row with the totals. The cities are in alphabetical order. Follows the
    /// markets while it is shown, writing what changed at most once a frame.
    /// </summary>
    public sealed class DebugPopulationTable
    {
        private const string RowClass = "debug-cities__row";
        private const string HeaderRowClass = "debug-cities__row--header";
        private const string TotalRowClass = "debug-cities__row--total";
        private const string CellClass = "debug-cities__cell";
        private const string NameCellClass = "debug-cities__cell--name";
        private const string PopulationCellClass = "debug-cities__cell--population";
        private const string BeggarsCellClass = "debug-cities__cell--beggars";
        private const string ClassCellClass = "debug-cities__cell--class";
        private const string GrowthSinceStartCellClass = "debug-cities__cell--growth-since-start";
        private const string HousesCellClass = "debug-cities__cell--houses";

        private static readonly SocialClass[] Classes = { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };

        private sealed class Row
        {
            public string Name;
            public CityMarket Market;
            public Label[] Cells;
            public Action Refresh;
            public bool IsDirty;
        }

        private readonly VisualElement rowContainer;
        private readonly List<Row> rows = new List<Row>();
        private readonly Label[] totals;

        private bool isShown;

        // A row has changed since it was last written.
        private bool hasChanges;

        public DebugPopulationTable(VisualElement header, VisualElement rowContainer, VisualElement totalContainer)
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
            string[] titles = { "Ville", "Population", "Mendiants", "Riches", "Aisés", "Pauvres", "Croiss. totale", "Maisons de la ville" };
            Label[] titleCells = AddCells(headerRow);

            for (int i = 0; i < titles.Length; i++)
            {
                titleCells[i].text = titles[i];
            }

            header.Add(headerRow);

            VisualElement totalRow = NewRow(TotalRowClass);
            totals = AddCells(totalRow);
            totals[0].text = "Total";
            totalContainer.Add(totalRow);
            WriteTotals();
        }

        /// <summary>Adds the row of a city, among the others in alphabetical order, written at once.</summary>
        public void AddCity(string cityName, CityMarket market)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            VisualElement element = NewRow();
            var row = new Row { Name = cityName, Market = market, Cells = AddCells(element) };
            row.Cells[0].text = cityName;

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
            ListStripes.Apply(rowContainer);
            Write(row);
            WriteTotals();

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
                row.Market.Changed -= row.Refresh;
            }

            isShown = false;
        }

        private static void Write(Row row)
        {
            CityMarket market = row.Market;
            CityPopulation population = market.Population;
            Label[] cells = row.Cells;

            cells[1].text = NumberText.Format(population.Inhabitants);
            cells[2].text = DebugCitiesTable.FormatBeggars(population.Beggars, market.BeggarTarget);

            for (int i = 0; i < Classes.Length; i++)
            {
                SocialClass housedClass = Classes[i];
                cells[3 + i].text = DebugCitiesTable.FormatClass(
                    population.CountOf(housedClass), population.RoomOf(housedClass), population.SatisfactionOf(housedClass));
            }

            cells[6].text = DebugCitiesTable.FormatRate(population.GrowthSinceStart);
            cells[7].text = NumberText.Format(market.CityHouses);
        }

        // Summed again at most once a frame: a handful of cities.
        private void WriteTotals()
        {
            long inhabitants = 0;
            long startingInhabitants = 0;
            long beggars = 0;
            long beggarTargets = 0;
            long houses = 0;
            var counts = new long[Classes.Length];
            var rooms = new long[Classes.Length];

            foreach (Row row in rows)
            {
                CityPopulation population = row.Market.Population;
                inhabitants += population.Inhabitants;
                startingInhabitants += population.StartingInhabitants;
                beggars += population.Beggars;
                beggarTargets += row.Market.BeggarTarget;
                houses += row.Market.CityHouses;

                for (int i = 0; i < Classes.Length; i++)
                {
                    counts[i] += population.CountOf(Classes[i]);
                    rooms[i] += population.RoomOf(Classes[i]);
                }
            }

            totals[1].text = NumberText.Format(inhabitants);
            totals[2].text = DebugCitiesTable.FormatBeggars(beggars, beggarTargets);

            // The classes of every city together; their satisfactions do not add up.
            for (int i = 0; i < Classes.Length; i++)
            {
                totals[3 + i].text = DebugCitiesTable.FormatClass(counts[i], rooms[i], null);
            }

            // The growth of the world: all its inhabitants together, against all those it started with.
            totals[6].text = DebugCitiesTable.FormatRate(
                startingInhabitants > 0 ? (double)inhabitants / startingInhabitants - 1d : 0d);
            totals[7].text = NumberText.Format(houses);
        }

        private static Label[] AddCells(VisualElement row)
        {
            string[] cellClasses =
            {
                NameCellClass, PopulationCellClass, BeggarsCellClass, ClassCellClass, ClassCellClass, ClassCellClass,
                GrowthSinceStartCellClass, HousesCellClass,
            };
            var cells = new Label[cellClasses.Length];

            for (int i = 0; i < cellClasses.Length; i++)
            {
                cells[i] = new Label();
                cells[i].AddToClassList(CellClass);
                cells[i].AddToClassList(cellClasses[i]);
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
