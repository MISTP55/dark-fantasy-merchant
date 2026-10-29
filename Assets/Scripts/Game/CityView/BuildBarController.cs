using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The build bar of the city view, over its document: four categories at the bottom (houses, workshops, roads,
    /// trading post), and above them the offers of the chosen one in the alphabetical order of their names, each with
    /// what it would cost now, the road for one cell. Only the workshops of the goods the city produces are offered; an
    /// offer that cannot be built now is disabled, the trading post once it stands. The pointer over an offer, enabled
    /// or not, shows its full cost in a tip, or that the trading post stands. Follows the market, the treasury, the
    /// player's trading post and the hold the materials come from, the opening of that trading post and the debug
    /// tools' free building while it is shown, never every frame.
    /// </summary>
    public sealed class BuildBarController
    {
        public const string HousesText = "Maisons";
        public const string WorkshopsText = "Ateliers";
        public const string RoadsText = "Routes";
        public const string TradingPostText = "Comptoir";

        /// <summary>The trading post's tip once it stands in the city.</summary>
        public const string TradingPostBuiltText = "Votre comptoir est déjà construit dans cette ville.";

        /// <summary>A workshop's tip in a city where the player has no trading post.</summary>
        public const string WorkshopNeedsTradingPostText = "Il faut un comptoir dans cette ville pour y construire un atelier.";

        /// <summary>The cost tip while the debug tools make every building free.</summary>
        public const string FreeText = "Gratuit (débogage)";

        private const string OfferClass = "build-bar__offer";
        private const string OfferAreaClass = "build-bar__offer-area";
        private const string ChosenCategoryClass = "build-bar__category--chosen";

        // Between the tip and the offer it is about.
        private const float CostTipGap = 4f;

        internal enum Category
        {
            None,
            Houses,
            Workshops,
            Roads,
            TradingPost,
        }

        private sealed class Offer
        {
            // One of the four, the others null.
            public HouseOffer House;
            public WorkshopOffer Workshop;
            public RoadOffer Road;
            public TradingPostOffer TradingPost;
            public Button Button;

            public string Name => House != null ? House.Name : Workshop != null ? Workshop.Name
                : Road != null ? Road.Name : TradingPost.Name;

            public string CostText => House != null ? House.CostText
                : Workshop != null ? Workshop.CostText
                : Road != null ? PerCellText(Road.CostText)
                : TradingPost.CostText;
        }

        private readonly VisualElement offerContainer;
        private readonly Button housesButton;
        private readonly Button workshopsButton;
        private readonly Button roadsButton;
        private readonly Button tradingPostButton;
        private readonly Label costTip;
        private readonly List<Offer> offers = new List<Offer>();

        private Category category;
        private CityDefinition city;
        private PlayerBuilder builder;
        private CityMarket market;
        private Treasury treasury;
        private CargoHold hold;
        private ShipDocking<CityDefinition> docking;
        private IReadOnlyList<HouseOffer> houseOffers;
        private IReadOnlyList<WorkshopOffer> workshopOffers;
        private RoadOffer roadOffer;
        private TradingPostOffer tradingPostOffer;

        // The city's grid, which tells whether its trading post stands, and that trading post's warehouse, whose goods
        // pay for materials; either may be null.
        private CityGrid grid;
        private Warehouse tradingPost;

        // Followed for the opening of the city's trading post; null when shown without a world.
        private WorldEconomy worldEconomy;

        /// <param name="root">The city view's document root, which holds the bar's elements.</param>
        /// <exception cref="ArgumentException">An element of the bar is missing.</exception>
        public BuildBarController(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            offerContainer = root.Q<VisualElement>("build-offers");
            housesButton = root.Q<Button>("houses-button");
            workshopsButton = root.Q<Button>("workshops-button");
            roadsButton = root.Q<Button>("roads-button");
            tradingPostButton = root.Q<Button>("category-trading-post");
            costTip = root.Q<Label>("build-cost-tip");

            if (offerContainer == null || housesButton == null || workshopsButton == null || roadsButton == null
                || tradingPostButton == null || costTip == null)
            {
                throw new ArgumentException("CityView.uxml is missing an element of the build bar.", nameof(root));
            }

            housesButton.text = HousesText;
            workshopsButton.text = WorkshopsText;
            roadsButton.text = RoadsText;
            tradingPostButton.text = TradingPostText;

            // Clicked, not navigated.
            housesButton.focusable = false;
            workshopsButton.focusable = false;
            roadsButton.focusable = false;
            tradingPostButton.focusable = false;
            housesButton.clicked += () => Choose(Category.Houses);
            workshopsButton.clicked += () => Choose(Category.Workshops);
            roadsButton.clicked += () => Choose(Category.Roads);
            tradingPostButton.clicked += () => Choose(Category.TradingPost);

            // The grid under the tip stays reachable.
            costTip.pickingMode = PickingMode.Ignore;
            ShowCategory(Category.None);
        }

        /// <summary>Raised when the player picks a house to place.</summary>
        public event Action<HouseOffer> HouseChosen;

        /// <summary>Raised when the player picks a workshop to place.</summary>
        public event Action<WorkshopOffer> WorkshopChosen;

        /// <summary>Raised when the player picks the road to trace.</summary>
        public event Action<RoadOffer> RoadChosen;

        /// <summary>Raised when the player picks the trading post to place on its site.</summary>
        public event Action<TradingPostOffer> TradingPostChosen;

        /// <summary>
        /// Raised each time the bar quotes its offers again, while it follows a city, whether a category is open
        /// or not: what the player can pay for may have changed.
        /// </summary>
        public event Action Refreshed;

        /// <summary>"Maison de pauvres\n5 000 or": the offer's name over its gold.</summary>
        public static string OfferText(string name, long gold)
        {
            return $"{name}\n{NumberText.FormatGold(gold)}";
        }

        /// <summary>"50 or par case": the road's cost tip, the cost of one cell.</summary>
        public static string PerCellText(string costText)
        {
            return $"{costText} par case";
        }

        /// <summary>Shows the offers of the city, in place of those shown; no category is open.</summary>
        public void Show(CityDefinition shownCity, SimulationServices services)
        {
            Hide();

            WorldEconomy economy = services != null ? services.WorldEconomy : null;

            if (shownCity == null || economy == null || !economy.IsReady || services.PlayerBuilder == null)
            {
                return;
            }

            Show(
                shownCity,
                services.PlayerBuilder,
                economy.MarketOf(shownCity),
                services.PlayerTreasury != null && services.PlayerTreasury.IsReady ? services.PlayerTreasury.Treasury : null,
                services.ShipsView != null ? services.ShipsView.Docking : null,
                economy.HouseOffers,
                economy.WorkshopOffers,
                economy.RoadOffer,
                economy.TradingPostOffer,
                economy.GridOf(shownCity),
                economy.TradingPostOf(shownCity));

            // A trading post built while the view is open opens its warehouse once it stands.
            if (city != null)
            {
                worldEconomy = economy;
                worldEconomy.TradingPostOpened += OnTradingPostOpened;
            }
        }

        /// <summary>
        /// Shows the offers given, quoted through the builder, in place of those shown; no category is open. The
        /// market, the treasury, the docking and the city's trading post (<paramref name="shownTradingPost"/>, its
        /// warehouse) are followed when not null; the grid tells whether the trading post stands.
        /// </summary>
        internal void Show(
            CityDefinition shownCity,
            PlayerBuilder shownBuilder,
            CityMarket shownMarket,
            Treasury shownTreasury,
            ShipDocking<CityDefinition> shownDocking,
            IReadOnlyList<HouseOffer> shownHouseOffers,
            IReadOnlyList<WorkshopOffer> shownWorkshopOffers,
            RoadOffer shownRoadOffer,
            TradingPostOffer shownTradingPostOffer,
            CityGrid shownGrid,
            Warehouse shownTradingPost)
        {
            Hide();

            if (shownCity == null || shownBuilder == null)
            {
                return;
            }

            city = shownCity;
            builder = shownBuilder;
            market = shownMarket;
            houseOffers = shownHouseOffers;
            workshopOffers = shownWorkshopOffers;
            roadOffer = shownRoadOffer;
            tradingPostOffer = shownTradingPostOffer;
            grid = shownGrid;
            treasury = shownTreasury;
            docking = shownDocking;

            if (market != null)
            {
                market.Changed += Refresh;
            }

            if (treasury != null)
            {
                treasury.Changed += OnGoldChanged;
            }

            builder.FreeChanged += Refresh;
            SetTradingPost(shownTradingPost);

            if (docking != null)
            {
                docking.Docked += OnDockingChanged;
                docking.Undocked += OnDockingChanged;
            }

            ChooseHold();
        }

        /// <summary>Closes the open category and stops following the city.</summary>
        public void Hide()
        {
            if (market != null)
            {
                market.Changed -= Refresh;
            }

            if (treasury != null)
            {
                treasury.Changed -= OnGoldChanged;
            }

            if (builder != null)
            {
                builder.FreeChanged -= Refresh;
            }

            if (worldEconomy != null)
            {
                worldEconomy.TradingPostOpened -= OnTradingPostOpened;
            }

            if (docking != null)
            {
                docking.Docked -= OnDockingChanged;
                docking.Undocked -= OnDockingChanged;
            }

            SetHold(null);
            SetTradingPost(null);
            city = null;
            builder = null;
            market = null;
            treasury = null;
            docking = null;
            houseOffers = null;
            workshopOffers = null;
            roadOffer = null;
            tradingPostOffer = null;
            grid = null;
            worldEconomy = null;
            ShowCategory(Category.None);
        }

        /// <summary>The category's button opens it, or closes it when it is open.</summary>
        internal void Choose(Category chosen)
        {
            ShowCategory(chosen == category ? Category.None : chosen);
        }

        private void ShowCategory(Category shown)
        {
            category = city != null ? shown : Category.None;
            housesButton.EnableInClassList(ChosenCategoryClass, category == Category.Houses);
            workshopsButton.EnableInClassList(ChosenCategoryClass, category == Category.Workshops);
            roadsButton.EnableInClassList(ChosenCategoryClass, category == Category.Roads);
            tradingPostButton.EnableInClassList(ChosenCategoryClass, category == Category.TradingPost);

            HideCostTip();
            offerContainer.Clear();
            offers.Clear();

            if (category == Category.Houses && houseOffers != null)
            {
                foreach (HouseOffer house in houseOffers)
                {
                    offers.Add(new Offer { House = house });
                }
            }
            else if (category == Category.Workshops && workshopOffers != null)
            {
                foreach (WorkshopOffer workshop in workshopOffers)
                {
                    // A workshop of a good the city does not produce could never be built there.
                    if (IsProduced(workshop.Good))
                    {
                        offers.Add(new Offer { Workshop = workshop });
                    }
                }
            }
            else if (category == Category.Roads && roadOffer != null)
            {
                offers.Add(new Offer { Road = roadOffer });
            }
            else if (category == Category.TradingPost && tradingPostOffer != null)
            {
                offers.Add(new Offer { TradingPost = tradingPostOffer });
            }

            NameOrder.Sort(offers, offer => offer.Name);

            foreach (Offer offer in offers)
            {
                AddButton(offer);
            }

            offerContainer.style.display = offers.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh();
        }

        private void AddButton(Offer offer)
        {
            offer.Button = new Button(() => OnOfferClicked(offer));

            // Clicked, not navigated.
            offer.Button.focusable = false;
            offer.Button.AddToClassList(OfferClass);

            // A disabled button gets no pointer event: the area around it does.
            var area = new VisualElement();
            area.AddToClassList(OfferAreaClass);
            area.Add(offer.Button);
            area.RegisterCallback<PointerEnterEvent>(_ => ShowCostTip(offer, area));
            area.RegisterCallback<PointerLeaveEvent>(_ => HideCostTip());
            offerContainer.Add(area);
        }

        private void OnOfferClicked(Offer offer)
        {
            if (offer.House != null)
            {
                HouseChosen?.Invoke(offer.House);
            }
            else if (offer.Workshop != null)
            {
                WorkshopChosen?.Invoke(offer.Workshop);
            }
            else if (offer.Road != null)
            {
                RoadChosen?.Invoke(offer.Road);
            }
            else
            {
                TradingPostChosen?.Invoke(offer.TradingPost);
            }
        }

        private void ShowCostTip(Offer offer, VisualElement area)
        {
            costTip.text = CostTipOf(offer);
            costTip.style.display = DisplayStyle.Flex;

            VisualElement parent = costTip.parent;

            if (parent == null || area.panel == null || float.IsNaN(parent.layout.height))
            {
                return;
            }

            // Above the offer, its left edge on the offer's.
            Rect bounds = parent.WorldToLocal(area.worldBound);
            costTip.style.left = bounds.xMin;
            costTip.style.bottom = parent.layout.height - bounds.yMin + CostTipGap;
        }

        /// <summary>The cost tip of the shown offer at this index, in the order the bar lists them.</summary>
        internal string CostTipAt(int index)
        {
            return CostTipOf(offers[index]);
        }

        private string CostTipOf(Offer offer)
        {
            return offer.TradingPost != null && TradingPostStands ? TradingPostBuiltText
                : offer.Workshop != null && market?.PlayerTradingPost == null ? WorkshopNeedsTradingPostText
                : builder != null && builder.IsFree ? FreeText
                : offer.CostText;
        }

        private void HideCostTip()
        {
            costTip.style.display = DisplayStyle.None;
        }

        // A ship of the player docking or leaving may change the hold the materials come from.
        private void OnDockingChanged(Ship ship, CityDefinition port)
        {
            if (port == city)
            {
                ChooseHold();
            }
        }

        private void ChooseHold()
        {
            SetHold(builder != null ? builder.HoldIn(city) : null);
            Refresh();
        }

        private void SetHold(CargoHold chosen)
        {
            if (hold == chosen)
            {
                return;
            }

            if (hold != null)
            {
                hold.Changed -= Refresh;
            }

            hold = chosen;

            if (hold != null)
            {
                hold.Changed += Refresh;
            }
        }

        private bool TradingPostStands => grid != null && grid.TradingPost != null;

        // Its goods pay for materials: what the offers cost changes with them.
        private void SetTradingPost(Warehouse chosen)
        {
            if (tradingPost == chosen)
            {
                return;
            }

            if (tradingPost != null)
            {
                tradingPost.Changed -= Refresh;
            }

            tradingPost = chosen;

            if (tradingPost != null)
            {
                tradingPost.Changed += Refresh;
            }
        }

        // The city's trading post now stands and has its warehouse.
        private void OnTradingPostOpened(CityDefinition opened)
        {
            if (opened == city && worldEconomy != null)
            {
                SetTradingPost(worldEconomy.TradingPostOf(city));
                Refresh();
            }
        }

        private void OnGoldChanged(long gold)
        {
            Refresh();
        }

        // A build changes the hold, the market and the gold: a few rewrites of the open category's offers.
        private void Refresh()
        {
            // Another listener of the same change may have closed the bar.
            if (city == null)
            {
                return;
            }

            foreach (Offer offer in offers)
            {
                ConstructionQuote quote = offer.House != null ? builder.QuoteHouse(city, offer.House)
                    : offer.Workshop != null ? builder.QuoteWorkshop(city, offer.Workshop)
                    : offer.Road != null ? builder.QuoteRoadCell(city, offer.Road)
                    : builder.QuoteTradingPost(city, offer.TradingPost);

                // An impossible quote's gold means nothing: the offer shows its name alone.
                offer.Button.text = quote.IsPossible ? OfferText(offer.Name, quote.Gold) : offer.Name;
                // A trading post that stands cannot be quoted either; its tip says why.
                offer.Button.SetEnabled(quote.IsPossible && (offer.TradingPost == null || !TradingPostStands));
            }

            Refreshed?.Invoke();
        }

        private bool IsProduced(int good)
        {
            return market != null && good >= 0 && good < market.GoodCount && market.MaxWorkersOf(good) > 0;
        }
    }
}
