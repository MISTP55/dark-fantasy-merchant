using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class AiMerchantFleetTests
    {
        private sealed class Port
        {
        }

        private const int Fish = 0;
        private const float Speed = 4f;
        private const double Stopover = 30d;

        // Ports: A where the surplus is, B near it, C far from both.
        private const int A = 0;
        private const int B = 1;
        private const int C = 2;

        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;
        private ShipDocking<Port> docking;
        private Port[] ports;
        private Vector2[] positions;
        private CityMarket[] markets;
        private AiMerchantFleet<Port> fleet;

        [SetUp]
        public void SetUp()
        {
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#..##",
                "...#..#.");
            navigation = new NavigationPathfinder(grid);
            docking = new ShipDocking<Port>();
            ports = new[] { new Port(), new Port(), new Port() };
            positions = new[]
            {
                GridAssert.CellCenter(grid, 0, 3), GridAssert.CellCenter(grid, 5, 0), GridAssert.CellCenter(grid, 0, 0),
            };
        }

        /// <summary>One good, 2 barrels a day, a target of 60 barrels.</summary>
        private static CityMarket Market(double days)
        {
            return new CityMarket(
                new[] { new MarketGood(5, 10, 25, 2, 0, 0) }, 30, days, new MarketPricing(0.05, 0.05), 7, new CityPopulation(1000));
        }

        private void CreateFleet(double daysInA, double daysInB, double daysInC)
        {
            markets = new[] { Market(daysInA), Market(daysInB), Market(daysInC) };

            var days = new double[3, 3];
            days[A, B] = days[B, A] = 1d;
            days[A, C] = days[C, A] = 2d;
            days[B, C] = days[C, B] = 2d;

            fleet = new AiMerchantFleet<Port>(
                docking, ports, positions, markets, new TradeRoutePlanner(markets, days, 1d), Stopover);
        }

        private Ship AddShip(int port)
        {
            var ship = new Ship(projection, positions[port], Speed, TestCrew.Full, navigation, new CargoHold(200));
            fleet.Add(ship, port);
            docking.Update();
            return ship;
        }

        private void Arrive(Ship ship)
        {
            ship.Advance(1000f);
            docking.Update();
        }

        [Test]
        public void AShipAdded_EntersItsStartPort()
        {
            CreateFleet(60, 0, 30);

            Ship ship = AddShip(A);

            Assert.AreSame(ports[A], docking.PortOf(ship));
            Assert.IsTrue(fleet.Contains(ship));
            Assert.AreEqual(1, fleet.Count);
            Assert.AreEqual(0, fleet.ProfitOf(ship));
        }

        [Test]
        public void DuringTheStopover_ItStaysInPort()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);

            fleet.Advance(Stopover - 1d);

            Assert.IsTrue(docking.IsDocked(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
        }

        [Test]
        public void AfterTheStopover_ItBuysAndSailsToTheShortage()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);

            fleet.Advance(Stopover - 1d);
            fleet.Advance(1d);

            Assert.IsFalse(docking.IsDocked(ship));
            Assert.AreSame(ports[B], docking.DestinationPortOf(ship));
            Assert.Greater(ship.Cargo.Used, 0);
            Assert.AreEqual(120 - ship.Cargo.Used, markets[A].AvailableOf(Fish));
            Assert.Less(fleet.ProfitOf(ship), 0);
        }

        [Test]
        public void OnArrival_ItSellsItsWholeHold()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);
            fleet.Advance(Stopover);
            int carried = ship.Cargo.Used;

            Arrive(ship);

            Assert.AreSame(ports[B], docking.PortOf(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.AreEqual(carried, markets[B].AvailableOf(Fish));
            Assert.Greater(fleet.ProfitOf(ship), 0);
        }

        [Test]
        public void OnArrival_ItSellsEvenAtALoss()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);
            fleet.Advance(Stopover);

            // Someone else filled the port meanwhile.
            markets[B].Put(Fish, 1000);
            Arrive(ship);

            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.Less(fleet.ProfitOf(ship), 0);
        }

        [Test]
        public void ALongStep_BuysOnce()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);
            int purchases = 0;
            markets[A].Changed += () => purchases++;

            fleet.Advance(Stopover * 10d);
            fleet.Advance(Stopover * 10d);

            // One good, so one purchase changes the market once.
            Assert.AreEqual(1, purchases);
            Assert.AreSame(ports[B], docking.DestinationPortOf(ship));
            Assert.AreEqual(120 - ship.Cargo.Used, markets[A].AvailableOf(Fish));
        }

        [Test]
        public void WithoutARoute_ItWaitsAndAsksAgain()
        {
            CreateFleet(30, 30, 30);
            Ship ship = AddShip(A);

            fleet.Advance(Stopover);
            Assert.IsTrue(docking.IsDocked(ship));

            // A surplus appears: not acted on before the next stopover has passed.
            markets[A].Put(Fish, 200);
            fleet.Advance(Stopover - 1d);
            Assert.IsTrue(docking.IsDocked(ship));

            fleet.Advance(1d);
            Assert.IsFalse(docking.IsDocked(ship));
            Assert.Greater(ship.Cargo.Used, 0);
        }

        [Test]
        public void WithNothingToBuyHere_ItSailsEmptyToTheBestSource()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(B);

            fleet.Advance(Stopover);

            Assert.AreSame(ports[A], docking.DestinationPortOf(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.AreEqual(0, fleet.ProfitOf(ship));
        }

        [Test]
        public void TwoShips_DoNotAimAtTheSameShortage()
        {
            // Enough in A for both; B and C both lack it.
            CreateFleet(300, 0, 0);
            Ship first = AddShip(A);
            Ship second = AddShip(A);

            fleet.Advance(Stopover);

            Assert.AreSame(ports[B], docking.DestinationPortOf(first));
            Assert.AreSame(ports[C], docking.DestinationPortOf(second));
        }

        [Test]
        public void ItSailsEmptyToAFarBetterSource_RatherThanMakeASmallTrade()
        {
            // B has a little more than it needs, which C lacks: a small trade. A has far
            // more to sell to C, and is a day away.
            CreateFleet(300, 35, 0);
            Ship ship = AddShip(B);

            fleet.Advance(Stopover);

            Assert.AreSame(ports[A], docking.DestinationPortOf(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.AreEqual(70, markets[B].AvailableOf(Fish));
        }

        [Test]
        public void TwoEmptyShips_DoNotSailToTheSameSource()
        {
            // Nothing to buy in B: A is the best port to trade from, C the next.
            CreateFleet(60, 0, 30);
            Ship first = AddShip(B);
            Ship second = AddShip(B);

            fleet.Advance(Stopover);

            Assert.AreSame(ports[A], docking.DestinationPortOf(first));
            Assert.AreSame(ports[C], docking.DestinationPortOf(second));
            Assert.AreEqual(0, second.Cargo.Used);
        }

        [Test]
        public void AShipThatIsNotOfTheFleet_IsLeftAlone()
        {
            CreateFleet(60, 0, 30);
            var stranger = new Ship(projection, positions[A], Speed, TestCrew.Full, navigation, new CargoHold(200));
            stranger.Cargo.TryAdd(Fish, 10);

            docking.OrderToPort(stranger, ports[B], positions[B]);
            Arrive(stranger);
            fleet.Advance(Stopover * 3d);

            Assert.IsFalse(fleet.Contains(stranger));
            Assert.AreEqual(0, fleet.ProfitOf(stranger));
            Assert.AreEqual(10, stranger.Cargo.Used);
            Assert.IsTrue(docking.IsDocked(stranger));
        }

        [Test]
        public void AShipIdleAtSea_IsSentBackToThePortItLeft()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);
            fleet.Advance(Stopover);

            // An order that is not the fleet's leaves it at sea, with no port to enter.
            docking.OrderToPoint(ship, GridAssert.CellCenter(grid, 2, 3));
            ship.Advance(1000f);
            docking.Update();
            Assert.IsFalse(docking.IsDocked(ship));

            fleet.Advance(1d);

            Assert.AreSame(ports[A], docking.DestinationPortOf(ship));
        }

        [Test]
        public void ARemovedShip_NoLongerTrades()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);

            Assert.IsTrue(fleet.Remove(ship));
            fleet.Advance(Stopover * 3d);

            Assert.IsFalse(fleet.Contains(ship));
            Assert.AreEqual(0, fleet.Count);
            Assert.IsTrue(docking.IsDocked(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.AreEqual(120, markets[A].AvailableOf(Fish));
        }

        [Test]
        public void ARemovedShip_NoLongerSellsWhenItDocks()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);
            ship.Cargo.TryAdd(Fish, 5);

            fleet.Remove(ship);
            docking.OrderToPort(ship, ports[B], positions[B]);
            Arrive(ship);

            Assert.AreSame(ports[B], docking.PortOf(ship));
            Assert.AreEqual(5, ship.Cargo.Used);
            Assert.AreEqual(0, markets[B].AvailableOf(Fish));
            Assert.AreEqual(0, fleet.ProfitOf(ship));
        }

        [Test]
        public void ARemovedShip_NoLongerKeepsTheOthersFromAPort()
        {
            // As in TwoEmptyShips_DoNotSailToTheSameSource, where the second ship is left C.
            CreateFleet(60, 0, 30);
            Ship first = AddShip(B);
            Ship second = AddShip(B);

            // The first sails empty to A, but not for the fleet any more.
            fleet.Remove(first);
            docking.OrderToPort(first, ports[A], positions[A]);
            fleet.Advance(Stopover);

            Assert.AreSame(ports[A], docking.DestinationPortOf(second));
        }

        [Test]
        public void Remove_OfAShipThatIsNotOfTheFleet_IsRefused()
        {
            CreateFleet(60, 0, 30);
            var stranger = new Ship(projection, positions[A], Speed, TestCrew.Full, navigation, new CargoHold(200));

            Assert.IsFalse(fleet.Remove(stranger));
            Assert.IsFalse(fleet.Remove(null));
        }
    }
}
