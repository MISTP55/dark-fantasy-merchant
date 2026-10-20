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
        /// <summary>Its name in the legend; null to leave it out.</summary>
        public string Name;

        public float[] Lower;
        public float[] Upper;
        public Color Color;
    }

    /// <summary>
    /// A chart of days for the debug panel, drawn by hand: lines, dashed lines, stacked areas and
    /// bands, under a title and the legend of its colours. Its scale is written at its left, and
    /// at its right that of a curve with a scale of its own; its dates under it, in days, months
    /// or years as its length asks. Hovering it shows the day and the value of every curve.
    /// </summary>
    public sealed class TimeSeriesChart : VisualElement
    {
        private const string ChartClass = "debug-chart";
        private const string TitleClass = "debug-chart__title";
        private const string BodyClass = "debug-chart__body";
        private const string AxisClass = "debug-chart__axis";
        private const string AxisLabelClass = "debug-chart__axis-label";
        private const string ColumnClass = "debug-chart__column";
        private const string PlotClass = "debug-chart__plot";
        private const string TimeClass = "debug-chart__time";
        private const string TooltipClass = "debug-chart__tooltip";

        private const float DashLength = 4f;
        private const float GapLength = 3f;

        // The room a label of each axis needs, in pixels.
        private const float ValueLabelSpacing = 22f;
        private const float TimeLabelSpacing = 70f;
        private const float AxisLabelHeight = 12f;

        private static readonly Color HoverColor = new Color(0.94f, 0.89f, 0.77f, 0.5f);
        private static readonly Color GridColor = new Color(0.94f, 0.89f, 0.77f, 0.07f);

        private readonly ChartCalendar calendar;
        private readonly Label title;
        private readonly ChartLegend legend;
        private readonly VisualElement leftAxis;
        private readonly VisualElement rightAxis;
        private readonly VisualElement plot;
        private readonly VisualElement timeAxis;
        private readonly Label tooltip;
        private readonly List<Label> leftLabels = new List<Label>();
        private readonly List<Label> rightLabels = new List<Label>();
        private readonly List<Label> timeLabels = new List<Label>();
        private readonly List<ChartSeries> series = new List<ChartSeries>();
        private readonly List<ChartBand> bands = new List<ChartBand>();
        private readonly List<ChartLegendEntry> legendEntries = new List<ChartLegendEntry>();

        // The top of each stacked area, by series; null for the other series.
        private readonly List<float[]> stackTops = new List<float[]>();
        private readonly List<float[]> rangeValues = new List<float[]>();
        private readonly float[][] oneSeries = new float[1][];

        // Each series' scale: the chart's, or its own for a series that has one.
        private readonly List<Scale> scales = new List<Scale>();
        private readonly List<(float Min, float Max)> ownRanges = new List<(float, float)>();
        private readonly List<ChartTick> ticks = new List<ChartTick>();
        private readonly List<(Vector2, Vector2)> dashes = new List<(Vector2, Vector2)>();

        // The points of a dashed line since its last break, and whether each is an anchor of its dashes.
        private readonly List<Vector2> runPoints = new List<Vector2>();
        private readonly List<bool> runAnchors = new List<bool>();
        private readonly StringBuilder tooltipText = new StringBuilder();

        private int firstDay;
        private int count;
        private float rawMin;
        private float rawMax = 1f;
        private Scale scale = new Scale(0f, 1f, 0.25f);
        private bool hasChartScale;

        // The series whose scale each side writes; -1 for the chart's at the left, or for none.
        private int leftSeries = -1;
        private int rightSeries = -1;

        // The ticks of the time axis are made again only when these change.
        private int ticksFirst = -1;
        private int ticksLast = -1;
        private int ticksMax = -1;

        private int hoverIndex = -1;

        /// <param name="calendar">Writes the dates of the days; null writes their indices.</param>
        public TimeSeriesChart(string chartTitle, ChartCalendar calendar)
        {
            this.calendar = calendar;
            AddToClassList(ChartClass);
            title = new Label(chartTitle);
            title.AddToClassList(TitleClass);
            Add(title);

            legend = new ChartLegend();
            legend.style.display = DisplayStyle.None;
            Add(legend);

            var body = new VisualElement();
            body.AddToClassList(BodyClass);
            Add(body);

            leftAxis = Axis("left");
            body.Add(leftAxis);

            var column = new VisualElement();
            column.AddToClassList(ColumnClass);
            body.Add(column);

            rightAxis = Axis("right");
            rightAxis.style.display = DisplayStyle.None;
            body.Add(rightAxis);

            plot = new VisualElement();
            plot.AddToClassList(PlotClass);
            plot.generateVisualContent += Draw;
            plot.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            plot.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            plot.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            column.Add(plot);

            timeAxis = new VisualElement { pickingMode = PickingMode.Ignore };
            timeAxis.AddToClassList(TimeClass);
            column.Add(timeAxis);

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

        /// <summary>False to leave the legend out, when the page shows one for all its charts. A legend of a single colour is always left out.</summary>
        public bool ShowsLegend { get; set; } = true;

        /// <param name="first">The day of the first values, counted from the start of the game.</param>
        /// <param name="dayCount">Days drawn, one after the other: the first values of every series and band.</param>
        /// <param name="commonMin">With <paramref name="commonMax"/>, the range of the series without a scale of their own.</param>
        public void SetData(
            int first,
            int dayCount,
            IReadOnlyList<ChartSeries> chartSeries,
            IReadOnlyList<ChartBand> chartBands,
            float? commonMin = null,
            float? commonMax = null)
        {
            firstDay = first;
            count = Math.Max(0, dayCount);
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
            hasChartScale = bands.Count > 0;
            ownRanges.Clear();

            foreach (ChartSeries one in series)
            {
                hasChartScale |= !one.OwnScale;
                oneSeries[0] = one.Values;
                ChartMath.TryRange(oneSeries, count, out float ownMin, out float ownMax);
                ownRanges.Add((ownMin, ownMax));
            }

            oneSeries[0] = null;

            if (commonMin.HasValue && commonMax.HasValue && commonMax.Value > commonMin.Value)
            {
                rawMin = commonMin.Value;
                rawMax = commonMax.Value;
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

                ChartMath.TryRange(rangeValues, count, out rawMin, out rawMax);
            }

            ChooseAxes();
            WriteLegend();
            Rescale();

            if (hoverIndex >= count)
            {
                HideTooltip();
            }
            else if (hoverIndex >= 0)
            {
                WriteTooltip();
            }
        }

        private static VisualElement Axis(string side)
        {
            var axis = new VisualElement { pickingMode = PickingMode.Ignore };
            axis.AddToClassList(AxisClass);
            axis.AddToClassList(AxisClass + "--" + side);
            return axis;
        }

        // The left writes the chart's scale, or the first series' own when no series follows the
        // chart's; the right, the first series with its own scale that the left does not write.
        private void ChooseAxes()
        {
            leftSeries = -1;
            rightSeries = -1;

            for (int i = 0; i < series.Count; i++)
            {
                if (!series[i].OwnScale || series[i].Values == null || stackTops[i] != null)
                {
                    continue;
                }

                if (!hasChartScale && leftSeries < 0)
                {
                    leftSeries = i;
                }
                else if (rightSeries < 0)
                {
                    rightSeries = i;
                    break;
                }
            }
        }

        private void WriteLegend()
        {
            legendEntries.Clear();

            foreach (ChartSeries one in series)
            {
                legendEntries.Add(new ChartLegendEntry(one.Name, one.Color, one.Style));
            }

            foreach (ChartBand band in bands)
            {
                if (band.Name != null)
                {
                    legendEntries.Add(new ChartLegendEntry(band.Name, band.Color, ChartSeriesStyle.StackedArea));
                }
            }

            bool shows = ShowsLegend && legendEntries.Count > 1;
            legend.style.display = shows ? DisplayStyle.Flex : DisplayStyle.None;

            if (shows)
            {
                legend.SetEntries(legendEntries);
            }
        }

        // The scales from the ranges, with as many steps as the plot's height has room for, then
        // the axes that write them.
        private void Rescale()
        {
            float height = plot.contentRect.height;
            int maxTicks = float.IsNaN(height) || height <= 0f ? 4 : Mathf.Clamp((int)(height / ValueLabelSpacing), 2, 8);
            scale = Scale.Of(rawMin, rawMax, maxTicks);
            scales.Clear();

            for (int i = 0; i < series.Count; i++)
            {
                scales.Add(series[i].OwnScale && stackTops[i] == null ? Scale.Of(ownRanges[i].Min, ownRanges[i].Max, maxTicks) : scale);
            }

            WriteValueAxis(leftLabels, leftAxis, leftSeries < 0 ? scale : scales[leftSeries], leftSeries);
            rightAxis.style.display = rightSeries >= 0 ? DisplayStyle.Flex : DisplayStyle.None;

            if (rightSeries >= 0)
            {
                WriteValueAxis(rightLabels, rightAxis, scales[rightSeries], rightSeries);
            }

            WriteTimeAxis();
            plot.MarkDirtyRepaint();
        }

        private void OnGeometryChanged(GeometryChangedEvent change)
        {
            Rescale();
        }

        // In the colour of the series whose own scale it writes.
        private void WriteValueAxis(List<Label> labels, VisualElement axis, Scale axisScale, int ownSeries)
        {
            float height = plot.contentRect.height;
            int used = 0;

            if (count > 0 && !float.IsNaN(height) && height > 0f)
            {
                StyleColor color = ownSeries >= 0 ? new StyleColor(series[ownSeries].Color) : new StyleColor(StyleKeyword.Null);

                for (int step = 0; step <= axisScale.Steps; step++)
                {
                    float value = axisScale.ValueAt(step);
                    float y = ChartMath.YOf(value, axisScale.Min, axisScale.Max, height);
                    Label label = LabelAt(labels, axis, used++);
                    SetText(label, ChartMath.FormatTick(value, axisScale.Step));
                    label.style.top = Mathf.Clamp(y - AxisLabelHeight / 2f, 0f, Math.Max(0f, height - AxisLabelHeight));
                    label.style.color = color;
                }
            }

            HideFrom(labels, used);
        }

        private void WriteTimeAxis()
        {
            float width = plot.contentRect.width;
            int used = 0;

            if (count > 0 && calendar != null && !float.IsNaN(width) && width > 0f)
            {
                int maxTicks = Mathf.Clamp((int)(width / TimeLabelSpacing), 1, 12);
                int lastDay = firstDay + count - 1;

                if (firstDay != ticksFirst || lastDay != ticksLast || maxTicks != ticksMax)
                {
                    ChartTimeAxis.Ticks(firstDay, lastDay, maxTicks, calendar, ticks);
                    ticksFirst = firstDay;
                    ticksLast = lastDay;
                    ticksMax = maxTicks;
                }

                foreach (ChartTick tick in ticks)
                {
                    float x = ChartMath.XOf(tick.Day - firstDay, count, width);
                    Label label = LabelAt(timeLabels, timeAxis, used++);
                    SetText(label, tick.Label);
                    label.style.left = x;

                    // Centred on its day, but for those at the edges, which stay within the plot.
                    float shift = x < TimeLabelSpacing / 3f ? 0f : x > width - TimeLabelSpacing / 3f ? -100f : -50f;
                    label.style.translate = new Translate(Length.Percent(shift), 0f);
                }
            }

            HideFrom(timeLabels, used);
        }

        private static Label LabelAt(List<Label> labels, VisualElement axis, int index)
        {
            if (index == labels.Count)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList(AxisLabelClass);
                axis.Add(label);
                labels.Add(label);
            }

            labels[index].style.display = DisplayStyle.Flex;
            return labels[index];
        }

        private static void HideFrom(List<Label> labels, int index)
        {
            for (int i = index; i < labels.Count; i++)
            {
                labels[i].style.display = DisplayStyle.None;
            }
        }

        // Only a new text: the charts are set on most frames.
        private static void SetText(Label label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
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
                FillBetween(painter, rect, band.Lower, band.Upper, band.Color, scale.Min, scale.Max);
            }

            float[] below = null;

            for (int i = 0; i < series.Count; i++)
            {
                if (stackTops[i] != null)
                {
                    FillBetween(painter, rect, below, stackTops[i], series[i].Color, scale.Min, scale.Max);
                    below = stackTops[i];
                }
            }

            DrawGrid(painter, rect);

            for (int i = 0; i < series.Count; i++)
            {
                ChartSeries one = series[i];

                if (stackTops[i] != null || one.Values == null)
                {
                    continue;
                }

                StrokeLine(painter, rect, one.Values, one.Color, one.Style == ChartSeriesStyle.Dashed, scales[i].Min, scales[i].Max);
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

        // Faint lines at the steps of the left scale and at the dates.
        private void DrawGrid(Painter2D painter, Rect rect)
        {
            Scale leftScale = leftSeries < 0 ? scale : scales[leftSeries];
            painter.strokeColor = GridColor;
            painter.lineWidth = 1f;
            painter.BeginPath();

            for (int step = 0; step <= leftScale.Steps; step++)
            {
                float y = ChartMath.YOf(leftScale.ValueAt(step), leftScale.Min, leftScale.Max, rect.height);
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(rect.width, y));
            }

            if (calendar != null && ticksFirst == firstDay)
            {
                foreach (ChartTick tick in ticks)
                {
                    float x = ChartMath.XOf(tick.Day - firstDay, count, rect.width);
                    painter.MoveTo(new Vector2(x, 0f));
                    painter.LineTo(new Vector2(x, rect.height));
                }
            }

            painter.Stroke();
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

        // NaN breaks the line. A dashed line is measured along its length, but from anchors on
        // days of the game rather than from its first point: from there, its dashes crawled along
        // the curve each time a day came or went, which flickered.
        private void StrokeLine(Painter2D painter, Rect rect, float[] values, Color color, bool dashed, float low, float high)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1.5f;
            painter.BeginPath();

            int last = Math.Min(count, values.Length);
            int period = count > 1 ? ChartMath.AnchorPeriodDays(rect.width / (count - 1)) : 1;
            bool drawing = false;
            runPoints.Clear();
            runAnchors.Clear();

            for (int day = 0; day < last; day++)
            {
                if (float.IsNaN(values[day]))
                {
                    drawing = false;
                    DashRun(painter);
                    continue;
                }

                Vector2 point = PointOf(rect, day, values[day], low, high);

                if (dashed)
                {
                    runPoints.Add(point);
                    runAnchors.Add((firstDay + day) % period == 0);
                }
                else if (drawing)
                {
                    painter.LineTo(point);
                }
                else
                {
                    painter.MoveTo(point);
                }

                drawing = true;
            }

            DashRun(painter);
            painter.Stroke();
        }

        // The dashes of the points gathered since the last break of the line.
        private void DashRun(Painter2D painter)
        {
            dashes.Clear();
            ChartMath.DashPath(runPoints, runAnchors, DashLength, GapLength, dashes);

            foreach ((Vector2 from, Vector2 to) in dashes)
            {
                painter.MoveTo(from);
                painter.LineTo(to);
            }

            runPoints.Clear();
            runAnchors.Clear();
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

        // The chart's own days, not the record's: a chart drawn before the horizon was cut can be
        // hovered before it is drawn again.
        private void WriteTooltip()
        {
            tooltipText.Clear();
            tooltipText.Append(calendar != null ? calendar.FormatDay(firstDay + hoverIndex) : hoverIndex.ToString());

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

        /// <summary>A vertical scale of round steps.</summary>
        private readonly struct Scale
        {
            public Scale(float min, float max, float step)
            {
                Min = min;
                Max = max;
                Step = step;
            }

            public float Min { get; }

            public float Max { get; }

            public float Step { get; }

            public int Steps => Step > 0f ? (int)Math.Round((Max - Min) / Step) : 0;

            public static Scale Of(float min, float max, int maxTicks)
            {
                ChartMath.NiceScale(min, max, maxTicks, out float niceMin, out float niceMax, out float step);
                return new Scale(niceMin, niceMax, step);
            }

            public float ValueAt(int step)
            {
                return Min + step * Step;
            }
        }
    }
}
