using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityBuildingsSectionTests
    {
        // Good 0 is the material, good 1 is produced, good 2 is neither.
        private const int Material = 0;
        private const int Produced = 1;
        private const int Salt = 2;

        // The house rows, in the alphabetical order of their names.
        private const int WealthyRow = 0;
        private const int PoorRow = 1;
        private const int RichRow = 2;

        private VisualElement sectionElement;
        private VisualElement houses;
        private VisualElement workshops;
        private CityMarket market;
        private Treasury treasury;
        private BuildingCost houseCost;
        private BuildingCost workshopCost;
        private CityBuildingsSection section;

        private static CityMarket NewMarket()
        {
            return new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 2, 30, 0.1), new MarketGood(10, 2, 0, 0) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));
        }

        private CityBuildingsSection NewSection(Treasury forTreasury)
        {
            // In no particular order: the section sorts them.
            var houseOffers = new[]
            {
                new HouseOffer(SocialClass.Rich, "Maison de riches", 40, houseCost, "100 or, 3 blé"),
                new HouseOffer(SocialClass.Wealthy, "Maison d'aisés", 70, houseCost, "100 or, 3 blé"),
                new HouseOffer(SocialClass.Poor, "Maison de pauvres", 140, houseCost, "100 or, 3 blé"),
            };
            var workshopOffers = new[]
            {
                new WorkshopOffer(Produced, "Vignoble", 30, workshopCost, "200 or, 2 blé"),
                new WorkshopOffer(Salt, "Saline", 30, workshopCost, "200 or, 2 blé"),
            };

            return new CityBuildingsSection(sectionElement, houses, workshops, houseOffers, workshopOffers, forTreasury);
        }

        [SetUp]
        public void SetUp()
        {
            sectionElement = new VisualElement();
            houses = new VisualElement();
            workshops = new VisualElement();
            market = NewMarket();
            treasury = new Treasury(100000);
            houseCost = new BuildingCost(100, new[] { new MaterialCost(Material, 3) });
            workshopCost = new BuildingCost(200, new[] { new MaterialCost(Material, 2) });
            section = NewSection(treasury);
        }

        private static string Text(VisualElement row)
        {
            return ((Label)row[0]).text;
        }

        private static Button ButtonOf(VisualElement row)
        {
            return row.Q<Button>();
        }

        [Test]
        public void HousesText_WritesTheHousesAndTheCity()
        {
            Assert.AreEqual(
                "Maison de pauvres : 2 (ville : 1 008 / 1 300 places)",
                CityBuildingsSection.HousesText("Maison de pauvres", 2, 1008, 1300));
            Assert.AreEqual(
                "Maison d'aisés : 0 (ville : 330 / 350 places)",
                CityBuildingsSection.HousesText("Maison d'aisés", 0, 330, 350));
        }

        [Test]
        public void WorkshopsText_WritesTheWorkshopsAndTheirWorkers()
        {
            Assert.AreEqual("Ferme : 2 ateliers (45 / 60 ouvriers)", CityBuildingsSection.WorkshopsText("Ferme", 2, 45, 60));
            Assert.AreEqual("Brasserie : 0 atelier", CityBuildingsSection.WorkshopsText("Brasserie", 0, 0, 0));
            Assert.AreEqual("Bûcheron : 1 atelier (30 / 30 ouvriers)", CityBuildingsSection.WorkshopsText("Bûcheron", 1, 30, 30));
        }

        [Test]
        public void BuildButtonText_WritesTheGold()
        {
            Assert.AreEqual("Construire (12 340 or)", CityBuildingsSection.BuildButtonText(12340));
        }

        [Test]
        public void IsHiddenUntilItShowsAMarket()
        {
            Assert.AreEqual(DisplayStyle.None, sectionElement.style.display.value);

            section.Show(market, null);
            Assert.AreEqual(DisplayStyle.Flex, sectionElement.style.display.value);

            section.Hide();
            Assert.AreEqual(DisplayStyle.None, sectionElement.style.display.value);
        }

        [Test]
        public void Show_BuildsARowPerHouse_InAlphabeticalOrder_AndOneRowPerWorkshopOfAGoodTheCityProduces()
        {
            section.Show(market, null);

            Assert.AreEqual(3, houses.childCount);
            Assert.AreEqual(1, workshops.childCount);
            StringAssert.StartsWith("Maison d'aisés : 0", Text(houses[WealthyRow]));
            StringAssert.StartsWith("Maison de pauvres : 0 (ville : 1 000 / 1 000 places)", Text(houses[PoorRow]));
            StringAssert.StartsWith("Maison de riches : 0", Text(houses[RichRow]));
            StringAssert.StartsWith("Vignoble : 0 atelier", Text(workshops[0]));
        }

        [Test]
        public void AGoodWithoutAWorkshop_HasNoRow()
        {
            // Good 0 is produced too, but no offer makes it.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 30, 0.1), new MarketGood(10, 2, 30, 0.1), new MarketGood(10, 2, 0, 0) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));

            section.Show(market, null);

            Assert.AreEqual(1, workshops.childCount);
            StringAssert.StartsWith("Vignoble : 0 atelier", Text(workshops[0]));
            Assert.IsFalse(section.BuildWorkshop(Material));
        }

        [Test]
        public void AButton_ShowsTheQuote_AndIsDisabledWithoutAPriceWhenImpossible()
        {
            section.Show(market, null);
            ConstructionQuote quote = Construction.QuoteHouse(market, null, treasury, houseCost);

            Assert.AreEqual(CityBuildingsSection.BuildButtonText(quote.Gold), ButtonOf(houses[0]).text);
            Assert.IsTrue(ButtonOf(houses[0]).enabledSelf);
            Assert.IsTrue(ButtonOf(workshops[0]).enabledSelf);

            section = NewSection(new Treasury(0));
            section.Show(market, null);

            Assert.IsFalse(ButtonOf(houses[0]).enabledSelf);
            Assert.IsFalse(ButtonOf(workshops[0]).enabledSelf);
            Assert.AreEqual(CityBuildingsSection.BuildText, ButtonOf(houses[0]).text);
            Assert.AreEqual(CityBuildingsSection.BuildText, ButtonOf(workshops[0]).text);
        }

        [Test]
        public void WithoutTheMaterials_TheButtonsShowNoPrice()
        {
            section.Show(market, null);
            market.Take(Material, market.AvailableOf(Material));

            Assert.IsFalse(ButtonOf(houses[0]).enabledSelf);
            Assert.AreEqual(CityBuildingsSection.BuildText, ButtonOf(houses[0]).text);
            Assert.AreEqual(CityBuildingsSection.BuildText, ButtonOf(workshops[0]).text);
        }

        [Test]
        public void WithoutATreasury_TheButtonsAreDisabled_AndNothingIsBuilt()
        {
            section = NewSection(null);
            section.Show(market, null);

            Assert.IsFalse(ButtonOf(houses[0]).enabledSelf);
            Assert.AreEqual(CityBuildingsSection.BuildText, ButtonOf(houses[0]).text);
            Assert.IsFalse(section.BuildHouse(SocialClass.Poor));
            Assert.AreEqual(0, market.PlayerHousesOf(SocialClass.Poor));
        }

        [Test]
        public void BuildHouse_BuildsAHouseOfThatClass_RaisesBuildingBuilt_AndRewritesTheRows()
        {
            section.Show(market, null);
            int built = 0;
            string builtName = null;
            section.BuildingBuilt += name =>
            {
                built++;
                builtName = name;
            };

            Assert.IsTrue(section.BuildHouse(SocialClass.Rich));

            Assert.AreEqual(1, built);
            Assert.AreEqual("Maison de riches", builtName);
            Assert.AreEqual(1, market.PlayerHousesOf(SocialClass.Rich));
            Assert.AreEqual(40, market.Population.RoomOf(SocialClass.Rich));
            StringAssert.StartsWith("Maison de riches : 1 (ville : 0 / 40 places)", Text(houses[RichRow]));
            StringAssert.StartsWith("Maison de pauvres : 0", Text(houses[PoorRow]));
        }

        [Test]
        public void BuildHouse_WithoutTheGold_BuildsNothing()
        {
            section = NewSection(new Treasury(0));
            section.Show(market, null);
            int built = 0;
            section.BuildingBuilt += _ => built++;

            Assert.IsFalse(section.BuildHouse(SocialClass.Poor));

            Assert.AreEqual(0, built);
            Assert.AreEqual(0, market.PlayerHousesOf(SocialClass.Poor));
        }

        [Test]
        public void BuildWorkshop_RaisesBuildingBuiltWithItsName_AndTakesItsJobs()
        {
            section.Show(market, null);
            string builtName = null;
            section.BuildingBuilt += name => builtName = name;

            Assert.IsTrue(section.BuildWorkshop(Produced));

            Assert.AreEqual("Vignoble", builtName);
            Assert.AreEqual(1, market.PlayerWorkshopsOf(Produced));
            Assert.AreEqual(30, market.PlayerJobsOf(Produced));
            StringAssert.StartsWith("Vignoble : 1 atelier", Text(workshops[0]));
        }

        [Test]
        public void BuildWorkshop_OfAGoodNotProduced_BuildsNothing()
        {
            section.Show(market, null);

            Assert.IsFalse(section.BuildWorkshop(Salt));
            Assert.AreEqual(0, market.PlayerWorkshopsOf(Salt));
        }

        [Test]
        public void AfterHide_ATreasuryChangeRewritesNothing()
        {
            section.Show(market, null);
            string before = ButtonOf(houses[0]).text;

            section.Hide();
            treasury.Deposit(1000000);
            market.Take(Material, 5);

            Assert.AreEqual(before, ButtonOf(houses[0]).text);
        }

        [Test]
        public void TheWorkshops_AreInTheAlphabeticalOrderOfTheirNames()
        {
            // The vineyard (good 1) comes before the saltworks (good 2) among the goods, after it by name.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 2, 30, 0.1), new MarketGood(10, 2, 30, 0.1) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));

            section.Show(market, null);

            Assert.AreEqual(2, workshops.childCount);
            StringAssert.StartsWith("Saline : 0 atelier", Text(workshops[0]));
            StringAssert.StartsWith("Vignoble : 0 atelier", Text(workshops[1]));
            Assert.IsTrue(section.BuildWorkshop(Salt));
            StringAssert.StartsWith("Saline : 1 atelier", Text(workshops[0]));
        }

        [Test]
        public void AnUnavailableBuilding_HasADisabledButtonWithoutAPrice_AndBuildsNothing()
        {
            houseCost = null;
            section = NewSection(treasury);
            section.Show(market, null);

            Assert.AreEqual(3, houses.childCount);
            Assert.AreEqual(1, workshops.childCount);
            StringAssert.StartsWith("Maison de pauvres : 0", Text(houses[PoorRow]));
            Assert.AreEqual(CityBuildingsSection.BuildText, ButtonOf(houses[PoorRow]).text);
            Assert.IsFalse(ButtonOf(houses[PoorRow]).enabledSelf);
            Assert.IsFalse(section.BuildHouse(SocialClass.Poor));
            Assert.AreEqual(0, market.PlayerHousesOf(SocialClass.Poor));
            Assert.AreEqual(100000, treasury.Gold);

            // The other building is still available.
            Assert.IsTrue(ButtonOf(workshops[0]).enabledSelf);
            Assert.IsTrue(section.BuildWorkshop(Produced));
        }

        [Test]
        public void AnUnavailableWorkshop_HasDisabledButtons_AndBuildsNothing()
        {
            workshopCost = null;
            section = NewSection(treasury);
            section.Show(market, null);

            Assert.AreEqual(CityBuildingsSection.BuildText, ButtonOf(workshops[0]).text);
            Assert.IsFalse(ButtonOf(workshops[0]).enabledSelf);
            Assert.IsFalse(section.BuildWorkshop(Produced));
            Assert.AreEqual(0, market.PlayerWorkshopsOf(Produced));
            Assert.IsTrue(ButtonOf(houses[0]).enabledSelf);
        }

        [Test]
        public void ATreasuryChange_RewritesTheButtons()
        {
            section.Show(market, null);
            Assert.IsTrue(ButtonOf(houses[0]).enabledSelf);

            treasury.Withdraw(100000);

            Assert.IsFalse(ButtonOf(houses[0]).enabledSelf);
        }
    }
}
