using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The curves tab of the debug panel: the past of the cities, in three sub-tabs, by city, by
    /// indicator, and composed of the curves one chooses.
    /// </summary>
    public sealed class DebugCurvesView : IDebugView
    {
        private readonly DebugTabs subtabs;
        private readonly IReadOnlyList<IDebugView> views;
        private readonly ChartZoom zoom;

        private IDebugView shownView;
        private bool isShown;

        /// <param name="views">One view per sub-tab, in the same order; a null one shows nothing.</param>
        /// <param name="zoom">Optional: the large chart, closed when the sub-tab changes or the tab is left.</param>
        public DebugCurvesView(DebugTabs subtabs, IReadOnlyList<IDebugView> views, ChartZoom zoom = null)
        {
            this.subtabs = subtabs ?? throw new ArgumentNullException(nameof(subtabs));
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            this.zoom = zoom;
            subtabs.Changed += OnSubtabChanged;

            if (subtabs.Selected < 0)
            {
                subtabs.Select(0);
            }
        }

        public void Show()
        {
            isShown = true;
            OnSubtabChanged(subtabs.Selected);
        }

        public void Hide()
        {
            isShown = false;
            zoom?.Close();
            shownView?.Hide();
            shownView = null;
        }

        public void Refresh()
        {
            shownView?.Refresh();
        }

        private void OnSubtabChanged(int index)
        {
            zoom?.Close();
            shownView?.Hide();
            shownView = isShown && index >= 0 && index < views.Count ? views[index] : null;
            shownView?.Show();
        }
    }
}
