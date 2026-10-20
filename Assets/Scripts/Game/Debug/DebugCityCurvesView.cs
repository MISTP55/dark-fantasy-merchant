using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The curves of one city, chosen in a list: its inhabitants per class and its beggars, its
    /// satisfactions, its workers and their reserve, then, for every good, its stock over the
    /// bands of its thresholds and its price.
    /// </summary>
    public sealed class DebugCityCurvesView : IDebugView
    {
        private const string CityButtonClass = "debug-curves__city";
        private const string ThirdClass = "debug-chart--third";
        private const string QuarterClass = "debug-chart--quarter";

        private readonly SimulationRecorder recorder;
        private readonly List<Button> cityButtons = new List<Button>();
        private readonly int[] goodOrder;

        private readonly TimeSeriesChart classesChart;
        private readonly TimeSeriesChart satisfactionChart;
        private readonly TimeSeriesChart workersChart;
        private readonly TimeSeriesChart[] goodCharts;

        // The arrays of the curves, kept from one refresh to the next and grown with the days.
        private readonly Dictionary<string, float[]> buffers = new Dictionary<string, float[]>();
        private readonly Func<int, string> dayLabel;

        private int city;
        private bool isShown;

        /// <param name="page">The sub-page the view fills.</param>
        /// <param name="goodTitles">The title of every good's chart, in the catalogue's order: its name with its unit.</param>
        public DebugCityCurvesView(VisualElement page, SimulationRecorder recorder, IReadOnlyList<string> goodTitles)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            dayLabel = index => recorder.FormatDay(recorder.Record.History.DayAt(index));
            page.Clear();
            page.AddToClassList("debug-curves");

            var cityList = new ScrollView(ScrollViewMode.Vertical);
            cityList.AddToClassList("debug-curves__cities");
            page.Add(cityList);

            var names = new List<string>();

            foreach (CityDefinition definition in recorder.Cities)
            {
                names.Add(definition.DisplayName);
            }

            foreach (int index in NameOrder.Of(names))
            {
                var button = new Button(() => Select(index)) { text = names[index], focusable = false };
                button.AddToClassList(CityButtonClass);
                button.userData = index;
                cityList.Add(button);
                cityButtons.Add(button);
            }

            ListStripes.Apply(cityList.contentContainer);

            var charts = new ScrollView(ScrollViewMode.Vertical);
            charts.AddToClassList("debug-curves__charts");
            page.Add(charts);

            charts.Add(Section("Population"));
            VisualElement populationGrid = Grid();
            charts.Add(populationGrid);
            classesChart = AddChart(populationGrid, "Habitants par classe et mendiants", ThirdClass);
            satisfactionChart = AddChart(populationGrid, "Satisfaction", ThirdClass);
            workersChart = AddChart(populationGrid, "Ouvriers et réserve", ThirdClass);

            charts.Add(Section("Marchandises : stock sur les bandes des seuils, prix d'achat en pointillé (échelle propre)"));
            VisualElement goodsGrid = Grid();
            charts.Add(goodsGrid);
            goodOrder = NameOrder.Of(goodTitles);
            goodCharts = new TimeSeriesChart[goodTitles.Count];

            foreach (int good in goodOrder)
            {
                goodCharts[good] = AddChart(goodsGrid, goodTitles[good], QuarterClass);
            }

            Select(cityButtons.Count > 0 ? (int)cityButtons[0].userData : 0);
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
            if (!isShown || recorder.Cities.Count == 0)
            {
                return;
            }

            SimulationHistory history = recorder.Record.History;
            int count = history.DayCount;

            classesChart.SetData(count, dayLabel, new[]
            {
                Series("Riches", DebugChartColors.Rich, ChartSeriesStyle.StackedArea, Fill("rich", history, CityIndicator.Rich)),
                Series("Aisés", DebugChartColors.Wealthy, ChartSeriesStyle.StackedArea, Fill("wealthy", history, CityIndicator.Wealthy)),
                Series("Pauvres", DebugChartColors.Poor, ChartSeriesStyle.StackedArea, Fill("poor", history, CityIndicator.Poor)),
                Series("Mendiants", DebugChartColors.Beggars, ChartSeriesStyle.StackedArea, Fill("beggars", history, CityIndicator.Beggars)),
            }, null);

            satisfactionChart.SetData(count, dayLabel, new[]
            {
                Series("Riches", DebugChartColors.Rich, ChartSeriesStyle.Line, Fill("rich-satisfaction", history, CityIndicator.RichSatisfaction)),
                Series("Aisés", DebugChartColors.Wealthy, ChartSeriesStyle.Line, Fill("wealthy-satisfaction", history, CityIndicator.WealthySatisfaction)),
                Series("Pauvres", DebugChartColors.Poor, ChartSeriesStyle.Line, Fill("poor-satisfaction", history, CityIndicator.PoorSatisfaction)),
            }, null);

            workersChart.SetData(count, dayLabel, new[]
            {
                Series("Ouvriers", DebugChartColors.Workers, ChartSeriesStyle.Line, FillSum("workers", history, GoodIndicator.Workers)),
                Series("Réserve", DebugChartColors.Reserve, ChartSeriesStyle.Dashed, FillSum("reserve", history, GoodIndicator.Reserve)),
            }, null);

            for (int good = 0; good < goodCharts.Length; good++)
            {
                float[] zero = Buffer("zero", count);
                Array.Clear(zero, 0, count);
                float[] t0 = Fill("t0-" + good, history, GoodIndicator.T0, good);
                float[] t1 = Fill("t1-" + good, history, GoodIndicator.T1, good);
                float[] t2 = Fill("t2-" + good, history, GoodIndicator.T2, good);
                float[] t3 = Fill("t3-" + good, history, GoodIndicator.T3, good);

                ChartSeries price = Series("Prix d'achat", DebugChartColors.Price, ChartSeriesStyle.Dashed, Fill("price-" + good, history, GoodIndicator.BuyPrice, good));
                price.OwnScale = true;
                price.Format = value => TreasuryHudController.FormatNumber((long)Math.Round(value));

                goodCharts[good].SetData(
                    count,
                    dayLabel,
                    new[] { Series("Stock", DebugChartColors.Stock, ChartSeriesStyle.Line, Fill("stock-" + good, history, GoodIndicator.Stock, good)), price },
                    new[]
                    {
                        new ChartBand { Lower = zero, Upper = t0, Color = DebugChartColors.BelowT0 },
                        new ChartBand { Lower = t0, Upper = t1, Color = DebugChartColors.T0ToT1 },
                        new ChartBand { Lower = t1, Upper = t2, Color = DebugChartColors.T1ToT2 },
                        new ChartBand { Lower = t2, Upper = t3, Color = DebugChartColors.T2ToT3 },
                    });
            }
        }

        private void Select(int index)
        {
            city = index;

            foreach (Button button in cityButtons)
            {
                button.EnableInClassList(DebugTabs.SelectedClass, (int)button.userData == index);
            }

            Refresh();
        }

        private static ChartSeries Series(string name, UnityEngine.Color color, ChartSeriesStyle style, float[] values)
        {
            return new ChartSeries { Name = name, Color = color, Style = style, Values = values };
        }

        private float[] Fill(string key, SimulationHistory history, CityIndicator indicator)
        {
            float[] values = Buffer(key, history.DayCount);

            for (int day = 0; day < history.DayCount; day++)
            {
                values[day] = history.ValueOf(day, city, indicator);
            }

            return values;
        }

        private float[] Fill(string key, SimulationHistory history, GoodIndicator indicator, int good)
        {
            float[] values = Buffer(key, history.DayCount);

            for (int day = 0; day < history.DayCount; day++)
            {
                values[day] = history.ValueOf(day, city, indicator, good);
            }

            return values;
        }

        // A figure of the goods added up: the workers of every workshop.
        private float[] FillSum(string key, SimulationHistory history, GoodIndicator indicator)
        {
            float[] values = Buffer(key, history.DayCount);

            for (int day = 0; day < history.DayCount; day++)
            {
                float sum = 0f;

                for (int good = 0; good < goodCharts.Length; good++)
                {
                    float value = history.ValueOf(day, city, indicator, good);
                    sum += float.IsNaN(value) ? 0f : value;
                }

                values[day] = sum;
            }

            return values;
        }

        private float[] Buffer(string key, int count)
        {
            if (!buffers.TryGetValue(key, out float[] values) || values.Length < count)
            {
                values = new float[Math.Max(count, values != null ? values.Length * 2 : 64)];
                buffers[key] = values;
            }

            return values;
        }

        private static Label Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList("debug-curves__section");
            return label;
        }

        private static VisualElement Grid()
        {
            var grid = new VisualElement();
            grid.AddToClassList("debug-curves__grid");
            return grid;
        }

        private static TimeSeriesChart AddChart(VisualElement grid, string title, string sizeClass)
        {
            var chart = new TimeSeriesChart(title);
            chart.AddToClassList(sizeClass);
            grid.Add(chart);
            return chart;
        }
    }
}
