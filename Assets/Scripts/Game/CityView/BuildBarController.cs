using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The build bar of the city view, over its document: two categories at the bottom, and above them the offers
    /// of the chosen one in the alphabetical order of their names, each with what it would cost now. An offer that
    /// cannot be built now is disabled, and so is a workshop of a good the city does not produce. The pointer over
    /// an offer, enabled or not, shows its full cost in a tip. Follows the market, the treasury and the hold the
    /// materials come from while it is shown, never every frame.
    /// </summary>
    public sealed class BuildBarController
    {
        public const string HousesText = "Maisons";
        public const string WorkshopsText = "Ateliers";

        private const string OfferClass = "build-bar__offer";
        private const string OfferAreaClass = "build-bar__offer-area";
        private const string ChosenCategoryClass = "build-bar__category--chosen";

        // Between the tip and the offer it is about.
        private const float CostTipGap = 4f;

        private enum Category
        {
            None,
            Houses,
            Workshops,
        }

        private sealed class Offer
        {
            // One of the two, the other null.
            public HouseOffer House;
            public WorkshopOffer Workshop;
            public Button Button;

            public string Name => House != null ? House.Name : Workshop.Name;

            public string CostText => House != null ? House.CostText : Workshop.CostText;
        }

        private readonly VisualElement offerContainer;
        private readonly Button housesButton;
        private readonly Button workshopsButton;
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
            costTip = root.Q<Label>("build-cost-tip");

            if (offerContainer == null || housesButton == null || workshopsButton == null || costTip == null)
            {
                throw new ArgumentException("CityView.uxml is missing an element of the build bar.", nameof(root));
            }

            housesButton.text = HousesText;
            workshopsButton.text = WorkshopsText;

            // Clicked, not navigated.
            housesButton.focusable = false;
            workshopsButton.focusable = false;
            housesButton.clicked += () => Choose(Category.Houses);
            workshopsButton.clicked += () => Choose(Category.Workshops);

            // The grid under the tip stays reachable.
            costTip.pickingMode = PickingMode.Ignore;
            ShowCategory(Category.None);
        }

        /// <summary>Raised when the player picks a house to place.</summary>
        public event Action<HouseOffer> HouseChosen;

        /// <summary>Raised when the player picks a workshop to place.</summary>
        public event Action<WorkshopOffer> WorkshopChosen;

        /// <summary>"Maison de pauvres\n5 000 or": the offer's name over its gold.</summary>
        public static string OfferText(string name, long gold)
        {
            return $"{name}\n{TreasuryHudController.FormatGold(gold)}";
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

            city = shownCity;
            builder = services.PlayerBuilder;
            market = economy.MarketOf(city);
            houseOffers = economy.HouseOffers;
            workshopOffers = economy.WorkshopOffers;
            treasury = services.PlayerTreasury != null && services.PlayerTreasury.IsReady
                ? services.PlayerTreasury.Treasury
                : null;
            docking = services.ShipsView != null ? services.ShipsView.Docking : null;

            if (market != null)
            {
                market.Changed += Refresh;
            }

            if (treasury != null)
            {
                treasury.Changed += OnGoldChanged;
            }

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

            if (docking != null)
            {
                docking.Docked -= OnDockingChanged;
                docking.Undocked -= OnDockingChanged;
            }

            SetHold(null);
            city = null;
            builder = null;
            market = null;
            treasury = null;
            docking = null;
            houseOffers = null;
            workshopOffers = null;
            ShowCategory(Category.None);
        }

        // The category's button opens it, or closes it when it is open.
        private void Choose(Category chosen)
        {
            ShowCategory(chosen == category ? Category.None : chosen);
        }

        private void ShowCategory(Category shown)
        {
            category = city != null ? shown : Category.None;
            housesButton.EnableInClassList(ChosenCategoryClass, category == Category.Houses);
            workshopsButton.EnableInClassList(ChosenCategoryClass, category == Category.Workshops);

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
                    offers.Add(new Offer { Workshop = workshop });
                }
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
            else
            {
                WorkshopChosen?.Invoke(offer.Workshop);
            }
        }

        private void ShowCostTip(Offer offer, VisualElement area)
        {
            costTip.text = offer.CostText;
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
                ConstructionQuote quote;

                if (offer.House != null)
                {
                    quote = builder.QuoteHouse(city, offer.House);
                }
                else
                {
                    int good = offer.Workshop.Good;
                    bool isProduced = market != null && good >= 0 && good < market.GoodCount && market.MaxWorkersOf(good) > 0;
                    quote = isProduced ? builder.QuoteWorkshop(city, offer.Workshop) : default;
                }

                // An impossible quote's gold means nothing: the offer shows its name alone.
                offer.Button.text = quote.IsPossible ? OfferText(offer.Name, quote.Gold) : offer.Name;
                offer.Button.SetEnabled(quote.IsPossible);
            }
        }
    }
}
