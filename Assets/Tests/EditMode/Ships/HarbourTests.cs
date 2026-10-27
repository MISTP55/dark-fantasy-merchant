using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class HarbourTests
    {
        private const float Speed = 2f;

        // The port's water at x 1..4, y 5..6; the sea below it, edge row y 0. Berth 0 at (3, 2) is 2 cells from the edge,
        // berth 1 at (2, 1) is 1 cell from it.
        private static readonly Vector2Int FarBerth = new Vector2Int(3, 2);
        private static readonly Vector2Int NearBerth = new Vector2Int(2, 1);

        private MapProjection projection;
        private CityGrid grid;
        private Harbour harbour;
        private List<string> events;

        [SetUp]
        public void SetUp()
        {
            projection = new MapProjection(40f, 2f);
            grid = TestHarbours.FromRows(
                "........",
                ".~~~~...",
                ".~~~~...",
                "~~~~~~~~",
                "~~~~~~~~",
                "~~~~~~~~",
                "~~~~~~~~",
                "~~~~~~~~");
            // The port's water is at y 5 and 6, the sea below it.
            TestHarbours.PortSouth(grid, 1, 5);
            grid.PlaceBerth(FarBerth);
            grid.PlaceBerth(NearBerth);
            harbour = new Harbour(grid, Speed);
            events = new List<string>();
            harbour.Entered += ship => events.Add("entered");
            harbour.Berthed += ship => events.Add("berthed");
            harbour.Exited += ship => events.Add("exited");
        }

        private Ship CreateShip()
        {
            return new Ship(projection, Vector2.zero, 4f, TestCrew.Full, null);
        }

        private Vector2 PositionOf(Ship ship)
        {
            Assert.IsTrue(harbour.TryGetPosition(ship, out Vector2 point));
            return point;
        }

        [Test]
        public void Constructor_UnusableGrid_Throws()
        {
            CityGrid noWater = TestHarbours.FromRows("....", "....");

            Assert.Throws<System.ArgumentException>(() => new Harbour(noWater, Speed));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Harbour(grid, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Harbour(grid, float.PositiveInfinity));
        }

        [Test]
        public void Admit_FreeBerth_ArrivesFromTheEdge()
        {
            Ship ship = CreateShip();

            HarbourState state = harbour.Admit(ship);

            Assert.AreEqual(HarbourState.Arriving, state);
            Assert.AreEqual(HarbourState.Arriving, harbour.StateOf(ship));
            Assert.AreEqual(new Vector2(2f, 0f), PositionOf(ship));
            Assert.AreEqual(1, harbour.FreeBerths);
        }

        [Test]
        public void Admit_ChoosesTheBerthNearestTheEdge()
        {
            Ship first = CreateShip();
            Ship second = CreateShip();

            harbour.Admit(first);
            harbour.Admit(second);
            harbour.Advance(100f);

            Assert.AreEqual(new Vector2(NearBerth.x, NearBerth.y), PositionOf(first));
            Assert.AreEqual(new Vector2(FarBerth.x, FarBerth.y), PositionOf(second));
        }

        [Test]
        public void Admit_Full_Queues()
        {
            harbour.Admit(CreateShip());
            harbour.Admit(CreateShip());
            Ship third = CreateShip();

            HarbourState state = harbour.Admit(third);

            Assert.AreEqual(HarbourState.Waiting, state);
            Assert.AreEqual(0, harbour.FreeBerths);
            Assert.IsFalse(harbour.TryGetPosition(third, out _));
            Assert.IsTrue(harbour.Contains(third));
            Assert.Throws<System.InvalidOperationException>(() => harbour.Admit(third));
        }

        [Test]
        public void Advance_ReachesTheBerth_DocksAndRaisesBerthed()
        {
            Ship ship = CreateShip();
            harbour.Admit(ship);

            harbour.Advance(0.5f);

            Assert.AreEqual(HarbourState.Docked, harbour.StateOf(ship));
            Assert.AreEqual(new Vector2(NearBerth.x, NearBerth.y), PositionOf(ship));
            CollectionAssert.AreEqual(new[] { "berthed" }, events);
        }

        [Test]
        public void Advance_StepLongerThanThePath_StopsAtTheBerth()
        {
            Ship ship = CreateShip();
            harbour.Admit(ship);

            harbour.Advance(60f);

            Assert.AreEqual(HarbourState.Docked, harbour.StateOf(ship));
            Assert.AreEqual(new Vector2(NearBerth.x, NearBerth.y), PositionOf(ship));
        }

        [Test]
        public void Advance_MovesTwoCellsPerSecond()
        {
            harbour.Admit(CreateShip());
            Ship ship = CreateShip();
            harbour.Admit(ship);

            harbour.Advance(0.5f);

            // The second ship took the far berth, whose edge is at (3, 0): one cell along it.
            Assert.AreEqual(HarbourState.Arriving, harbour.StateOf(ship));
            Assert.AreEqual(3f, PositionOf(ship).x, 1e-4f);
            Assert.AreEqual(1f, PositionOf(ship).y, 1e-4f);
        }

        [Test]
        public void Leave_FromDocked_ExitsAndFreesTheBerth()
        {
            Ship ship = CreateShip();
            harbour.Admit(ship);
            harbour.Advance(10f);
            events.Clear();

            harbour.Leave(ship);
            Assert.AreEqual(HarbourState.Leaving, harbour.StateOf(ship));
            Assert.AreEqual(1, harbour.FreeBerths);
            harbour.Advance(10f);

            CollectionAssert.AreEqual(new[] { "exited" }, events);
            Assert.AreEqual(HarbourState.AtSea, harbour.StateOf(ship));
            Assert.IsFalse(harbour.Contains(ship));
            Assert.AreEqual(2, harbour.FreeBerths);
        }

        [Test]
        public void Leave_WhileArriving_TurnsRound()
        {
            harbour.Admit(CreateShip());
            Ship ship = CreateShip();
            harbour.Admit(ship);
            harbour.Advance(0.5f);
            float before = PositionOf(ship).y;

            harbour.Leave(ship);
            harbour.Advance(0.1f);

            // Back where it entered: nearer the edge than before.
            Assert.Less(PositionOf(ship).y, before);
            Assert.AreEqual(HarbourState.Leaving, harbour.StateOf(ship));
        }

        [Test]
        public void Return_WhileLeaving_GoesBackAndDocks()
        {
            Ship ship = CreateShip();
            harbour.Admit(ship);
            harbour.Advance(10f);
            harbour.Leave(ship);
            harbour.Advance(0.25f);
            events.Clear();

            harbour.Return(ship);
            Assert.AreEqual(HarbourState.Arriving, harbour.StateOf(ship));
            harbour.Advance(10f);

            Assert.AreEqual(HarbourState.Docked, harbour.StateOf(ship));
            Assert.AreEqual(new Vector2(NearBerth.x, NearBerth.y), PositionOf(ship));
            CollectionAssert.AreEqual(new[] { "berthed" }, events);
            Assert.Throws<System.InvalidOperationException>(() => harbour.Return(ship));
        }

        [Test]
        public void Exit_AdmitsTheFirstQueuedShip()
        {
            Ship docked = CreateShip();
            harbour.Admit(docked);
            harbour.Admit(CreateShip());
            Ship queued = CreateShip();
            harbour.Admit(queued);
            harbour.Advance(10f);
            harbour.Leave(docked);
            events.Clear();

            harbour.Advance(10f);

            CollectionAssert.AreEqual(new[] { "exited", "entered" }, events);
            Assert.AreEqual(HarbourState.Arriving, harbour.StateOf(queued));
            Assert.AreEqual(new Vector2(2f, 0f), PositionOf(queued));
        }

        [Test]
        public void Advance_TwoExitsInOneStep_AdmitsTwoInQueueOrder()
        {
            Ship a = CreateShip();
            Ship b = CreateShip();
            Ship c = CreateShip();
            Ship d = CreateShip();
            harbour.Admit(a);
            harbour.Admit(b);
            harbour.Admit(c);
            harbour.Admit(d);
            harbour.Advance(10f);
            harbour.Leave(a);
            harbour.Leave(b);
            events.Clear();
            var entered = new List<Ship>();
            harbour.Entered += entered.Add;

            harbour.Advance(10f);

            CollectionAssert.AreEqual(new[] { c, d }, entered);
            Assert.AreEqual(HarbourState.Arriving, harbour.StateOf(c));
            Assert.AreEqual(HarbourState.Arriving, harbour.StateOf(d));
            var ships = new List<Ship>();
            harbour.GetShips(ships);
            CollectionAssert.AreEqual(new[] { c, d }, ships);
        }

        [Test]
        public void Withdraw_LeavesTheQueue()
        {
            harbour.Admit(CreateShip());
            harbour.Admit(CreateShip());
            Ship queued = CreateShip();
            harbour.Admit(queued);

            Assert.IsTrue(harbour.Withdraw(queued));

            Assert.IsFalse(harbour.Contains(queued));
            Assert.IsFalse(harbour.Withdraw(queued));
        }

        [Test]
        public void Remove_OnTheGrid_FreesTheBerthAndAdmitsTheQueue()
        {
            Ship onGrid = CreateShip();
            harbour.Admit(onGrid);
            harbour.Admit(CreateShip());
            Ship queued = CreateShip();
            harbour.Admit(queued);
            events.Clear();

            harbour.Remove(onGrid);

            Assert.IsFalse(harbour.Contains(onGrid));
            Assert.AreEqual(HarbourState.Arriving, harbour.StateOf(queued));
            CollectionAssert.AreEqual(new[] { "entered" }, events);
            Assert.AreEqual(0, harbour.FreeBerths);
        }

        [Test]
        public void BerthStaysReservedUntilTheEdge()
        {
            harbour.Admit(CreateShip());
            Ship leaving = CreateShip();
            harbour.Admit(leaving);
            harbour.Advance(10f);
            harbour.Leave(leaving);
            harbour.Advance(0.5f);
            Ship newcomer = CreateShip();

            HarbourState state = harbour.Admit(newcomer);

            Assert.AreEqual(HarbourState.Waiting, state);
            Assert.AreEqual(HarbourState.Leaving, harbour.StateOf(leaving));
        }

        [Test]
        public void Admit_FromExitedHandler_QueuesBehindShipsAlreadyWaiting()
        {
            Ship leaving = CreateShip();
            harbour.Admit(leaving);
            harbour.Admit(CreateShip());
            Ship queued = CreateShip();
            harbour.Admit(queued);
            Ship newcomer = CreateShip();
            harbour.Exited += ship => harbour.Admit(newcomer);
            harbour.Advance(10f);
            harbour.Leave(leaving);

            harbour.Advance(10f);

            Assert.AreEqual(HarbourState.Arriving, harbour.StateOf(queued));
            Assert.AreEqual(HarbourState.Waiting, harbour.StateOf(newcomer));
        }

        [Test]
        public void Place_DocksAtTheBerthNearestTheEdgeWithoutBerthed()
        {
            Ship ship = CreateShip();

            Assert.IsTrue(harbour.Place(ship));

            Assert.AreEqual(HarbourState.Docked, harbour.StateOf(ship));
            Assert.AreEqual(new Vector2(NearBerth.x, NearBerth.y), PositionOf(ship));
            Assert.AreEqual(1, harbour.FreeBerths);
            Assert.IsEmpty(events);
        }

        [Test]
        public void Place_NoFreeBerth_ReturnsFalse()
        {
            Assert.IsTrue(harbour.Place(CreateShip()));
            Assert.IsTrue(harbour.Place(CreateShip()));
            Ship third = CreateShip();

            Assert.IsFalse(harbour.Place(third));

            Assert.IsFalse(harbour.Contains(third));
            Assert.AreEqual(HarbourState.AtSea, harbour.StateOf(third));
        }

        [Test]
        public void Place_ThenAdmit_NewcomerTakesTheOtherBerth()
        {
            Ship placed = CreateShip();
            harbour.Place(placed);
            Ship newcomer = CreateShip();

            harbour.Admit(newcomer);
            harbour.Advance(100f);

            Assert.AreEqual(new Vector2(NearBerth.x, NearBerth.y), PositionOf(placed));
            Assert.AreEqual(new Vector2(FarBerth.x, FarBerth.y), PositionOf(newcomer));
        }

        [Test]
        public void Place_ShipAlreadyHere_Throws()
        {
            Ship ship = CreateShip();
            harbour.Admit(ship);

            Assert.Throws<System.InvalidOperationException>(() => harbour.Place(ship));
            Assert.Throws<System.ArgumentNullException>(() => harbour.Place(null));
        }

        [Test]
        public void Place_ThenLeave_ExitsAndFreesTheBerth()
        {
            Ship ship = CreateShip();
            harbour.Place(ship);

            harbour.Leave(ship);
            harbour.Advance(100f);

            Assert.IsFalse(harbour.Contains(ship));
            Assert.AreEqual(harbour.Capacity, harbour.FreeBerths);
            CollectionAssert.AreEqual(new[] { "exited" }, events);
        }
    }
}
