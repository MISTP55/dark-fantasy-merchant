using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugCitiesTableTests
    {
        private const int Fish = 0;
        private const int Iron = 1;

        // Children of a row, in order: the city, its population, its growth in a month and
        // since the start, then four cells per good.
        private const int NameCell = 0;
        private const int PopulationCell = 1;
        private const int GrowthCell = 2;
        private const int GrowthSinceStartCell = 3;
        private const int FirstGoodCell = 4;
        private const int CellsPerGood = 4;
        private const int Stock = 0;
        private const int Production = 1;
        private const int Consumption = 2;
        private const int Net = 3;

        private VisualElement header;
        private VisualElement rows;
        private VisualElement totals;
        private CityMarket market;
        private CityMarket village;
        private DebugCitiesTable table;

        [SetUp]
        public void SetUp()
        {
            header = new VisualElement();
            rows = new VisualElement();
            totals = new VisualElement();

            // A thousand inhabitants. Fish: 2 barrels a day, not produced, 60 in stock.
            // Iron: 2 barrels a day and 3 produced, 60 in stock under a ceiling of 120.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 1.5, 60) },
                TestMarkets.Thresholds,
                30,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));

            // A village of 500: 1 barrel of fish a day, 30 in stock, and half a barrel of iron, 15 in stock.
            village = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 1, 0, 0) },
                TestMarkets.Thresholds,
                30,
                TestMarkets.Pricing,
                7,
                new CityPopulation(500));
            table = new DebugCitiesTable(header, rows, totals, new[] { "Poisson", "Fer" });
        }

        private string Total(int cell)
        {
            return ((Label)totals[0][cell]).text;
        }

        private string Total(int good, int figure)
        {
            return Total(FirstGoodCell + good * CellsPerGood + figure);
        }

        private string Text(int city, int cell)
        {
            return ((Label)rows[city][cell]).text;
        }

        private string Text(int city, int good, int figure)
        {
            return Text(city, FirstGoodCell + good * CellsPerGood + figure);
        }

        private static string Positive(string text)
        {
            return $"<color={DebugCitiesTable.PositiveColor}>{text}</color>";
        }

        private static string Negative(string text)
        {
            return $"<color={DebugCitiesTable.NegativeColor}>{text}</color>";
        }

        [Test]
        public void TheHeader_NamesTheGoods_AndTheirFigures()
        {
            Assert.AreEqual(2, header.childCount);

            // A blank over the city's columns, then one name per good.
            Assert.AreEqual(3, header[0].childCount);
            Assert.AreEqual("Poisson", ((Label)header[0][1]).text);
            Assert.AreEqual("Fer", ((Label)header[0][2]).text);

            Assert.AreEqual(FirstGoodCell + 2 * CellsPerGood, header[1].childCount);
            Assert.AreEqual("Ville", ((Label)header[1][NameCell]).text);
            Assert.AreEqual("Population", ((Label)header[1][PopulationCell]).text);
            Assert.AreEqual("Croiss. / mois", ((Label)header[1][GrowthCell]).text);
            Assert.AreEqual("Croiss. totale", ((Label)header[1][GrowthSinceStartCell]).text);
            Assert.AreEqual("Stock", ((Label)header[1][FirstGoodCell + Stock]).text);
            Assert.AreEqual("Prod.", ((Label)header[1][FirstGoodCell + Production]).text);
            Assert.AreEqual("Conso.", ((Label)header[1][FirstGoodCell + Consumption]).text);
            Assert.AreEqual("Net", ((Label)header[1][FirstGoodCell + CellsPerGood + Net]).text);
        }

        [Test]
        public void AddCity_AddsARow_WithItsNameAndPopulation()
        {
            table.AddCity("Sparia", market);
            table.AddCity("Elforth", village);

            Assert.AreEqual(2, rows.childCount);
            Assert.AreEqual("Sparia", Text(0, NameCell));
            Assert.AreEqual("1\u00A0000", Text(0, PopulationCell));
            Assert.AreEqual("Elforth", Text(1, NameCell));
            Assert.AreEqual("500", Text(1, PopulationCell));
        }

        [Test]
        public void ARow_WritesTheMonthlyGrowth_OfAPopulationThatFoodChanges()
        {
            // Fish is food: 20 barrels for 2 a day is 10 days, between the 7 that keep the
            // population and the 14 that make it grow by 5 % a month.
            CityMarket fed = FedMarket(startingDays: 10);
            CityMarket starved = FedMarket(startingDays: 0);

            table.AddCity("Sparia", fed);
            table.AddCity("Elforth", starved);
            table.AddCity("Hitrun", market);

            Assert.AreEqual(Positive("+2,1\u00A0%"), Text(0, GrowthCell));
            Assert.AreEqual(Negative("-10,0\u00A0%"), Text(1, GrowthCell));

            // A market whose population nothing changes.
            Assert.AreEqual("0,0\u00A0%", Text(2, GrowthCell));
        }

        [Test]
        public void WhileShown_ADayThatStarts_RewritesThePopulation_AndTheTotals()
        {
            CityMarket starved = FedMarket(startingDays: 0);
            table.AddCity("Elforth", starved);
            table.Show();

            for (int day = 0; day < 30; day++)
            {
                starved.StartDay();
            }

            // A month without food: a tenth of the thousand inhabitants is gone.
            Assert.AreEqual("900", Text(0, PopulationCell));
            Assert.AreEqual("900", Total(PopulationCell));
            Assert.AreEqual(Negative("-10,0\u00A0%"), Total(GrowthCell));
        }

        [Test]
        public void ARow_WritesTheGrowthSinceTheStart_AndTheTotalTheOneOfAllTheInhabitants()
        {
            // 1 000 inhabitants without food and 4 000 with plenty of it.
            CityMarket starved = FedMarket(startingDays: 0);
            CityMarket fed = FedMarket(startingDays: 1000, inhabitants: 4000);
            table.AddCity("Elforth", starved);
            table.AddCity("Sparia", fed);

            Assert.AreEqual("0,0\u00A0%", Text(0, GrowthSinceStartCell));
            Assert.AreEqual("0,0\u00A0%", Total(GrowthSinceStartCell));

            table.Show();

            for (int day = 0; day < 30; day++)
            {
                starved.StartDay();
                fed.StartDay();
            }

            // A month: 900 and 4 200 inhabitants, 5 100 for the 5 000 of the start.
            Assert.AreEqual(Negative("-10,0\u00A0%"), Text(0, GrowthSinceStartCell));
            Assert.AreEqual(Positive("+5,0\u00A0%"), Text(1, GrowthSinceStartCell));
            Assert.AreEqual(Positive("+2,0\u00A0%"), Total(GrowthSinceStartCell));
        }

        [Test]
        public void TheTotalGrowth_IsTheOneOfAllTheInhabitantsTogether()
        {
            // 1 000 inhabitants who lose 10 % a month and 4 000 who gain 5 %: +100 of 5 000.
            table.AddCity("Elforth", FedMarket(startingDays: 0));
            table.AddCity("Sparia", FedMarket(startingDays: 14, inhabitants: 4000));

            Assert.AreEqual("5\u00A0000", Total(PopulationCell));
            Assert.AreEqual(Positive("+2,0\u00A0%"), Total(GrowthCell));
        }

        [Test]
        public void FormatRate_WritesAPercentage_SignedAndColoured()
        {
            Assert.AreEqual(Positive("+5,0\u00A0%"), DebugCitiesTable.FormatRate(0.05));
            Assert.AreEqual(Negative("-7,5\u00A0%"), DebugCitiesTable.FormatRate(-0.075));
            Assert.AreEqual("0,0\u00A0%", DebugCitiesTable.FormatRate(0));
            Assert.AreEqual("0,0\u00A0%", DebugCitiesTable.FormatRate(-0.0004));
        }

        // One food, 2 barrels a day per thousand inhabitants, in a city whose population follows it.
        private static CityMarket FedMarket(double startingDays, double inhabitants = 1000)
        {
            return new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0, isFood: true) },
                TestMarkets.Thresholds,
                startingDays,
                TestMarkets.Pricing,
                7,
                new CityPopulation(inhabitants),
                new FoodGrowth(7, 14, 0.05, 0.1, 30));
        }

        [Test]
        public void ARow_WritesTheStock_AndTheNominalFiguresBeforeTheRecentOnes()
        {
            table.AddCity("Sparia", market);

            // Nothing has happened yet: the recent figures are all zero.
            Assert.AreEqual("60", Text(0, Fish, Stock));
            Assert.AreEqual("0,0 (0,0)", Text(0, Fish, Production));
            Assert.AreEqual("14,0 (0,0)", Text(0, Fish, Consumption));
            Assert.AreEqual($"{Negative("-14,0")} (0,0)", Text(0, Fish, Net));

            Assert.AreEqual("21,0 (0,0)", Text(0, Iron, Production));
            Assert.AreEqual("14,0 (0,0)", Text(0, Iron, Consumption));
            Assert.AreEqual($"{Positive("+7,0")} (0,0)", Text(0, Iron, Net));
        }

        [Test]
        public void WhileShown_ADayThatStarts_RewritesTheRow()
        {
            table.AddCity("Sparia", market);
            table.Show();

            market.StartDay();

            Assert.AreEqual("58", Text(0, Fish, Stock));
            Assert.AreEqual("14,0 (2,0)", Text(0, Fish, Consumption));
            Assert.AreEqual($"{Negative("-14,0")} ({Negative("-2,0")})", Text(0, Fish, Net));
            Assert.AreEqual("61", Text(0, Iron, Stock));
            Assert.AreEqual("21,0 (3,0)", Text(0, Iron, Production));
            Assert.AreEqual($"{Positive("+7,0")} ({Positive("+1,0")})", Text(0, Iron, Net));
        }

        [Test]
        public void WhileShown_ATrade_RewritesTheStock()
        {
            table.AddCity("Sparia", market);
            table.Show();

            market.Take(Fish, 25);

            Assert.AreEqual("35", Text(0, Fish, Stock));
        }

        [Test]
        public void BeforeItIsShown_AndOnceHidden_TheTableDoesNotFollowTheMarkets()
        {
            table.AddCity("Sparia", market);

            market.StartDay();
            Assert.AreEqual("60", Text(0, Fish, Stock));

            table.Show();
            table.Hide();
            market.StartDay();

            Assert.AreEqual("58", Text(0, Fish, Stock));
        }

        [Test]
        public void Show_WritesWhatChangedWhileHidden()
        {
            table.AddCity("Sparia", market);
            market.StartDay();

            table.Show();

            Assert.AreEqual("58", Text(0, Fish, Stock));
        }

        [Test]
        public void Show_Twice_FollowsAMarketOnce()
        {
            table.AddCity("Sparia", market);
            table.Show();
            table.Show();
            table.Hide();

            market.StartDay();

            Assert.AreEqual("60", Text(0, Fish, Stock));
        }

        [Test]
        public void ACityAddedWhileShown_IsFollowedToo()
        {
            table.Show();
            table.AddCity("Sparia", market);

            market.StartDay();
            Assert.AreEqual("58", Text(0, Fish, Stock));

            table.Hide();
            market.StartDay();
            Assert.AreEqual("58", Text(0, Fish, Stock));
        }

        [Test]
        public void AMarketWithFewerGoodsThanColumns_LeavesTheExtraCellsEmpty()
        {
            var small = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));

            Assert.DoesNotThrow(() => table.AddCity("Sparia", small));
            Assert.AreEqual("60", Text(0, Fish, Stock));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Text(0, Iron, Stock));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Text(0, Iron, Net));
        }

        [Test]
        public void TheTotalsOfAnEmptyTable_AreZero()
        {
            Assert.AreEqual(1, totals.childCount);
            Assert.AreEqual("Total", Total(NameCell));
            Assert.AreEqual("0", Total(PopulationCell));
            Assert.AreEqual("0,0\u00A0%", Total(GrowthCell));
            Assert.AreEqual("0,0\u00A0%", Total(GrowthSinceStartCell));
            Assert.AreEqual("0", Total(Fish, Stock));
            Assert.AreEqual("0,0 (0,0)", Total(Fish, Production));
            Assert.AreEqual("0,0 (0,0)", Total(Iron, Net));
        }

        [Test]
        public void TheTotals_SumEveryColumnOfTheCities()
        {
            table.AddCity("Sparia", market);
            table.AddCity("Elforth", village);

            Assert.AreEqual("1\u00A0500", Total(PopulationCell));

            Assert.AreEqual("90", Total(Fish, Stock));
            Assert.AreEqual("0,0 (0,0)", Total(Fish, Production));
            Assert.AreEqual("21,0 (0,0)", Total(Fish, Consumption));
            Assert.AreEqual($"{Negative("-21,0")} (0,0)", Total(Fish, Net));

            Assert.AreEqual("75", Total(Iron, Stock));
            Assert.AreEqual("21,0 (0,0)", Total(Iron, Production));
            Assert.AreEqual("17,5 (0,0)", Total(Iron, Consumption));
            Assert.AreEqual($"{Positive("+3,5")} (0,0)", Total(Iron, Net));
        }

        [Test]
        public void WhileShown_TheTotalsFollowEveryMarket()
        {
            table.AddCity("Sparia", market);
            table.AddCity("Elforth", village);
            table.Show();

            market.StartDay();
            village.StartDay();

            // Fish: 58 and 29 left, 2 and 1 consumed. Iron: 61 and 14.5 left, 3 produced, 2 and 0.5 consumed.
            Assert.AreEqual("87", Total(Fish, Stock));
            Assert.AreEqual("21,0 (3,0)", Total(Fish, Consumption));
            Assert.AreEqual($"{Negative("-21,0")} ({Negative("-3,0")})", Total(Fish, Net));
            Assert.AreEqual("75", Total(Iron, Stock));
            Assert.AreEqual("21,0 (3,0)", Total(Iron, Production));
            Assert.AreEqual($"{Positive("+3,5")} ({Positive("+0,5")})", Total(Iron, Net));
        }

        [Test]
        public void OnceHidden_TheTotalsDoNotFollowTheMarkets()
        {
            table.AddCity("Sparia", market);
            table.Show();
            table.Hide();

            market.StartDay();

            Assert.AreEqual("60", Total(Fish, Stock));
        }

        [Test]
        public void TheTotals_LeaveOutAGoodThatAMarketDoesNotHave()
        {
            var small = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));

            table.AddCity("Sparia", market);
            table.AddCity("Elforth", small);

            Assert.AreEqual("120", Total(Fish, Stock));
            Assert.AreEqual("60", Total(Iron, Stock));
            Assert.AreEqual("14,0 (0,0)", Total(Iron, Consumption));
        }

        [Test]
        public void FormatBarrels_WritesOneDecimal_TheFrenchWay()
        {
            Assert.AreEqual("84,0", DebugCitiesTable.FormatBarrels(84));
            Assert.AreEqual("5,6", DebugCitiesTable.FormatBarrels(5.6));
            Assert.AreEqual("1\u00A0234,6", DebugCitiesTable.FormatBarrels(1234.56));
        }

        [Test]
        public void FormatNet_SignsAndColoursWhatIsNotZero()
        {
            Assert.AreEqual(Positive("+42,0"), DebugCitiesTable.FormatNet(42));
            Assert.AreEqual(Negative("-5,6"), DebugCitiesTable.FormatNet(-5.6));
            Assert.AreEqual("0,0", DebugCitiesTable.FormatNet(0));
        }

        [Test]
        public void FormatNet_WritesWhatRoundsToZeroAsZero()
        {
            Assert.AreEqual("0,0", DebugCitiesTable.FormatNet(-0.04));
            Assert.AreEqual("0,0", DebugCitiesTable.FormatNet(0.04));
        }
    }
}
