using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The debug tools' panel, with one tab per tool: the cities, with their population
    /// and the weekly production, consumption and net gain of every good, and the ships,
    /// the player's and the others, with what they are doing and carrying. Opened and
    /// closed with the debug key of the map's input; exists only in the Editor and in
    /// development builds. Reads the simulation and changes nothing, but for the buttons
    /// of the ships tab, which add and remove the ships of the AI merchants.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugPanelController : MonoBehaviour
    {
        private const string SelectedTabClass = "debug-panel__tab--selected";

        [SerializeField] private WorldEconomy worldEconomy;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldMapInput input;

        [Tooltip("Optional. Without it, the panel has no ships tab.")]
        [SerializeField] private ShipsView shipsView;

        [Tooltip("Optional. Without it, the ships tab shows no profit and has no button to add or remove a ship.")]
        [SerializeField] private AiMerchants aiMerchants;

        private VisualElement panel;
        private VisualElement citiesPage;
        private VisualElement shipsPage;
        private VisualElement citiesHeader;
        private VisualElement citiesRows;
        private VisualElement citiesTotals;
        private VisualElement shipsHeader;
        private VisualElement shipsRows;
        private Button citiesTab;
        private Button shipsTab;
        private Button closeButton;
        private Button addShipButton;

        // Made the first time the panel is opened: the world's economy starts after this panel is enabled.
        private DebugCitiesTable citiesTable;
        private DebugShipsTable shipsTable;

        // Reused by every refresh of the ships tab.
        private readonly List<DebugShipInfo> shipInfos = new List<DebugShipInfo>();

        private bool isBound;
        private bool isOpen;

        // Kept while the panel is closed: it opens on the tab it was last on.
        private bool showsShips;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null)
            {
                Debug.LogError("DebugPanelController needs a UIDocument with a source asset.", this);
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
                Debug.LogError("DebugPanelController needs a world economy, a map view and an input component.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            panel = root.Q<VisualElement>("debug-panel");
            citiesPage = root.Q<VisualElement>("cities-page");
            shipsPage = root.Q<VisualElement>("ships-page");
            citiesHeader = root.Q<VisualElement>("debug-cities-header");
            citiesRows = root.Q<VisualElement>("debug-cities-rows");
            citiesTotals = root.Q<VisualElement>("debug-cities-totals");
            shipsHeader = root.Q<VisualElement>("debug-ships-header");
            shipsRows = root.Q<VisualElement>("debug-ships-rows");
            citiesTab = root.Q<Button>("cities-tab");
            shipsTab = root.Q<Button>("ships-tab");
            closeButton = root.Q<Button>("close-button");
            addShipButton = root.Q<Button>("add-ship-button");

            if (panel == null || citiesPage == null || shipsPage == null || citiesHeader == null || citiesRows == null
                || citiesTotals == null || shipsHeader == null || shipsRows == null || citiesTab == null
                || shipsTab == null || closeButton == null || addShipButton == null)
            {
                Debug.LogError("DebugPanel.uxml is missing an expected element.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            // A document that is enabled again has a new tree: the tables are rebuilt in it.
            citiesTable = null;
            shipsTable = null;
            isOpen = false;
            panel.style.display = DisplayStyle.None;

            // Without ships there is nothing to list.
            if (shipsView == null)
            {
                showsShips = false;
                shipsTab.style.display = DisplayStyle.None;
            }

            // Only the AI merchants add ships.
            if (aiMerchants == null)
            {
                addShipButton.style.display = DisplayStyle.None;
            }

            // The buttons are clicked, not navigated: the keyboard pans the map.
            addShipButton.focusable = false;
            closeButton.focusable = false;
            citiesTab.focusable = false;
            shipsTab.focusable = false;

            closeButton.clicked += Close;
            citiesTab.clicked += ShowCities;
            shipsTab.clicked += ShowShips;
            addShipButton.clicked += AddShip;
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
            citiesTab.clicked -= ShowCities;
            shipsTab.clicked -= ShowShips;
            addShipButton.clicked -= AddShip;
            input.DebugToggled -= Toggle;

            // Stops listening to the markets.
            citiesTable?.Hide();

            isBound = false;
        }

        // Ships sail every frame: their tab is rewritten as often, while it is shown.
        private void Update()
        {
            if (isBound && isOpen && showsShips)
            {
                RefreshShips();
            }
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
            if (citiesTable == null && !TryBuildTables())
            {
                return;
            }

            isOpen = true;
            panel.style.display = DisplayStyle.Flex;
            ShowTab(showsShips);
        }

        private void Close()
        {
            citiesTable?.Hide();
            panel.style.display = DisplayStyle.None;
            isOpen = false;
        }

        private void ShowCities()
        {
            ShowTab(false);
        }

        private void ShowShips()
        {
            ShowTab(true);
        }

        private void ShowTab(bool ships)
        {
            showsShips = ships;
            citiesPage.style.display = ships ? DisplayStyle.None : DisplayStyle.Flex;
            shipsPage.style.display = ships ? DisplayStyle.Flex : DisplayStyle.None;
            citiesTab.EnableInClassList(SelectedTabClass, !ships);
            shipsTab.EnableInClassList(SelectedTabClass, ships);

            // A table that is not shown costs nothing: the cities' only follows the markets on its tab.
            if (ships)
            {
                citiesTable.Hide();
                RefreshShips();
            }
            else
            {
                citiesTable.Show();
            }
        }

        // In the port of a city drawn at random. Nothing happens before the merchants have started.
        private void AddShip()
        {
            aiMerchants.AddShip();
            RefreshShips();
        }

        private void RemoveShip(Ship ship)
        {
            aiMerchants.RemoveShip(ship);
            RefreshShips();
        }

        private void RefreshShips()
        {
            shipInfos.Clear();

            // The player's ship is the first of the map's.
            foreach (Ship ship in shipsView.Ships)
            {
                CityDefinition port = shipsView.Docking.PortOf(ship);
                CityDefinition destination = shipsView.Docking.DestinationPortOf(ship);
                long? profit = aiMerchants != null && aiMerchants.TryGetProfit(ship, out long gold) ? gold : (long?)null;

                shipInfos.Add(new DebugShipInfo(
                    shipsView.DisplayNameOf(ship),
                    shipsView.IsPlayerShip(ship),
                    ship,
                    port != null ? port.DisplayName : null,
                    destination != null ? destination.DisplayName : null,
                    profit));
            }

            shipsTable.Refresh(shipInfos);

            // Greyed until the merchants have started, and on a map with no city to start in.
            if (aiMerchants != null)
            {
                addShipButton.SetEnabled(aiMerchants.CanAddShip);
            }
        }

        private bool TryBuildTables()
        {
            if (!worldEconomy.IsReady)
            {
                // WorldEconomy already logged why cities have no market.
                return false;
            }

            var goodNames = new List<string>();

            foreach (GoodDefinition good in worldEconomy.Economy.Goods)
            {
                goodNames.Add(GoodUnitText.NameWithUnit(good.DisplayName, good.Unit));
            }

            citiesTable = new DebugCitiesTable(citiesHeader, citiesRows, citiesTotals, goodNames);

            foreach (CityDefinition city in mapView.Cities)
            {
                CityMarket market = worldEconomy.MarketOf(city);

                if (market != null)
                {
                    citiesTable.AddCity(city.DisplayName, market);
                }
            }

            // Without the AI merchants, no ship can be removed: the rows have no button.
            shipsTable = aiMerchants != null
                ? new DebugShipsTable(shipsHeader, shipsRows, goodNames, RemoveShip)
                : new DebugShipsTable(shipsHeader, shipsRows, goodNames);
            return true;
        }
    }
}
