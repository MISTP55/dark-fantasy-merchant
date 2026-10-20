using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// One large chart of the curves one adds, each a figure of a city or of one of its goods,
    /// each on its own scale: to set two things of the simulation against each other.
    /// </summary>
    public sealed class DebugComposedCurvesView : IDebugView
    {
        private readonly SimulationRecorder recorder;
        private readonly DropdownField cityField;
        private readonly DropdownField goodField;
        private readonly DropdownField indicatorField;
        private readonly VisualElement chips;
        private readonly TimeSeriesChart chart;
        private readonly ComposedSeriesList list = new ComposedSeriesList();
        private readonly List<float[]> values = new List<float[]>();
        private readonly int[] cityOrder;
        private readonly int[] goodOrder;
        private readonly List<string> cityNames = new List<string>();
        private readonly IReadOnlyList<string> goodNames;

        private bool isShown;

        public DebugComposedCurvesView(VisualElement page, SimulationRecorder recorder, IReadOnlyList<string> goodTitles)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            goodNames = goodTitles ?? throw new ArgumentNullException(nameof(goodTitles));
            page.Clear();

            foreach (CityDefinition city in recorder.Cities)
            {
                cityNames.Add(city.DisplayName);
            }

            cityOrder = NameOrder.Of(cityNames);
            goodOrder = NameOrder.Of(goodTitles);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("debug-curves__toolbar");
            page.Add(toolbar);

            cityField = Dropdown(toolbar, "Ville", Ordered(cityNames, cityOrder));
            goodField = Dropdown(toolbar, "Marchandise", Ordered(goodTitles, goodOrder));

            var indicatorNames = new List<string>();

            foreach (DebugIndicator indicator in DebugIndicators.All)
            {
                indicatorNames.Add(indicator.Name);
            }

            indicatorField = Dropdown(toolbar, "Indicateur", indicatorNames);
            indicatorField.RegisterValueChangedCallback(change => goodField.SetEnabled(DebugIndicators.All[indicatorField.index].IsGood));
            goodField.SetEnabled(DebugIndicators.All[0].IsGood);

            var add = new Button(Add) { text = "Ajouter", focusable = false };
            add.AddToClassList("debug-panel__button");
            toolbar.Add(add);

            chips = new VisualElement();
            chips.AddToClassList("debug-curves__chips");
            page.Add(chips);

            // The chips above are its legend.
            chart = new TimeSeriesChart(
                "Chaque courbe a sa propre échelle : celle de la première à gauche, celle de la deuxième à droite ; toutes se lisent au survol.",
                recorder.ChartCalendar) { ShowsLegend = false };
            chart.AddToClassList("debug-chart--large");
            page.Add(chart);
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

            SimulationHistory history = recorder.Record.History;
            int count = history.DayCount;
            var series = new List<ChartSeries>();

            for (int i = 0; i < list.Items.Count; i++)
            {
                ComposedSeries item = list.Items[i];
                float[] curve = values[i];

                if (curve.Length < count)
                {
                    curve = new float[Math.Max(count, Math.Max(64, curve.Length * 2))];
                    values[i] = curve;
                }

                for (int day = 0; day < count; day++)
                {
                    curve[day] = DebugIndicators.ValueOf(history, day, item.City, item.Indicator, item.Good);
                }

                DebugIndicator indicator = item.Indicator;
                series.Add(new ChartSeries
                {
                    Name = ComposedSeriesList.LabelOf(item, cityNames, goodNames),
                    Color = list.ColorOf(i),
                    Style = ChartSeriesStyle.Line,
                    Values = curve,
                    Format = value => DebugIndicators.Format(indicator, value),
                    OwnScale = true,
                });
            }

            chart.SetData(history.FirstDay, count, series, null);
        }

        private void Add()
        {
            if (cityField.index < 0 || goodField.index < 0 || indicatorField.index < 0)
            {
                return;
            }

            var item = new ComposedSeries(cityOrder[cityField.index], DebugIndicators.All[indicatorField.index], goodOrder[goodField.index]);

            if (list.TryAdd(item))
            {
                values.Add(new float[0]);
                WriteChips();
                Refresh();
            }
        }

        private void Remove(int index)
        {
            list.Remove(index);
            values.RemoveAt(index);
            WriteChips();
            Refresh();
        }

        private void WriteChips()
        {
            chips.Clear();

            for (int i = 0; i < list.Items.Count; i++)
            {
                int index = i;
                var chip = new VisualElement();
                chip.AddToClassList("debug-curves__chip");
                chip.style.borderLeftColor = list.ColorOf(i);
                chip.style.borderRightColor = list.ColorOf(i);
                chip.style.borderTopColor = list.ColorOf(i);
                chip.style.borderBottomColor = list.ColorOf(i);

                var label = new Label(ComposedSeriesList.LabelOf(list.Items[i], cityNames, goodNames));
                label.style.color = list.ColorOf(i);
                chip.Add(label);

                var remove = new Button(() => Remove(index)) { text = "✕", focusable = false };
                remove.AddToClassList("debug-curves__chip-remove");
                chip.Add(remove);
                chips.Add(chip);
            }
        }

        private static List<string> Ordered(IReadOnlyList<string> names, int[] order)
        {
            var ordered = new List<string>();

            foreach (int index in order)
            {
                ordered.Add(names[index]);
            }

            return ordered;
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
