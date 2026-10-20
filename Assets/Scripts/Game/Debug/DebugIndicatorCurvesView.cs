using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// One figure of every city side by side, chosen in a list with its good when it is a good's:
    /// a small chart per city, on a scale common to all of them if asked, then the world's when
    /// the figure adds up. With a good's figure, the charts of the cities that produce it are framed.
    /// </summary>
    public sealed class DebugIndicatorCurvesView : IDebugView
    {
        private const string QuarterClass = "debug-chart--quarter";
        private const string ProducedClass = "debug-chart--produced";
        private const string InefficientlyProducedClass = "debug-chart--produced-inefficient";

        private readonly SimulationRecorder recorder;
        private readonly DropdownField indicatorField;
        private readonly DropdownField goodField;
        private readonly Toggle commonScale;
        private readonly Label producedHint;
        private readonly int[] goodOrder;
        private readonly int[] cityOrder;
        private readonly TimeSeriesChart[] cityCharts;
        private readonly TimeSeriesChart worldChart;
        private readonly float[][] cityValues;

        private float[] worldValues = new float[0];
        private bool isShown;

        public DebugIndicatorCurvesView(VisualElement page, SimulationRecorder recorder, IReadOnlyList<string> goodNames)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
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

            goodOrder = NameOrder.Of(goodNames);
            var goodChoices = new List<string>();

            foreach (int good in goodOrder)
            {
                goodChoices.Add(goodNames[good]);
            }

            goodField = Dropdown(toolbar, "Marchandise", goodChoices);
            goodField.index = 0;

            // On at first: the cities are compared at a glance.
            commonScale = new Toggle { text = "Échelle commune", value = true, focusable = false };
            commonScale.AddToClassList("debug-panel__toggle");
            toolbar.Add(commonScale);

            producedHint = new Label("Encadrées, les villes qui produisent la marchandise (en clair : efficacement, en sombre : peu efficacement)");
            producedHint.AddToClassList("debug-curves__hint");
            toolbar.Add(producedHint);

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
                cityCharts[city] = new TimeSeriesChart(names[city], recorder.ChartCalendar);
                cityCharts[city].AddToClassList(QuarterClass);
                grid.Add(cityCharts[city]);
                cityValues[city] = new float[0];
            }

            worldChart = new TimeSeriesChart("Monde", recorder.ChartCalendar);
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
            ShowProducers(indicator.IsGood ? good : -1);
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
                cityCharts[city].SetData(history.FirstDay, count, new[] { SeriesOf(indicator.Name, cityValues[city], format) }, null, commonMin, commonMax);
            }

            worldChart.style.display = indicator.AddsUp ? DisplayStyle.Flex : DisplayStyle.None;

            if (indicator.AddsUp)
            {
                float[] values = Grown(ref worldValues, count);

                for (int day = 0; day < count; day++)
                {
                    values[day] = DebugIndicators.WorldValueOf(history, day, cityCharts.Length, indicator, good);
                }

                worldChart.SetData(history.FirstDay, count, new[] { SeriesOf(indicator.Name, values, format) }, null);
            }
        }

        // Frames the charts of the cities that produce the good; none without a good.
        private void ShowProducers(int good)
        {
            producedHint.style.display = good >= 0 ? DisplayStyle.Flex : DisplayStyle.None;

            for (int city = 0; city < cityCharts.Length; city++)
            {
                GoodProduction production = good >= 0 && good < recorder.Goods.Count
                    ? recorder.Cities[city].ProductionOf(recorder.Goods[good])
                    : GoodProduction.None;
                cityCharts[city].EnableInClassList(ProducedClass, production == GoodProduction.Efficient);
                cityCharts[city].EnableInClassList(InefficientlyProducedClass, production == GoodProduction.Inefficient);
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
