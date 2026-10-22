using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugGoodMarketTableTests
    {
        private const int Fish = 0;
        private const int Iron = 1;

        // Children of a row, in order.
        private const int NameCell = 0;
        private const int StockCell = 1;
        private const int T0Cell = 2;
        private const int T3Cell = 5;
        private const int BuyCell = 6;
        private const int SellCell = 7;
        private const int ProductionCell = 8;
        private const int InputsCell = 10;
        private const int NetCell = 11;
        private const int WorkersCell = 12;
        private const int UnderWayCell = 13;
        private const int Cells = 14;

        private VisualElement header;
        private VisualElement rows;
        private VisualElement totals;
        private CityMarket market;
        private CityMarket village;
        private DebugGoodMarketTable table;

        [SetUp]
        public void SetUp()
        {
            header = new VisualElement();
            rows = new VisualElement();
            totals = new VisualElement();

            // A thousand inhabitants. Fish: 2 barrels a day, not produced, 60 in stock.
            // Iron: 2 barrels a day, and a full workshop of 30 workers making 3; 60 in stock.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 30, 0.1) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));

            // A village of 500 that has only fish: 1 barrel a day, 30 in stock.
            village = new CityMarket(new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, TestMarkets.Workforce, 30,
                TestMarkets.Pricing, 7, new CityPopulation(500));

            table = new DebugGoodMarketTable(header, rows, totals);
            table.AddCity("Lübeck", village, good => 0);
            table.AddCity("Cologne", market, good => good == Fish ? 40 : 0);
            table.SetGood(Fish);
            table.Show();
        }

        private string Text(int row, int cell)
        {
            return ((Label)rows[row][cell]).text;
        }

        private string Total(int cell)
        {
            return ((Label)totals[0][cell]).text;
        }

        [Test]
        public void TheHeader_NamesTheColumns()
        {
            Assert.AreEqual(Cells, header[0].childCount);
            Assert.AreEqual("Ville", ((Label)header[0][NameCell]).text);
            Assert.AreEqual("T0", ((Label)header[0][T0Cell]).text);
            Assert.AreEqual("Achat", ((Label)header[0][BuyCell]).text);
            Assert.AreEqual("Conso.", ((Label)header[0][InputsCell - 1]).text);
            Assert.AreEqual("Intr.", ((Label)header[0][InputsCell]).text);
            Assert.AreEqual("Net", ((Label)header[0][NetCell]).text);
            Assert.AreEqual("En route (IA)", ((Label)header[0][UnderWayCell]).text);
        }

        [Test]
        public void GoodMarket_WritesThresholdsAndPrices()
        {
            // Cologne before Lübeck.
            Assert.AreEqual("Cologne", Text(0, NameCell));
            Assert.AreEqual("60", Text(0, StockCell));
            Assert.AreEqual(DebugCitiesTable.FormatQuantity(market.ThresholdsOf(Fish).T0), Text(0, T0Cell));
            Assert.AreEqual(DebugCitiesTable.FormatQuantity(market.ThresholdsOf(Fish).T3), Text(0, T3Cell));
            Assert.AreEqual(TreasuryHudController.FormatNumber(market.BuyPriceOf(Fish)), Text(0, BuyCell));
            Assert.AreEqual(TreasuryHudController.FormatNumber(market.SellPriceOf(Fish)), Text(0, SellCell));
        }

        [Test]
        public void GoodMarket_WritesUnderWay()
        {
            Assert.AreEqual("40", Text(0, UnderWayCell));
            Assert.AreEqual("0", Text(1, UnderWayCell));
            Assert.AreEqual("40", Total(UnderWayCell));
        }

        [Test]
        public void GoodMarket_WritesWorkersAndProduction_OfTheChosenGood()
        {
            table.SetGood(Iron);

            Assert.AreEqual(DebugCitiesTable.FormatWorkers(30, 30), Text(0, WorkersCell));
            Assert.AreEqual(
                $"{DebugCitiesTable.FormatQuantity(market.NominalWeeklyProductionOf(Iron))} ({DebugCitiesTable.FormatQuantity(0)})",
                Text(0, ProductionCell));
        }

        [Test]
        public void GoodMarket_AGoodTheMarketLacks_IsADash()
        {
            table.SetGood(Iron);

            for (int cell = StockCell; cell < Cells; cell++)
            {
                Assert.AreEqual(DebugCitiesTable.NoFigure, Text(1, cell), $"cell {cell}");
            }
        }

        [Test]
        public void GoodMarket_TotalsHaveNoThresholdNorPrice()
        {
            Assert.AreEqual("Total", Total(NameCell));
            Assert.AreEqual("90", Total(StockCell));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Total(T0Cell));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Total(BuyCell));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Total(SellCell));
            Assert.AreEqual(DebugCitiesTable.FormatNet(-21) + " (" + DebugCitiesTable.FormatNet(0) + ")", Total(NetCell));
        }

        [Test]
        public void GoodMarket_WritesTheInputs_AndANetThatTakesThemOff()
        {
            // A smithy whose iron workshop takes half a barrel of fish for every barrel it makes: 10.5 a week.
            var smithy = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 30, 0.1).WithInputs(new[] { new MarketInput(Fish, 0.5) }) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));
            var alone = new DebugGoodMarketTable(new VisualElement(), rows = new VisualElement(), totals = new VisualElement());
            alone.AddCity("Sparia", smithy, good => 0);
            alone.SetGood(Fish);
            alone.Show();

            Assert.AreEqual("10,5 (0,0)", Text(0, InputsCell));
            Assert.AreEqual(DebugCitiesTable.FormatNet(-24.5) + " (" + DebugCitiesTable.FormatNet(0) + ")", Text(0, NetCell));
            Assert.AreEqual("10,5 (0,0)", Total(InputsCell));
            Assert.AreEqual(DebugCitiesTable.FormatNet(-24.5) + " (" + DebugCitiesTable.FormatNet(0) + ")", Total(NetCell));

            smithy.StartDay();

            Assert.AreEqual("10,5 (1,5)", Text(0, InputsCell));
            Assert.AreEqual(DebugCitiesTable.FormatNet(-24.5) + " (" + DebugCitiesTable.FormatNet(-3.5) + ")", Text(0, NetCell));
        }

        [Test]
        public void GoodMarket_FollowsTheMarkets_WhileShown()
        {
            market.Take(Fish, 10);

            Assert.AreEqual("50", Text(0, StockCell));
        }
    }
}
