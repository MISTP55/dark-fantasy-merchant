using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketSectionTests
    {
        private const int Wheat = 0;
        private const int Wine = 1;

        // Children of a row, in order. The two prices are the buttons that trade.
        private const int NameCell = 0;
        private const int StockCell = 1;
        private const int BuyCell = 2;
        private const int SellCell = 3;
        private const int HoldCell = 4;

        private VisualElement sectionElement;
        private VisualElement rows;
        private Label holdLabel;
        private CityMarket market;
        private CargoHold hold;
        private Treasury treasury;
        private CityMarketSection section;

        [SetUp]
        public void SetUp()
        {
            sectionElement = new VisualElement();
            rows = new VisualElement();
            holdLabel = new Label();

            // Wheat: 2 barrels a day, 60 in stock. Wine: not consumed here, none in stock.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(60, 0, 0, 0) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                30,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));
            hold = new CargoHold(200);
            treasury = new Treasury(10000);
            section = new CityMarketSection(sectionElement, rows, holdLabel, new[] { "Blé", "Vin" }, treasury);
        }

        private string Text(int good, int cell)
        {
            return ((TextElement)rows[good][cell]).text;
        }

        private bool IsEnabled(int good, int cell)
        {
            return rows[good][cell].enabledSelf;
        }

        [Test]
        public void BuildsOneRowPerGood_InTheCatalogueOrder()
        {
            Assert.AreEqual(2, rows.childCount);
            Assert.AreEqual("Blé", Text(Wheat, NameCell));
            Assert.AreEqual("Vin", Text(Wine, NameCell));
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
        public void Show_WritesTheStockAndThePrices()
        {
            section.Show(market, null);

            Assert.AreEqual("60", Text(Wheat, StockCell));
            Assert.AreEqual(market.BuyPriceOf(Wheat).ToString(), Text(Wheat, BuyCell));
            Assert.AreEqual(market.SellPriceOf(Wheat).ToString(), Text(Wheat, SellCell));
        }

        [Test]
        public void AGoodOutOfStock_HasNoBuyPrice()
        {
            section.Show(market, hold);

            Assert.AreEqual("0", Text(Wine, StockCell));
            Assert.AreEqual(CityMarketSection.NoPrice, Text(Wine, BuyCell));
            Assert.AreEqual(market.SellPriceOf(Wine).ToString(), Text(Wine, SellCell));
            Assert.IsFalse(IsEnabled(Wine, BuyCell));
        }

        [Test]
        public void Show_WithoutAHold_IsReadOnly()
        {
            section.Show(market, null);

            Assert.IsFalse(sectionElement.ClassListContains(CityMarketSection.TradingClass));
            Assert.AreEqual(string.Empty, holdLabel.text);
            Assert.IsFalse(IsEnabled(Wheat, BuyCell));
            Assert.IsFalse(IsEnabled(Wheat, SellCell));
        }

        [Test]
        public void ThePrices_AreTheButtonsThatTrade()
        {
            Assert.AreEqual(5, rows[Wheat].childCount);
            Assert.IsInstanceOf<Button>(rows[Wheat][BuyCell]);
            Assert.IsInstanceOf<Button>(rows[Wheat][SellCell]);
        }

        [Test]
        public void Show_WithAHold_AllowsTrading()
        {
            hold.TryAdd(Wine, 20);

            section.Show(market, hold);

            Assert.IsTrue(sectionElement.ClassListContains(CityMarketSection.TradingClass));
            Assert.AreEqual("Cale : 20 / 200 tonneaux", holdLabel.text);
            Assert.AreEqual("0", Text(Wheat, HoldCell));
            Assert.AreEqual("20", Text(Wine, HoldCell));
            Assert.IsTrue(IsEnabled(Wheat, BuyCell));
            Assert.IsFalse(IsEnabled(Wheat, SellCell));
            Assert.IsTrue(IsEnabled(Wine, SellCell));
        }

        [Test]
        public void Buy_TradesAndRewritesTheRows()
        {
            section.Show(market, hold);

            section.Buy(Wheat, 10);

            Assert.AreEqual(10, hold.UnitsOf(Wheat));
            Assert.AreEqual("50", Text(Wheat, StockCell));
            Assert.AreEqual("10", Text(Wheat, HoldCell));
            Assert.AreEqual(market.BuyPriceOf(Wheat).ToString(), Text(Wheat, BuyCell));
            Assert.AreEqual("Cale : 10 / 200 tonneaux", holdLabel.text);
            Assert.IsTrue(IsEnabled(Wheat, SellCell));
            Assert.Less(treasury.Gold, 10000);
        }

        [Test]
        public void Sell_TradesAndRewritesTheRows()
        {
            hold.TryAdd(Wine, 20);
            section.Show(market, hold);

            section.Sell(Wine, 100);

            Assert.AreEqual(0, hold.UnitsOf(Wine));
            Assert.AreEqual("20", Text(Wine, StockCell));
            Assert.IsFalse(IsEnabled(Wine, SellCell));
            Assert.Greater(treasury.Gold, 10000);
        }

        [Test]
        public void AFullHold_DisablesBuying()
        {
            hold = new CargoHold(5);
            section.Show(market, hold);

            section.Buy(Wheat, 5);

            Assert.IsFalse(IsEnabled(Wheat, BuyCell));
            Assert.IsTrue(IsEnabled(Wheat, SellCell));
        }

        [Test]
        public void InDebt_DisablesBuying_NotSelling()
        {
            hold.TryAdd(Wheat, 5);
            section.Show(market, hold);
            Assert.IsTrue(IsEnabled(Wheat, BuyCell));

            // The weekly wages, for instance: the section follows the treasury.
            treasury.Withdraw(20000);

            Assert.IsFalse(IsEnabled(Wheat, BuyCell));
            Assert.IsTrue(IsEnabled(Wheat, SellCell));
        }

        [Test]
        public void ADayThatStarts_RewritesTheRows()
        {
            section.Show(market, hold);

            market.StartDay();

            Assert.AreEqual("58", Text(Wheat, StockCell));
        }

        [Test]
        public void Show_WithoutAHold_StopsTrading()
        {
            section.Show(market, hold);
            section.Show(market, null);

            Assert.IsFalse(sectionElement.ClassListContains(CityMarketSection.TradingClass));
            Assert.AreEqual(string.Empty, holdLabel.text);

            // Nothing is traded without a hold, and the old one is no longer listened to.
            section.Buy(Wheat, 10);
            hold.TryAdd(Wheat, 7);

            Assert.AreEqual("60", Text(Wheat, StockCell));
            Assert.AreEqual(string.Empty, holdLabel.text);
            Assert.IsFalse(IsEnabled(Wheat, BuyCell));
            Assert.IsFalse(IsEnabled(Wheat, SellCell));
        }

        [Test]
        public void Hide_StopsListening()
        {
            section.Show(market, hold);
            section.Hide();

            market.StartDay();

            Assert.AreEqual("60", Text(Wheat, StockCell));
        }

        [Test]
        public void WithoutATreasury_TheMarketIsReadOnly()
        {
            var readOnly = new CityMarketSection(
                new VisualElement(), new VisualElement(), new Label(), new[] { "Blé", "Vin" }, null);

            Assert.DoesNotThrow(() => readOnly.Show(market, hold));
            Assert.DoesNotThrow(() => readOnly.Buy(Wheat, 1));
            Assert.AreEqual(0, hold.UnitsOf(Wheat));
        }

        [Test]
        public void AMarketWithFewerGoodsThanRows_LeavesTheExtraRowsEmpty()
        {
            var small = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing,
                7,
                new CityPopulation(1000));

            Assert.DoesNotThrow(() => section.Show(small, hold));
            Assert.AreEqual(CityMarketSection.NoPrice, Text(Wine, BuyCell));
            Assert.IsFalse(IsEnabled(Wine, BuyCell));
            Assert.IsFalse(IsEnabled(Wine, SellCell));
        }

        [TestCase(false, false, 1)]
        [TestCase(true, false, 10)]
        [TestCase(false, true, 100)]
        [TestCase(true, true, 100)]
        public void UnitsFor_FollowsTheModifierKeys(bool shift, bool ctrl, int expected)
        {
            Assert.AreEqual(expected, CityMarketSection.UnitsFor(shift, ctrl));
        }

        [Test]
        public void ABuyPrice_IsDisabled_WithoutRoomForOneUnit()
        {
            // Wheat in bushels of ten barrels: nine barrels of room are not enough for one.
            var bushelHold = new CargoHold(9, new GoodUnits(new[] { 10, 1 }));

            section.Show(market, bushelHold);

            Assert.IsFalse(IsEnabled(Wheat, BuyCell));
        }

        [Test]
        public void FormatPopulation_WritesThousandsSeparators()
        {
            Assert.AreEqual("Population : 6\u00A0000", CityInfoPanelController.FormatPopulation(6000));
            Assert.AreEqual("Population : 0", CityInfoPanelController.FormatPopulation(0));
        }
    }
}
