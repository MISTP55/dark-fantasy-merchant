using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>An entry of the legend of a debug chart: a curve, an area or a band.</summary>
    public readonly struct ChartLegendEntry
    {
        public ChartLegendEntry(string name, Color color, ChartSeriesStyle style)
        {
            Name = name;
            Color = color;
            Style = style;
        }

        public string Name { get; }

        public Color Color { get; }

        /// <summary>How its swatch is drawn: a line, a dashed line, or a square for an area or a band.</summary>
        public ChartSeriesStyle Style { get; }
    }

    /// <summary>The colours of a debug chart, each with its name, in a row that wraps.</summary>
    public sealed class ChartLegend : VisualElement
    {
        public const string LegendClass = "debug-legend";
        public const string EntryClass = "debug-legend__entry";
        public const string SwatchClass = "debug-legend__swatch";
        public const string NameClass = "debug-legend__name";

        private readonly List<ChartLegendEntry> shown = new List<ChartLegendEntry>();

        public ChartLegend()
        {
            AddToClassList(LegendClass);
            pickingMode = PickingMode.Ignore;
        }

        public int EntryCount => shown.Count;

        /// <summary>Rebuilds the legend only when its entries changed: charts set theirs on every refresh.</summary>
        public void SetEntries(IReadOnlyList<ChartLegendEntry> entries)
        {
            if (IsShowing(entries))
            {
                return;
            }

            Clear();
            shown.Clear();

            foreach (ChartLegendEntry entry in entries)
            {
                shown.Add(entry);
                Add(EntryOf(entry));
            }
        }

        private bool IsShowing(IReadOnlyList<ChartLegendEntry> entries)
        {
            if (entries.Count != shown.Count)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Name != shown[i].Name || entries[i].Color != shown[i].Color || entries[i].Style != shown[i].Style)
                {
                    return false;
                }
            }

            return true;
        }

        private static VisualElement EntryOf(ChartLegendEntry entry)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(EntryClass);

            var swatch = new VisualElement { pickingMode = PickingMode.Ignore };
            swatch.AddToClassList(SwatchClass);

            switch (entry.Style)
            {
                case ChartSeriesStyle.Line:
                    swatch.AddToClassList(SwatchClass + "--line");
                    swatch.style.backgroundColor = entry.Color;
                    break;
                case ChartSeriesStyle.Dashed:
                    // Two short dashes.
                    swatch.AddToClassList(SwatchClass + "--dashed");

                    for (int i = 0; i < 2; i++)
                    {
                        var dash = new VisualElement { pickingMode = PickingMode.Ignore };
                        dash.AddToClassList(SwatchClass + "-dash");
                        dash.style.backgroundColor = entry.Color;
                        swatch.Add(dash);
                    }

                    break;
                default:
                    // A band's colour is faint: its square has a border of the same colour, opaque.
                    swatch.AddToClassList(SwatchClass + "--area");
                    swatch.style.backgroundColor = entry.Color;
                    Color border = entry.Color;
                    border.a = Mathf.Max(border.a, 0.6f);
                    swatch.style.borderLeftColor = border;
                    swatch.style.borderRightColor = border;
                    swatch.style.borderTopColor = border;
                    swatch.style.borderBottomColor = border;
                    break;
            }

            element.Add(swatch);

            var name = new Label(entry.Name) { pickingMode = PickingMode.Ignore };
            name.AddToClassList(NameClass);
            element.Add(name);
            return element;
        }
    }
}
