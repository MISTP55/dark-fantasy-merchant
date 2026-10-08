using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipDockingTests
    {
        private sealed class Port
        {
        }

        private const float Speed = 4f;

        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;
        private ShipDocking<Port> docking;
        private Port port;
        private List<string> events;

        [SetUp]
        public void SetUp()
        {
            // 40 x 20 world units over 8 x 4 cells. Cell (7, 0) is a pond of its own.
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#..##",
                "...#..#.");
            navigation = new NavigationPathfinder(grid);
            docking = new ShipDocking<Port>();
            port = new Port();
            events = new List<string>();
            docking.Docked += (ship, where) => events.Add(where == port ? "docked" : "docked elsewhere");
            docking.Undocked += (ship, where) => events.Add(where == port ? "undocked" : "undocked elsewhere");
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(grid, x, y);
        }

        private Ship CreateShip()
        {
            return new Ship(projection, Cell(1, 0), Speed, navigation);
        }

        private Ship CreateDockedShip()
        {
            Ship ship = CreateShip();
            docking.OrderToPort(ship, port, Cell(5, 0));
            ship.Advance(100f);
            docking.Update();
            events.Clear();
            return ship;
        }

        private List<Ship> ShipsIn(Port where)
        {
            var ships = new List<Ship>();
            docking.GetShipsIn(where, ships);
            return ships;
        }

        [Test]
        public void AShipOrderedToAPort_SailsThere_AndIsNotDockedUnderWay()
        {
            Ship ship = CreateShip();

            bool accepted = docking.OrderToPort(ship, port, Cell(5, 0));
            ship.Advance(1f);
            docking.Update();

            Assert.IsTrue(accepted);
            Assert.IsTrue(ship.IsMoving);
            TestAssert.AreEqual(Cell(5, 0), ship.Destination.Value);
            Assert.IsFalse(docking.IsDocked(ship));
            Assert.IsNull(docking.PortOf(ship));
            Assert.AreSame(port, docking.DestinationPortOf(ship));
            Assert.IsEmpty(events);
        }

        [Test]
        public void AShipThatArrives_EntersThePort()
        {
            Ship ship = CreateShip();
            docking.OrderToPort(ship, port, Cell(5, 0));

            ship.Advance(100f);
            docking.Update();
            docking.Update();

            Assert.IsTrue(docking.IsDocked(ship));
            Assert.AreSame(port, docking.PortOf(ship));
            Assert.IsNull(docking.DestinationPortOf(ship));
            CollectionAssert.AreEqual(new[] { ship }, ShipsIn(port));
            CollectionAssert.AreEqual(new[] { "docked" }, events);
        }

        [Test]
        public void APortOnLand_IsEnteredFromTheNearestWater()
        {
            Ship ship = CreateShip();

            // In the wall, nearer its west side.
            bool accepted = docking.OrderToPort(ship, port, GridAssert.CellPoint(grid, 3.2f, 0.5f));
            ship.Advance(100f);
            docking.Update();

            Assert.IsTrue(accepted);
            Assert.AreEqual(Cell(2, 0), ship.Position);
            Assert.AreSame(port, docking.PortOf(ship));
        }

        [Test]
        public void AShipAlreadyAtThePort_EntersItAtTheNextUpdate()
        {
            Ship ship = CreateShip();

            bool accepted = docking.OrderToPort(ship, port, ship.Position);

            Assert.IsTrue(accepted);
            Assert.IsFalse(docking.IsDocked(ship));

            docking.Update();

            Assert.AreSame(port, docking.PortOf(ship));
        }

        [Test]
        public void AnOrderToAPoint_UnderWay_CancelsTheEntry()
        {
            Ship ship = CreateShip();
            docking.OrderToPort(ship, port, Cell(5, 0));
            ship.Advance(1f);

            docking.OrderToPoint(ship, Cell(0, 3));
            ship.Advance(100f);
            docking.Update();

            Assert.AreEqual(Cell(0, 3), ship.Position);
            Assert.IsFalse(docking.IsDocked(ship));
            Assert.IsNull(docking.DestinationPortOf(ship));
            Assert.IsEmpty(events);
        }

        [Test]
        public void AnOrderToThePointOfAPort_DoesNotEnterIt()
        {
            Ship ship = CreateShip();

            docking.OrderToPoint(ship, Cell(5, 0));
            ship.Advance(100f);
            docking.Update();

            Assert.AreEqual(Cell(5, 0), ship.Position);
            Assert.IsFalse(docking.IsDocked(ship));
        }

        [Test]
        public void APortInAnotherSea_IsNotEntered_AndTheShipSailsAsNearAsItCan()
        {
            Ship ship = CreateShip();

            bool accepted = docking.OrderToPort(ship, port, Cell(7, 0));

            Assert.IsFalse(accepted);
            Assert.IsTrue(ship.IsMoving);
            Assert.IsNull(docking.DestinationPortOf(ship));

            ship.Advance(100f);
            docking.Update();

            Assert.IsFalse(docking.IsDocked(ship));
            Assert.IsEmpty(events);
        }

        [Test]
        public void APortWhoseNearestWaterIsInAnotherSea_IsNotEntered()
        {
            Ship ship = CreateShip();

            // On land, nearer the pond than the sea the ship is in.
            bool accepted = docking.OrderToPort(ship, port, GridAssert.CellPoint(grid, 6.8f, 0.5f));
            ship.Advance(100f);
            docking.Update();

            Assert.IsFalse(accepted);
            Assert.IsFalse(docking.IsDocked(ship));
        }

        [Test]
        public void AnOrderToAnotherPort_UnderWay_ReplacesTheDestinationPort()
        {
            Ship ship = CreateShip();
            var other = new Port();
            docking.OrderToPort(ship, port, Cell(5, 0));
            ship.Advance(1f);

            docking.OrderToPort(ship, other, Cell(0, 3));
            ship.Advance(100f);
            docking.Update();

            Assert.AreSame(other, docking.PortOf(ship));
            Assert.IsEmpty(ShipsIn(port));
            CollectionAssert.AreEqual(new[] { "docked elsewhere" }, events);
        }

        [Test]
        public void AShipOrderedAwayByAListener_WhileAnotherDocks_StaysAtSea()
        {
            var first = new Ship(projection, Cell(4, 0), Speed, navigation);
            var second = new Ship(projection, Cell(6, 3), Speed, navigation);
            docking.OrderToPort(first, port, Cell(5, 0));
            docking.OrderToPort(second, port, Cell(5, 0));
            first.Advance(100f);
            second.Advance(100f);

            // Whichever docks first sends the other away, in the same update.
            docking.Docked += (docked, where) =>
                docking.OrderToPoint(ReferenceEquals(docked, first) ? second : first, Cell(0, 3));
            docking.Update();

            Assert.AreEqual(1, ShipsIn(port).Count);
            Assert.AreNotEqual(docking.IsDocked(first), docking.IsDocked(second));
        }

        [Test]
        public void ADockedShip_OrderedToAPoint_LeavesThePort()
        {
            Ship ship = CreateDockedShip();

            bool accepted = docking.OrderToPoint(ship, Cell(4, 3));

            Assert.IsTrue(accepted);
            Assert.IsFalse(docking.IsDocked(ship));
            Assert.IsEmpty(ShipsIn(port));
            Assert.IsTrue(ship.IsMoving);
            CollectionAssert.AreEqual(new[] { "undocked" }, events);

            ship.Advance(100f);
            docking.Update();

            Assert.AreEqual(Cell(4, 3), ship.Position);
            Assert.IsFalse(docking.IsDocked(ship));
        }

        [Test]
        public void ADockedShip_OrderedToItsOwnPort_StaysThere()
        {
            Ship ship = CreateDockedShip();

            bool accepted = docking.OrderToPort(ship, port, Cell(5, 0));
            docking.Update();

            Assert.IsTrue(accepted);
            Assert.AreSame(port, docking.PortOf(ship));
            Assert.IsFalse(ship.IsMoving);
            Assert.IsEmpty(events);
        }

        [Test]
        public void ADockedShip_OrderedToAnotherPort_LeavesAndEntersTheOther()
        {
            Ship ship = CreateDockedShip();
            var other = new Port();

            docking.OrderToPort(ship, other, Cell(0, 3));

            Assert.IsFalse(docking.IsDocked(ship));
            Assert.AreSame(other, docking.DestinationPortOf(ship));

            ship.Advance(100f);
            docking.Update();

            Assert.AreSame(other, docking.PortOf(ship));
            Assert.IsEmpty(ShipsIn(port));
            CollectionAssert.AreEqual(new[] { ship }, ShipsIn(other));
            CollectionAssert.AreEqual(new[] { "undocked", "docked elsewhere" }, events);
        }

        [Test]
        public void AnIgnoredOrder_LeavesADockedShipInItsPort()
        {
            Ship ship = CreateDockedShip();
            var nowhere = new Vector2(float.NaN, 0.5f);

            Assert.IsFalse(docking.OrderToPoint(ship, nowhere));
            Assert.IsFalse(docking.OrderToPort(ship, new Port(), nowhere));

            Assert.AreSame(port, docking.PortOf(ship));
            Assert.IsEmpty(events);
        }

        [Test]
        public void AnIgnoredOrder_UnderWay_KeepsTheDestinationPort()
        {
            Ship ship = CreateShip();
            docking.OrderToPort(ship, port, Cell(5, 0));

            docking.OrderToPoint(ship, new Vector2(float.NaN, 0.5f));
            ship.Advance(100f);
            docking.Update();

            Assert.AreSame(port, docking.PortOf(ship));
        }

        [Test]
        public void ShipsInAPort_AreListedInOrderOfArrival()
        {
            Ship first = CreateDockedShip();
            var far = new Ship(projection, Cell(0, 3), Speed, navigation);
            var near = new Ship(projection, Cell(4, 0), Speed, navigation);
            docking.OrderToPort(far, port, Cell(5, 0));
            docking.OrderToPort(near, port, Cell(5, 0));

            near.Advance(2f);
            far.Advance(2f);
            docking.Update();
            far.Advance(100f);
            docking.Update();

            CollectionAssert.AreEqual(new[] { first, near, far }, ShipsIn(port));
        }

        [Test]
        public void WithoutAPathfinder_AShipEntersAPortAtItsPosition()
        {
            var ship = new Ship(projection, new Vector2(0.1f, 0.5f), Speed);

            Assert.IsTrue(docking.OrderToPort(ship, port, new Vector2(0.4f, 0.5f)));

            ship.Advance(100f);
            docking.Update();

            Assert.AreSame(port, docking.PortOf(ship));
        }
    }
}
