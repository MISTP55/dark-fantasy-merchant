using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The debug tools' panel, in four tabs: the cities as they are, the curves of their past, the
    /// flows of the trades in their markets, and the ships, the player's and the others, with what
    /// they are doing and carrying. The header chooses how far back the record goes, speeds the
    /// world's normal speed up and makes the player's buildings free. Opened and closed with the
    /// debug key, on the map and in the city view; exists only in the Editor and in development builds. Reads the
    /// simulation and changes nothing, but for the buttons of the ships tab, which add and remove
    /// the ships of the AI merchants, and the slider and the free building box of the header.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugPanelController : MonoBehaviour
    {
        private const int CitiesTab = 0;
        private const int CurvesTab = 1;
        private const int TradesTab = 2;
        private const int ShipsTab = 3;

        private static readonly string[] TabNames = { "cities", "curves", "trades", "ships" };

        [SerializeField] private WorldEconomy worldEconomy;
        [SerializeField] private WorldMapView mapView;

        [Tooltip("Optional. Without it, the panel has no ships tab.")]
        [SerializeField] private ShipsView shipsView;

        [Tooltip("Optional. Without it, the ships tab shows no profit and has no button to add or remove a ship.")]
        [SerializeField] private AiMerchants aiMerchants;

        [Tooltip("Optional. Without it, the panel has no slider to speed the world up.")]
        [SerializeField] private WorldClock worldClock;

        [Tooltip("Optional. Without it, the curves and trades tabs have nothing to show.")]
        [SerializeField] private SimulationRecorder recorder;

        [Tooltip("Optional. Without it, the panel has no box to make the player's buildings free.")]
        [SerializeField] private PlayerBuilder playerBuilder;

        private VisualElement panel;
        private readonly Button[] tabButtons = new Button[TabNames.Length];
        private readonly VisualElement[] pages = new VisualElement[TabNames.Length];
        private VisualElement citiesHeader;
        private VisualElement citiesRows;
        private VisualElement citiesTotals;
        private VisualElement goodsHeader;
        private VisualElement goodsRows;
        private VisualElement goodsTotals;
        private VisualElement shipsHeader;
        private VisualElement shipsRows;
        private Label unavailable;
        private Button closeButton;
        private Button addShipButton;
        private VisualElement speed;
        private Label speedLabel;
        private SliderInt speedSlider;
        private VisualElement horizon;
        private DropdownField horizonField;
        private Toggle freeBuildingToggle;

        // Null without a usable world clock.
        private GameClock clock;

        // Made the first time the panel is opened: the world's economy starts after this panel is enabled.
        private DebugTabs tabs;
        private DebugShipsTable shipsTable;
        private readonly IDebugView[] views = new IDebugView[TabNames.Length];

        // The cities tab's view, also in views: its tables write what the markets changed once a frame.
        private DebugCitiesView citiesView;

        // The sub-tabs of the curves, whose view comes later.
        private DebugTabs curvesSubtabs;

        // The goods' names, in the catalogue's order, for the views made later.
        private readonly List<string> goodNames = new List<string>();

        // Reused by every refresh of the ships tab.
        private readonly List<DebugShipInfo> shipInfos = new List<DebugShipInfo>();

        private IDebugView shownView;
        private bool isBound;
        private bool isOpen;

        // The record has days the shown view has not drawn.
        private bool isDirty;
        private bool followsRecord;

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

            if (worldEconomy == null || mapView == null)
            {
                Debug.LogError("DebugPanelController needs a world economy and a map view.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            if (!TryFindElements(root))
            {
                Debug.LogError("DebugPanel.uxml is missing an expected element.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            // A document that is enabled again has a new tree: the tables are rebuilt in it.
            tabs = null;
            shipsTable = null;
            System.Array.Clear(views, 0, views.Length);
            citiesView = null;
            shownView = null;
            isOpen = false;
            panel.style.display = DisplayStyle.None;
            unavailable.style.display = DisplayStyle.None;

            // Without ships there is nothing to list.
            if (shipsView == null)
            {
                tabButtons[ShipsTab].style.display = DisplayStyle.None;
            }

            // Only the AI merchants add ships.
            if (aiMerchants == null)
            {
                addShipButton.style.display = DisplayStyle.None;
            }

            // The buttons are clicked, not navigated: the keyboard pans the map.
            addShipButton.focusable = false;
            closeButton.focusable = false;
            speedSlider.focusable = false;
            horizonField.focusable = false;
            freeBuildingToggle.focusable = false;

            if (playerBuilder == null)
            {
                freeBuildingToggle.style.display = DisplayStyle.None;
            }
            else
            {
                freeBuildingToggle.SetValueWithoutNotify(playerBuilder.IsFree);
                freeBuildingToggle.RegisterValueChangedCallback(OnFreeBuildingChanged);
            }

            clock = worldClock != null && worldClock.IsReady ? worldClock.Clock : null;

            if (clock == null)
            {
                speed.style.display = DisplayStyle.None;
            }
            else
            {
                speedSlider.SetValueWithoutNotify(Mathf.RoundToInt(clock.NormalSpeedMultiplier));
                speedSlider.RegisterValueChangedCallback(OnSpeedChanged);
                ShowSpeed();
            }

            if (recorder == null)
            {
                horizon.style.display = DisplayStyle.None;
            }
            else
            {
                horizonField.choices = new List<string>(HorizonChoices.Labels);
                horizonField.index = recorder.HorizonIndex;
                horizonField.RegisterValueChangedCallback(OnHorizonChanged);
                recorder.Started += OnRecordStarted;
                FollowRecord();
            }

            closeButton.clicked += Close;
            addShipButton.clicked += AddShip;
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            closeButton.clicked -= Close;
            addShipButton.clicked -= AddShip;

            if (clock != null)
            {
                speedSlider.UnregisterValueChangedCallback(OnSpeedChanged);
            }

            if (playerBuilder != null)
            {
                freeBuildingToggle.UnregisterValueChangedCallback(OnFreeBuildingChanged);
            }

            if (recorder != null)
            {
                horizonField.UnregisterValueChangedCallback(OnHorizonChanged);
                recorder.Started -= OnRecordStarted;

                if (followsRecord)
                {
                    recorder.Record.Recorded -= OnRecorded;
                    followsRecord = false;
                }
            }

            // Stops following the markets.
            shownView?.Hide();
            shownView = null;

            isBound = false;
        }

        // Ships sail every frame: their tab is rewritten as often, while it is shown. The other
        // views are rewritten at most once a frame, when the record has new days, and the cities
        // tab writes what the markets changed once a frame.
        private void Update()
        {
            if (!isBound)
            {
                return;
            }

            // Read here, not by the map's input, which the city view switches off.
            if (DebugKey.WasPressedThisFrame())
            {
                Toggle();
            }

            if (!isOpen)
            {
                return;
            }

            if (tabs.Selected == ShipsTab)
            {
                RefreshShips();
            }
            else if (isDirty)
            {
                shownView?.Refresh();
            }

            if (shownView != null && shownView == citiesView)
            {
                citiesView.WriteChanges();
            }

            isDirty = false;
        }

        private bool TryFindElements(VisualElement root)
        {
            panel = root.Q<VisualElement>("debug-panel");

            for (int i = 0; i < TabNames.Length; i++)
            {
                tabButtons[i] = root.Q<Button>(TabNames[i] + "-tab");
                pages[i] = root.Q<VisualElement>(TabNames[i] + "-page");
            }

            citiesHeader = root.Q<VisualElement>("debug-cities-header");
            citiesRows = root.Q<VisualElement>("debug-cities-rows");
            citiesTotals = root.Q<VisualElement>("debug-cities-totals");
            goodsHeader = root.Q<VisualElement>("debug-goods-header");
            goodsRows = root.Q<VisualElement>("debug-goods-rows");
            goodsTotals = root.Q<VisualElement>("debug-goods-totals");
            shipsHeader = root.Q<VisualElement>("debug-ships-header");
            shipsRows = root.Q<VisualElement>("debug-ships-rows");
            unavailable = root.Q<Label>("recording-unavailable");
            closeButton = root.Q<Button>("close-button");
            addShipButton = root.Q<Button>("add-ship-button");
            speed = root.Q<VisualElement>("speed");
            speedLabel = root.Q<Label>("speed-label");
            speedSlider = root.Q<SliderInt>("speed-slider");
            horizon = root.Q<VisualElement>("horizon");
            horizonField = root.Q<DropdownField>("horizon-field");
            freeBuildingToggle = root.Q<Toggle>("free-building-toggle");

            foreach (VisualElement element in new VisualElement[]
                     {
                         panel, citiesHeader, citiesRows, citiesTotals, goodsHeader, goodsRows, goodsTotals, shipsHeader,
                         shipsRows, unavailable, closeButton, addShipButton, speed, speedLabel, speedSlider, horizon, horizonField,
                         freeBuildingToggle,
                     })
            {
                if (element == null)
                {
                    return false;
                }
            }

            for (int i = 0; i < TabNames.Length; i++)
            {
                if (tabButtons[i] == null || pages[i] == null)
                {
                    return false;
                }
            }

            return true;
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

        // On the tab it was last on.
        private void Open()
        {
            if (tabs == null && !TryBuildViews())
            {
                return;
            }

            isOpen = true;
            panel.style.display = DisplayStyle.Flex;

            if (tabs.Selected < 0)
            {
                tabs.Select(CitiesTab);
            }
            else
            {
                ShowTab(tabs.Selected);
            }
        }

        private void Close()
        {
            shownView?.Hide();
            shownView = null;
            panel.style.display = DisplayStyle.None;
            isOpen = false;
        }

        private void OnTabChanged(int index)
        {
            if (isOpen)
            {
                ShowTab(index);
            }
        }

        private void ShowTab(int index)
        {
            shownView?.Hide();
            shownView = null;

            // The tabs of the past have nothing to show before the record exists.
            bool needsRecord = index == CurvesTab || index == TradesTab;
            bool available = !needsRecord || (recorder != null && recorder.IsReady);
            pages[index].style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            unavailable.style.display = available ? DisplayStyle.None : DisplayStyle.Flex;

            if (index == ShipsTab)
            {
                RefreshShips();
                return;
            }

            if (available && needsRecord && views[index] == null)
            {
                CreateRecordView(index);
            }

            if (available)
            {
                shownView = views[index];
                shownView?.Show();
            }
        }

        private void OnRecordStarted()
        {
            FollowRecord();

            if (isOpen)
            {
                ShowTab(tabs.Selected);
            }
        }

        private void FollowRecord()
        {
            if (!followsRecord && recorder.IsReady)
            {
                recorder.Record.Recorded += OnRecorded;
                followsRecord = true;
            }
        }

        private void OnRecorded()
        {
            isDirty = true;
        }

        private void OnHorizonChanged(ChangeEvent<string> change)
        {
            recorder.SetHorizon(horizonField.index);
        }

        // The normal speed, not a fast forward: the map stays playable.
        private void OnSpeedChanged(ChangeEvent<int> change)
        {
            clock.SetNormalSpeedMultiplier(change.newValue);
            ShowSpeed();
        }

        // Neither gold nor materials: the city view's offers are quoted again at once.
        private void OnFreeBuildingChanged(ChangeEvent<bool> change)
        {
            playerBuilder.SetFree(change.newValue);
        }

        private void ShowSpeed()
        {
            speedLabel.text = $"Vitesse ×{Mathf.RoundToInt(clock.NormalSpeedMultiplier)}";
        }

        // In the port of a city with the fewest AI ships homed there, at random among ties.
        // Nothing happens before the merchants have started.
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
            if (shipsTable == null)
            {
                return;
            }

            shipInfos.Clear();

            // The player's ship is the first of the map's.
            foreach (Ship ship in shipsView.Ships)
            {
                CityDefinition city = shipsView.Docking.CityOf(ship);
                CityDefinition destination = shipsView.Docking.DestinationPortOf(ship);
                long? profit = aiMerchants != null && aiMerchants.TryGetProfit(ship, out long gold) ? gold : (long?)null;
                CityDefinition home = null;
                CityDefinition finalDestination = null;

                if (aiMerchants != null)
                {
                    aiMerchants.TryGetTour(ship, out home, out finalDestination);
                }

                shipInfos.Add(new DebugShipInfo(
                    shipsView.DisplayNameOf(ship),
                    shipsView.IsPlayerShip(ship),
                    ship,
                    city != null ? city.DisplayName : null,
                    destination != null ? destination.DisplayName : null,
                    profit,
                    home != null ? home.DisplayName : null,
                    finalDestination != null ? finalDestination.DisplayName : null,
                    shipsView.Docking.StateOf(ship)));
            }

            shipsTable.Refresh(shipInfos);

            // Greyed until the merchants have started, and on a map with no city to start in.
            if (aiMerchants != null)
            {
                addShipButton.SetEnabled(aiMerchants.CanAddShip);
            }
        }

        private bool TryBuildViews()
        {
            if (!worldEconomy.IsReady)
            {
                // WorldEconomy already logged why cities have no market.
                return false;
            }

            goodNames.Clear();

            foreach (GoodDefinition good in worldEconomy.Economy.Goods)
            {
                goodNames.Add(good.DisplayName);
            }

            VisualElement citiesPage = pages[CitiesTab];
            var citiesTable = new DebugCitiesTable(
                citiesHeader, citiesRows, citiesTotals, goodsHeader, goodsRows, goodsTotals, goodNames);
            var populationTable = new DebugPopulationTable(
                citiesPage.Q<VisualElement>("population-header"),
                citiesPage.Q<VisualElement>("population-rows"),
                citiesPage.Q<VisualElement>("population-totals"));
            var goodTable = new DebugGoodMarketTable(
                citiesPage.Q<VisualElement>("good-market-header"),
                citiesPage.Q<VisualElement>("good-market-rows"),
                citiesPage.Q<VisualElement>("good-market-totals"));

            foreach (CityDefinition city in mapView.Cities)
            {
                CityMarket market = worldEconomy.MarketOf(city);

                if (market != null)
                {
                    citiesTable.AddCity(city.DisplayName, market);
                    populationTable.AddCity(city.DisplayName, market);
                    goodTable.AddCity(city.DisplayName, market, good => aiMerchants != null ? aiMerchants.UnderWayTo(city, good) : 0);
                }
            }

            citiesView = new DebugCitiesView(
                SubtabsOf(citiesPage, "population", "markets"),
                populationTable,
                citiesTable,
                goodTable,
                citiesPage.Q<DropdownField>("market-good-field"),
                citiesPage.Q<VisualElement>("all-goods"),
                citiesPage.Q<VisualElement>("good-market"),
                goodNames);
            views[CitiesTab] = citiesView;
            curvesSubtabs = SubtabsOf(pages[CurvesTab], "city-curves", "indicator-curves", "composed-curves");
            curvesSubtabs.Select(0);

            // Without the AI merchants, no ship can be removed: the rows have no button.
            if (shipsView != null)
            {
                shipsTable = aiMerchants != null
                    ? new DebugShipsTable(shipsHeader, shipsRows, goodNames, RemoveShip)
                    : new DebugShipsTable(shipsHeader, shipsRows, goodNames);
            }

            tabs = new DebugTabs(tabButtons, pages);
            tabs.Changed += OnTabChanged;
            return true;
        }

        // The views of the past, made once the record exists: they list its cities.
        private void CreateRecordView(int index)
        {
            if (index == CurvesTab)
            {
                VisualElement page = pages[CurvesTab];
                var curves = new IDebugView[]
                {
                    new DebugCityCurvesView(page.Q<VisualElement>("city-curves-page"), recorder, goodNames),
                    new DebugIndicatorCurvesView(page.Q<VisualElement>("indicator-curves-page"), recorder, goodNames),
                    new DebugComposedCurvesView(page.Q<VisualElement>("composed-curves-page"), recorder, goodNames),
                };

                // Every chart of the sub-tabs is enlarged by a click, over the whole tab.
                var zoom = new ChartZoom(recorder.ChartCalendar);
                page.Query<TimeSeriesChart>().ForEach(zoom.Attach);
                page.Add(zoom);
                views[CurvesTab] = new DebugCurvesView(curvesSubtabs, curves, zoom);
            }
            else if (index == TradesTab)
            {
                views[TradesTab] = new DebugTradesView(pages[TradesTab], recorder, goodNames);
            }
        }

        private static DebugTabs SubtabsOf(VisualElement page, params string[] names)
        {
            var buttons = new Button[names.Length];
            var subpages = new VisualElement[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                buttons[i] = page.Q<Button>(names[i] + "-tab");
                subpages[i] = page.Q<VisualElement>(names[i] + "-page");
            }

            return new DebugTabs(buttons, subpages);
        }
    }
}
