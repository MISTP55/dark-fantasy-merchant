using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Presents the hovered city's name and the selected city's information panel: the city
    /// on the left, and on the right its market and the ships in the city (waiting,
    /// crossing its harbour or in port) in their tabs, and a button that asks for the city view. The market trades with the player's ship, or the player's
    /// trading post, chosen in its drop-down list; clicking a ship in the ships tab selects it, so that it can be ordered out.
    /// While the city has the player's trading post, a third tab moves goods between it and a ship of the player in port.
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
            Transfer,
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
        private Button viewCityButton;
        private Label populationLabel;
        private VisualElement marketElement;
        private VisualElement marketRows;
        private Label marketHoldLabel;
        private Label marketStoreHeader;
        private Button marketTabButton;
        private Button shipsTabButton;
        private VisualElement marketPage;
        private VisualElement shipsPage;
        private DropdownField tradingShipField;
        private Button transferTabButton;
        private VisualElement transferPage;
        private VisualElement transferElement;
        private VisualElement transferRows;
        private Label transferHoldHeader;
        private Label noTransferShipLabel;
        private DropdownField transferShipField;

        /// <summary>
        /// Raised by every purchase and sale of the market tab: the city, the good, the units, the gold
        /// and what was done. For the debug tools.
        /// </summary>
        public event Action<CityDefinition, int, int, long, PlayerTradeKind> PlayerTraded;

        /// <summary>Raised by the "Voir la ville" button, with the city the panel shows.</summary>
        public event Action<CityDefinition> ViewCityRequested;

        // The city the panel shows, or null while it is closed.
        private CityDefinition shownCity;

        // Made on the first city shown: the world's economy starts after this panel is enabled.
        private CityMarketSection marketSection;

        // Made with the first trading post shown, for the same reason.
        private TradingPostTransferSection transferSection;

        private Tab openTab;

        // The market whose population the panel shows and follows, or null.
        private CityMarket populationMarket;

        private bool isBound;

        // Ship of each row of the ship list, in the same order.
        private readonly List<Ship> listedShips = new List<Ship>();

        // The ships in port in the shown city: only they can trade.
        private readonly List<Ship> dockedShips = new List<Ship>();

        // The player's ships in the shown port, in the order of the drop-down list.
        private readonly List<Ship> tradingShips = new List<Ship>();

        // The player's ship whose hold trades in the shown port, or null.
        private Ship tradingShip;

        // The player's trading post in the shown city, or null.
        private Warehouse tradingPost;

        // True while the market trades with the trading post rather than with a ship.
        private bool tradesWithTradingPost;

        // The player's ship in the shown port whose hold the transfer tab shows, or null.
        private Ship transferShip;

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
            viewCityButton = root.Q<Button>("view-city-button");
            populationLabel = root.Q<Label>("city-population");
            marketElement = root.Q<VisualElement>("market-section");
            marketRows = root.Q<VisualElement>("market-rows");
            marketHoldLabel = root.Q<Label>("market-hold");
            marketStoreHeader = root.Q<Label>("market-store-header");
            marketTabButton = root.Q<Button>("tab-market");
            shipsTabButton = root.Q<Button>("tab-ships");
            marketPage = root.Q<VisualElement>("market-page");
            shipsPage = root.Q<VisualElement>("ships-page");
            tradingShipField = root.Q<DropdownField>("trading-ship");
            transferTabButton = root.Q<Button>("tab-transfer");
            transferPage = root.Q<VisualElement>("transfer-page");
            transferElement = root.Q<VisualElement>("transfer-section");
            transferRows = root.Q<VisualElement>("transfer-rows");
            transferHoldHeader = root.Q<Label>("transfer-hold-header");
            noTransferShipLabel = root.Q<Label>("no-transfer-ship");
            transferShipField = root.Q<DropdownField>("transfer-ship");

            // A document that is enabled again has a new tree: the sections are rebuilt in it.
            marketSection = null;
            transferSection = null;

            if (cityPanel == null || hoverLabel == null || nameLabel == null || sizeLabel == null
                || accessLabel == null || descriptionLabel == null || noShipLabel == null
                || efficientGoodsLabel == null || inefficientGoodsLabel == null
                || shipList == null || closeButton == null || populationLabel == null
                || marketElement == null || marketRows == null || marketHoldLabel == null || marketStoreHeader == null
                || viewCityButton == null || marketTabButton == null || shipsTabButton == null
                || marketPage == null || shipsPage == null
                || tradingShipField == null || transferTabButton == null || transferPage == null
                || transferElement == null || transferRows == null || transferHoldHeader == null
                || noTransferShipLabel == null || transferShipField == null)
            {
                Debug.LogError("CityInfoPanel.uxml is missing an expected element.", this);
                return;
            }

            // Only the panel itself should block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // The buttons are clicked, not navigated: the keyboard pans the map.
            closeButton.focusable = false;
            viewCityButton.focusable = false;
            marketTabButton.focusable = false;
            shipsTabButton.focusable = false;
            tradingShipField.focusable = false;
            transferTabButton.focusable = false;
            transferShipField.focusable = false;

            closeButton.clicked += OnCloseClicked;
            viewCityButton.clicked += OnViewCityClicked;
            marketTabButton.clicked += OnMarketTabClicked;
            shipsTabButton.clicked += OnShipsTabClicked;
            tradingShipField.RegisterValueChangedCallback(OnTradingShipPicked);
            transferTabButton.clicked += OnTransferTabClicked;
            transferShipField.RegisterValueChangedCallback(OnTransferShipPicked);
            mapView.Selection.HoveredChanged += ShowHovered;
            mapView.Selection.SelectedChanged += ShowSelected;

            if (shipsView != null)
            {
                shipsView.Docking.CityStateChanged += OnCityStateChanged;
                shipsView.Selection.SelectedChanged += OnShipSelected;
            }

            if (worldEconomy != null)
            {
                worldEconomy.TradingPostOpened += OnTradingPostOpened;
            }

            // Hidden until a market is shown, also in a scene without an economy.
            marketElement.style.display = DisplayStyle.None;

            // Hidden until a trading post is shown.
            transferElement.style.display = DisplayStyle.None;
            transferTabButton.style.display = DisplayStyle.None;

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
            viewCityButton.clicked -= OnViewCityClicked;
            marketTabButton.clicked -= OnMarketTabClicked;
            shipsTabButton.clicked -= OnShipsTabClicked;
            tradingShipField.UnregisterValueChangedCallback(OnTradingShipPicked);
            transferTabButton.clicked -= OnTransferTabClicked;
            transferShipField.UnregisterValueChangedCallback(OnTransferShipPicked);
            mapView.Selection.HoveredChanged -= ShowHovered;
            mapView.Selection.SelectedChanged -= ShowSelected;

            if (shipsView != null)
            {
                shipsView.Docking.CityStateChanged -= OnCityStateChanged;
                shipsView.Selection.SelectedChanged -= OnShipSelected;
            }

            if (worldEconomy != null)
            {
                worldEconomy.TradingPostOpened -= OnTradingPostOpened;
            }

            // Stops listening to the market, the holds, the trading post and the treasury.
            marketSection?.Hide();
            transferSection?.Hide();
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

        /// <summary>What the panel shows of a city that has no market: its population is the market's.</summary>
        public const string NoPopulation = "Habitants : —";

        /// <summary>
        /// The population as the panel writes it, class by class from the top:
        /// "Riches : 110 · Aisés : 330 · Pauvres : 660 · Mendiants : 64".
        /// </summary>
        public static string FormatPopulation(int rich, int wealthy, int poor, int beggars)
        {
            // No-break spaces but after the dots: a line that wraps breaks only there.
            return $"Riches\u00A0:\u00A0{NumberText.Format(rich)}\u00A0· Aisés\u00A0:\u00A0{NumberText.Format(wealthy)}\u00A0· "
                + $"Pauvres\u00A0:\u00A0{NumberText.Format(poor)}\u00A0· Mendiants\u00A0:\u00A0{NumberText.Format(beggars)}";
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

        /// <summary>The item of the market's drop-down list, after the ships, that trades with the player's trading post.</summary>
        public const string TradingPostChoice = "Comptoir";

        /// <summary>
        /// The items of the market's drop-down list: the ships as <see cref="TradingShipLabels"/>
        /// names them, then <see cref="TradingPostChoice"/> when the player has a trading post in the
        /// city; <see cref="NoTradingShip"/> alone when there is neither.
        /// </summary>
        public static List<string> TradingPartnerLabels(IReadOnlyList<string> shipNames, bool hasTradingPost)
        {
            List<string> labels = TradingShipLabels(shipNames);

            if (hasTradingPost)
            {
                labels.Add(TradingPostChoice);
            }

            if (labels.Count == 0)
            {
                labels.Add(NoTradingShip);
            }

            return labels;
        }

        /// <summary>The title of the ships tab, which counts every ship in the city: "Navires (3)".</summary>
        public static string ShipsTabText(int shipCount)
        {
            return $"Navires ({NumberText.Format(shipCount)})";
        }

        /// <summary>Where a ship of the ships tab stands in the city: "à quai", "en attente", "arrive", "repart".</summary>
        public static string ShipStateText(HarbourState state)
        {
            switch (state)
            {
                case HarbourState.Waiting:
                    return "en attente";
                case HarbourState.Arriving:
                    return "arrive";
                case HarbourState.Leaving:
                    return "repart";
                default:
                    return "à quai";
            }
        }

        /// <summary>A row of the ships tab: "Aurore — à quai".</summary>
        public static string ShipRowText(string name, HarbourState state)
        {
            return $"{name} — {ShipStateText(state)}";
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
                transferSection?.Hide();
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
            // A new city: its first ship of the player trades, or its trading post without one.
            ShowShipsIn(city, null, false);
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

            CityPopulation population = populationMarket.Population;
            populationLabel.text = FormatPopulation(
                population.CountOf(SocialClass.Rich),
                population.CountOf(SocialClass.Wealthy),
                population.CountOf(SocialClass.Poor),
                population.Beggars);
        }

        /// <param name="keptTradingShip">The ship that trades, kept while it is still in port and the player's.</param>
        /// <param name="keepTradingPost">True to keep trading with the trading post while the city has one.</param>
        private void ShowShipsIn(CityDefinition city, Ship keptTradingShip, bool keepTradingPost)
        {
            shipList.Clear();
            listedShips.Clear();
            dockedShips.Clear();

            if (shipsView != null)
            {
                shipsView.Docking.GetShipsInCity(city, listedShips);
                NameOrder.Sort(listedShips, shipsView.DisplayNameOf);
                shipsView.Docking.GetShipsIn(city, dockedShips);
                NameOrder.Sort(dockedShips, shipsView.DisplayNameOf);
            }

            foreach (Ship ship in listedShips)
            {
                // Clicking the selected ship again deselects it.
                var row = new Button(() => shipsView.Selection.Select(
                    ReferenceEquals(shipsView.Selection.Selected, ship) ? null : ship));
                row.text = ShipRowText(shipsView.DisplayNameOf(ship), shipsView.Docking.StateOf(ship));

                // Rows are clicked, not navigated: the keyboard pans the map.
                row.focusable = false;
                row.AddToClassList(ShipRowClass);
                shipList.Add(row);
            }

            ListStripes.Apply(shipList);

            noShipLabel.style.display = listedShips.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            shipsTabButton.text = ShipsTabText(listedShips.Count);
            HighlightSelectedShip();
            ChooseTradingPartner(city, keptTradingShip, keepTradingPost);
        }

        // After the ship list: the ships that can trade are among the ships in port. The partner
        // kept while it is still there, otherwise the first ship, otherwise the trading post.
        private void ChooseTradingPartner(CityDefinition city, Ship currentShip, bool keepTradingPost)
        {
            tradingPost = worldEconomy != null ? worldEconomy.TradingPostOf(city) : null;

            if (shipsView != null)
            {
                TradingShipChoice.PlayerShipsAmong(dockedShips, shipsView.IsPlayerShip, tradingShips);
                tradingShip = TradingShipChoice.Choose(dockedShips, shipsView.IsPlayerShip, currentShip);
            }
            else
            {
                tradingShips.Clear();
                tradingShip = null;
            }

            tradesWithTradingPost = tradingPost != null && (keepTradingPost || tradingShip == null);

            if (tradesWithTradingPost)
            {
                tradingShip = null;
            }

            var names = new List<string>();

            foreach (Ship ship in tradingShips)
            {
                names.Add(shipsView.DisplayNameOf(ship));
            }

            names = TradingPartnerLabels(names, tradingPost != null);

            int chosen = tradesWithTradingPost ? tradingShips.Count : Mathf.Max(0, tradingShips.IndexOf(tradingShip));
            tradingShipField.choices = names;
            tradingShipField.SetValueWithoutNotify(names[chosen]);
            tradingShipField.SetEnabled(tradingShips.Count > 0 || tradingPost != null);

            ChooseTransferShip();
            ShowTransfer();
        }

        // After the trading partner: the transfer ship is one of the same ships, the one kept
        // while it is still there, otherwise the first. Chosen apart from the market's.
        private void ChooseTransferShip()
        {
            transferShip = shipsView != null
                ? TradingShipChoice.Choose(dockedShips, shipsView.IsPlayerShip, transferShip)
                : null;

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

            int chosen = Mathf.Max(0, tradingShips.IndexOf(transferShip));
            transferShipField.choices = names;
            transferShipField.SetValueWithoutNotify(names[chosen]);
            transferShipField.SetEnabled(tradingShips.Count > 0);
        }

        // The tab exists only while the city has the player's trading post; open when it goes,
        // the panel falls back on the market.
        private void ShowTransfer()
        {
            bool hasTradingPost = tradingPost != null;
            transferTabButton.style.display = hasTradingPost ? DisplayStyle.Flex : DisplayStyle.None;

            if (!hasTradingPost && openTab == Tab.Transfer)
            {
                ShowTab(Tab.Market);
            }

            if (!hasTradingPost || !TryGetTransferSection(out TradingPostTransferSection section))
            {
                transferSection?.Hide();
                return;
            }

            noTransferShipLabel.style.display = transferShip == null ? DisplayStyle.Flex : DisplayStyle.None;
            section.Show(transferShip?.Cargo, transferShipField.value, tradingPost);
        }

        private void OnTransferShipPicked(ChangeEvent<string> change)
        {
            int index = transferShipField.index;

            if (index < 0 || index >= tradingShips.Count || mapView.Selection.Selected == null)
            {
                return;
            }

            transferShip = tradingShips[index];
            ShowTransfer();
        }

        private void OnTradingShipPicked(ChangeEvent<string> change)
        {
            int index = tradingShipField.index;
            bool isTradingPost = tradingPost != null && index == tradingShips.Count;

            if (index < 0 || (index >= tradingShips.Count && !isTradingPost) || mapView.Selection.Selected == null)
            {
                return;
            }

            tradesWithTradingPost = isTradingPost;
            tradingShip = isTradingPost ? null : tradingShips[index];
            ShowMarketOf(mapView.Selection.Selected);
        }

        private void OnTradingPostOpened(CityDefinition city)
        {
            if (shownCity != null && ReferenceEquals(city, shownCity))
            {
                ChooseTradingPartner(city, tradingShip, tradesWithTradingPost);
                ShowMarketOf(city);
            }
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

        private void OnCityStateChanged(Ship ship, CityDefinition city)
        {
            if (ReferenceEquals(city, mapView.Selection.Selected))
            {
                ShowShipsIn(city, tradingShip, tradesWithTradingPost);
                ShowMarketOf(city);
            }
        }

        private void OnShipSelected(Ship ship)
        {
            HighlightSelectedShip();

            // A ship of the player picked in the ships tab also becomes the one that trades;
            // any other selection leaves the market as it is.
            if (mapView.Selection.Selected != null && shipsView.IsPlayerShip(ship) && dockedShips.Contains(ship))
            {
                ChooseTradingPartner(mapView.Selection.Selected, ship, false);
                ShowMarketOf(mapView.Selection.Selected);
            }
        }

        // After ShowShipsIn: what trades is one of the ships listed, or the trading post.
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
                return;
            }

            if (tradesWithTradingPost)
            {
                section.Show(market, tradingPost, TradingPostChoice);
            }
            else
            {
                section.Show(market, tradingShip?.Cargo, CityMarketSection.HoldHeader);
            }
        }

        private void OnTraded(int good, TradeResult result, PlayerTradeKind kind)
        {
            if (shownCity != null)
            {
                PlayerTraded?.Invoke(shownCity, good, result.Units, result.Gold, kind);
            }
        }

        private bool TryGetMarketSection(out CityMarketSection section)
        {
            if (marketSection == null && worldEconomy != null && worldEconomy.IsReady)
            {
                GetGoods(out List<string> goodNames, out List<Sprite> goodIcons);
                Treasury treasury = playerTreasury != null && playerTreasury.IsReady ? playerTreasury.Treasury : null;
                marketSection = new CityMarketSection(
                    marketElement, marketRows, marketHoldLabel, goodNames, treasury, goodIcons,
                    marketStoreHeader, worldEconomy.Buildings.TradingPost.RentPerBarrel);
                marketSection.Traded += OnTraded;
            }

            section = marketSection;
            return section != null;
        }

        private bool TryGetTransferSection(out TradingPostTransferSection section)
        {
            if (transferSection == null && worldEconomy != null && worldEconomy.IsReady)
            {
                GetGoods(out List<string> goodNames, out List<Sprite> goodIcons);
                transferSection = new TradingPostTransferSection(
                    transferElement, transferRows, transferHoldHeader, goodNames, goodIcons);
            }

            section = transferSection;
            return section != null;
        }

        // The names and icons of the economy's goods, by index.
        private void GetGoods(out List<string> goodNames, out List<Sprite> goodIcons)
        {
            goodNames = new List<string>();
            goodIcons = new List<Sprite>();

            foreach (GoodDefinition good in worldEconomy.Economy.Goods)
            {
                goodNames.Add(good.DisplayName);
                goodIcons.Add(good.Icon);
            }
        }

        private void OnCloseClicked()
        {
            mapView.Selection.ClearSelection();
        }

        private void OnMarketTabClicked()
        {
            ShowTab(Tab.Market);
        }

        private void OnViewCityClicked()
        {
            if (shownCity != null)
            {
                ViewCityRequested?.Invoke(shownCity);
            }
        }

        private void OnTransferTabClicked()
        {
            ShowTab(Tab.Transfer);
        }

        private void OnShipsTabClicked()
        {
            ShowTab(Tab.Ships);
        }

        // A page is shown or hidden by its tab; the section in it is shown or hidden by itself.
        private void ShowTab(Tab tab)
        {
            openTab = tab;
            ShowPage(marketPage, marketTabButton, tab == Tab.Market);
            ShowPage(transferPage, transferTabButton, tab == Tab.Transfer);
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
        /// The goods the city produces in this way, named in alphabetical order and
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

            NameOrder.Sort(names, name => name);

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
