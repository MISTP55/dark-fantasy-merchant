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

        // One frame of ShipsView.
        private void Step(float deltaTime)
        {
            ship.Advance(deltaTime);
            docking.Update();
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
            docking.Update();

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
    }
}
