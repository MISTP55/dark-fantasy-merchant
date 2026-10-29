using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipArrivalsTests
    {
        private const float Speed = 4f;
        private const string Port = "Sparia";

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 PortPosition = new Vector2(0.6f, 0.5f);

        private ShipDocking<string> docking;
        private ShipArrivals<string> arrivals;
        private List<(Ship Ship, string Port)> reported;
        private Ship ship;

        [SetUp]
        public void SetUp()
        {
            docking = new ShipDocking<string>();
            arrivals = new ShipArrivals<string>(docking);
            reported = new List<(Ship, string)>();
            arrivals.Arrived += (s, port) => reported.Add((s, port));

            // No pathfinder: a port is entered from its own position.
            ship = new Ship(new MapProjection(40f, 2f), Center, Speed, TestCrew.Full);
            arrivals.Track(ship);
        }

        [Test]
        public void ArrivalAtAnAnchorageWithAHarbour_ReportedWhenDocked()
        {
            UseAHarbour();
            docking.OrderToPort(ship, Port, PortPosition);

            Step(5f);

            Assert.AreEqual(HarbourState.Arriving, docking.StateOf(ship));
            Assert.AreEqual(0, reported.Count);

            Step(5f);

            Assert.AreEqual(1, reported.Count);
            Assert.AreSame(ship, reported[0].Ship);
            Assert.AreEqual(Port, reported[0].Port);
        }

        [Test]
        public void EnteringTheQueue_RaisesQueued()
        {
            UseAHarbour();
            var queued = new List<(Ship Ship, string Port)>();
            arrivals.Queued += (s, port) => queued.Add((s, port));
            var other = new Ship(new MapProjection(40f, 2f), PortPosition, Speed, TestCrew.Full);
            arrivals.Track(other);
            docking.OrderToPort(other, Port, PortPosition);
            docking.Update(0f);
            docking.OrderToPort(ship, Port, PortPosition);

            Step(5f);

            Assert.AreEqual(HarbourState.Waiting, docking.StateOf(ship));
            Assert.AreEqual(1, queued.Count);
            Assert.AreSame(ship, queued[0].Ship);
            Assert.AreEqual(Port, queued[0].Port);
            Assert.IsFalse(reported.Exists(r => r.Ship == ship));

            Step(0f);

            Assert.AreEqual(1, queued.Count);
        }

        // Replaces the docking with one whose port has a harbour of one berth, crossed in 1 second.
        private void UseAHarbour()
        {
            Harbour harbour = TestHarbours.OneBerth(2f);
            docking = new ShipDocking<string>(port => port == Port ? harbour : null);
            arrivals.Forget(ship);
            arrivals = new ShipArrivals<string>(docking);
            arrivals.Arrived += (s, port) => reported.Add((s, port));
            arrivals.Track(ship);
        }

        // One step of ShipFleet.
        private void Step(float deltaTime)
        {
            if (!docking.IsInCity(ship))
            {
                ship.Advance(deltaTime);
            }
            docking.Update(deltaTime);
            arrivals.Report();
        }

        [Test]
        public void Constructor_RejectsANullDocking()
        {
            Assert.Throws<ArgumentNullException>(() => new ShipArrivals<string>(null));
        }

        [Test]
        public void Track_RejectsANullShip()
        {
            Assert.Throws<ArgumentNullException>(() => arrivals.Track(null));
        }

        [Test]
        public void AShipThatStopsAtSea_IsReportedWithoutAPort()
        {
            docking.OrderToPoint(ship, PortPosition);

            Step(5f);

            Assert.AreEqual(1, reported.Count);
            Assert.AreSame(ship, reported[0].Ship);
            Assert.IsNull(reported[0].Port);
        }

        [Test]
        public void AShipThatEntersAPort_IsReportedOnce_WithThatPort()
        {
            docking.OrderToPort(ship, Port, PortPosition);

            Step(5f);

            Assert.AreEqual(1, reported.Count);
            Assert.AreSame(ship, reported[0].Ship);
            Assert.AreEqual(Port, reported[0].Port);
        }

        [Test]
        public void AShipAlreadyAtThePort_IsReportedWhenItEntersIt()
        {
            // It has no route to sail: it docks without arriving anywhere.
            docking.OrderToPort(ship, Port, Center);

            Step(1f);

            Assert.AreEqual(1, reported.Count);
            Assert.AreSame(ship, reported[0].Ship);
            Assert.AreEqual(Port, reported[0].Port);
        }

        [Test]
        public void AShipUnderWay_IsNotReported()
        {
            docking.OrderToPort(ship, Port, new Vector2(1f, 0.5f));

            Step(1f);

            Assert.AreEqual(0, reported.Count);
        }

        [Test]
        public void AnOrderThatReplacesTheRoute_IsNotReported()
        {
            docking.OrderToPort(ship, Port, new Vector2(1f, 0.5f));
            Step(1f);

            docking.OrderToPoint(ship, new Vector2(0f, 0.5f));
            Step(1f);

            Assert.AreEqual(0, reported.Count);
        }

        [Test]
        public void AnArrival_IsReportedOnlyOnce()
        {
            docking.OrderToPoint(ship, PortPosition);
            Step(5f);

            Step(5f);

            Assert.AreEqual(1, reported.Count);
        }

        [Test]
        public void AnArrival_IsReportedByReport_NotBefore()
        {
            docking.OrderToPort(ship, Port, PortPosition);

            ship.Advance(5f);
            docking.Update(0f);

            Assert.AreEqual(0, reported.Count);

            arrivals.Report();

            Assert.AreEqual(1, reported.Count);
        }

        [Test]
        public void AShipOrderedOutByAListener_IsReportedAgainWhenItArrives()
        {
            bool orderedOut = false;
            arrivals.Arrived += (s, port) =>
            {
                if (!orderedOut)
                {
                    orderedOut = true;
                    docking.OrderToPoint(s, Center);
                }
            };
            docking.OrderToPort(ship, Port, PortPosition);

            Step(5f);
            Step(5f);

            Assert.AreEqual(2, reported.Count);
            Assert.AreEqual(Port, reported[0].Port);
            Assert.IsNull(reported[1].Port);
        }

        [Test]
        public void AForgottenShip_IsNoLongerReported()
        {
            docking.OrderToPoint(ship, PortPosition);
            arrivals.Forget(ship);

            Step(5f);

            Assert.AreEqual(0, reported.Count);
        }

        [Test]
        public void Forget_DropsAnArrivalThatWasNotReportedYet()
        {
            docking.OrderToPoint(ship, PortPosition);
            ship.Advance(5f);

            arrivals.Forget(ship);
            arrivals.Report();

            Assert.AreEqual(0, reported.Count);
            Assert.Throws<ArgumentNullException>(() => arrivals.Forget(null));
        }
    }
}
