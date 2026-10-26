using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The cities tab of the debug panel: the present state of every city, in two sub-tabs. Its
    /// population; its markets, for one good chosen in a list, or for every good, side by side.
    /// </summary>
    public sealed class DebugCitiesView : IDebugView
    {
        private const int PopulationTab = 0;
        private const int MarketsTab = 1;

        // The first choice of the list of goods.
        private const string AllGoods = "Toutes les marchandises";

        private readonly DebugTabs subtabs;
        private readonly DebugPopulationTable populationTable;
        private readonly DebugCitiesTable allGoodsTable;
        private readonly DebugGoodMarketTable goodTable;
        private readonly DropdownField goodField;
        private readonly VisualElement allGoodsElement;
        private readonly VisualElement goodElement;

        // The good of every choice after the first: their indices in the catalogue, sorted by name.
        private readonly int[] goodOrder;

        private bool isShown;

        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        public DebugCitiesView(
            DebugTabs subtabs,
            DebugPopulationTable populationTable,
            DebugCitiesTable allGoodsTable,
            DebugGoodMarketTable goodTable,
            DropdownField goodField,
            VisualElement allGoodsElement,
            VisualElement goodElement,
            IReadOnlyList<string> goodNames)
        {
            this.subtabs = subtabs ?? throw new ArgumentNullException(nameof(subtabs));
            this.populationTable = populationTable ?? throw new ArgumentNullException(nameof(populationTable));
            this.allGoodsTable = allGoodsTable ?? throw new ArgumentNullException(nameof(allGoodsTable));
            this.goodTable = goodTable ?? throw new ArgumentNullException(nameof(goodTable));
            this.goodField = goodField ?? throw new ArgumentNullException(nameof(goodField));
            this.allGoodsElement = allGoodsElement ?? throw new ArgumentNullException(nameof(allGoodsElement));
            this.goodElement = goodElement ?? throw new ArgumentNullException(nameof(goodElement));

            goodOrder = NameOrder.Of(goodNames ?? throw new ArgumentNullException(nameof(goodNames)));
            var choices = new List<string> { AllGoods };

            foreach (int good in goodOrder)
            {
                choices.Add(goodNames[good]);
            }

            goodField.choices = choices;
            goodField.index = 0;
            goodField.focusable = false;
            goodField.RegisterValueChangedCallback(change => ShowTables());
            subtabs.Changed += index => ShowTables();
            subtabs.Select(MarketsTab);
        }

        public void Show()
        {
            isShown = true;
            ShowTables();
        }

        public void Hide()
        {
            isShown = false;
            ShowTables();
        }

        // The tables follow the markets themselves; what is under way changes without them.
        public void Refresh()
        {
            if (isShown && subtabs.Selected == MarketsTab && goodField.index > 0)
            {
                goodTable.WriteAll();
            }
        }

        /// <summary>
        /// Writes what the markets changed since the last frame in the table shown. Called every frame while the
        /// view is shown: a market changes many times a frame at high speed, so the tables only mark their rows.
        /// </summary>
        public void WriteChanges()
        {
            if (!isShown)
            {
                return;
            }

            if (subtabs.Selected == PopulationTab)
            {
                populationTable.WriteChanges();
            }
            else if (goodField.index > 0)
            {
                goodTable.WriteChanges();
            }
            else
            {
                allGoodsTable.WriteChanges();
            }
        }

        // A table that is not shown costs nothing: only the one in view follows the markets.
        private void ShowTables()
        {
            bool markets = isShown && subtabs.Selected == MarketsTab;
            bool oneGood = goodField.index > 0;

            allGoodsElement.style.display = oneGood ? DisplayStyle.None : DisplayStyle.Flex;
            goodElement.style.display = oneGood ? DisplayStyle.Flex : DisplayStyle.None;

            if (isShown && subtabs.Selected == PopulationTab)
            {
                populationTable.Show();
            }
            else
            {
                populationTable.Hide();
            }

            if (markets && !oneGood)
            {
                allGoodsTable.Show();
            }
            else
            {
                allGoodsTable.Hide();
            }

            if (markets && oneGood)
            {
                goodTable.SetGood(goodOrder[goodField.index - 1]);
                goodTable.Show();
            }
            else
            {
                goodTable.Hide();
            }
        }
    }
}
