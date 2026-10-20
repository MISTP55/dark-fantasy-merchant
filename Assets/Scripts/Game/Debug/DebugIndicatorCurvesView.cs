using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// One figure of every city side by side, chosen in a list with its good when it is a good's:
    /// a small chart per city, on a scale common to all of them if asked, then the world's when
    /// the figure adds up.
    /// </summary>
    public sealed class DebugIndicatorCurvesView : IDebugView
    {
        private const string QuarterClass = "debug-chart--quarter";

        private readonly SimulationRecorder recorder;
        private readonly DropdownField indicatorField;
        private readonly DropdownField goodField;
        private readonly Toggle commonScale;
        private readonly int[] goodOrder;
        private readonly int[] cityOrder;
        private readonly TimeSeriesChart[] cityCharts;
        private readonly TimeSeriesChart worldChart;
        private readonly float[][] cityValues;
        private readonly Func<int, string> dayLabel;

        private float[] worldValues = new float[0];
        private bool isShown;

        public DebugIndicatorCurvesView(VisualElement page, SimulationRecorder recorder, IReadOnlyList<string> goodTitles)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            dayLabel = index => recorder.FormatDay(recorder.Record.History.DayAt(index));
            page.Clear();

            var toolbar = new VisualElement();
            toolbar.AddToClassList("debug-curves__toolbar");
            page.Add(toolbar);

            var indicatorNames = new List<string>();

            foreach (DebugIndicator indicator in DebugIndicators.All)
            {
                indicatorNames.Add(indicator.Name);
            }

            indicatorField = Dropdown(toolbar, "Indicateur", indicatorNames);
            indicatorField.index = indicatorNames.IndexOf("Stock");

            goodOrder = NameOrder.Of(goodTitles);
            var goodChoices = new List<string>();

            foreach (int good in goodOrder)
            {
                goodChoices.Add(goodTitles[good]);
            }

            goodField = Dropdown(toolbar, "Marchandise", goodChoices);
            goodField.index = 0;

            commonScale = new Toggle { text = "Échelle commune", focusable = false };
            commonScale.AddToClassList("debug-panel__toggle");
            toolbar.Add(commonScale);

            indicatorField.RegisterValueChangedCallback(change => Refresh());
            goodField.RegisterValueChangedCallback(change => Refresh());
            commonScale.RegisterValueChangedCallback(change => Refresh());

            var charts = new ScrollView(ScrollViewMode.Vertical);
            charts.AddToClassList("debug-curves__charts");
            page.Add(charts);

            var grid = new VisualElement();
            grid.AddToClassList("debug-curves__grid");
            charts.Add(grid);

            var names = new List<string>();

            foreach (CityDefinition city in recorder.Cities)
            {
                names.Add(city.DisplayName);
            }

            cityOrder = NameOrder.Of(names);
            cityCharts = new TimeSeriesChart[names.Count];
            cityValues = new float[names.Count][];

            foreach (int city in cityOrder)
            {
                cityCharts[city] = new TimeSeriesChart(names[city]);
                cityCharts[city].AddToClassList(QuarterClass);
                grid.Add(cityCharts[city]);
                cityValues[city] = new float[0];
            }

            worldChart = new TimeSeriesChart("Monde");
            worldChart.AddToClassList(QuarterClass);
            grid.Add(worldChart);
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
            if (!isShown || indicatorField.index < 0 || goodField.index < 0)
            {
                return;
            }

            DebugIndicator indicator = DebugIndicators.All[indicatorField.index];
            int good = goodOrder[goodField.index];
            goodField.SetEnabled(indicator.IsGood);
            SimulationHistory history = recorder.Record.History;
            int count = history.DayCount;

            for (int city = 0; city < cityCharts.Length; city++)
            {
                float[] values = Grown(ref cityValues[city], count);

                for (int day = 0; day < count; day++)
                {
                    values[day] = DebugIndicators.ValueOf(history, day, city, indicator, good);
                }
            }

            float? commonMin = null;
            float? commonMax = null;

            if (commonScale.value && ChartMath.TryRange(cityValues, count, out float min, out float max))
            {
                commonMin = min;
                commonMax = max;
            }

            Func<float, string> format = value => DebugIndicators.Format(indicator, value);

            for (int city = 0; city < cityCharts.Length; city++)
            {
                cityCharts[city].SetData(count, dayLabel, new[] { SeriesOf(indicator.Name, cityValues[city], format) }, null, commonMin, commonMax);
            }

            worldChart.style.display = indicator.AddsUp ? DisplayStyle.Flex : DisplayStyle.None;

            if (indicator.AddsUp)
            {
                float[] values = Grown(ref worldValues, count);

                for (int day = 0; day < count; day++)
                {
                    values[day] = DebugIndicators.WorldValueOf(history, day, cityCharts.Length, indicator, good);
                }

                worldChart.SetData(count, dayLabel, new[] { SeriesOf(indicator.Name, values, format) }, null);
            }
        }

        private static ChartSeries SeriesOf(string name, float[] values, Func<float, string> format)
        {
            return new ChartSeries { Name = name, Color = DebugChartColors.Stock, Style = ChartSeriesStyle.Line, Values = values, Format = format };
        }

        private static float[] Grown(ref float[] values, int count)
        {
            if (values.Length < count)
            {
                values = new float[Math.Max(count, Math.Max(64, values.Length * 2))];
            }

            return values;
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
