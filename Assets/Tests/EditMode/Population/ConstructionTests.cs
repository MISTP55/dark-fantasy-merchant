using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ConstructionTests
    {
        // Good 0 is the material (not produced, stock 60); good 1 is produced. The player has a trading post there
        // unless told otherwise: a workshop needs one.
        private static CityMarket Market(bool withTradingPost = true)
        {
            var market = new CityMarket(new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 2, 30, 0.1) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));
            return WithTradingPost(market, withTradingPost);
        }

        private static CityMarket WithTradingPost(CityMarket market, bool withTradingPost)
        {
            if (withTradingPost)
            {
                market.SetPlayerTradingPost(new Warehouse(2000));
            }

            return market;
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

        private static CityMarket GridMarket(CityGrid grid, bool withTradingPost = true)
        {
            var house = new CityHousingRule(SocialClass.Poor, 0.95, 100, new MaterialCost[0], new Footprint(1, 1));

            return WithTradingPost(new CityMarket(new[] { new MarketGood(10, 2, 0, 0), new MarketGood(10, 2, 30, 0.1) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7,
                new CityPopulation(new PerClass<int>(0, 0, 100), 0, new PerClass<int>(0, 0, 100)), TestMarkets.Labour,
                new[] { house }, null, grid, new RandomPlacement(new System.Random(1))), withTradingPost);
        }

        [Test]
        public void TryBuildHouse_PlacesThePlayersHouse()
        {
            // The house touches the road at (3, 0), which joins the town hall: it is connected and gives its room.
            CityGrid grid = TestMarkets.GridWithTownHall(6, 4);
            grid.PlaceRoads(new[] { new Vector2Int(3, 0) }, BuildingOwner.City);
            CityMarket market = GridMarket(grid);
            var treasury = new Treasury(10000);
            var place = new GridPlace(new Vector2Int(4, 1), BuildingOrientation.Along);

            bool built = Construction.TryBuildHouse(market, HoldWith(3), treasury, Cost(), SocialClass.Poor, 100, new Footprint(1, 1), place);

            Assert.IsTrue(built);
            Assert.AreEqual(1, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(BuildingOwner.Player, grid.BuildingAt(new Vector2Int(4, 1)).Owner);
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
        public void QuoteWorkshop_WithoutTradingPost_IsImpossible()
        {
            CityMarket market = Market(withTradingPost: false);

            Assert.IsFalse(Construction.QuoteWorkshop(market, HoldWith(3), new Treasury(10000), Cost(), 1).IsPossible);
            Assert.IsFalse(Construction.QuoteWorkshop(market, null, new Treasury(10000), Cost(), 1, isFree: true).IsPossible);
        }

        [Test]
        public void TryBuildWorkshop_WithoutTradingPost_TakesNothing()
        {
            CityMarket market = Market(withTradingPost: false);
            var treasury = new Treasury(10000);
            CargoHold hold = HoldWith(3);
            double stock = market.AvailableOf(0);

            Assert.IsFalse(Construction.TryBuildWorkshop(market, hold, treasury, Cost(), 1, 30));

            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(0, market.PlayerWorkshopsOf(1));
        }

        [Test]
        public void TryBuildWorkshop_OnTheGrid_WithoutTradingPost_TakesNothing()
        {
            var grid = new CityGrid(4, 4, new Vector2Int[0]);
            CityMarket market = GridMarket(grid, withTradingPost: false);
            var treasury = new Treasury(10000);
            var place = new GridPlace(new Vector2Int(2, 2), BuildingOrientation.Along);

            Assert.IsFalse(Construction.TryBuildWorkshop(market, HoldWith(3), treasury, Cost(), 1, 5, new Footprint(2, 2), place));
            Assert.IsFalse(Construction.TryBuildWorkshop(market, null, treasury, Cost(), 1, 5, new Footprint(2, 2), place, isFree: true));

            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(0, grid.Buildings.Count);
            Assert.AreEqual(0, market.PlayerWorkshopsOf(1));
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

        // A road costs 50 gold a cell; the grid has a town hall at (0, 0) and roads at (3, 0) and (4, 0).
        private static CityMarket RoadMarket(out CityGrid grid)
        {
            grid = TestMarkets.GridWithTownHall(10, 6);
            CityMarket market = GridMarket(grid);
            market.AddPlayerRoads(new[] { new Vector2Int(3, 0), new Vector2Int(4, 0) });
            return market;
        }

        private static readonly BuildingCost RoadCost = new BuildingCost(50, Array.Empty<MaterialCost>());

        // Five cells along y = 0, the first two already roads.
        private static readonly Vector2Int[] Trace =
        {
            new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0), new Vector2Int(6, 0), new Vector2Int(7, 0),
        };

        [Test]
        public void QuoteRoads_ChargesOnlyNewCells()
        {
            CityMarket market = RoadMarket(out _);

            ConstructionQuote quote = Construction.QuoteRoads(market, null, new Treasury(10000), RoadCost, Trace);

            Assert.AreEqual(3, Construction.NewRoadCellsOf(market, Trace));
            Assert.IsTrue(quote.IsPossible);
            Assert.AreEqual(150, quote.Gold);
        }

        [Test]
        public void QuoteRoads_OnlyExistingRoads_IsImpossible()
        {
            CityMarket market = RoadMarket(out _);
            var existing = new[] { new Vector2Int(4, 0), new Vector2Int(3, 0) };

            Assert.AreEqual(0, Construction.NewRoadCellsOf(market, existing));
            Assert.IsFalse(Construction.QuoteRoads(market, null, new Treasury(10000), RoadCost, existing).IsPossible);
            Assert.IsFalse(Construction.QuoteRoads(market, null, new Treasury(10000), RoadCost, existing, isFree: true).IsPossible);
        }

        [Test]
        public void QuoteRoads_ACellThatCannotHoldARoad_IsImpossible()
        {
            CityMarket market = RoadMarket(out _);
            var underTheHall = new[] { new Vector2Int(5, 0), new Vector2Int(2, 0) };

            Assert.AreEqual(-1, Construction.NewRoadCellsOf(market, underTheHall));
            Assert.AreEqual(-1, Construction.NewRoadCellsOf(market, new[] { new Vector2Int(5, 0), new Vector2Int(10, 0) }));
            Assert.IsFalse(Construction.QuoteRoads(market, null, new Treasury(10000), RoadCost, underTheHall).IsPossible);
        }

        [Test]
        public void QuoteRoadCell_QuotesOneCellWherever_ItIsLaid()
        {
            CityMarket market = RoadMarket(out _);

            ConstructionQuote quote = Construction.QuoteRoadCell(market, null, new Treasury(1000), RoadCost);

            Assert.IsTrue(quote.IsPossible);
            Assert.AreEqual(50, quote.Gold);
            Assert.IsFalse(Construction.QuoteRoadCell(market, null, new Treasury(49), RoadCost).IsPossible);
            Assert.IsTrue(Construction.QuoteRoadCell(market, null, new Treasury(-10), RoadCost, isFree: true).IsPossible);
            Assert.AreEqual(0, Construction.QuoteRoadCell(market, null, new Treasury(-10), RoadCost, isFree: true).Gold);
        }

        [Test]
        public void TryBuildRoads_PaysAndPlacesTheNewCells()
        {
            CityMarket market = RoadMarket(out CityGrid grid);
            var treasury = new Treasury(1000);

            Assert.IsTrue(Construction.TryBuildRoads(market, null, treasury, RoadCost, Trace));

            Assert.AreEqual(850, treasury.Gold);
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(7, 0)));
            Assert.AreEqual(BuildingOwner.Player, grid.BuildingAt(new Vector2Int(7, 0)).Owner);
        }

        [Test]
        public void TryBuildRoads_GoldShort_TakesNothing()
        {
            CityMarket market = RoadMarket(out CityGrid grid);
            var treasury = new Treasury(149);

            Assert.IsFalse(Construction.TryBuildRoads(market, null, treasury, RoadCost, Trace));

            Assert.AreEqual(149, treasury.Gold);
            Assert.IsNull(grid.BuildingAt(new Vector2Int(5, 0)));
            Assert.AreEqual(3, grid.Buildings.Count);
        }

        [Test]
        public void TryBuildRoads_Free_TakesNothingAndBuilds()
        {
            CityMarket market = RoadMarket(out CityGrid grid);
            var treasury = new Treasury(-500);
            CargoHold hold = HoldWith(3);
            int stock = market.AvailableOf(0);
            var cost = new BuildingCost(50, new[] { new MaterialCost(0, 1) });

            Assert.IsTrue(Construction.TryBuildRoads(market, hold, treasury, cost, Trace, isFree: true));

            Assert.AreEqual(-500, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
            Assert.AreEqual(stock, market.AvailableOf(0));
            Assert.AreEqual(6, grid.Buildings.Count);
        }

        [Test]
        public void TryBuildRoads_TakesTheMaterialsOfEveryNewCell()
        {
            CityMarket market = RoadMarket(out _);
            CargoHold hold = HoldWith(2);
            int stock = market.AvailableOf(0);
            var cost = new BuildingCost(50, new[] { new MaterialCost(0, 1) });

            Assert.IsTrue(Construction.TryBuildRoads(market, hold, new Treasury(10000), cost, Trace));

            Assert.AreEqual(0, hold.UnitsOf(0));
            Assert.AreEqual(stock - 1, market.AvailableOf(0));
        }

        private static CityMarket PostMarket(bool withSite)
        {
            var grid = new CityGrid(8, 8, new Vector2Int[0]);

            if (withSite)
            {
                grid.ReserveTradingPostSite(new Footprint(3, 2), new GridPlace(new Vector2Int(1, 1), BuildingOrientation.Along));
            }

            return GridMarket(grid);
        }

        [Test]
        public void MaterialSources_TakesFromTheFirstThenTheSecond()
        {
            var post = new Warehouse(100);
            post.TryAdd(0, 30);
            CargoHold hold = HoldWith(50);
            var sources = new MaterialSources(post, hold);

            Assert.AreEqual(80, sources.UnitsOf(0));
            Assert.IsTrue(sources.TryRemove(0, 60));
            Assert.AreEqual(0, post.UnitsOf(0));
            Assert.AreEqual(20, hold.UnitsOf(0));
        }

        [Test]
        public void MaterialSources_TakesNothing_WhenTheyHoldTooLittle()
        {
            var post = new Warehouse(100);
            post.TryAdd(0, 30);
            CargoHold hold = HoldWith(50);
            var sources = new MaterialSources(post, hold);

            Assert.IsFalse(sources.TryRemove(0, 81));
            Assert.AreEqual(30, post.UnitsOf(0));
            Assert.AreEqual(50, hold.UnitsOf(0));
        }

        [Test]
        public void TryBuildHouse_TakesMaterialsFromThePostThenTheHoldThenBuys()
        {
            CityMarket market = Market();
            var post = new Warehouse(100);
            post.TryAdd(0, 30);
            CargoHold hold = HoldWith(50);
            var treasury = new Treasury(100000);
            BuildingCost cost = Cost(100, 100);
            var sources = new MaterialSources(post, hold);
            ConstructionQuote quote = Construction.QuoteHouse(market, sources, treasury, cost);
            long expected = 100;
            double stock = market.AvailableOf(0);

            for (int i = 0; i < 20; i++)
            {
                expected += market.BuyPriceOf(0, i);
            }

            bool built = Construction.TryBuildHouse(market, sources, treasury, cost, SocialClass.Poor, 100);

            Assert.IsTrue(quote.IsPossible);
            Assert.IsTrue(built);
            Assert.AreEqual(expected, quote.Gold);
            Assert.AreEqual(100000 - expected, treasury.Gold);
            Assert.AreEqual(0, post.UnitsOf(0));
            Assert.AreEqual(0, hold.UnitsOf(0));
            Assert.AreEqual(stock - 20, market.AvailableOf(0));
        }

        [Test]
        public void QuoteTradingPost_IsImpossibleWithoutASiteOrWhenBuilt()
        {
            var treasury = new Treasury(10000);
            CityMarket noSite = PostMarket(false);
            CityMarket withSite = PostMarket(true);

            Assert.IsFalse(Construction.QuoteTradingPost(Market(), null, treasury, Cost()).IsPossible, "no grid");
            Assert.IsFalse(Construction.QuoteTradingPost(noSite, null, treasury, Cost()).IsPossible, "no site");
            Assert.IsTrue(Construction.QuoteTradingPost(withSite, HoldWith(3), treasury, Cost()).IsPossible);

            withSite.Grid.PlaceTradingPost();

            Assert.IsFalse(Construction.QuoteTradingPost(withSite, HoldWith(3), treasury, Cost()).IsPossible, "built");
        }

        [Test]
        public void TryBuildTradingPost_PaysAndPlacesThePost()
        {
            CityMarket market = PostMarket(true);
            var treasury = new Treasury(10000);
            CargoHold hold = HoldWith(3);

            bool built = Construction.TryBuildTradingPost(market, hold, treasury, Cost());

            Assert.IsTrue(built);
            Assert.IsFalse(market.Grid.CanPlaceTradingPost);
            Assert.AreEqual(9900, treasury.Gold);
            Assert.AreEqual(0, hold.UnitsOf(0));
        }

        [Test]
        public void TryBuildTradingPost_Twice_PaysNothingTheSecondTime()
        {
            CityMarket market = PostMarket(true);
            var treasury = new Treasury(10000);
            CargoHold hold = HoldWith(6);
            Construction.TryBuildTradingPost(market, hold, treasury, Cost());

            bool again = Construction.TryBuildTradingPost(market, hold, treasury, Cost());

            Assert.IsFalse(again);
            Assert.AreEqual(9900, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
        }

        [Test]
        public void TryBuildTradingPost_Free_TakesNothing()
        {
            CityMarket market = PostMarket(true);
            var treasury = new Treasury(10000);
            CargoHold hold = HoldWith(3);

            bool built = Construction.TryBuildTradingPost(market, hold, treasury, Cost(), null, true);

            Assert.IsTrue(built);
            Assert.IsFalse(market.Grid.CanPlaceTradingPost);
            Assert.AreEqual(10000, treasury.Gold);
            Assert.AreEqual(3, hold.UnitsOf(0));
        }

        [Test]
        public void TryBuildTradingPost_AsksOnlyWhatTheCostLists()
        {
            // The Game level builds the cost with BuildingDefinition.MaterialsOf, which leaves out a switched-off
            // material (BuildingDefinitionTests); Core asks exactly the materials of the cost it is given: here
            // only good 0, so good 1's stock is untouched.
            CityMarket market = PostMarket(true);
            var treasury = new Treasury(10000);
            CargoHold hold = HoldWith(3);
            double otherStock = market.AvailableOf(1);

            Assert.IsTrue(Construction.TryBuildTradingPost(market, hold, treasury, Cost()));

            Assert.AreEqual(9900, treasury.Gold);
            Assert.AreEqual(otherStock, market.AvailableOf(1));
        }

        [Test]
        public void Times_MultipliesGoldAndMaterials()
        {
            var cost = new BuildingCost(50, new[] { new MaterialCost(0, 3), new MaterialCost(1, 2) });

            BuildingCost times = cost.Times(4);

            Assert.AreEqual(200, times.Gold);
            Assert.AreEqual(2, times.Materials.Count);
            Assert.AreEqual((0, 12), (times.Materials[0].Good, times.Materials[0].Units));
            Assert.AreEqual((1, 8), (times.Materials[1].Good, times.Materials[1].Units));
            Assert.Throws<ArgumentOutOfRangeException>(() => cost.Times(0));
        }

        [Test]
        public void BuildingCost_RejectsAGoodTwice()
        {
            Assert.Throws<ArgumentException>(() =>
                new BuildingCost(0, new[] { new MaterialCost(0, 1), new MaterialCost(0, 2) }));
        }
    }
}
