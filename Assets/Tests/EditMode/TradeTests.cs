using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TradeTests
    {
        private const int Good = 0;

        private CityMarket market;
        private CargoHold hold;
        private Treasury treasury;

        [SetUp]
        public void SetUp()
        {
            // One good at base price 10, two barrels a day: 60 in stock, the target.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0) }, 30, new MarketPricing(2.5, 0.5, 0.05, 0.05));
            hold = new CargoHold(200);
            treasury = new Treasury(10000);
        }

        [Test]
        public void Buy_MovesABarrelAndItsPrice()
        {
            long price = market.BuyPriceOf(Good);

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 1);

            Assert.AreEqual(1, result.Barrels);
            Assert.AreEqual(price, result.Gold);
            Assert.AreEqual(59, market.AvailableOf(Good));
            Assert.AreEqual(1, hold.BarrelsOf(Good));
            Assert.AreEqual(10000 - price, treasury.Gold);
        }

        [Test]
        public void Buy_PricesEveryBarrelAtTheStockItLeaves()
        {
            long first = market.BuyPriceOf(Good);
            long expected = 0;

            for (int i = 0; i < 30; i++)
            {
                expected += market.BuyPriceOf(Good, i);
            }

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 30);

            Assert.AreEqual(30, result.Barrels);
            Assert.AreEqual(expected, result.Gold);
            Assert.Greater(result.Gold, 30 * first);
            Assert.AreEqual(10000 - expected, treasury.Gold);
        }

        [Test]
        public void Buy_StopsAtTheStock()
        {
            TradeResult result = Trade.Buy(market, hold, treasury, Good, 100);

            Assert.AreEqual(60, result.Barrels);
            Assert.AreEqual(0, market.AvailableOf(Good));
            Assert.AreEqual(60, hold.BarrelsOf(Good));
        }

        [Test]
        public void Buy_StopsAtTheHold()
        {
            hold = new CargoHold(5);

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 100);

            Assert.AreEqual(5, result.Barrels);
            Assert.AreEqual(0, hold.Free);
            Assert.AreEqual(55, market.AvailableOf(Good));
        }

        [Test]
        public void Buy_StopsAtTheGold()
        {
            long two = market.BuyPriceOf(Good, 0) + market.BuyPriceOf(Good, 1);
            long three = two + market.BuyPriceOf(Good, 2);
            treasury = new Treasury(three - 1);

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 100);

            Assert.AreEqual(2, result.Barrels);
            Assert.AreEqual(two, result.Gold);
            Assert.AreEqual(three - 1 - two, treasury.Gold);
            Assert.IsFalse(treasury.IsInDebt);
        }

        [Test]
        public void Buy_InDebt_BuysNothing()
        {
            treasury = new Treasury(-50);

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 10);

            Assert.AreEqual(0, result.Barrels);
            Assert.AreEqual(0, result.Gold);
            Assert.AreEqual(-50, treasury.Gold);
            Assert.AreEqual(60, market.AvailableOf(Good));
        }

        [Test]
        public void Sell_MovesBarrelsAndDepositsTheirPrice()
        {
            hold.TryAdd(Good, 5);
            long expected = market.SellPriceOf(Good, 0) + market.SellPriceOf(Good, 1) + market.SellPriceOf(Good, 2);

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 3);

            Assert.AreEqual(3, result.Barrels);
            Assert.AreEqual(expected, result.Gold);
            Assert.AreEqual(2, hold.BarrelsOf(Good));
            Assert.AreEqual(63, market.AvailableOf(Good));
            Assert.AreEqual(10000 + expected, treasury.Gold);
        }

        [Test]
        public void Sell_StopsAtTheBarrelsAboard()
        {
            hold.TryAdd(Good, 2);

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 100);

            Assert.AreEqual(2, result.Barrels);
            Assert.AreEqual(0, hold.BarrelsOf(Good));
        }

        [Test]
        public void Sell_InDebt_StillDeposits()
        {
            treasury = new Treasury(-50);
            hold.TryAdd(Good, 10);

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 10);

            Assert.AreEqual(10, result.Barrels);
            Assert.AreEqual(-50 + result.Gold, treasury.Gold);
            Assert.Greater(result.Gold, 0);
        }

        [Test]
        public void Sell_PaysLessForEveryBarrel()
        {
            hold.TryAdd(Good, 100);
            long first = market.SellPriceOf(Good);

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 100);

            Assert.Less(result.Gold, 100 * first);
        }

        [Test]
        public void BuyingAndSellingBack_LosesGold()
        {
            Trade.Buy(market, hold, treasury, Good, 20);
            Trade.Sell(market, hold, treasury, Good, 20);

            Assert.Less(treasury.Gold, 10000);
            Assert.AreEqual(60, market.AvailableOf(Good));
        }

        [Test]
        public void ATrade_ChangesTheMarketAndTheHoldOnce()
        {
            int marketChanges = 0;
            int holdChanges = 0;
            market.Changed += () => marketChanges++;
            hold.Changed += () => holdChanges++;

            Trade.Buy(market, hold, treasury, Good, 10);
            Assert.AreEqual(1, marketChanges);
            Assert.AreEqual(1, holdChanges);

            Trade.Sell(market, hold, treasury, Good, 10);
            Assert.AreEqual(2, marketChanges);
            Assert.AreEqual(2, holdChanges);
        }

        [Test]
        public void ATradeOfNothing_ChangesNothing()
        {
            int changes = 0;
            market.Changed += () => changes++;
            hold.Changed += () => changes++;
            treasury.Changed += gold => changes++;

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 5);

            Assert.AreEqual(0, result.Barrels);
            Assert.AreEqual(0, changes);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsAQuantityThatIsNotPositive(int barrels)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Trade.Buy(market, hold, treasury, Good, barrels));
            Assert.Throws<ArgumentOutOfRangeException>(() => Trade.Sell(market, hold, treasury, Good, barrels));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => Trade.Buy(null, hold, treasury, Good, 1));
            Assert.Throws<ArgumentNullException>(() => Trade.Buy(market, null, treasury, Good, 1));
            Assert.Throws<ArgumentNullException>(() => Trade.Sell(market, hold, null, Good, 1));
        }
    }
}
