using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipFleetTests
    {
        private const float Speed = 4f;
        private const string Port = "Sparia";

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 PortPosition = new Vector2(0.6f, 0.5f);

        private ShipDocking<string> docking;
        private ShipFleet<string> fleet;
        private Ship ship;

        [SetUp]
        public void SetUp()
        {
            // No harbour: a port is entered at once, from its own position.
            docking = new ShipDocking<string>();
            fleet = new ShipFleet<string>(docking);
            ship = NewShip(Center);
        }

        private static Ship NewShip(Vector2 position)
        {
            return new Ship(new MapProjection(40f, 2f), position, Speed, TestCrew.Full);
        }

        // The ship docks at the port it lies at.
        private void EnterThePort()
        {
            docking.OrderToPort(ship, Port, Center);
            fleet.Step(0f);
        }

        [Test]
        public void Constructor_RejectsANullDocking()
        {
            Assert.Throws<ArgumentNullException>(() => new ShipFleet<string>(null));
        }

        [Test]
        public void Add_ListsTheShip_WithItsNameAndItsPosition()
        {
            fleet.Add(ship, "Aurore", false);

            CollectionAssert.AreEqual(new[] { ship }, fleet.Ships);
            Assert.AreEqual("Aurore", fleet.NameOf(ship));
            Assert.AreEqual(ship.WorldPosition, fleet.WorldPositions[0]);
            Assert.IsTrue(fleet.Contains(ship));
        }

        [Test]
        public void Add_KeepsThePlayersShipsApart()
        {
            Ship player = NewShip(Center);
            fleet.Add(player, "Le Marchand", true);
            fleet.Add(ship, "Aurore", false);

            CollectionAssert.AreEqual(new[] { player, ship }, fleet.Ships);
            CollectionAssert.AreEqual(new[] { player }, fleet.PlayerShips);
            Assert.IsTrue(fleet.IsPlayerShip(player));
            Assert.IsFalse(fleet.IsPlayerShip(ship));
            Assert.IsFalse(fleet.IsPlayerShip(null));
        }

        [Test]
        public void Add_RejectsANullShip_AndAShipAlreadyIn()
        {
            fleet.Add(ship, "Aurore", false);

            Assert.Throws<ArgumentNullException>(() => fleet.Add(null, "Aurore", false));
            Assert.Throws<ArgumentException>(() => fleet.Add(ship, "Aurore", false));
            Assert.AreEqual(1, fleet.Ships.Count);
        }

        [Test]
        public void AShipNotInTheFleet_HasNoName()
        {
            Assert.IsNull(fleet.NameOf(ship));
            Assert.IsNull(fleet.NameOf(null));
            Assert.IsFalse(fleet.Contains(ship));
            Assert.IsFalse(fleet.Contains(null));
        }

        [Test]
        public void Step_RaisesStepStarted_BeforeTheShipsSail()
        {
            fleet.Add(ship, "Aurore", false);
            docking.OrderToPoint(ship, PortPosition);
            Vector2 before = ship.WorldPosition;
            float seconds = 0f;
            Vector2 seen = default;
            fleet.StepStarted += deltaTime =>
            {
                seconds = deltaTime;
                seen = ship.WorldPosition;
            };

            fleet.Step(1f);

            Assert.AreEqual(1f, seconds);
            Assert.AreEqual(before, seen);
            Assert.AreNotEqual(before, ship.WorldPosition);
        }

        [Test]
        public void Step_ReportsAnArrival_InThePortTheShipEntered()
        {
            fleet.Add(ship, "Aurore", false);
            var reported = new List<(Ship Ship, string Port)>();
            fleet.Arrived += (s, port) => reported.Add((s, port));
            docking.OrderToPort(ship, Port, PortPosition);

            fleet.Step(5f);

            Assert.AreEqual(1, reported.Count);
            Assert.AreSame(ship, reported[0].Ship);
            Assert.AreEqual(Port, reported[0].Port);
            Assert.IsTrue(docking.IsInCity(ship));
        }

        [Test]
        public void Step_DoesNotSailAShipInACity()
        {
            fleet.Add(ship, "Aurore", false);
            EnterThePort();

            // A route given behind the docking's back: the ship keeps it for when it is back at sea.
            ship.SetDestination(new Vector2(0.9f, 0.5f));
            Vector2 before = ship.WorldPosition;

            fleet.Step(1f);

            Assert.AreEqual(before, ship.WorldPosition);
        }

        [Test]
        public void UpdatePositions_PutsAShipInACity_OffTheMap()
        {
            Ship atSea = NewShip(new Vector2(0.2f, 0.5f));
            fleet.Add(ship, "Aurore", false);
            fleet.Add(atSea, "Brume", false);
            EnterThePort();

            fleet.UpdatePositions();

            Assert.IsTrue(float.IsNaN(fleet.WorldPositions[0].x));
            Assert.AreEqual(atSea.WorldPosition, fleet.WorldPositions[1]);
        }

        [Test]
        public void UpdatePositionOf_UpdatesThatShipAlone()
        {
            Ship atSea = NewShip(new Vector2(0.2f, 0.5f));
            fleet.Add(ship, "Aurore", false);
            fleet.Add(atSea, "Brume", false);
            EnterThePort();
            Vector2 recorded = fleet.WorldPositions[1];
            atSea.SetDestination(new Vector2(0.1f, 0.5f));
            atSea.Advance(1f);

            fleet.UpdatePositionOf(ship);

            Assert.IsTrue(float.IsNaN(fleet.WorldPositions[0].x));
            Assert.AreNotEqual(recorded, atSea.WorldPosition);
            Assert.AreEqual(recorded, fleet.WorldPositions[1], "The other ship's position waits for UpdatePositions.");
        }

        [Test]
        public void Remove_TakesTheShipOutOfItsCity_WhileStillListed()
        {
            fleet.Add(ship, "Aurore", false);
            EnterThePort();
            bool listedWhenLeaving = false;
            docking.CityStateChanged += (s, city) => listedWhenLeaving = fleet.Contains(s);

            Assert.IsTrue(fleet.Remove(ship));

            Assert.IsTrue(listedWhenLeaving);
            Assert.IsFalse(docking.IsInCity(ship));
            Assert.IsFalse(fleet.Contains(ship));
            Assert.AreEqual(0, fleet.Ships.Count);
            Assert.AreEqual(0, fleet.WorldPositions.Count);
            Assert.IsNull(fleet.NameOf(ship));
        }

        [Test]
        public void Remove_TakesAPlayersShipOutOfThePlayersShips()
        {
            fleet.Add(ship, "Le Marchand", true);

            fleet.Remove(ship);

            Assert.AreEqual(0, fleet.PlayerShips.Count);
            Assert.IsFalse(fleet.IsPlayerShip(ship));
        }

        [Test]
        public void Remove_StopsReportingTheShipsArrivals()
        {
            fleet.Add(ship, "Aurore", false);
            var reported = new List<Ship>();
            fleet.Arrived += (s, port) => reported.Add(s);
            docking.OrderToPoint(ship, PortPosition);

            fleet.Remove(ship);
            ship.Advance(5f);
            fleet.Step(0f);

            Assert.AreEqual(0, reported.Count);
        }

        [Test]
        public void Remove_IsFalse_ForAShipNotInTheFleet()
        {
            Assert.IsFalse(fleet.Remove(ship));
            Assert.IsFalse(fleet.Remove(null));
        }
    }
}
