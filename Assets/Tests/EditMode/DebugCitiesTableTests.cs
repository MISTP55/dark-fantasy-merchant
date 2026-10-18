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

        // Children of a row of the city's columns, in order: the city, its population, its beggars, its housing,
        // its growth since the start. A row of the goods' columns has five cells per good.
        private const int NameCell = 0;
        private const int PopulationCell = 1;
        private const int BeggarsCell = 2;
        private const int HousingCell = 3;
        private const int GrowthSinceStartCell = 4;
        private const int CityCells = 5;
        private const int CellsPerGood = 5;

        // The goods' columns in alphabetical order: "Fer" before "Poisson".
        private const int IronColumn = 0;
        private const int FishColumn = 1;
        private const int Stock = 0;
        private const int Production = 1;
        private const int Consumption = 2;
        private const int Net = 3;
        private const int Workers = 4;

        private VisualElement header;
        private VisualElement rows;
        private VisualElement totals;
        private VisualElement goodHeader;
        private VisualElement goodRows;
        private VisualElement goodTotals;
        private CityMarket market;
        private CityMarket village;
        private DebugCitiesTable table;

        [SetUp]
        public void SetUp()
        {
            header = new VisualElement();
            rows = new VisualElement();
            totals = new VisualElement();
            goodHeader = new VisualElement();
            goodRows = new VisualElement();
            goodTotals = new VisualElement();

            // A thousand inhabitants. Fish: 2 barrels a day, not produced, 60 in stock.
            // Iron: 2 barrels a day, and a full workshop of 30 workers making 3; 60 in stock.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 30, 0.1) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                30,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));

            // A village of 500: 1 barrel of fish a day, 30 in stock, and half a barrel of iron, 15 in stock.
            village = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 1, 0, 0) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                30,
                TestMarkets.Pricing,
                7,
                new CityPopulation(500));
            table = new DebugCitiesTable(header, rows, totals, goodHeader, goodRows, goodTotals, new[] { "Poisson", "Fer" });
        }

        private string Total(int cell)
        {
            return ((Label)totals[0][cell]).text;
        }

        private string Total(int good, int figure)
        {
            return ((Label)goodTotals[0][ColumnOf(good) * CellsPerGood + figure]).text;
        }

        private string Text(int city, int cell)
        {
            return ((Label)rows[city][cell]).text;
        }

        private string Text(int city, int good, int figure)
        {
            return ((Label)goodRows[city][ColumnOf(good) * CellsPerGood + figure]).text;
        }

        private static int ColumnOf(int good)
        {
            return good == Iron ? IronColumn : FishColumn;
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
            Assert.AreEqual(2, goodHeader.childCount);

            // A blank over the city's columns, one name per good over theirs.
            Assert.AreEqual(1, header[0].childCount);
            Assert.AreEqual(2, goodHeader[0].childCount);
            Assert.AreEqual("Fer", ((Label)goodHeader[0][IronColumn]).text);
            Assert.AreEqual("Poisson", ((Label)goodHeader[0][FishColumn]).text);

            Assert.AreEqual(CityCells, header[1].childCount);
            Assert.AreEqual("Ville", ((Label)header[1][NameCell]).text);
            Assert.AreEqual("Population", ((Label)header[1][PopulationCell]).text);
            Assert.AreEqual("Mendiants", ((Label)header[1][BeggarsCell]).text);
            Assert.AreEqual("Logements", ((Label)header[1][HousingCell]).text);
            Assert.AreEqual("Croiss. totale", ((Label)header[1][GrowthSinceStartCell]).text);

            Assert.AreEqual(2 * CellsPerGood, goodHeader[1].childCount);
            Assert.AreEqual("Stock", ((Label)goodHeader[1][Stock]).text);
            Assert.AreEqual("Prod.", ((Label)goodHeader[1][Production]).text);
            Assert.AreEqual("Conso.", ((Label)goodHeader[1][Consumption]).text);
            Assert.AreEqual("Net", ((Label)goodHeader[1][FishColumn * CellsPerGood + Net]).text);
            Assert.AreEqual("Ouv.", ((Label)goodHeader[1][Workers]).text);
        }

        [Test]
        public void AddCity_AddsARow_WithItsNameAndPopulation()
        {
            table.AddCity("Elforth", village);
            table.AddCity("Sparia", market);

            Assert.AreEqual(2, rows.childCount);
            Assert.AreEqual(2, goodRows.childCount);
            Assert.AreEqual(CityCells, rows[0].childCount);
            Assert.AreEqual(2 * CellsPerGood, goodRows[0].childCount);
            Assert.AreEqual("Elforth", Text(0, NameCell));
            Assert.AreEqual("500", Text(0, PopulationCell));
            Assert.AreEqual("Sparia", Text(1, NameCell));
            Assert.AreEqual("1\u00A0000", Text(1, PopulationCell));
        }

        [Test]
        public void ACityInsertedBetweenOthers_RestripesBothSidesOfTheRows()
        {
            table.AddCity("Sparia", market);
            table.AddCity("Elforth", village);
            table.AddCity("Kivar", FedMarket(startingDays: 0, new CityPopulation(700)));

            for (int i = 0; i < 3; i++)
            {
                bool isAlternate = i % 2 == 1;
                Assert.AreEqual(isAlternate, rows[i].ClassListContains(ListStripes.AlternateClass), $"row {i}");
                Assert.AreEqual(isAlternate, goodRows[i].ClassListContains(ListStripes.AlternateClass), $"goods of row {i}");
            }
        }

        [Test]
        public void TheCities_AreInAlphabeticalOrder_AccentsAndCaseAside()
        {
            CityMarket edimbourg = FedMarket(startingDays: 0, new CityPopulation(700));
            table.AddCity("Sparia", market);
            table.AddCity("elforth", village);
            table.AddCity("Édimbourg", edimbourg);

            Assert.AreEqual("Édimbourg", Text(0, NameCell));
            Assert.AreEqual("elforth", Text(1, NameCell));
            Assert.AreEqual("Sparia", Text(2, NameCell));

            // The goods' columns of a row are the same city's.
            Assert.AreEqual("700", Text(0, PopulationCell));
            Assert.AreEqual("0", Text(0, Fish, Stock));
            Assert.AreEqual("30", Text(1, Fish, Stock));
            Assert.AreEqual("60", Text(2, Fish, Stock));
        }

        [Test]
        public void WhileShown_ADayThatStarts_RewritesThePopulation_AndTheTotals()
        {
            CityMarket city = FedMarket(startingDays: 0, new CityPopulation(900, 100, 1000));
            table.AddCity("Elforth", city);
            table.Show();

            city.Population.SetBeggars(0);
            city.StartDay();

            Assert.AreEqual("900", Text(0, PopulationCell));
            Assert.AreEqual("900", Total(PopulationCell));
        }

        [Test]
        public void ARow_WritesTheGrowthSinceTheStart_AndTheTotalTheOneOfAllTheInhabitants()
        {
            CityMarket shrinking = FedMarket(startingDays: 0, new CityPopulation(900, 100, 1000));
            CityMarket growing = FedMarket(startingDays: 0, new CityPopulation(1000, 0, 1100));
            table.AddCity("Elforth", shrinking);
            table.AddCity("Sparia", growing);

            Assert.AreEqual("0,0\u00A0%", Text(0, GrowthSinceStartCell));
            Assert.AreEqual("0,0\u00A0%", Total(GrowthSinceStartCell));

            table.Show();

            shrinking.Population.SetBeggars(0);
            growing.Population.SetBeggars(50);
            shrinking.StartDay();
            growing.StartDay();

            // 900 and 1 050 inhabitants, 1 950 for the 2 000 of the start.
            Assert.AreEqual(Negative("-10,0\u00A0%"), Text(0, GrowthSinceStartCell));
            Assert.AreEqual(Positive("+5,0\u00A0%"), Text(1, GrowthSinceStartCell));
            Assert.AreEqual(Negative("-2,5\u00A0%"), Total(GrowthSinceStartCell));
        }

        [Test]
        public void FormatRate_WritesAPercentage_SignedAndColoured()
        {
            Assert.AreEqual(Positive("+5,0\u00A0%"), DebugCitiesTable.FormatRate(0.05));
            Assert.AreEqual(Negative("-7,5\u00A0%"), DebugCitiesTable.FormatRate(-0.075));
            Assert.AreEqual("0,0\u00A0%", DebugCitiesTable.FormatRate(0));
            Assert.AreEqual("0,0\u00A0%", DebugCitiesTable.FormatRate(-0.0004));
        }

        // One good, 2 barrels a day per thousand inhabitants, in a city with the given population.
        private static CityMarket FedMarket(double startingDays, CityPopulation population)
        {
            return new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                startingDays,
                TestMarkets.Pricing,
                7,
                population);
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
                new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));

            Assert.DoesNotThrow(() => table.AddCity("Sparia", small));
            Assert.AreEqual("60", Text(0, Fish, Stock));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Text(0, Iron, Stock));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Text(0, Iron, Net));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Text(0, Iron, Workers));
        }

        [Test]
        public void TheTotalsOfAnEmptyTable_AreZero()
        {
            Assert.AreEqual(1, totals.childCount);
            Assert.AreEqual(1, goodTotals.childCount);
            Assert.AreEqual("Total", Total(NameCell));
            Assert.AreEqual("0", Total(PopulationCell));
            Assert.AreEqual("0,0\u00A0%", Total(GrowthSinceStartCell));
            Assert.AreEqual("0", Total(Fish, Stock));
            Assert.AreEqual("0,0 (0,0)", Total(Fish, Production));
            Assert.AreEqual("0,0 (0,0)", Total(Iron, Net));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Total(Fish, Workers));
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
                new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));

            table.AddCity("Sparia", market);
            table.AddCity("Elforth", small);

            Assert.AreEqual("120", Total(Fish, Stock));
            Assert.AreEqual("60", Total(Iron, Stock));
            Assert.AreEqual("14,0 (0,0)", Total(Iron, Consumption));
        }

        [Test]
        public void ARow_WritesTheWorkersOfEveryWorkshop()
        {
            table.AddCity("Sparia", market);

            // Fish is not produced; iron has a full workshop of 30.
            Assert.AreEqual(DebugCitiesTable.NoFigure, Text(0, Fish, Workers));
            Assert.AreEqual("30/30", Text(0, Iron, Workers));
        }

        [Test]
        public void WhileShown_TheWorkersFollowTheMarket()
        {
            table.AddCity("Sparia", market);
            table.Show();
            market.Put(Iron, 100);

            market.StartDay();

            // 158 iron once consumed, above T3: the workshop keeps one worker.
            Assert.AreEqual("1/30", Text(0, Iron, Workers));
        }

        [Test]
        public void TheTotals_SumTheWorkersAndTheirMaxima()
        {
            var other = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 40, 0.1) },
                TestMarkets.Thresholds,
                new WorkforceRule(16, 8, 4, 1, 1, 10),
                30,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));

            table.AddCity("Sparia", market);
            table.AddCity("Elforth", other);

            Assert.AreEqual("40/70", Total(Iron, Workers));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Total(Fish, Workers));
        }

        [Test]
        public void FormatBeggars_WritesTheBeggarsAndTheirTarget()
        {
            Assert.AreEqual("59 (62)", DebugCitiesTable.FormatBeggars(59, 62));
            Assert.AreEqual("1 393 (1 480)", DebugCitiesTable.FormatBeggars(1393, 1480));
        }

        [Test]
        public void FormatHousing_WritesTheHousedOverTheRoom()
        {
            Assert.AreEqual("1\u00A0008 / 1\u00A0100", DebugCitiesTable.FormatHousing(1008, 1100));
        }

        [Test]
        public void FormatHousing_AddsTheCityHouses_WhenThereAreSome()
        {
            Assert.AreEqual("1\u00A0008 / 1\u00A0100", DebugCitiesTable.FormatHousing(1008, 1100, 0));
            Assert.AreEqual("1\u00A0108 / 1\u00A0200 (+1)", DebugCitiesTable.FormatHousing(1108, 1200, 1));
        }

        [Test]
        public void FormatWorkers_AddsTheReserve_WhenThereIsOne()
        {
            Assert.AreEqual("45/60 +10", DebugCitiesTable.FormatWorkers(45, 60, 10));
            Assert.AreEqual("45/60", DebugCitiesTable.FormatWorkers(45, 60));
            Assert.AreEqual(DebugCitiesTable.NoFigure, DebugCitiesTable.FormatWorkers(0, 0, 0));
        }

        // A thousand housed in a room of 1 100, 100 beggars. Iron is stocked above T3: the workshop lays off to 1 worker.
        private static CityMarket LabourMarket()
        {
            return new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 2, 60, 0.1) },
                TestMarkets.Thresholds,
                new WorkforceRule(16, 8, 4, 1, 1, 10),
                100,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000, 100, 1100),
                TestMarkets.Labour);
        }

        [Test]
        public void ARow_WritesTheBeggarsTheirTarget_TheHousing_AndAReserveAfterALayoff()
        {
            CityMarket city = LabourMarket();
            table.AddCity("Sparia", city);
            table.Show();

            city.StartDay();

            Assert.Greater(city.ReserveOf(Iron), 0);
            Assert.AreEqual(
                DebugCitiesTable.FormatBeggars(city.Population.Beggars, city.BeggarTarget), Text(0, BeggarsCell));
            Assert.AreEqual(
                DebugCitiesTable.FormatHousing(city.Population.Housed, city.Population.Room), Text(0, HousingCell));
            Assert.AreEqual(
                $"{city.WorkersOf(Iron)}/{city.MaxWorkersOf(Iron)} +{city.ReserveOf(Iron)}", Text(0, Iron, Workers));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Text(0, Fish, Workers));
        }

        [Test]
        public void TheTotalRow_SumsTheBeggarsAndTheirTargets_TheHousedAndTheRoom_AndTheReserves()
        {
            CityMarket first = LabourMarket();
            CityMarket second = LabourMarket();
            table.AddCity("Sparia", first);
            table.AddCity("Elforth", second);
            table.Show();

            first.StartDay();
            second.StartDay();

            Assert.AreEqual(
                DebugCitiesTable.FormatBeggars(
                    first.Population.Beggars + second.Population.Beggars, first.BeggarTarget + second.BeggarTarget),
                Total(BeggarsCell));
            Assert.AreEqual(
                DebugCitiesTable.FormatHousing(
                    first.Population.Housed + second.Population.Housed, first.Population.Room + second.Population.Room),
                Total(HousingCell));
            Assert.AreEqual(
                $"{first.WorkersOf(Iron) + second.WorkersOf(Iron)}/{first.MaxWorkersOf(Iron) + second.MaxWorkersOf(Iron)}"
                + $" +{first.ReserveOf(Iron) + second.ReserveOf(Iron)}",
                Total(Iron, Workers));
        }

        [Test]
        public void FormatQuantity_WritesOneDecimal_TheFrenchWay()
        {
            Assert.AreEqual("84,0", DebugCitiesTable.FormatQuantity(84));
            Assert.AreEqual("5,6", DebugCitiesTable.FormatQuantity(5.6));
            Assert.AreEqual("1\u00A0234,6", DebugCitiesTable.FormatQuantity(1234.56));
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
