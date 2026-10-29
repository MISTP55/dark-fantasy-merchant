using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugPopulationTableTests
    {
        // Children of a row, in order: the city, its population, its beggars, its rich, wealthy and poor,
        // its growth since the start, the houses it built.
        private const int NameCell = 0;
        private const int PopulationCell = 1;
        private const int GrowthSinceStartCell = 6;
        private const int HousesCell = 7;

        private VisualElement header;
        private VisualElement rows;
        private VisualElement totals;
        private DebugPopulationTable table;

        [SetUp]
        public void SetUp()
        {
            header = new VisualElement();
            rows = new VisualElement();
            totals = new VisualElement();
            table = new DebugPopulationTable(header, rows, totals);
        }

        private static CityMarket Market(int inhabitants)
        {
            return new CityMarket(new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, TestMarkets.Workforce, 30,
                TestMarkets.Pricing, 7, new CityPopulation(inhabitants));
        }

        // A city of 960 in 1 000 places, with the stock of a house of 100 places.
        private static CityMarket CrowdedMarket()
        {
            var material = new MarketGood(10, 0, 0, 0);
            var housing = new CityHousingRule(SocialClass.Poor, 0.95, 100, new[] { new MaterialCost(0, 10) });
            var market = new CityMarket(new[] { material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(960, 0, 1000), TestMarkets.Labour, new[] { housing });
            market.Put(0, 25);
            return market;
        }

        private string Text(int row, int cell)
        {
            return ((Label)rows[row][cell]).text;
        }

        [Test]
        public void TheHeader_NamesTheColumns()
        {
            Assert.AreEqual(1, header.childCount);
            Assert.AreEqual("Ville", ((Label)header[0][NameCell]).text);
            Assert.AreEqual("Croiss. totale", ((Label)header[0][GrowthSinceStartCell]).text);
            Assert.AreEqual("Maisons de la ville", ((Label)header[0][HousesCell]).text);
        }

        [Test]
        public void Population_HasTheCityHousesColumn()
        {
            CityMarket market = CrowdedMarket();
            table.AddCity("Cologne", market);
            table.Show();
            Assert.AreEqual("0", Text(0, HousesCell));

            market.StartDay();
            table.WriteChanges();

            Assert.AreEqual("1", Text(0, HousesCell));
            Assert.AreEqual("1", ((Label)totals[0][HousesCell]).text);
        }

        [Test]
        public void Population_RowsAreAlphabetical()
        {
            table.AddCity("Lübeck", Market(1000));
            table.AddCity("Édimbourg", Market(500));
            table.AddCity("Cologne", Market(800));

            Assert.AreEqual("Cologne", Text(0, NameCell));
            Assert.AreEqual("Édimbourg", Text(1, NameCell));
            Assert.AreEqual("Lübeck", Text(2, NameCell));
            Assert.AreEqual(NumberText.Format(500), Text(1, PopulationCell));
        }

        [Test]
        public void TheTotals_AddTheCitiesUp()
        {
            table.AddCity("Cologne", Market(1000));
            table.AddCity("Lübeck", Market(500));

            Assert.AreEqual(NumberText.Format(1500), ((Label)totals[0][PopulationCell]).text);
            Assert.AreEqual("Total", ((Label)totals[0][NameCell]).text);
        }

        [Test]
        public void WhileShown_AChange_IsWrittenByWriteChanges_NotAtOnce()
        {
            CityMarket market = CrowdedMarket();
            table.AddCity("Cologne", market);
            table.Show();

            market.StartDay();
            Assert.AreEqual("0", Text(0, HousesCell));

            table.WriteChanges();

            Assert.AreEqual("1", Text(0, HousesCell));
            Assert.AreEqual("1", ((Label)totals[0][HousesCell]).text);
        }

        [Test]
        public void AHiddenTable_DoesNotFollowTheMarkets()
        {
            CityMarket market = CrowdedMarket();
            table.AddCity("Cologne", market);

            market.StartDay();
            table.WriteChanges();

            Assert.AreEqual("0", Text(0, HousesCell));
        }
    }
}
