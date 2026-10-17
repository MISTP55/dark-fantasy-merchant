using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Presents the hovered city's name and the selected city's information panel, with
    /// the ships in its port and its market: clicking a ship selects it, so that it can
    /// trade or be ordered out. Reads the map's selection state; knows nothing about markers.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CityInfoPanelController : MonoBehaviour
    {
        private const string ShipRowClass = "city-panel__ship";
        private const string SelectedShipRowClass = "city-panel__ship--selected";
        private const string ProducedGoodsClass = "city-panel__goods";
        private const string EmptyClass = "city-panel__empty";

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

            // A document that is enabled again has a new tree: the sections are rebuilt in it.
            marketSection = null;
            buildingsSection = null;

            if (cityPanel == null || hoverLabel == null || nameLabel == null || sizeLabel == null
                || accessLabel == null || descriptionLabel == null || noShipLabel == null
                || efficientGoodsLabel == null || inefficientGoodsLabel == null
                || shipList == null || closeButton == null || populationLabel == null
                || marketElement == null || marketRows == null || marketHoldLabel == null
                || buildingsElement == null || buildingsRows == null || buildingsCostsLabel == null)
            {
                Debug.LogError("CityInfoPanel.uxml is missing an expected element.", this);
                return;
            }

            // Only the panel itself should block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // The button is clicked, not navigated: the keyboard pans the map.
            closeButton.focusable = false;

            closeButton.clicked += OnCloseClicked;
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
            return $"Atelier construit à {city} : {good.ToLower()}.";
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
                return;
            }

            nameLabel.text = city.DisplayName;
            sizeLabel.text = SizeText(city.Size);
            accessLabel.text = AccessText(city.Access);
            ShowPopulationOf(city);
            descriptionLabel.text = city.Description;
            ShowProducedGoodsOf(city);
            ShowShipsIn(city);
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

        private void ShowShipsIn(CityDefinition city)
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
            HighlightSelectedShip();
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
                ShowShipsIn(city);
                ShowMarketOf(city);
            }
        }

        private void OnShipSelected(Ship ship)
        {
            HighlightSelectedShip();

            // The hold that trades is the selected ship's.
            if (mapView.Selection.Selected != null)
            {
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

            CargoHold hold = HoldOfSelectedShipInPort();
            section.Show(market, hold);

            buildingsCity = city;

            if (TryGetBuildingsSection(out CityBuildingsSection buildings))
            {
                buildings.Show(market, hold);
            }
        }

        private bool TryGetBuildingsSection(out CityBuildingsSection section)
        {
            if (buildingsSection == null && worldEconomy != null && worldEconomy.IsReady
                && worldEconomy.Buildings != null)
            {
                BuildingsDefinition buildings = worldEconomy.Buildings;
                EconomyDefinition economy = worldEconomy.Economy;

                // WorldEconomy has validated them: this only fails on a definition changed since.
                if (buildings.TryCreateCost(buildings.House, economy, out BuildingCost houseCost, out string houseProblem)
                    && buildings.TryCreateCost(buildings.Workshop, economy, out BuildingCost workshopCost, out string workshopProblem))
                {
                    var goodNames = new List<string>();

                    foreach (GoodDefinition good in economy.Goods)
                    {
                        goodNames.Add(good.DisplayName);
                    }

                    Treasury treasury = playerTreasury != null && playerTreasury.IsReady ? playerTreasury.Treasury : null;
                    buildingsSection = new CityBuildingsSection(
                        buildingsElement, buildingsRows, buildingsCostsLabel, goodNames,
                        houseCost, buildings.HouseRoom, workshopCost, buildings.WorkshopJobs,
                        buildings.CostsText(economy), treasury);
                    buildingsSection.HouseBuilt += OnHouseBuilt;
                    buildingsSection.WorkshopBuilt += OnWorkshopBuilt;
                }
                else
                {
                    Debug.LogError("The buildings are invalid: the city panel shows none.", this);
                }
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

                foreach (GoodDefinition good in worldEconomy.Economy.Goods)
                {
                    goodNames.Add(GoodUnitText.NameWithUnit(good.DisplayName, good.Unit));
                }

                Treasury treasury = playerTreasury != null && playerTreasury.IsReady ? playerTreasury.Treasury : null;
                marketSection = new CityMarketSection(marketElement, marketRows, marketHoldLabel, goodNames, treasury);
            }

            section = marketSection;
            return section != null;
        }

        /// <returns>The hold of the selected ship when it is the player's and lies in the shown city's port, otherwise null.</returns>
        private CargoHold HoldOfSelectedShipInPort()
        {
            Ship selected = shipsView != null ? shipsView.Selection.Selected : null;

            // Only the player's ships trade here: another merchant's is only looked at.
            return selected != null && shipsView.IsPlayerShip(selected) && listedShips.Contains(selected)
                ? selected.Cargo
                : null;
        }

        private void OnCloseClicked()
        {
            mapView.Selection.ClearSelection();
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
