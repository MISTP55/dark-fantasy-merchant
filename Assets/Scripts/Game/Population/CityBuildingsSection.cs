using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The "Bâtiments" tab of the city panel: under "Logements", a row for the player's houses;
    /// under "Ateliers", one row per good the city produces with the player's workshops of it, in
    /// the alphabetical order of the goods;
    /// and the buttons that build, each
    /// showing what the building would cost now. A building without a cost is unavailable: its
    /// rows stay, their buttons disabled. Follows the market, the hold and the treasury while it
    /// is shown.
    /// </summary>
    public sealed class CityBuildingsSection
    {
        private const string RowClass = "buildings__row";
        private const string TextClass = "buildings__text";
        private const string ButtonClass = "buildings__button";

        private const int HouseRow = -1;

        private sealed class Row
        {
            // The good of a workshop row, or HouseRow.
            public int Good;
            public Label Text;
            public Button Button;
        }

        private readonly VisualElement section;
        private readonly VisualElement houseRowContainer;
        private readonly VisualElement workshopRowContainer;
        private readonly Label costsLabel;
        private readonly IReadOnlyList<string> goodNames;
        private readonly BuildingCost houseCost;
        private readonly int houseRoom;
        private readonly BuildingCost workshopCost;
        private readonly int workshopJobs;
        private readonly string costsText;
        private readonly Treasury treasury;
        private readonly List<Row> rows = new List<Row>();

        private CityMarket market;
        private CargoHold hold;

        /// <param name="section">The whole section, shown and hidden with the market.</param>
        /// <param name="houseRowContainer">Where the row of the houses is built.</param>
        /// <param name="workshopRowContainer">Where the rows of the workshops are built.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        /// <param name="houseCost">Null: a house is unavailable, its button stays disabled.</param>
        /// <param name="workshopCost">Null: a workshop is unavailable, its buttons stay disabled.</param>
        /// <param name="treasury">What pays. Null: the buttons stay disabled.</param>
        public CityBuildingsSection(
            VisualElement section,
            VisualElement houseRowContainer,
            VisualElement workshopRowContainer,
            Label costsLabel,
            IReadOnlyList<string> goodNames,
            BuildingCost houseCost,
            int houseRoom,
            BuildingCost workshopCost,
            int workshopJobs,
            string costsText,
            Treasury treasury)
        {
            this.section = section ?? throw new ArgumentNullException(nameof(section));
            this.houseRowContainer = houseRowContainer ?? throw new ArgumentNullException(nameof(houseRowContainer));
            this.workshopRowContainer = workshopRowContainer ?? throw new ArgumentNullException(nameof(workshopRowContainer));
            this.costsLabel = costsLabel ?? throw new ArgumentNullException(nameof(costsLabel));
            this.goodNames = goodNames ?? throw new ArgumentNullException(nameof(goodNames));
            this.houseCost = houseCost;
            this.workshopCost = workshopCost;
            this.houseRoom = houseRoom;
            this.workshopJobs = workshopJobs;
            this.costsText = costsText ?? string.Empty;
            this.treasury = treasury;

            Hide();
        }

        /// <summary>Raised once a house is built.</summary>
        public event Action HouseBuilt;

        /// <summary>Raised once a workshop is built, with its good.</summary>
        public event Action<int> WorkshopBuilt;

        /// <summary>"Logements : 2 (ville : 1 008 / 1 300 places)".</summary>
        public static string HousesText(int playerHouses, int housed, int room)
        {
            return $"Logements : {TreasuryHudController.FormatNumber(playerHouses)} "
                + $"(ville : {TreasuryHudController.FormatNumber(housed)} / {TreasuryHudController.FormatNumber(room)} places)";
        }

        /// <summary>"Céréales : 2 ateliers (45 / 60 ouvriers)", or "Bière : 0 atelier".</summary>
        public static string WorkshopsText(string goodName, int workshops, int workers, int jobs)
        {
            if (workshops <= 0)
            {
                return $"{goodName} : 0 atelier";
            }

            string noun = workshops > 1 ? "ateliers" : "atelier";

            return $"{goodName} : {TreasuryHudController.FormatNumber(workshops)} {noun} "
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
            costsLabel.text = costsText;

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

        /// <returns>True when a house was built in the shown city.</returns>
        public bool BuildHouse()
        {
            if (market == null || treasury == null || houseCost == null)
            {
                return false;
            }

            if (!Construction.TryBuildHouse(market, hold, treasury, houseCost, SocialClass.Poor, houseRoom))
            {
                return false;
            }

            HouseBuilt?.Invoke();
            return true;
        }

        /// <returns>True when a workshop of that good was built in the shown city.</returns>
        public bool BuildWorkshop(int good)
        {
            if (market == null || treasury == null || workshopCost == null)
            {
                return false;
            }

            if (!Construction.TryBuildWorkshop(market, hold, treasury, workshopCost, good, workshopJobs))
            {
                return false;
            }

            WorkshopBuilt?.Invoke(good);
            return true;
        }

        private void BuildRows()
        {
            houseRowContainer.Clear();
            workshopRowContainer.Clear();
            rows.Clear();

            AddRow(houseRowContainer, HouseRow, () => BuildHouse());

            var produced = new List<int>();

            for (int good = 0; good < market.GoodCount && good < goodNames.Count; good++)
            {
                if (market.MaxWorkersOf(good) > 0)
                {
                    produced.Add(good);
                }
            }

            NameOrder.Sort(produced, good => goodNames[good]);

            foreach (int good in produced)
            {
                AddRow(workshopRowContainer, good, () => BuildWorkshop(good));
            }

            ListStripes.Apply(houseRowContainer);
            ListStripes.Apply(workshopRowContainer);
        }

        private void AddRow(VisualElement container, int good, Action build)
        {
            var element = new VisualElement();
            element.AddToClassList(RowClass);

            var text = new Label();
            text.AddToClassList(TextClass);
            element.Add(text);

            var button = new Button(build);

            // Buttons are clicked, not navigated: the keyboard pans the map.
            button.focusable = false;
            button.AddToClassList(ButtonClass);
            element.Add(button);

            container.Add(element);
            rows.Add(new Row { Good = good, Text = text, Button = button });
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

                if (row.Good == HouseRow)
                {
                    row.Text.text = HousesText(market.PlayerHousesOf(SocialClass.Poor), market.Population.Housed, market.Population.Room);
                    quote = treasury != null && houseCost != null
                        ? Construction.QuoteHouse(market, hold, treasury, houseCost)
                        : default;
                }
                else
                {
                    row.Text.text = WorkshopsText(
                        goodNames[row.Good],
                        market.PlayerWorkshopsOf(row.Good),
                        market.PlayerWorkersOf(row.Good),
                        market.PlayerJobsOf(row.Good));
                    quote = treasury != null && workshopCost != null
                        ? Construction.QuoteWorkshop(market, hold, treasury, workshopCost, row.Good)
                        : default;
                }

                // An impossible quote's gold means nothing: the button shows no price.
                row.Button.text = quote.IsPossible ? BuildButtonText(quote.Gold) : BuildText;
                row.Button.SetEnabled(quote.IsPossible);
            }
        }
    }
}
