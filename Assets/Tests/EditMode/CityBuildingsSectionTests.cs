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

        private VisualElement sectionElement;
        private VisualElement rows;
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
                sectionElement, rows, costsLabel, new[] { "Blé", "Vin", "Sel" },
                houseCost, 100, workshopCost, 30, "Coûts", forTreasury);
        }

        [SetUp]
        public void SetUp()
        {
            sectionElement = new VisualElement();
            rows = new VisualElement();
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
                "Logements : 2 (ville : 1\u00A0008 / 1\u00A0300 places)",
                CityBuildingsSection.HousesText(2, 1008, 1300));
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
        public void Show_BuildsAHousesRow_AndOneRowPerGoodTheCityProduces()
        {
            section.Show(market, null);

            Assert.AreEqual(2, rows.childCount);
            StringAssert.StartsWith("Logements : 0", Text(rows[0]));
            StringAssert.StartsWith("Vin : 0 atelier", Text(rows[1]));
            Assert.AreEqual("Coûts", costsLabel.text);
        }

        [Test]
        public void AButton_ShowsTheQuote_AndIsDisabledWhenImpossible()
        {
            section.Show(market, null);
            ConstructionQuote quote = Construction.QuoteHouse(market, null, treasury, houseCost);

            Assert.AreEqual(CityBuildingsSection.BuildButtonText(quote.Gold), ButtonOf(rows[0]).text);
            Assert.IsTrue(ButtonOf(rows[0]).enabledSelf);
            Assert.IsTrue(ButtonOf(rows[1]).enabledSelf);

            section = NewSection(new Treasury(0));
            section.Show(market, null);

            Assert.IsFalse(ButtonOf(rows[0]).enabledSelf);
            Assert.IsFalse(ButtonOf(rows[1]).enabledSelf);
        }

        [Test]
        public void WithoutATreasury_TheButtonsAreDisabled_AndNothingIsBuilt()
        {
            section = NewSection(null);
            section.Show(market, null);

            Assert.IsFalse(ButtonOf(rows[0]).enabledSelf);
            Assert.IsFalse(section.BuildHouse());
            Assert.AreEqual(0, market.PlayerHouses);
        }

        [Test]
        public void BuildHouse_Builds_RaisesHouseBuilt_AndRewritesTheRows()
        {
            section.Show(market, null);
            int built = 0;
            section.HouseBuilt += () => built++;

            Assert.IsTrue(section.BuildHouse());

            Assert.AreEqual(1, built);
            Assert.AreEqual(1, market.PlayerHouses);
            StringAssert.StartsWith("Logements : 1", Text(rows[0]));
        }

        [Test]
        public void BuildHouse_WithoutTheGold_BuildsNothing()
        {
            section = NewSection(new Treasury(0));
            section.Show(market, null);
            int built = 0;
            section.HouseBuilt += () => built++;

            Assert.IsFalse(section.BuildHouse());

            Assert.AreEqual(0, built);
            Assert.AreEqual(0, market.PlayerHouses);
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
            StringAssert.StartsWith("Vin : 1 atelier", Text(rows[1]));
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
            string before = ButtonOf(rows[0]).text;

            section.Hide();
            treasury.Deposit(1000000);
            market.Take(Material, 5);

            Assert.AreEqual(before, ButtonOf(rows[0]).text);
        }

        [Test]
        public void ATreasuryChange_RewritesTheButtons()
        {
            section.Show(market, null);
            Assert.IsTrue(ButtonOf(rows[0]).enabledSelf);

            treasury.Withdraw(100000);

            Assert.IsFalse(ButtonOf(rows[0]).enabledSelf);
        }
    }
}
