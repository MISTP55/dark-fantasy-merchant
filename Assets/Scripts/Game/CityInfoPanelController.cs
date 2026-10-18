using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Presents the hovered city's name and the selected city's information panel: the city
    /// on the left, and on the right its market, the player's buildings and the ships in its
    /// port in three tabs. The market trades with the player's ship chosen in its drop-down
    /// list; clicking a ship in the ships tab selects it, so that it can be ordered out.
    /// Reads the map's selection state; knows nothing about markers.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CityInfoPanelController : MonoBehaviour
    {
        private const string ShipRowClass = "city-panel__ship";
        private const string SelectedShipRowClass = "city-panel__ship--selected";
        private const string ProducedGoodsClass = "city-panel__goods";
        private const string EmptyClass = "city-panel__empty";
        private const string OpenTabClass = "city-panel__tab--open";

        private enum Tab
        {
            Market,
            Buildings,
            Ships,
        }

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private Camera mapCamera;

        [Tooltip("Optional. Without it, the panel lists no ship in port.")]
        [SerializeField] private ShipsView shipsView;

        [Tooltip("Optional. Without it, the panel shows no market.")]
        [SerializeField] private WorldEconomy worldEconomy;

        [Tooltip("Optional. Without it, the market is only read: nothing is bought or sold.")]
        [SerializeField] private PlayerTreasury playerTreasury;

        [Tooltip("Optional. Without it, a building built is not announced.")]
        [SerializeField] private PlayerNotifications playerNotifications;

        [Tooltip("Offset of the hover label from the city, in panel units.")]
        [SerializeField] private Vector2 hoverLabelOffset = new Vector2(14f, -30f);

        private VisualElement root;
        private VisualElement cityPanel;
        private Label hoverLabel;
        private Label nameLabel;
        private Label sizeLabel;
        private Label accessLabel;
        private Label descriptionLabel;
        private Label efficientGoodsLabel;
        private Label inefficientGoodsLabel;
        private Label noShipLabel;
        private VisualElement shipList;
        private Button closeButton;
        private Label populationLabel;
        private VisualElement marketElement;
        private VisualElement marketRows;
        private Label marketHoldLabel;
        private VisualElement buildingsElement;
        private VisualElement buildingsRows;
        private Label buildingsCostsLabel;
        private Button marketTabButton;
        private Button buildingsTabButton;
        private Button shipsTabButton;
        private VisualElement marketPage;
        private VisualElement buildingsPage;
        private VisualElement shipsPage;
        private DropdownField tradingShipField;


        // The city the panel shows, or null while it is closed.
        private CityDefinition shownCity;

        // Made on the first city shown: the world's economy starts after this panel is enabled.
        private CityMarketSection marketSection;
        private CityBuildingsSection buildingsSection;

        // The city the buildings section shows, for the notifications.
        private CityDefinition buildingsCity;

        // The market whose population the panel shows and follows, or null.
        private CityMarket populationMarket;

        private bool isBound;

        // Ship of each row of the ship list, in the same order.
        private readonly List<Ship> listedShips = new List<Ship>();

        // The player's ships in the shown port, in the order of the drop-down list.
        private readonly List<Ship> tradingShips = new List<Ship>();

        // The player's ship whose hold trades in the shown port, or null.
        private Ship tradingShip;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || mapView == null || mapCamera == null)
            {
                Debug.LogError("CityInfoPanelController needs a UIDocument with a source asset, a map view and a camera.", this);
                return;
            }

            cityPanel = root.Q<VisualElement>("city-panel");
            hoverLabel = root.Q<Label>("hover-label");
            nameLabel = root.Q<Label>("city-name");
            sizeLabel = root.Q<Label>("city-size");
            accessLabel = root.Q<Label>("city-access");
            descriptionLabel = root.Q<Label>("city-description");
            efficientGoodsLabel = root.Q<Label>("efficient-goods");
            inefficientGoodsLabel = root.Q<Label>("inefficient-goods");
            noShipLabel = root.Q<Label>("no-ship-label");
            shipList = root.Q<VisualElement>("ship-list");
            closeButton = root.Q<Button>("close-button");
            populationLabel = root.Q<Label>("city-population");
            marketElement = root.Q<VisualElement>("market-section");
            marketRows = root.Q<VisualElement>("market-rows");
            marketHoldLabel = root.Q<Label>("market-hold");
            buildingsElement = root.Q<VisualElement>("buildings-section");
            buildingsRows = root.Q<VisualElement>("buildings-rows");
            buildingsCostsLabel = root.Q<Label>("buildings-costs");
            marketTabButton = root.Q<Button>("tab-market");
            buildingsTabButton = root.Q<Button>("tab-buildings");
            shipsTabButton = root.Q<Button>("tab-ships");
            marketPage = root.Q<VisualElement>("market-page");
            buildingsPage = root.Q<VisualElement>("buildings-page");
            shipsPage = root.Q<VisualElement>("ships-page");
            tradingShipField = root.Q<DropdownField>("trading-ship");

            // A document that is enabled again has a new tree: the sections are rebuilt in it.
            marketSection = null;
            buildingsSection = null;

            if (cityPanel == null || hoverLabel == null || nameLabel == null || sizeLabel == null
                || accessLabel == null || descriptionLabel == null || noShipLabel == null
                || efficientGoodsLabel == null || inefficientGoodsLabel == null
                || shipList == null || closeButton == null || populationLabel == null
                || marketElement == null || marketRows == null || marketHoldLabel == null
                || buildingsElement == null || buildingsRows == null || buildingsCostsLabel == null
                || marketTabButton == null || buildingsTabButton == null || shipsTabButton == null
                || marketPage == null || buildingsPage == null || shipsPage == null
                || tradingShipField == null)
            {
                Debug.LogError("CityInfoPanel.uxml is missing an expected element.", this);
                return;
            }

            // Only the panel itself should block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // The buttons are clicked, not navigated: the keyboard pans the map.
            closeButton.focusable = false;
            marketTabButton.focusable = false;
            buildingsTabButton.focusable = false;
            shipsTabButton.focusable = false;
            tradingShipField.focusable = false;

            closeButton.clicked += OnCloseClicked;
            marketTabButton.clicked += OnMarketTabClicked;
            buildingsTabButton.clicked += OnBuildingsTabClicked;
            shipsTabButton.clicked += OnShipsTabClicked;
            tradingShipField.RegisterValueChangedCallback(OnTradingShipPicked);
            mapView.Selection.HoveredChanged += ShowHovered;
            mapView.Selection.SelectedChanged += ShowSelected;

            if (shipsView != null)
            {
                shipsView.Docking.Docked += OnDockingChanged;
                shipsView.Docking.Undocked += OnDockingChanged;
                shipsView.Selection.SelectedChanged += OnShipSelected;
            }

            // Hidden until a market is shown, also in a scene without an economy.
            marketElement.style.display = DisplayStyle.None;
            buildingsElement.style.display = DisplayStyle.None;

            isBound = true;

            ShowHovered(mapView.Selection.Hovered);
            ShowSelected(mapView.Selection.Selected);
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            closeButton.clicked -= OnCloseClicked;
            marketTabButton.clicked -= OnMarketTabClicked;
            buildingsTabButton.clicked -= OnBuildingsTabClicked;
            shipsTabButton.clicked -= OnShipsTabClicked;
            tradingShipField.UnregisterValueChangedCallback(OnTradingShipPicked);
            mapView.Selection.HoveredChanged -= ShowHovered;
            mapView.Selection.SelectedChanged -= ShowSelected;

            if (shipsView != null)
            {
                shipsView.Docking.Docked -= OnDockingChanged;
                shipsView.Docking.Undocked -= OnDockingChanged;
                shipsView.Selection.SelectedChanged -= OnShipSelected;
            }

            // Stops listening to the market, the hold and the treasury.
            marketSection?.Hide();
            buildingsSection?.Hide();
            FollowPopulationOf(null);
            shownCity = null;

            isBound = false;
        }

        private void LateUpdate()
        {
            if (!isBound || !mapView.IsReady)
            {
                return;
            }

            CityDefinition hovered = mapView.Selection.Hovered;

            if (hovered == null || root.panel == null)
            {
                return;
            }

            Vector2 panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(
                root.panel, mapView.GetWorldPosition(hovered), mapCamera);

            hoverLabel.style.left = panelPosition.x + hoverLabelOffset.x;
            hoverLabel.style.top = panelPosition.y + hoverLabelOffset.y;
        }

        /// <param name="screenPosition">Screen position with the origin at the bottom-left.</param>
        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            if (!isBound || root.panel == null)
            {
                return false;
            }

            // Panel coordinates have their origin at the top-left.
            Vector2 flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, flipped);
            return root.panel.Pick(panelPosition) != null;
        }

        /// <summary>What the panel shows of a city that has no market: its population is the market's.</summary>
        public const string NoPopulation = "Habitants : —";

        /// <summary>The population as the panel writes it: "Habitants : 1 008 · Mendiants : 59".</summary>
        public static string FormatPopulation(int housed, int beggars)
        {
            return $"Habitants : {TreasuryHudController.FormatNumber(housed)} · Mendiants : {TreasuryHudController.FormatNumber(beggars)}";
        }

        /// <summary>"Atelier construit à Lübeck : céréales."</summary>
        public static string WorkshopBuiltText(string city, string good)
        {
            return $"Atelier construit à {city} : {good.ToLowerInvariant()}.";
        }

        /// <summary>What the market's drop-down list reads when no ship of the player lies in the port.</summary>
        public const string NoTradingShip = "Aucun navire";

        /// <summary>
        /// The names of the drop-down list, one per ship in the order given. A ship that shares
        /// its name with one before it is numbered, "Navire marchand (2)": the list tells its
        /// items apart by their text.
        /// </summary>
        public static List<string> TradingShipLabels(IReadOnlyList<string> shipNames)
        {
            var labels = new List<string>();
            var counts = new Dictionary<string, int>();

            foreach (string name in shipNames)
            {
                counts.TryGetValue(name, out int count);
                counts[name] = ++count;
                labels.Add(count > 1 ? $"{name} ({count})" : name);
            }

            return labels;
        }

        /// <summary>The title of the ships tab: "Navires au port (3)".</summary>
        public static string ShipsTabText(int shipCount)
        {
            return $"Navires au port ({TreasuryHudController.FormatNumber(shipCount)})";
        }

        /// <summary>"Logement construit à Lübeck."</summary>
        public static string HouseBuiltText(string city)
        {
            return $"Logement construit à {city}.";
        }

        private void ShowHovered(CityDefinition city)
        {
            if (city == null)
            {
                hoverLabel.style.display = DisplayStyle.None;
                return;
            }

            hoverLabel.text = city.DisplayName;
            hoverLabel.style.display = DisplayStyle.Flex;
        }

        private void ShowSelected(CityDefinition city)
        {
            if (city == null)
            {
                cityPanel.style.display = DisplayStyle.None;
                marketSection?.Hide();
                buildingsSection?.Hide();
                FollowPopulationOf(null);
                shownCity = null;
                return;
            }

            // The panel opens on the market; from one city to the next, the open tab stays.
            if (shownCity == null)
            {
                ShowTab(Tab.Market);
            }

            shownCity = city;
            nameLabel.text = city.DisplayName;
            sizeLabel.text = SizeText(city.Size);
            accessLabel.text = AccessText(city.Access);
            ShowPopulationOf(city);
            descriptionLabel.text = city.Description;
            ShowProducedGoodsOf(city);
            // A new city: its first ship of the player trades.
            ShowShipsIn(city, null);
            ShowMarketOf(city);
            cityPanel.style.display = DisplayStyle.Flex;
        }

        // The inhabitants of the game are in the market; a city without one has none to show.
        private void ShowPopulationOf(CityDefinition city)
        {
            FollowPopulationOf(worldEconomy != null ? worldEconomy.MarketOf(city) : null);

            if (populationMarket != null)
            {
                WritePopulation();
            }
            else
            {
                populationLabel.text = NoPopulation;
            }
        }

        private void FollowPopulationOf(CityMarket market)
        {
            if (ReferenceEquals(market, populationMarket))
            {
                return;
            }

            if (populationMarket != null)
            {
                populationMarket.Changed -= WritePopulation;
            }

            populationMarket = market;

            if (populationMarket != null)
            {
                populationMarket.Changed += WritePopulation;
            }
        }

        // Also on a trade, which changes no inhabitant: a market has one event.
        private void WritePopulation()
        {
            // Another listener of the same change may have closed the panel.
            if (populationMarket == null)
            {
                return;
            }

            populationLabel.text = FormatPopulation(populationMarket.Population.Housed, populationMarket.Population.Beggars);
        }

        /// <param name="keptTradingShip">The ship that trades, kept while it is still in port and the player's.</param>
        private void ShowShipsIn(CityDefinition city, Ship keptTradingShip)
        {
            shipList.Clear();
            listedShips.Clear();

            if (shipsView != null)
            {
                shipsView.Docking.GetShipsIn(city, listedShips);
            }

            foreach (Ship ship in listedShips)
            {
                // Clicking the selected ship again deselects it.
                var row = new Button(() => shipsView.Selection.Select(
                    ReferenceEquals(shipsView.Selection.Selected, ship) ? null : ship));
                row.text = shipsView.DisplayNameOf(ship);

                // Rows are clicked, not navigated: the keyboard pans the map.
                row.focusable = false;
                row.AddToClassList(ShipRowClass);
                shipList.Add(row);
            }

            noShipLabel.style.display = listedShips.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            shipsTabButton.text = ShipsTabText(listedShips.Count);
            HighlightSelectedShip();
            ChooseTradingShip(keptTradingShip);
        }

        // After the ship list: the ships that can trade are among the ships listed.
        private void ChooseTradingShip(Ship current)
        {
            if (shipsView != null)
            {
                TradingShipChoice.PlayerShipsAmong(listedShips, shipsView.IsPlayerShip, tradingShips);
                tradingShip = TradingShipChoice.Choose(listedShips, shipsView.IsPlayerShip, current);
            }
            else
            {
                tradingShips.Clear();
                tradingShip = null;
            }

            var names = new List<string>();

            foreach (Ship ship in tradingShips)
            {
                names.Add(shipsView.DisplayNameOf(ship));
            }

            names = TradingShipLabels(names);

            if (names.Count == 0)
            {
                names.Add(NoTradingShip);
            }

            tradingShipField.choices = names;
            tradingShipField.SetValueWithoutNotify(names[Mathf.Max(0, tradingShips.IndexOf(tradingShip))]);
            tradingShipField.SetEnabled(tradingShips.Count > 0);
        }

        private void OnTradingShipPicked(ChangeEvent<string> change)
        {
            int index = tradingShipField.index;

            if (index < 0 || index >= tradingShips.Count || mapView.Selection.Selected == null)
            {
                return;
            }

            tradingShip = tradingShips[index];
            ShowMarketOf(mapView.Selection.Selected);
        }

        // Rows are restyled, not rebuilt: a selection comes from a click on one of them.
        private void HighlightSelectedShip()
        {
            Ship selected = shipsView != null ? shipsView.Selection.Selected : null;

            for (int i = 0; i < listedShips.Count; i++)
            {
                shipList[i].EnableInClassList(SelectedShipRowClass, ReferenceEquals(listedShips[i], selected));
            }
        }

        private void OnDockingChanged(Ship ship, CityDefinition city)
        {
            if (ReferenceEquals(city, mapView.Selection.Selected))
            {
                ShowShipsIn(city, tradingShip);
                ShowMarketOf(city);
            }
        }

        private void OnShipSelected(Ship ship)
        {
            HighlightSelectedShip();

            // A ship of the player picked in the ships tab also becomes the one that trades;
            // any other selection leaves the market as it is.
            if (mapView.Selection.Selected != null && shipsView.IsPlayerShip(ship) && listedShips.Contains(ship))
            {
                ChooseTradingShip(ship);
                ShowMarketOf(mapView.Selection.Selected);
            }
        }

        // After ShowShipsIn: the ship that trades is one of the ships listed.
        private void ShowMarketOf(CityDefinition city)
        {
            if (!TryGetMarketSection(out CityMarketSection section))
            {
                return;
            }

            CityMarket market = worldEconomy.MarketOf(city);

            if (market == null)
            {
                section.Hide();
                buildingsSection?.Hide();
                return;
            }

            CargoHold hold = tradingShip?.Cargo;
            section.Show(market, hold);

            buildingsCity = city;

            if (TryGetBuildingsSection(out CityBuildingsSection buildings))
            {
                buildings.Show(market, hold);
            }
        }

        private bool TryGetBuildingsSection(out CityBuildingsSection section)
        {
            if (buildingsSection == null && worldEconomy != null && worldEconomy.IsReady)
            {
                BuildingsDefinition buildings = worldEconomy.Buildings;
                EconomyDefinition economy = worldEconomy.Economy;
                var goodNames = new List<string>();

                foreach (GoodDefinition good in economy.Goods)
                {
                    goodNames.Add(good.DisplayName);
                }

                // A building whose cost WorldEconomy could not work out has a null cost: its
                // buttons stay disabled. WorldEconomy has warned about it once.
                Treasury treasury = playerTreasury != null && playerTreasury.IsReady ? playerTreasury.Treasury : null;
                buildingsSection = new CityBuildingsSection(
                    buildingsElement, buildingsRows, buildingsCostsLabel, goodNames,
                    worldEconomy.HouseCost, buildings.HouseRoom, worldEconomy.WorkshopCost, buildings.WorkshopJobs,
                    buildings.CostsText(economy), treasury);
                buildingsSection.HouseBuilt += OnHouseBuilt;
                buildingsSection.WorkshopBuilt += OnWorkshopBuilt;
            }

            section = buildingsSection;
            return section != null;
        }

        private void OnHouseBuilt()
        {
            if (buildingsCity != null && playerNotifications != null && playerNotifications.IsReady)
            {
                playerNotifications.Feed.Post(HouseBuiltText(buildingsCity.DisplayName));
            }
        }

        private void OnWorkshopBuilt(int good)
        {
            if (buildingsCity != null && playerNotifications != null && playerNotifications.IsReady)
            {
                playerNotifications.Feed.Post(WorkshopBuiltText(
                    buildingsCity.DisplayName, worldEconomy.Economy.Goods[good].DisplayName));
            }
        }

        private bool TryGetMarketSection(out CityMarketSection section)
        {
            if (marketSection == null && worldEconomy != null && worldEconomy.IsReady)
            {
                var goodNames = new List<string>();
                var goodIcons = new List<Sprite>();

                foreach (GoodDefinition good in worldEconomy.Economy.Goods)
                {
                    goodNames.Add(good.DisplayName);
                    goodIcons.Add(good.Icon);
                }

                Treasury treasury = playerTreasury != null && playerTreasury.IsReady ? playerTreasury.Treasury : null;
                marketSection = new CityMarketSection(
                    marketElement, marketRows, marketHoldLabel, goodNames, treasury, goodIcons);
            }

            section = marketSection;
            return section != null;
        }

        private void OnCloseClicked()
        {
            mapView.Selection.ClearSelection();
        }

        private void OnMarketTabClicked()
        {
            ShowTab(Tab.Market);
        }

        private void OnBuildingsTabClicked()
        {
            ShowTab(Tab.Buildings);
        }

        private void OnShipsTabClicked()
        {
            ShowTab(Tab.Ships);
        }

        // A page is shown or hidden by its tab; the section in it is shown or hidden by itself.
        private void ShowTab(Tab tab)
        {
            ShowPage(marketPage, marketTabButton, tab == Tab.Market);
            ShowPage(buildingsPage, buildingsTabButton, tab == Tab.Buildings);
            ShowPage(shipsPage, shipsTabButton, tab == Tab.Ships);
        }

        private static void ShowPage(VisualElement page, Button tabButton, bool isOpen)
        {
            page.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            tabButton.EnableInClassList(OpenTabClass, isOpen);
        }

        /// <summary>What a production section of the panel shows when the city produces nothing that way.</summary>
        public const string NoProducedGoods = "Aucune";

        /// <summary>
        /// The goods the city produces in this way, named in the order of the goods given and
        /// separated by commas, or <see cref="NoProducedGoods"/>. A good that is not given (an
        /// inactive good, which is not among the economy's goods) is left out.
        /// </summary>
        public static string ProducedGoodsText(CityDefinition city, IReadOnlyList<GoodDefinition> goods, GoodProduction production)
        {
            var names = new List<string>();

            foreach (GoodDefinition good in goods)
            {
                if (good != null && city.ProductionOf(good) == production)
                {
                    names.Add(good.DisplayName);
                }
            }

            return names.Count > 0 ? string.Join(", ", names) : NoProducedGoods;
        }

        private void ShowProducedGoodsOf(CityDefinition city)
        {
            IReadOnlyList<GoodDefinition> goods = worldEconomy != null && worldEconomy.Economy != null
                ? worldEconomy.Economy.Goods
                : ProducedGoodsListedBy(city);

            WriteProducedGoods(efficientGoodsLabel, ProducedGoodsText(city, goods, GoodProduction.Efficient));
            WriteProducedGoods(inefficientGoodsLabel, ProducedGoodsText(city, goods, GoodProduction.Inefficient));
        }

        // Without an economy, the city's own lists say what it produces, in their order.
        private static List<GoodDefinition> ProducedGoodsListedBy(CityDefinition city)
        {
            var goods = new List<GoodDefinition>(city.EfficientGoods);

            foreach (GoodDefinition good in city.InefficientGoods)
            {
                // A good listed both ways is efficient: it is named once.
                if (!goods.Contains(good))
                {
                    goods.Add(good);
                }
            }

            return goods;
        }

        private static void WriteProducedGoods(Label label, string text)
        {
            bool none = text == NoProducedGoods;
            label.text = text;
            label.EnableInClassList(ProducedGoodsClass, !none);
            label.EnableInClassList(EmptyClass, none);
        }

        private static string SizeText(CitySize size)
        {
            switch (size)
            {
                case CitySize.Capital:
                    return "Capitale";
                case CitySize.Town:
                    return "Ville";
                default:
                    return "Village";
            }
        }

        private static string AccessText(CityAccess access)
        {
            return access == CityAccess.River ? "Port fluvial" : "Port maritime";
        }
    }
}
