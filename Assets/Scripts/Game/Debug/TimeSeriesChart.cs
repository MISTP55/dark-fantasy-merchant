using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    public enum ChartSeriesStyle
    {
        Line,
        Dashed,

        /// <summary>An area over the stacked series before it, in the list's order.</summary>
        StackedArea,
    }

    /// <summary>A curve of a debug chart: one value per day, NaN where there is none.</summary>
    public sealed class ChartSeries
    {
        public string Name;
        public Color Color;
        public ChartSeriesStyle Style;

        /// <summary>One value per day; only the chart's count of them is read.</summary>
        public float[] Values;

        /// <summary>How the hover writes a value; null for one decimal.</summary>
        public Func<float, string> Format;

        /// <summary>A scale of its own, from its minimum to its maximum, rather than the chart's.</summary>
        public bool OwnScale;
    }

    /// <summary>A band of a debug chart, between two bounds that change with the days: the thresholds of a good.</summary>
    public sealed class ChartBand
    {
        public float[] Lower;
        public float[] Upper;
        public Color Color;
    }

    /// <summary>
    /// A chart of days for the debug panel, drawn by hand: lines, dashed lines, stacked areas and
    /// bands, under a title. Hovering it shows the day and the value of every curve.
    /// </summary>
    public sealed class TimeSeriesChart : VisualElement
    {
        private const string ChartClass = "debug-chart";
        private const string TitleClass = "debug-chart__title";
        private const string PlotClass = "debug-chart__plot";
        private const string TooltipClass = "debug-chart__tooltip";
        private const float DashLength = 4f;
        private const float GapLength = 3f;

        private static readonly Color HoverColor = new Color(0.94f, 0.89f, 0.77f, 0.5f);

        private readonly Label title;
        private readonly VisualElement plot;
        private readonly Label tooltip;
        private readonly List<ChartSeries> series = new List<ChartSeries>();
        private readonly List<ChartBand> bands = new List<ChartBand>();

        // The top of each stacked area, by series; null for the other series.
        private readonly List<float[]> stackTops = new List<float[]>();
        private readonly List<float[]> rangeValues = new List<float[]>();
        private readonly StringBuilder tooltipText = new StringBuilder();

        private Func<int, string> dayLabel;
        private int count;
        private float min;
        private float max = 1f;
        private int hoverIndex = -1;

        public TimeSeriesChart(string chartTitle)
        {
            AddToClassList(ChartClass);
            title = new Label(chartTitle);
            title.AddToClassList(TitleClass);
            Add(title);

            plot = new VisualElement();
            plot.AddToClassList(PlotClass);
            plot.generateVisualContent += Draw;
            plot.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            plot.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            Add(plot);

            tooltip = new Label();
            tooltip.AddToClassList(TooltipClass);
            tooltip.pickingMode = PickingMode.Ignore;
            tooltip.style.display = DisplayStyle.None;
            plot.Add(tooltip);
        }

        public string Title
        {
            get => title.text;
            set => title.text = value;
        }

        /// <param name="dayCount">Days drawn: the first values of every series and band.</param>
        /// <param name="dayText">How the hover writes a day, by its index.</param>
        /// <param name="commonMin">With <paramref name="commonMax"/>, the range of the series without a scale of their own.</param>
        public void SetData(
            int dayCount,
            Func<int, string> dayText,
            IReadOnlyList<ChartSeries> chartSeries,
            IReadOnlyList<ChartBand> chartBands,
            float? commonMin = null,
            float? commonMax = null)
        {
            count = Math.Max(0, dayCount);
            dayLabel = dayText;
            series.Clear();
            bands.Clear();

            if (chartSeries != null)
            {
                series.AddRange(chartSeries);
            }

            if (chartBands != null)
            {
                bands.AddRange(chartBands);
            }

            Stack();

            if (commonMin.HasValue && commonMax.HasValue && commonMax.Value > commonMin.Value)
            {
                min = commonMin.Value;
                max = commonMax.Value;
            }
            else
            {
                rangeValues.Clear();

                for (int i = 0; i < series.Count; i++)
                {
                    if (!series[i].OwnScale)
                    {
                        rangeValues.Add(stackTops[i] ?? series[i].Values);
                    }
                }

                foreach (ChartBand band in bands)
                {
                    rangeValues.Add(band.Lower);
                }

                ChartMath.TryRange(rangeValues, count, out min, out max);
            }

            if (hoverIndex >= count)
            {
                HideTooltip();
            }
            else if (hoverIndex >= 0)
            {
                WriteTooltip();
            }

            plot.MarkDirtyRepaint();
        }

        // The tops of the stacked areas, each over the ones before it.
        private void Stack()
        {
            stackTops.Clear();
            float[] below = null;

            foreach (ChartSeries one in series)
            {
                if (one.Style != ChartSeriesStyle.StackedArea || one.Values == null)
                {
                    stackTops.Add(null);
                    continue;
                }

                var top = new float[count];

                for (int day = 0; day < count; day++)
                {
                    float value = day < one.Values.Length && !float.IsNaN(one.Values[day]) ? one.Values[day] : 0f;
                    top[day] = (below != null ? below[day] : 0f) + value;
                }

                stackTops.Add(top);
                below = top;
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = plot.contentRect;

            if (count == 0 || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Painter2D painter = context.painter2D;

            foreach (ChartBand band in bands)
            {
                FillBetween(painter, rect, band.Lower, band.Upper, band.Color, min, max);
            }

            float[] below = null;

            for (int i = 0; i < series.Count; i++)
            {
                if (stackTops[i] != null)
                {
                    FillBetween(painter, rect, below, stackTops[i], series[i].Color, min, max);
                    below = stackTops[i];
                }
            }

            for (int i = 0; i < series.Count; i++)
            {
                ChartSeries one = series[i];

                if (stackTops[i] != null || one.Values == null)
                {
                    continue;
                }

                float seriesMin = min;
                float seriesMax = max;

                if (one.OwnScale)
                {
                    ChartMath.TryRange(new[] { one.Values }, count, out seriesMin, out seriesMax);
                }

                StrokeLine(painter, rect, one.Values, one.Color, one.Style == ChartSeriesStyle.Dashed, seriesMin, seriesMax);
            }

            if (hoverIndex >= 0 && hoverIndex < count)
            {
                float x = ChartMath.XOf(hoverIndex, count, rect.width);
                painter.strokeColor = HoverColor;
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, rect.height));
                painter.Stroke();
            }
        }

        private void FillBetween(Painter2D painter, Rect rect, float[] lower, float[] upper, Color color, float low, float high)
        {
            if (upper == null)
            {
                return;
            }

            painter.fillColor = color;
            painter.BeginPath();

            for (int day = 0; day < count; day++)
            {
                Vector2 point = PointOf(rect, day, ValueAt(upper, day, low), low, high);

                if (day == 0)
                {
                    painter.MoveTo(point);
                }
                else
                {
                    painter.LineTo(point);
                }
            }

            for (int day = count - 1; day >= 0; day--)
            {
                painter.LineTo(PointOf(rect, day, lower != null ? ValueAt(lower, day, low) : low, low, high));
            }

            painter.ClosePath();
            painter.Fill();
        }

        // NaN breaks the line; a dashed line is cut into dashes along its length.
        private void StrokeLine(Painter2D painter, Rect rect, float[] values, Color color, bool dashed, float low, float high)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1.5f;
            painter.BeginPath();

            bool drawing = false;
            Vector2 previous = default;
            float dashLeft = DashLength;
            bool inDash = true;

            for (int day = 0; day < Math.Min(count, values.Length); day++)
            {
                if (float.IsNaN(values[day]))
                {
                    drawing = false;
                    continue;
                }

                Vector2 point = PointOf(rect, day, Mathf.Clamp(values[day], low, high), low, high);

                if (!drawing)
                {
                    painter.MoveTo(point);
                    drawing = true;
                    previous = point;
                    continue;
                }

                if (!dashed)
                {
                    painter.LineTo(point);
                    previous = point;
                    continue;
                }

                // Walks the segment, switching between dash and gap.
                Vector2 from = previous;
                float length = Vector2.Distance(from, point);

                while (length > 0f)
                {
                    float step = Math.Min(dashLeft, length);
                    Vector2 to = Vector2.MoveTowards(from, point, step);

                    if (inDash)
                    {
                        painter.LineTo(to);
                    }
                    else
                    {
                        painter.MoveTo(to);
                    }

                    from = to;
                    length -= step;
                    dashLeft -= step;

                    if (dashLeft <= 0f)
                    {
                        inDash = !inDash;
                        dashLeft = inDash ? DashLength : GapLength;
                    }
                }

                previous = point;
            }

            painter.Stroke();
        }

        private Vector2 PointOf(Rect rect, int day, float value, float low, float high)
        {
            return new Vector2(ChartMath.XOf(day, count, rect.width), ChartMath.YOf(Mathf.Clamp(value, low, high), low, high, rect.height));
        }

        private static float ValueAt(float[] values, int day, float fallback)
        {
            return day < values.Length && !float.IsNaN(values[day]) ? values[day] : fallback;
        }

        private void OnPointerMove(PointerMoveEvent pointer)
        {
            hoverIndex = ChartMath.DayIndexAt(pointer.localPosition.x, plot.contentRect.width, count);

            if (hoverIndex < 0)
            {
                HideTooltip();
                return;
            }

            WriteTooltip();

            // On the side of the plot away from the pointer, so as not to hide what it points at.
            bool left = pointer.localPosition.x > plot.contentRect.width / 2f;
            tooltip.style.left = left ? StyleKeyword.Auto : new StyleLength(pointer.localPosition.x + 12f);
            tooltip.style.right = left ? new StyleLength(plot.contentRect.width - pointer.localPosition.x + 12f) : StyleKeyword.Auto;
            tooltip.style.display = DisplayStyle.Flex;
            plot.MarkDirtyRepaint();
        }

        private void OnPointerLeave(PointerLeaveEvent pointer)
        {
            HideTooltip();
        }

        private void HideTooltip()
        {
            hoverIndex = -1;
            tooltip.style.display = DisplayStyle.None;
            plot.MarkDirtyRepaint();
        }

        private void WriteTooltip()
        {
            tooltipText.Clear();
            tooltipText.Append(dayLabel != null ? dayLabel(hoverIndex) : hoverIndex.ToString());

            foreach (ChartSeries one in series)
            {
                if (one.Values == null || hoverIndex >= one.Values.Length)
                {
                    continue;
                }

                float value = one.Values[hoverIndex];
                string text = float.IsNaN(value) ? DebugCitiesTable.NoFigure
                    : one.Format != null ? one.Format(value) : DebugCitiesTable.FormatQuantity(value);
                tooltipText.Append('\n').Append(one.Name).Append(" : ").Append(text);
            }

            tooltip.text = tooltipText.ToString();
        }
    }
}
