using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ConstructionTests
    {
        // Good 0 is the material (not produced, stock 60); good 1 is produced.
        private static CityMarket Market()
        {
            return new CityMarket(new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 2, 30, 0.1) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));
        }

        private static BuildingCost Cost(int units = 3, long gold = 100)
        {
            return new BuildingCost(gold, new[] { new MaterialCost(0, units) });
        }

        private static CargoHold HoldWith(int units)
        {
            var hold = new CargoHold(200);

            if (units > 0)
            {
                hold.TryAdd(0, units);
            }

            return hold;
        }

        [Test]
        public void Quote_TakesTheHoldFirst_ThenPricesWhatIsBought()
        {
            CityMarket market = Market();

            ConstructionQuote quote = Construction.QuoteHouse(market, HoldWith(1), new Treasury(10000), Cost());

            Assert.IsTrue(quote.IsPossible);
            Assert.AreEqual(100 + market.BuyPriceOf(0, 0) + market.BuyPriceOf(0, 1), quote.Gold);
        }

        [Test]
        public void TryBuild_PaysExactlyTheQuote()
        {
            CityMarket market = Market();
            CargoHold hold = HoldWith(1);
            var treasury = new Treasury(10000);
            int stock = market.AvailableOf(0);
            ConstructionQuote quote = Construction.QuoteHouse(market, hold, treasury, Cost());

            bool built = Construction.TryBuildHouse(market, hold, treasury, Cost(), 100);

            Assert.IsTrue(built);
            Assert.AreEqual(10000 - quote.Gold, treasury.Gold);
            Assert.AreEqual(0, hold.UnitsOf(0));
            Assert.AreEqual(stock - 2, market.AvailableOf(0));
            Assert.AreEqual(1, market.PlayerHouses);
            Assert.AreEqual(1100, market.Population.Room);
        }

        [Test]
        public void AHoldWithMoreThanNeeded_GivesOnlyWhatIsNeeded()
        {
            CityMarket market = Market();
            CargoHold hold = HoldWith(5);
            var treasury = new Treasury(10000);
            int stock = market.AvailableOf(0);

            Assert.AreEqual(100, Construction.QuoteHouse(market, hold, treasury, Cost()).Gold);
            Assert.IsTrue(Construction.TryBuildHouse(market, hold, treasury, Cost(), 100));

            Assert.AreEqual(2, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
        }

        [Test]
        public void WithoutAShip_EverythingIsBought()
        {
            CityMarket market = Market();

            ConstructionQuote quote = Construction.QuoteHouse(market, null, new Treasury(10000), Cost());

            Assert.AreEqual(100 + market.BuyPriceOf(0, 0) + market.BuyPriceOf(0, 1) + market.BuyPriceOf(0, 2), quote.Gold);
        }

        [Test]
        public void WhenHoldAndMarketLack_NothingIsTaken()
        {
            CityMarket market = Market();
            CargoHold hold = HoldWith(5);
            var treasury = new Treasury(10000);
            int stock = market.AvailableOf(0);

            Assert.IsFalse(Construction.QuoteHouse(market, hold, treasury, Cost(70)).IsPossible);
            Assert.IsFalse(Construction.TryBuildHouse(market, hold, treasury, Cost(70), 100));

            Assert.AreEqual(5, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(1000, market.Population.Room);
        }

        [Test]
        public void WhenTheGoldLacks_NothingIsTaken()
        {
            CityMarket market = Market();
            CargoHold hold = HoldWith(1);
            var treasury = new Treasury(110);
            int stock = market.AvailableOf(0);

            Assert.IsFalse(Construction.QuoteHouse(market, hold, treasury, Cost()).IsPossible);
            Assert.IsFalse(Construction.TryBuildHouse(market, hold, treasury, Cost(), 100));

            Assert.AreEqual(1, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(110, treasury.Gold);
            Assert.AreEqual(1000, market.Population.Room);
        }

        [Test]
        public void ATreasuryInDebt_BuildsNothing()
        {
            CityMarket market = Market();
            var free = new BuildingCost(0, new MaterialCost[0]);

            Assert.IsFalse(Construction.QuoteHouse(market, null, new Treasury(-10), free).IsPossible);
            Assert.IsFalse(Construction.TryBuildHouse(market, null, new Treasury(-10), free, 100));
            Assert.AreEqual(0, market.PlayerHouses);
        }

        [Test]
        public void AWorkshop_AddsItsJobs()
        {
            CityMarket market = Market();

            Assert.IsTrue(Construction.TryBuildWorkshop(market, HoldWith(3), new Treasury(10000), Cost(), 1, 30));

            Assert.AreEqual(1, market.PlayerWorkshopsOf(1));
            Assert.AreEqual(30, market.PlayerJobsOf(1));
        }

        [Test]
        public void AWorkshopForAGoodNotProduced_IsImpossible()
        {
            CityMarket market = Market();
            CargoHold hold = HoldWith(3);
            var treasury = new Treasury(10000);

            Assert.IsFalse(Construction.QuoteWorkshop(market, hold, treasury, Cost(), 0).IsPossible);
            Assert.IsFalse(Construction.TryBuildWorkshop(market, hold, treasury, Cost(), 0, 30));

            Assert.AreEqual(3, hold.UnitsOf(0));
            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(0, market.PlayerWorkshopsOf(0));
        }

        [Test]
        public void BuildingCost_RejectsAGoodTwice()
        {
            Assert.Throws<ArgumentException>(() =>
                new BuildingCost(0, new[] { new MaterialCost(0, 1), new MaterialCost(0, 2) }));
        }
    }
}
