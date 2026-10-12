using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// A debug tool: the panel that lists every city of the map with its population and
    /// the weekly production, consumption and net gain of every good. Opened and closed
    /// with the debug key of the map's input; exists only in the Editor and in
    /// development builds. Reads the world's economy and changes nothing.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugCitiesPanelController : MonoBehaviour
    {
        [SerializeField] private WorldEconomy worldEconomy;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldMapInput input;

        private VisualElement panel;
        private VisualElement header;
        private VisualElement rows;
        private VisualElement totals;
        private Button closeButton;

        // Made the first time the panel is opened: the world's economy starts after this panel is enabled.
        private DebugCitiesTable table;

        private bool isBound;
        private bool isOpen;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null)
            {
                Debug.LogError("DebugCitiesPanelController needs a UIDocument with a source asset.", this);
                return;
            }

            // Only the panel itself should block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // Covers the screen whatever else shares the panel. Set before anything can
            // fail: a root left in the flow would push the city panel off the center.
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;

            // A debug tool is not a part of the game the player gets.
            if (!Debug.isDebugBuild)
            {
                root.style.display = DisplayStyle.None;
                return;
            }

            if (worldEconomy == null || mapView == null || input == null)
            {
                Debug.LogError("DebugCitiesPanelController needs a world economy, a map view and an input component.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            panel = root.Q<VisualElement>("debug-cities-panel");
            header = root.Q<VisualElement>("debug-cities-header");
            rows = root.Q<VisualElement>("debug-cities-rows");
            totals = root.Q<VisualElement>("debug-cities-totals");
            closeButton = root.Q<Button>("close-button");

            if (panel == null || header == null || rows == null || totals == null || closeButton == null)
            {
                Debug.LogError("DebugCitiesPanel.uxml is missing an expected element.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            // A document that is enabled again has a new tree: the table is rebuilt in it.
            table = null;
            isOpen = false;
            panel.style.display = DisplayStyle.None;

            // The button is clicked, not navigated: the keyboard pans the map.
            closeButton.focusable = false;

            closeButton.clicked += Close;
            input.DebugToggled += Toggle;
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            closeButton.clicked -= Close;
            input.DebugToggled -= Toggle;

            // Stops listening to the markets.
            table?.Hide();

            isBound = false;
        }

        private void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Open()
        {
            if (table == null && !TryBuildTable())
            {
                return;
            }

            table.Show();
            panel.style.display = DisplayStyle.Flex;
            isOpen = true;
        }

        private void Close()
        {
            table?.Hide();
            panel.style.display = DisplayStyle.None;
            isOpen = false;
        }

        private bool TryBuildTable()
        {
            if (!worldEconomy.IsReady)
            {
                // WorldEconomy already logged why cities have no market.
                return false;
            }

            var goodNames = new List<string>();

            foreach (GoodDefinition good in worldEconomy.Economy.Goods)
            {
                goodNames.Add(good.DisplayName);
            }

            table = new DebugCitiesTable(header, rows, totals, goodNames);

            foreach (CityDefinition city in mapView.Cities)
            {
                CityMarket market = worldEconomy.MarketOf(city);

                if (market != null)
                {
                    table.AddCity(city.DisplayName, city.Population, market);
                }
            }

            return true;
        }
    }
}
