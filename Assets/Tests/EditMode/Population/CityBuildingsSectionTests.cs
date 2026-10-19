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

        // The house rows, in the alphabetical order of their names.
        private const int WealthyRow = 0;
        private const int PoorRow = 1;
        private const int RichRow = 2;

        private VisualElement sectionElement;
        private VisualElement houses;
        private VisualElement workshops;
        private Label costsLabel;
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
            return new CityBuildingsSection(
                sectionElement, houses, workshops, costsLabel, new[] { "Blé", "Vin", "Sel" },
                PerClass<BuildingCost>.All(houseCost), new PerClass<int>(40, 70, 140), workshopCost, 30, "Coûts", forTreasury);
        }

        [SetUp]
        public void SetUp()
        {
            sectionElement = new VisualElement();
            houses = new VisualElement();
            workshops = new VisualElement();
            costsLabel = new Label();
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
            return (Button)row[1];
        }

        [Test]
        public void HousesText_WritesTheHousesAndTheCity()
        {
            Assert.AreEqual(
                "Maisons de pauvres : 2 (ville : 1\u00A0008 / 1\u00A0300 places)",
                CityBuildingsSection.HousesText(SocialClass.Poor, 2, 1008, 1300));
            Assert.AreEqual(
                "Maisons d'aisés : 0 (ville : 330 / 350 places)",
                CityBuildingsSection.HousesText(SocialClass.Wealthy, 0, 330, 350));
        }

        [Test]
        public void WorkshopsText_WritesTheWorkshopsAndTheirWorkers()
        {
            Assert.AreEqual("Céréales : 2 ateliers (45 / 60 ouvriers)", CityBuildingsSection.WorkshopsText("Céréales", 2, 45, 60));
            Assert.AreEqual("Bière : 0 atelier", CityBuildingsSection.WorkshopsText("Bière", 0, 0, 0));
            Assert.AreEqual("Bois : 1 atelier (30 / 30 ouvriers)", CityBuildingsSection.WorkshopsText("Bois", 1, 30, 30));
        }

        [Test]
        public void BuildButtonText_WritesTheGold()
        {
            Assert.AreEqual("Construire (12\u00A0340 or)", CityBuildingsSection.BuildButtonText(12340));
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
        public void Show_BuildsARowPerHouse_InAlphabeticalOrder_AndOneRowPerGoodTheCityProduces()
        {
            section.Show(market, null);

            Assert.AreEqual(3, houses.childCount);
            Assert.AreEqual(1, workshops.childCount);
            StringAssert.StartsWith("Maisons d'aisés : 0", Text(houses[WealthyRow]));
            StringAssert.StartsWith("Maisons de pauvres : 0 (ville : 1\u00A0000 / 1\u00A0000 places)", Text(houses[PoorRow]));
            StringAssert.StartsWith("Maisons de riches : 0", Text(houses[RichRow]));
            StringAssert.StartsWith("Vin : 0 atelier", Text(workshops[0]));
            Assert.AreEqual("Coûts", costsLabel.text);
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
        public void BuildHouse_BuildsAHouseOfThatClass_RaisesHouseBuilt_AndRewritesTheRows()
        {
            section.Show(market, null);
            int built = 0;
            SocialClass builtClass = SocialClass.Poor;
            section.HouseBuilt += housedClass =>
            {
                built++;
                builtClass = housedClass;
            };

            Assert.IsTrue(section.BuildHouse(SocialClass.Rich));

            Assert.AreEqual(1, built);
            Assert.AreEqual(SocialClass.Rich, builtClass);
            Assert.AreEqual(1, market.PlayerHousesOf(SocialClass.Rich));
            Assert.AreEqual(40, market.Population.RoomOf(SocialClass.Rich));
            StringAssert.StartsWith("Maisons de riches : 1 (ville : 0 / 40 places)", Text(houses[RichRow]));
            StringAssert.StartsWith("Maisons de pauvres : 0", Text(houses[PoorRow]));
        }

        [Test]
        public void BuildHouse_WithoutTheGold_BuildsNothing()
        {
            section = NewSection(new Treasury(0));
            section.Show(market, null);
            int built = 0;
            section.HouseBuilt += _ => built++;

            Assert.IsFalse(section.BuildHouse(SocialClass.Poor));

            Assert.AreEqual(0, built);
            Assert.AreEqual(0, market.PlayerHousesOf(SocialClass.Poor));
        }

        [Test]
        public void BuildWorkshop_RaisesWorkshopBuiltWithTheGood()
        {
            section.Show(market, null);
            int good = -1;
            section.WorkshopBuilt += g => good = g;

            Assert.IsTrue(section.BuildWorkshop(Produced));

            Assert.AreEqual(Produced, good);
            Assert.AreEqual(1, market.PlayerWorkshopsOf(Produced));
            StringAssert.StartsWith("Vin : 1 atelier", Text(workshops[0]));
        }

        [Test]
        public void BuildWorkshop_OfAGoodNotProduced_BuildsNothing()
        {
            section.Show(market, null);

            Assert.IsFalse(section.BuildWorkshop(2));
            Assert.AreEqual(0, market.PlayerWorkshopsOf(2));
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
        public void TheWorkshops_AreInTheAlphabeticalOrderOfTheirGoods()
        {
            // Wine (good 1) and salt (good 2) are both produced: salt comes first.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 2, 30, 0.1), new MarketGood(10, 2, 30, 0.1) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));

            section.Show(market, null);

            Assert.AreEqual(2, workshops.childCount);
            StringAssert.StartsWith("Sel : 0 atelier", Text(workshops[0]));
            StringAssert.StartsWith("Vin : 0 atelier", Text(workshops[1]));
            Assert.IsTrue(section.BuildWorkshop(2));
            StringAssert.StartsWith("Sel : 1 atelier", Text(workshops[0]));
        }

        [Test]
        public void AnUnavailableBuilding_HasADisabledButtonWithoutAPrice_AndBuildsNothing()
        {
            houseCost = null;
            section = NewSection(treasury);
            section.Show(market, null);

            Assert.AreEqual(3, houses.childCount);
            Assert.AreEqual(1, workshops.childCount);
            StringAssert.StartsWith("Maisons de pauvres : 0", Text(houses[PoorRow]));
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
