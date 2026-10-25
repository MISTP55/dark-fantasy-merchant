using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipDockingHarbourTests
    {
        private sealed class Port
        {
        }

        private const float Speed = 4f;

        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;
        private ShipDocking<Port> docking;
        private Harbour harbour;
        private Port port;
        private Port open;
        private List<Ship> ships;
        private List<string> events;

        [SetUp]
        public void SetUp()
        {
            // 40 x 20 world units over 8 x 4 cells, as in ShipDockingTests.
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#..##",
                "...#..#.");
            navigation = new NavigationPathfinder(grid);

            // One berth, crossed in 1 second either way.
            harbour = TestHarbours.OneBerth(2f);
            port = new Port();
            open = new Port();
            docking = new ShipDocking<Port>(where => where == port ? harbour : null);
            ships = new List<Ship>();
            events = new List<string>();
            docking.Docked += (ship, where) => events.Add("docked");
            docking.Undocked += (ship, where) => events.Add("undocked");
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(grid, x, y);
        }

        private Vector2 PortPosition => Cell(5, 0);

        private Ship CreateShip(int x = 1, int y = 0)
        {
            var ship = new Ship(projection, Cell(x, y), Speed, TestCrew.Full, navigation);
            ships.Add(ship);
            return ship;
        }

        // One step of ShipsView: the ships in a city do not sail.
        private void Step(float deltaTime)
        {
            foreach (Ship ship in ships)
            {
                if (!docking.IsInCity(ship))
                {
                    ship.Advance(deltaTime);
                }
            }

            docking.Update(deltaTime);
        }

        private Ship SailToPort(Ship ship)
        {
            Assert.IsTrue(docking.OrderToPort(ship, port, PortPosition));
            ship.Advance(100f);
            docking.Update(0f);
            return ship;
        }

        private Ship DockedShip()
        {
            Ship ship = SailToPort(CreateShip());
            docking.Update(1f);
            Assert.IsTrue(docking.IsDocked(ship));
            events.Clear();
            return ship;
        }

        [Test]
        public void ArrivingAtTheAnchorage_EntersTheGridNotThePort()
        {
            Ship ship = SailToPort(CreateShip());

            Assert.AreEqual(HarbourState.Arriving, docking.StateOf(ship));
            Assert.IsTrue(docking.IsInCity(ship));
            Assert.AreSame(port, docking.CityOf(ship));
            Assert.IsTrue(docking.IsUnderWay(ship));
            Assert.IsFalse(docking.IsDocked(ship));
            Assert.IsNull(docking.PortOf(ship));
            Assert.IsNull(docking.DestinationPortOf(ship));
            Assert.IsEmpty(events);
        }

        [Test]
        public void ReachingTheBerth_Docks()
        {
            Ship ship = SailToPort(CreateShip());

            docking.Update(0.5f);

            Assert.IsEmpty(events);

            docking.Update(0.5f);

            CollectionAssert.AreEqual(new[] { "docked" }, events);
            Assert.AreEqual(HarbourState.Docked, docking.StateOf(ship));
            Assert.AreSame(port, docking.PortOf(ship));
            Assert.IsFalse(docking.IsUnderWay(ship));
            var inPort = new List<Ship>();
            docking.GetShipsIn(port, inPort);
            CollectionAssert.AreEqual(new[] { ship }, inPort);
        }

        [Test]
        public void Full_Queues()
        {
            Ship first = SailToPort(CreateShip());
            Ship second = SailToPort(CreateShip(4, 0));

            Assert.AreEqual(HarbourState.Arriving, docking.StateOf(first));
            Assert.AreEqual(HarbourState.Waiting, docking.StateOf(second));
            Assert.AreSame(port, docking.CityOf(second));
            Assert.IsFalse(docking.IsDocked(second));
            Assert.IsFalse(docking.IsUnderWay(second));
            var inCity = new List<Ship>();
            docking.GetShipsInCity(port, inCity);
            CollectionAssert.AreEqual(new[] { first, second }, inCity);
        }

        [Test]
        public void OrderWhileDocked_LeavesThenSailsAtTheEdge()
        {
            Ship ship = DockedShip();
            Vector2 anchorage = ship.Position;

            Assert.IsTrue(docking.OrderToPoint(ship, Cell(4, 3)));

            CollectionAssert.AreEqual(new[] { "undocked" }, events);
            Assert.AreEqual(HarbourState.Leaving, docking.StateOf(ship));
            Assert.IsTrue(docking.IsInCity(ship));
            Assert.IsTrue(docking.IsUnderWay(ship));
            Assert.IsTrue(ship.IsMoving);

            Step(0.5f);

            Assert.AreEqual(anchorage, ship.Position);
            Assert.IsTrue(docking.IsInCity(ship));

            Step(0.6f);

            Assert.AreEqual(anchorage, ship.Position);
            Assert.IsFalse(docking.IsInCity(ship));
            Assert.AreEqual(HarbourState.AtSea, docking.StateOf(ship));
            Assert.IsTrue(ship.IsMoving);

            Step(1f);

            Assert.AreNotEqual(anchorage, ship.Position);
            Step(100f);
            Assert.AreEqual(Cell(4, 3), ship.Position);
            Assert.AreEqual(harbour.Capacity, harbour.FreeBerths);
            CollectionAssert.AreEqual(new[] { "undocked" }, events);
        }

        [Test]
        public void OrderWhileArriving_TurnsRoundWithoutDocking()
        {
            Ship ship = SailToPort(CreateShip());
            Step(0.5f);

            Assert.IsTrue(docking.OrderToPoint(ship, Cell(4, 3)));

            Assert.AreEqual(HarbourState.Leaving, docking.StateOf(ship));

            Step(0.6f);

            Assert.IsFalse(docking.IsInCity(ship));

            Step(100f);
            docking.Update(10f);

            Assert.AreEqual(Cell(4, 3), ship.Position);
            Assert.IsEmpty(events);
        }

        [Test]
        public void OrderWhileWaiting_SailsAtOnce()
        {
            SailToPort(CreateShip());
            Ship second = SailToPort(CreateShip(4, 0));

            Assert.IsTrue(docking.OrderToPoint(second, Cell(4, 3)));

            Assert.IsFalse(docking.IsInCity(second));
            Assert.AreEqual(HarbourState.AtSea, docking.StateOf(second));
            Assert.IsTrue(docking.IsUnderWay(second));
            Assert.IsFalse(harbour.Contains(second));

            Step(100f);

            Assert.AreEqual(Cell(4, 3), second.Position);
        }

        [Test]
        public void OrderBackToTheCityWhileLeaving_Returns()
        {
            Ship ship = DockedShip();
            docking.OrderToPoint(ship, Cell(4, 3));
            Step(0.5f);

            Assert.IsTrue(docking.OrderToPort(ship, port, PortPosition));

            Assert.AreEqual(HarbourState.Arriving, docking.StateOf(ship));
            Assert.IsFalse(ship.IsMoving);
            Assert.IsNull(docking.DestinationPortOf(ship));

            Step(0.6f);

            Assert.AreEqual(HarbourState.Docked, docking.StateOf(ship));
            CollectionAssert.AreEqual(new[] { "undocked", "docked" }, events);
        }

        [Test]
        public void AnOrderToItsOwnCity_ChangesNothingOutsideLeaving()
        {
            Ship ship = SailToPort(CreateShip());

            Assert.IsTrue(docking.OrderToPort(ship, port, PortPosition));
            Assert.AreEqual(HarbourState.Arriving, docking.StateOf(ship));

            Step(1f);

            Assert.IsTrue(docking.OrderToPort(ship, port, PortPosition));
            Assert.AreEqual(HarbourState.Docked, docking.StateOf(ship));
            CollectionAssert.AreEqual(new[] { "docked" }, events);
        }

        [Test]
        public void AnIgnoredOrder_InACity_ChangesNothing()
        {
            Ship ship = DockedShip();

            Assert.IsFalse(docking.OrderToPoint(ship, new Vector2(float.NaN, 0.5f)));

            Assert.AreEqual(HarbourState.Docked, docking.StateOf(ship));
            Assert.IsEmpty(events);
        }

        [Test]
        public void AnOrderToAnotherPort_WhileDocked_EntersItAfterLeaving()
        {
            Ship ship = DockedShip();

            Assert.IsTrue(docking.OrderToPort(ship, open, Cell(0, 3)));

            Assert.AreSame(open, docking.DestinationPortOf(ship));
            Assert.AreSame(port, docking.CityOf(ship));

            Step(1f);
            Step(100f);

            Assert.AreSame(open, docking.PortOf(ship));
            Assert.AreSame(open, docking.CityOf(ship));
            CollectionAssert.AreEqual(new[] { "undocked", "docked" }, events);
        }

        [Test]
        public void OrderBackAndForth_NeverLeaksABerth()
        {
            Ship first = SailToPort(CreateShip());
            Ship second = SailToPort(CreateShip(4, 0));
            Vector2 away = Cell(4, 3);

            Step(1f);
            docking.OrderToPoint(first, away);
            Step(0.3f);
            docking.OrderToPort(first, port, PortPosition);
            Step(0.3f);
            docking.OrderToPoint(first, away);
            docking.OrderToPoint(second, away);
            docking.OrderToPort(second, port, PortPosition);
            Step(0.2f);
            docking.OrderToPort(first, port, PortPosition);
            Step(0.4f);
            docking.OrderToPoint(first, away);
            Step(0.4f);
            docking.OrderToPort(first, port, PortPosition);

            for (int i = 0; i < 10; i++)
            {
                Step(0.5f);
            }

            Assert.AreEqual(0, harbour.FreeBerths);

            docking.OrderToPoint(first, away);
            docking.OrderToPoint(second, away);

            for (int i = 0; i < 10; i++)
            {
                Step(0.5f);
            }

            Assert.IsFalse(docking.IsInCity(first));
            Assert.IsFalse(docking.IsInCity(second));
            Assert.AreEqual(harbour.Capacity, harbour.FreeBerths);
        }

        [Test]
        public void Remove_WhileArriving_RaisesNoDocked()
        {
            Ship ship = SailToPort(CreateShip());
            var changes = new List<HarbourState>();
            docking.CityStateChanged += (s, where) => changes.Add(docking.StateOf(s));

            docking.Remove(ship);
            docking.Update(5f);

            Assert.IsFalse(docking.IsInCity(ship));
            Assert.IsFalse(harbour.Contains(ship));
            Assert.AreEqual(harbour.Capacity, harbour.FreeBerths);
            CollectionAssert.AreEqual(new[] { HarbourState.AtSea }, changes);
            Assert.IsEmpty(events);
        }

        [Test]
        public void Remove_WhileDocked_Undocks()
        {
            Ship ship = DockedShip();

            docking.Remove(ship);

            Assert.IsFalse(docking.IsDocked(ship));
            Assert.IsFalse(docking.IsInCity(ship));
            CollectionAssert.AreEqual(new[] { "undocked" }, events);
        }

        [Test]
        public void CityStateChanged_RaisedOnEveryChange()
        {
            Ship blocker = DockedShip();
            Ship ship = CreateShip(4, 0);
            var changes = new List<HarbourState>();
            docking.CityStateChanged += (s, where) =>
            {
                Assert.AreSame(port, where);
                if (s == ship)
                {
                    changes.Add(docking.StateOf(s));
                }
            };

            SailToPort(ship);
            docking.OrderToPoint(blocker, Cell(4, 3));
            Step(1.1f);
            Step(1.1f);
            docking.OrderToPoint(ship, Cell(4, 3));
            Step(1.1f);

            CollectionAssert.AreEqual(
                new[]
                {
                    HarbourState.Waiting, HarbourState.Arriving, HarbourState.Docked,
                    HarbourState.Leaving, HarbourState.AtSea,
                },
                changes);
        }

        [Test]
        public void StartingToLeave_IsUndockedBeforeTheStateChangeIsRaised()
        {
            Ship ship = DockedShip();
            bool? dockedWhenRaised = null;
            var inPort = new List<Ship>();
            docking.CityStateChanged += (s, where) =>
            {
                if (s == ship && docking.StateOf(s) == HarbourState.Leaving)
                {
                    dockedWhenRaised = docking.IsDocked(s);
                    docking.GetShipsIn(port, inPort);
                }
            };

            docking.OrderToPoint(ship, Cell(4, 3));

            Assert.AreEqual(false, dockedWhenRaised);
            Assert.IsEmpty(inPort);
        }

        [Test]
        public void NoHarbour_DocksAtOnce()
        {
            Ship ship = CreateShip();
            var changes = new List<HarbourState>();
            docking.CityStateChanged += (s, where) => changes.Add(docking.StateOf(s));

            docking.OrderToPort(ship, open, Cell(0, 3));
            ship.Advance(100f);
            docking.Update(0f);

            Assert.AreSame(open, docking.PortOf(ship));
            Assert.AreSame(open, docking.CityOf(ship));
            Assert.AreEqual(HarbourState.Docked, docking.StateOf(ship));

            docking.OrderToPoint(ship, Cell(4, 3));

            Assert.IsFalse(docking.IsInCity(ship));
            CollectionAssert.AreEqual(new[] { "docked", "undocked" }, events);
            CollectionAssert.AreEqual(new[] { HarbourState.Docked, HarbourState.AtSea }, changes);
        }
    }
}
