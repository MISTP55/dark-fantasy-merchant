using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

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

        private static CityMarket GridMarket(CityGrid grid)
        {
            var house = new CityHousingRule(SocialClass.Poor, 0.95, 100, new MaterialCost[0], new Footprint(1, 1));

            return new CityMarket(new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 2, 30, 0.1) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7,
                new CityPopulation(new PerClass<int>(0, 0, 100), 0, new PerClass<int>(0, 0, 100)), TestMarkets.Labour,
                new[] { house }, null, grid, new RandomPlacement(new System.Random(1)));
        }

        [Test]
        public void TryBuildHouse_PlacesThePlayersHouse()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            CityMarket market = GridMarket(grid);
            var treasury = new Treasury(10000);
            var place = new GridPlace(new Vector2Int(1, 1), BuildingOrientation.Along);

            bool built = Construction.TryBuildHouse(market, HoldWith(3), treasury, Cost(), SocialClass.Poor, 100, new Footprint(1, 1), place);

            Assert.IsTrue(built);
            Assert.AreEqual(1, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(BuildingOwner.Player, grid.BuildingAt(new Vector2Int(1, 1)).Owner);
            Assert.AreEqual(200, market.Population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(9900, treasury.Gold);
        }

        [Test]
        public void TryBuildHouse_TakesNothing_WhenThePlaceIsTaken()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            var place = new GridPlace(new Vector2Int(1, 1), BuildingOrientation.Along);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, new Footprint(1, 1), place);
            CityMarket market = GridMarket(grid);
            var treasury = new Treasury(10000);
            CargoHold hold = HoldWith(3);
            double stock = market.AvailableOf(0);

            bool built = Construction.TryBuildHouse(market, hold, treasury, Cost(), SocialClass.Poor, 100, new Footprint(1, 1), place);

            Assert.IsFalse(built);
            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(100, market.Population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(1, grid.Buildings.Count);
        }

        [Test]
        public void TryBuildWorkshop_TakesNothing_WhenThePlaceIsTaken()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            var place = new GridPlace(new Vector2Int(1, 1), BuildingOrientation.Along);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, new Footprint(1, 1), place);
            CityMarket market = GridMarket(grid);
            var treasury = new Treasury(10000);
            CargoHold hold = HoldWith(3);
            double stock = market.AvailableOf(0);

            bool built = Construction.TryBuildWorkshop(market, hold, treasury, Cost(), 1, 5, new Footprint(2, 2), place);

            Assert.IsFalse(built);
            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(0, market.PlayerWorkshopsOf(1));
        }

        [Test]
        public void TryBuildHouse_WithAPlace_ReturnsFalseAndPaysNothing_WhenTheMarketHasNoGrid()
        {
            CityMarket market = Market();
            var treasury = new Treasury(10000);
            CargoHold hold = HoldWith(3);
            double stock = market.AvailableOf(0);
            var place = new GridPlace(Vector2Int.zero, BuildingOrientation.Along);

            bool built = Construction.TryBuildHouse(market, hold, treasury, Cost(), SocialClass.Poor, 100, new Footprint(1, 1), place);

            Assert.IsFalse(built);
            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
        }

        [Test]
        public void PlacelessBuilds_Throw_AndPayNothing_WhenTheMarketHasAGrid()
        {
            CityMarket market = GridMarket(new CityGrid(4, 4, new Vector2Int[0]));
            var treasury = new Treasury(10000);
            CargoHold hold = HoldWith(3);
            double stock = market.AvailableOf(0);

            Assert.Throws<InvalidOperationException>(
                () => Construction.TryBuildHouse(market, hold, treasury, Cost(), SocialClass.Poor, 100));
            Assert.Throws<InvalidOperationException>(
                () => Construction.TryBuildWorkshop(market, hold, treasury, Cost(), 1, 5));

            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(100, market.Population.RoomOf(SocialClass.Poor));
        }

        [Test]
        public void PlacedHouse_OfAnUnknownClass_Throws_AndPaysNothing()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            CityMarket market = GridMarket(grid);
            var treasury = new Treasury(10000);
            var place = new GridPlace(Vector2Int.zero, BuildingOrientation.Along);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => Construction.TryBuildHouse(market, HoldWith(3), treasury, Cost(), (SocialClass)9, 100, new Footprint(1, 1), place));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => market.AddPlayerHouse((SocialClass)9, 100, new Footprint(1, 1), place));

            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(0, grid.Buildings.Count);
        }

        [Test]
        public void TryBuildWorkshop_PlacesA2x2Workshop()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            CityMarket market = GridMarket(grid);
            var place = new GridPlace(new Vector2Int(2, 2), BuildingOrientation.Along);

            bool built = Construction.TryBuildWorkshop(market, HoldWith(3), new Treasury(10000), Cost(), 1, 5, new Footprint(2, 2), place);

            Assert.IsTrue(built);
            PlacedBuilding building = grid.BuildingAt(new Vector2Int(3, 3));
            Assert.IsFalse(building.Kind.IsHouse);
            Assert.AreEqual(1, building.Kind.Good);
            Assert.AreEqual(BuildingOwner.Player, building.Owner);
            Assert.AreEqual(1, market.PlayerWorkshopsOf(1));
        }

        [Test]
        public void TryBuildHouse_Free_TakesNothing_EvenFromATreasuryInDebt()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            CityMarket market = GridMarket(grid);
            CargoHold hold = HoldWith(3);
            var treasury = new Treasury(-500);
            int stock = market.AvailableOf(0);
            int reports = 0;
            var place = new GridPlace(new Vector2Int(1, 1), BuildingOrientation.Along);

            bool built = Construction.TryBuildHouse(market, hold, treasury, Cost(100), SocialClass.Poor, 100, new Footprint(1, 1), place,
                (good, units, gold) => reports++, isFree: true);

            Assert.IsTrue(built);
            Assert.AreEqual(1, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(-500, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(0, reports);
        }

        [Test]
        public void TryBuildHouse_Free_StillNeedsAFreePlace()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            CityMarket market = GridMarket(grid);
            var place = new GridPlace(new Vector2Int(1, 1), BuildingOrientation.Along);
            Construction.TryBuildHouse(market, null, new Treasury(0), Cost(), SocialClass.Poor, 100, new Footprint(1, 1), place, isFree: true);

            bool built = Construction.TryBuildHouse(market, null, new Treasury(0), Cost(), SocialClass.Poor, 100, new Footprint(1, 1), place, isFree: true);

            Assert.IsFalse(built);
            Assert.AreEqual(1, grid.Buildings.Count);
        }

        [Test]
        public void TryBuildWorkshop_Free_TakesNothing_EvenFromATreasuryInDebt()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            CityMarket market = GridMarket(grid);
            CargoHold hold = HoldWith(3);
            var treasury = new Treasury(-500);
            int stock = market.AvailableOf(0);
            int reports = 0;
            var place = new GridPlace(new Vector2Int(2, 2), BuildingOrientation.Along);

            bool built = Construction.TryBuildWorkshop(market, hold, treasury, Cost(1000), 1, 5, new Footprint(2, 2), place,
                (good, units, gold) => reports++, isFree: true);

            Assert.IsTrue(built);
            Assert.AreEqual(1, market.PlayerWorkshopsOf(1));
            Assert.AreEqual(-500, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(0, reports);
        }

        [Test]
        public void TryBuildWorkshop_Free_RefusesAGoodTheCityDoesNotProduce()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            CityMarket market = GridMarket(grid);
            var place = new GridPlace(new Vector2Int(2, 2), BuildingOrientation.Along);

            bool built = Construction.TryBuildWorkshop(market, null, new Treasury(0), Cost(), 0, 5, new Footprint(2, 2), place, isFree: true);

            Assert.IsFalse(built);
            Assert.AreEqual(0, grid.Buildings.Count);
        }

        [Test]
        public void QuoteHouse_Free_IsPossibleAtNoGold_EvenFromATreasuryInDebt()
        {
            ConstructionQuote quote = Construction.QuoteHouse(Market(), null, new Treasury(-1), Cost(1000), isFree: true);

            Assert.IsTrue(quote.IsPossible);
            Assert.AreEqual(0, quote.Gold);
        }

        [Test]
        public void QuoteWorkshop_Free_IsPossibleAtNoGold_OnlyForAProducedGood()
        {
            CityMarket market = Market();

            ConstructionQuote produced = Construction.QuoteWorkshop(market, null, new Treasury(-1), Cost(1000), 1, isFree: true);
            ConstructionQuote notProduced = Construction.QuoteWorkshop(market, null, new Treasury(-1), Cost(1000), 0, isFree: true);

            Assert.IsTrue(produced.IsPossible);
            Assert.AreEqual(0, produced.Gold);
            Assert.IsFalse(notProduced.IsPossible);
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
        public void TryBuildHouse_ReportsTheMaterialsBoughtFromTheMarket()
        {
            CityMarket market = Market();
            long expected = 0;

            for (int i = 0; i < 6; i++)
            {
                expected += market.BuyPriceOf(0, i);
            }

            var reports = new System.Collections.Generic.List<(int good, int units, long gold)>();

            bool built = Construction.TryBuildHouse(market, HoldWith(4), new Treasury(10000), Cost(10), SocialClass.Poor, 100,
                (good, units, gold) => reports.Add((good, units, gold)));

            Assert.IsTrue(built);
            Assert.AreEqual(1, reports.Count);
            Assert.AreEqual((0, 6, expected), reports[0]);
        }

        [Test]
        public void TryBuildHouse_ReportsNothing_WhenTheHoldHoldsEverything()
        {
            int reports = 0;

            Construction.TryBuildHouse(Market(), HoldWith(10), new Treasury(10000), Cost(10), SocialClass.Poor, 100,
                (good, units, gold) => reports++);

            Assert.AreEqual(0, reports);
        }

        [Test]
        public void TryBuildWorkshop_ReportsTheMaterialsBoughtFromTheMarket()
        {
            int units = 0;

            Construction.TryBuildWorkshop(Market(), null, new Treasury(10000), Cost(3), 1, 10,
                (good, bought, gold) => units += bought);

            Assert.AreEqual(3, units);
        }

        [Test]
        public void TryBuild_PaysExactlyTheQuote()
        {
            CityMarket market = Market();
            CargoHold hold = HoldWith(1);
            var treasury = new Treasury(10000);
            int stock = market.AvailableOf(0);
            ConstructionQuote quote = Construction.QuoteHouse(market, hold, treasury, Cost());

            bool built = Construction.TryBuildHouse(market, hold, treasury, Cost(), SocialClass.Poor, 100);

            Assert.IsTrue(built);
            Assert.AreEqual(10000 - quote.Gold, treasury.Gold);
            Assert.AreEqual(0, hold.UnitsOf(0));
            Assert.AreEqual(stock - 2, market.AvailableOf(0));
            Assert.AreEqual(1, market.PlayerHousesOf(SocialClass.Poor));
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
            Assert.IsTrue(Construction.TryBuildHouse(market, hold, treasury, Cost(), SocialClass.Poor, 100));

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
            Assert.IsFalse(Construction.TryBuildHouse(market, hold, treasury, Cost(70), SocialClass.Poor, 100));

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
            Assert.IsFalse(Construction.TryBuildHouse(market, hold, treasury, Cost(), SocialClass.Poor, 100));

            Assert.AreEqual(1, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(110, treasury.Gold);
            Assert.AreEqual(1000, market.Population.Room);
        }

        [Test]
        public void AHouseWithoutMaterials_CostsOnlyItsGold()
        {
            CityMarket market = Market();
            var treasury = new Treasury(10000);
            int stock = market.AvailableOf(0);
            var cost = new BuildingCost(5000, Array.Empty<MaterialCost>());

            Assert.AreEqual(5000, Construction.QuoteHouse(market, null, treasury, cost).Gold);
            Assert.IsTrue(Construction.TryBuildHouse(market, null, treasury, cost, SocialClass.Poor, 100));
            Assert.AreEqual(5000, treasury.Gold);
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(1, market.PlayerHousesOf(SocialClass.Poor));
        }

        [Test]
        public void ATreasuryInDebt_BuildsNothing()
        {
            CityMarket market = Market();
            var free = new BuildingCost(0, new MaterialCost[0]);

            Assert.IsFalse(Construction.QuoteHouse(market, null, new Treasury(-10), free).IsPossible);
            Assert.IsFalse(Construction.TryBuildHouse(market, null, new Treasury(-10), free, SocialClass.Poor, 100));
            Assert.AreEqual(0, market.PlayerHousesOf(SocialClass.Poor));
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
        public void TryBuild_RefusesAHouseWithoutRoomOrAWorkshopWithoutJobs_BeforeTakingAnything()
        {
            CityMarket market = Market();
            CargoHold hold = HoldWith(1);
            var treasury = new Treasury(10000);
            int stock = market.AvailableOf(0);

            Assert.Throws<ArgumentOutOfRangeException>(() => Construction.TryBuildHouse(market, hold, treasury, Cost(), SocialClass.Poor, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Construction.TryBuildWorkshop(market, hold, treasury, Cost(), 1, 0));

            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(1, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(0, market.PlayerHousesOf(SocialClass.Poor));
            Assert.AreEqual(0, market.PlayerWorkshopsOf(1));
        }

        [Test]
        public void BuildingCost_RejectsAGoodTwice()
        {
            Assert.Throws<ArgumentException>(() =>
                new BuildingCost(0, new[] { new MaterialCost(0, 1), new MaterialCost(0, 2) }));
        }
    }
}
