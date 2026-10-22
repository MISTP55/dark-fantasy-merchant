using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The "Bâtiments" tab of the city panel: under "Logements", a row for the player's houses of
    /// each class; under "Ateliers", one row per workshop of a good the city produces, with the
    /// player's workshops of it; each list in the alphabetical order of the buildings' names, and
    /// each row with the button that builds, showing what the building would cost now. A building
    /// without a cost is unavailable: its row stays, its button disabled. Follows the market, the
    /// hold and the treasury while it is shown.
    /// </summary>
    public sealed class CityBuildingsSection
    {
        private const string RowClass = "buildings__row";
        private const string TextClass = "buildings__text";
        private const string ButtonClass = "buildings__button";

        private sealed class Row
        {
            // One of the two, the other null.
            public HouseOffer House;
            public WorkshopOffer Workshop;
            public Label Text;
            public Button Button;
        }

        private readonly VisualElement section;
        private readonly VisualElement houseRowContainer;
        private readonly VisualElement workshopRowContainer;
        private readonly List<HouseOffer> houses;
        private readonly List<WorkshopOffer> workshops;
        private readonly Treasury treasury;
        private readonly List<Row> rows = new List<Row>();

        private CityMarket market;
        private CargoHold hold;

        /// <param name="section">The whole section, shown and hidden with the market.</param>
        /// <param name="houseRowContainer">Where the rows of the houses are built.</param>
        /// <param name="workshopRowContainer">Where the rows of the workshops are built.</param>
        /// <param name="houses">The houses the player can build; one with a null cost is unavailable, its button stays disabled.</param>
        /// <param name="workshops">The workshops the player can build, the same way.</param>
        /// <param name="treasury">What pays. Null: the buttons stay disabled.</param>
        public CityBuildingsSection(
            VisualElement section,
            VisualElement houseRowContainer,
            VisualElement workshopRowContainer,
            IReadOnlyList<HouseOffer> houses,
            IReadOnlyList<WorkshopOffer> workshops,
            Treasury treasury)
        {
            this.section = section ?? throw new ArgumentNullException(nameof(section));
            this.houseRowContainer = houseRowContainer ?? throw new ArgumentNullException(nameof(houseRowContainer));
            this.workshopRowContainer = workshopRowContainer ?? throw new ArgumentNullException(nameof(workshopRowContainer));
            this.houses = new List<HouseOffer>(houses ?? throw new ArgumentNullException(nameof(houses)));
            this.workshops = new List<WorkshopOffer>(workshops ?? throw new ArgumentNullException(nameof(workshops)));
            this.treasury = treasury;

            NameOrder.Sort(this.houses, house => house.Name);
            NameOrder.Sort(this.workshops, workshop => workshop.Name);

            Hide();
        }

        /// <summary>Raised once a building is built, with its name.</summary>
        public event Action<string> BuildingBuilt;

        /// <summary>Raised for each material a building bought from the market: its good, units and gold. For the debug tools.</summary>
        public event Action<int, int, long> MaterialBought;

        /// <summary>"Maison de pauvres : 2 (ville : 1 008 / 1 300 places)": the player's houses, then the class's inhabitants and room.</summary>
        public static string HousesText(string name, int playerHouses, int inhabitants, int room)
        {
            return $"{name} : {TreasuryHudController.FormatNumber(playerHouses)} "
                + $"(ville : {TreasuryHudController.FormatNumber(inhabitants)} / {TreasuryHudController.FormatNumber(room)} places)";
        }

        /// <summary>"Ferme : 2 ateliers (45 / 60 ouvriers)", or "Brasserie : 0 atelier".</summary>
        public static string WorkshopsText(string name, int workshops, int workers, int jobs)
        {
            if (workshops <= 0)
            {
                return $"{name} : 0 atelier";
            }

            string noun = workshops > 1 ? "ateliers" : "atelier";

            return $"{name} : {TreasuryHudController.FormatNumber(workshops)} {noun} "
                + $"({TreasuryHudController.FormatNumber(workers)} / {TreasuryHudController.FormatNumber(jobs)} ouvriers)";
        }

        /// <summary>The button of a building that cannot be built now, which shows no price.</summary>
        public const string BuildText = "Construire";

        /// <summary>"Construire (12 340 or)".</summary>
        public static string BuildButtonText(long gold)
        {
            return $"Construire ({TreasuryHudController.FormatGold(gold)})";
        }

        /// <summary>Shows a city's buildings; the materials come first from the hold, when one is given.</summary>
        public void Show(CityMarket market, CargoHold hold)
        {
            Unbind();

            this.market = market ?? throw new ArgumentNullException(nameof(market));
            this.hold = hold;

            BuildRows();

            this.market.Changed += Refresh;

            if (this.hold != null)
            {
                this.hold.Changed += Refresh;
            }

            if (treasury != null)
            {
                treasury.Changed += OnGoldChanged;
            }

            section.style.display = DisplayStyle.Flex;
            Refresh();
        }

        public void Hide()
        {
            Unbind();
            section.style.display = DisplayStyle.None;
        }

        /// <returns>True when a house of the class was built in the shown city.</returns>
        public bool BuildHouse(SocialClass housedClass)
        {
            HouseOffer house = houses.Find(offer => offer.HousedClass == housedClass);

            if (market == null || treasury == null || house?.Cost == null)
            {
                return false;
            }

            if (!Construction.TryBuildHouse(market, hold, treasury, house.Cost, housedClass, house.Room, OnMaterialBought))
            {
                return false;
            }

            BuildingBuilt?.Invoke(house.Name);
            return true;
        }

        /// <returns>True when a workshop of that good was built in the shown city.</returns>
        public bool BuildWorkshop(int good)
        {
            WorkshopOffer workshop = workshops.Find(offer => offer.Good == good);

            if (market == null || treasury == null || workshop?.Cost == null)
            {
                return false;
            }

            if (!Construction.TryBuildWorkshop(market, hold, treasury, workshop.Cost, good, workshop.Jobs, OnMaterialBought))
            {
                return false;
            }

            BuildingBuilt?.Invoke(workshop.Name);
            return true;
        }

        private void OnMaterialBought(int good, int units, long gold)
        {
            MaterialBought?.Invoke(good, units, gold);
        }

        private void BuildRows()
        {
            houseRowContainer.Clear();
            workshopRowContainer.Clear();
            rows.Clear();

            foreach (HouseOffer house in houses)
            {
                AddRow(houseRowContainer, new Row { House = house }, () => BuildHouse(house.HousedClass));
            }

            foreach (WorkshopOffer workshop in workshops)
            {
                if (workshop.Good < market.GoodCount && market.MaxWorkersOf(workshop.Good) > 0)
                {
                    AddRow(workshopRowContainer, new Row { Workshop = workshop }, () => BuildWorkshop(workshop.Good));
                }
            }

            ListStripes.Apply(houseRowContainer);
            ListStripes.Apply(workshopRowContainer);
        }

        private void AddRow(VisualElement container, Row row, Action build)
        {
            var element = new VisualElement();
            element.AddToClassList(RowClass);

            row.Text = new Label();
            row.Text.AddToClassList(TextClass);
            element.Add(row.Text);

            row.Button = new Button(build);

            // Buttons are clicked, not navigated: the keyboard pans the map.
            row.Button.focusable = false;
            row.Button.AddToClassList(ButtonClass);
            element.Add(row.Button);

            container.Add(element);
            rows.Add(row);
        }

        private void Unbind()
        {
            if (market != null)
            {
                market.Changed -= Refresh;
            }

            if (hold != null)
            {
                hold.Changed -= Refresh;
            }

            if (treasury != null)
            {
                treasury.Changed -= OnGoldChanged;
            }

            market = null;
            hold = null;
        }

        private void OnGoldChanged(long gold)
        {
            Refresh();
        }

        // A build changes the hold, the market and the gold: a few rewrites of a few labels.
        private void Refresh()
        {
            // Another listener of the same change may have closed the section.
            if (market == null)
            {
                return;
            }

            foreach (Row row in rows)
            {
                ConstructionQuote quote;

                if (row.House != null)
                {
                    SocialClass housedClass = row.House.HousedClass;
                    row.Text.text = HousesText(
                        row.House.Name,
                        market.PlayerHousesOf(housedClass),
                        market.Population.CountOf(housedClass),
                        market.Population.RoomOf(housedClass));
                    quote = treasury != null && row.House.Cost != null
                        ? Construction.QuoteHouse(market, hold, treasury, row.House.Cost)
                        : default;
                }
                else
                {
                    int good = row.Workshop.Good;
                    row.Text.text = WorkshopsText(
                        row.Workshop.Name,
                        market.PlayerWorkshopsOf(good),
                        market.PlayerWorkersOf(good),
                        market.PlayerJobsOf(good));
                    quote = treasury != null && row.Workshop.Cost != null
                        ? Construction.QuoteWorkshop(market, hold, treasury, row.Workshop.Cost, good)
                        : default;
                }

                // An impossible quote's gold means nothing: the button shows no price.
                row.Button.text = quote.IsPossible ? BuildButtonText(quote.Gold) : BuildText;
                row.Button.SetEnabled(quote.IsPossible);
            }
        }
    }
}
