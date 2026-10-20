using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// A debug chart drawn large over its page, from a click on it: it follows the chart it
    /// enlarges, refresh after refresh, until a click anywhere on it closes it.
    /// </summary>
    public sealed class ChartZoom : VisualElement
    {
        public const string ZoomableClass = "debug-chart--zoomable";

        private const string ZoomClass = "debug-zoom";
        private const string HintClass = "debug-zoom__hint";
        private const string ChartClass = "debug-zoom__chart";

        private TimeSeriesChart source;

        public ChartZoom(ChartCalendar calendar)
        {
            AddToClassList(ZoomClass);
            style.display = DisplayStyle.None;

            // With its legend, whatever the small one shows: the chips of a composed chart stay under it.
            Chart = new TimeSeriesChart(string.Empty, calendar);
            Chart.AddToClassList(ChartClass);
            Add(Chart);

            // Over the chart's corner.
            var hint = new Label("Cliquer pour réduire") { pickingMode = PickingMode.Ignore };
            hint.AddToClassList(HintClass);
            Add(hint);

            RegisterCallback<ClickEvent>(click => Close());
        }

        /// <summary>The large chart.</summary>
        public TimeSeriesChart Chart { get; }

        public bool IsOpen => source != null;

        /// <summary>Lets a click on a chart enlarge it.</summary>
        public void Attach(TimeSeriesChart chart)
        {
            chart.Clicked += Show;
            chart.AddToClassList(ZoomableClass);
        }

        public void Show(TimeSeriesChart chart)
        {
            if (source != null)
            {
                source.Mirror = null;
            }

            source = chart;
            Chart.Title = chart.Title;
            chart.Mirror = Chart;
            style.display = DisplayStyle.Flex;
        }

        public void Close()
        {
            if (source == null)
            {
                return;
            }

            source.Mirror = null;
            source = null;
            style.display = DisplayStyle.None;
        }
    }
}
