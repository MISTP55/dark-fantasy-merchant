using System;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The cities tab of the debug panel: the present state of every city, in two sub-tabs, its
    /// population and its markets.
    /// </summary>
    public sealed class DebugCitiesView : IDebugView
    {
        private const int MarketsTab = 1;

        private readonly DebugTabs subtabs;
        private readonly DebugCitiesTable marketsTable;

        private bool isShown;

        public DebugCitiesView(DebugTabs subtabs, DebugCitiesTable marketsTable)
        {
            this.subtabs = subtabs ?? throw new ArgumentNullException(nameof(subtabs));
            this.marketsTable = marketsTable ?? throw new ArgumentNullException(nameof(marketsTable));
            subtabs.Changed += OnSubtabChanged;
            subtabs.Select(MarketsTab);
        }

        public void Show()
        {
            isShown = true;
            OnSubtabChanged(subtabs.Selected);
        }

        public void Hide()
        {
            isShown = false;
            marketsTable.Hide();
        }

        // The tables follow the markets themselves.
        public void Refresh()
        {
        }

        // A table that is not shown costs nothing: it only follows the markets on its sub-tab.
        private void OnSubtabChanged(int index)
        {
            if (isShown && index == MarketsTab)
            {
                marketsTable.Show();
            }
            else
            {
                marketsTable.Hide();
            }
        }
    }
}
