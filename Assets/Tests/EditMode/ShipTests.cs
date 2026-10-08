using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipTests
    {
        private const float Speed = 4f;

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private MapProjection projection;

        [SetUp]
        public void SetUp()
        {
            // 40 x 20 world units: not square, so normalized steps differ per axis.
            projection = new MapProjection(40f, 2f);
        }

        private Ship CreateShip()
        {
            return new Ship(projection, Center, Speed, TestCrew.Full);
        }

        [Test]
        public void NewShip_IsIdle_AtItsPosition_FacingSouth()
        {
            Ship ship = CreateShip();

            TestAssert.AreEqual(Center, ship.Position);
            TestAssert.AreEqual(Vector2.zero, ship.WorldPosition);
            Assert.IsFalse(ship.IsMoving);
            Assert.IsNull(ship.Destination);
            Assert.AreEqual(CompassDirection.S, ship.Heading);
        }

        [Test]
        public void Constructor_RejectsANullProjection()
        {
            Assert.Throws<ArgumentNullException>(() => new Ship(null, Center, Speed, TestCrew.Full));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Constructor_RejectsAnInvalidSpeed(float speed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Ship(projection, Center, speed, TestCrew.Full));
        }

        [Test]
        public void Constructor_RejectsANullCrew()
        {
            Assert.Throws<ArgumentNullException>(() => new Ship(projection, Center, Speed, null));
        }

        [Test]
        public void Crew_IsTheOneTheShipWasGiven()
        {
            var crew = new ShipCrew(28, 12);

            Assert.AreSame(crew, new Ship(projection, Center, Speed, crew).Crew);
        }

        [Test]
        public void Advance_IsSlowedByAShortCrew()
        {
            // 30 sailors of 100: three quarters of the speed.
            var ship = new Ship(projection, Center, Speed, new ShipCrew(100, 30));
            ship.SetDestination(new Vector2(1f, 0.5f));

            ship.Advance(1f);

            TestAssert.AreEqual(new Vector2(3f, 0f), ship.WorldPosition);
        }

        [Test]
        public void Advance_IsAtHalfSpeed_WithATenthOfTheCrew()
        {
            var ship = new Ship(projection, Center, Speed, new ShipCrew(100, 10));
            ship.SetDestination(new Vector2(1f, 0.5f));

            ship.Advance(1f);

            TestAssert.AreEqual(new Vector2(2f, 0f), ship.WorldPosition);
        }

        [Test]
        public void Advance_DoesNotMoveAShipWithoutASailor_WhichKeepsItsRoute()
        {
            var ship = new Ship(projection, Center, Speed, new ShipCrew(28, 0));
            var destination = new Vector2(1f, 0.5f);
            ship.SetDestination(destination);

            ship.Advance(float.PositiveInfinity);
            ship.Advance(1f);

            Assert.AreEqual(Center, ship.Position);
            Assert.IsTrue(ship.IsMoving);
            Assert.AreEqual(destination, ship.Destination.Value);
            Assert.AreEqual(1, ship.SailedWaypoints.Count);
            Assert.AreEqual(1, ship.RemainingWaypoints.Count);
        }

        [Test]
        public void Constructor_RejectsANaNPosition()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Ship(projection, new Vector2(float.NaN, 0.5f), Speed, TestCrew.Full));
        }

        [Test]
        public void Constructor_ClampsThePositionToTheMap()
        {
            var ship = new Ship(projection, new Vector2(-1f, 3f), Speed, TestCrew.Full);

            TestAssert.AreEqual(new Vector2(0f, 1f), ship.Position);
        }

        [Test]
        public void Advance_DoesNothing_WhenIdle()
        {
            Ship ship = CreateShip();

            ship.Advance(10f);

            TestAssert.AreEqual(Center, ship.Position);
            Assert.IsFalse(ship.IsMoving);
        }

        [Test]
        public void SetDestination_StartsTheTrip()
        {
            Ship ship = CreateShip();

            ship.SetDestination(new Vector2(1f, 0.5f));

            Assert.IsTrue(ship.IsMoving);
            Assert.IsTrue(ship.Destination.HasValue);
            TestAssert.AreEqual(new Vector2(1f, 0.5f), ship.Destination.Value);
            TestAssert.AreEqual(Center, ship.Position);
        }

        // East edge is 20 world units away, north edge 10, the north-east corner about 22.4.
        [TestCase(1f, 0.5f)]
        [TestCase(0.5f, 1f)]
        [TestCase(1f, 1f)]
        [TestCase(0f, 0f)]
        public void Advance_CoversSpeedTimesDeltaTime_InEveryDirection(float x, float y)
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(x, y));

            ship.Advance(1.5f);

            Assert.AreEqual(Speed * 1.5f, ship.WorldPosition.magnitude, 1e-3f);
            Assert.IsTrue(ship.IsMoving);
        }

        [Test]
        public void Advance_MovesTowardsTheDestination()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));

            ship.Advance(1f);

            TestAssert.AreEqual(new Vector2(4f, 0f), ship.WorldPosition);
            TestAssert.AreEqual(new Vector2(0.6f, 0.5f), ship.Position);
        }

        [Test]
        public void Advance_AccumulatesOverSeveralSteps()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));

            for (int i = 0; i < 10; i++)
            {
                ship.Advance(0.25f);
            }

            Assert.AreEqual(10f, ship.WorldPosition.x, 1e-3f);
            Assert.AreEqual(0f, ship.WorldPosition.y, 1e-3f);
        }

        // The destination is 4 world units away, one second of travel. Exactly one second
        // is left out: rounding decides whether that step reaches the point or the next one.
        [TestCase(1.01f)]
        [TestCase(2f)]
        [TestCase(1e6f)]
        [TestCase(float.PositiveInfinity)]
        public void Advance_StopsExactlyOnTheDestination_WithoutOvershooting(float deltaTime)
        {
            Ship ship = CreateShip();
            var destination = new Vector2(0.6f, 0.5f);
            ship.SetDestination(destination);

            ship.Advance(deltaTime);

            Assert.AreEqual(destination, ship.Position);
            Assert.IsFalse(ship.IsMoving);
            Assert.IsNull(ship.Destination);
        }

        [Test]
        public void Advance_DoesNotMoveAnArrivedShip()
        {
            Ship ship = CreateShip();
            var destination = new Vector2(0.6f, 0.5f);
            ship.SetDestination(destination);
            ship.Advance(5f);

            ship.Advance(5f);

            Assert.AreEqual(destination, ship.Position);
        }

        [Test]
        public void Arrived_IsRaisedOnce_WhenTheShipReachesItsDestination()
        {
            Ship ship = CreateShip();
            Ship arrived = null;
            int count = 0;
            ship.Arrived += s =>
            {
                arrived = s;
                count++;
            };
            ship.SetDestination(new Vector2(0.6f, 0.5f));

            ship.Advance(5f);
            ship.Advance(5f);

            Assert.AreEqual(1, count);
            Assert.AreSame(ship, arrived);
        }

        [Test]
        public void Arrived_IsRaisedWhenTheShipIsIdle()
        {
            Ship ship = CreateShip();
            bool wasMoving = true;
            ship.Arrived += s => wasMoving = s.IsMoving;
            ship.SetDestination(new Vector2(0.6f, 0.5f));

            ship.Advance(5f);

            Assert.IsFalse(wasMoving);
        }

        [Test]
        public void Arrived_IsNotRaised_WhileTheShipIsUnderWay()
        {
            Ship ship = CreateShip();
            int count = 0;
            ship.Arrived += _ => count++;
            ship.SetDestination(new Vector2(1f, 0.5f));

            ship.Advance(1f);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Arrived_IsNotRaised_ByAnOrderThatReplacesTheRoute()
        {
            Ship ship = CreateShip();
            int count = 0;
            ship.Arrived += _ => count++;
            ship.SetDestination(new Vector2(1f, 0.5f));
            ship.Advance(1f);

            ship.SetDestination(new Vector2(0f, 0.5f));
            ship.Advance(1f);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Arrived_IsNotRaised_ByAnOrderToWhereTheShipIs()
        {
            Ship ship = CreateShip();
            int count = 0;
            ship.Arrived += _ => count++;

            ship.SetDestination(Center);
            ship.Advance(1f);

            Assert.AreEqual(0, count);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Advance_IgnoresAnInvalidDeltaTime(float deltaTime)
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));

            ship.Advance(deltaTime);

            TestAssert.AreEqual(Center, ship.Position);
            Assert.IsTrue(ship.IsMoving);
        }

        [TestCase(0.5f, 1f, CompassDirection.N)]
        [TestCase(1f, 1f, CompassDirection.NE)]
        [TestCase(1f, 0.5f, CompassDirection.E)]
        [TestCase(1f, 0f, CompassDirection.SE)]
        [TestCase(0.5f, 0f, CompassDirection.S)]
        [TestCase(0f, 0f, CompassDirection.SW)]
        [TestCase(0f, 0.5f, CompassDirection.W)]
        [TestCase(0f, 1f, CompassDirection.NW)]
        public void SetDestination_TurnsTheShipTowardsIt_AtOnce(float x, float y, CompassDirection expected)
        {
            Ship ship = CreateShip();

            // From the center, the corners are at 63 degrees from north in world space:
            // inside the diagonal sectors (22.5 to 67.5).
            ship.SetDestination(new Vector2(x, y));

            Assert.AreEqual(expected, ship.Heading);
        }

        [Test]
        public void Heading_UsesWorldSpace_NotNormalizedSpace()
        {
            Ship ship = CreateShip();

            // The normalized offset (0.15, 0.5) is 16.7 degrees from north, which would be
            // north. In world space it is (6, 10): 31 degrees, north-east.
            ship.SetDestination(new Vector2(0.65f, 1f));

            Assert.AreEqual(CompassDirection.NE, ship.Heading);
        }

        [Test]
        public void Heading_IsKeptOnArrival()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(0.4f, 0.5f));

            ship.Advance(100f);

            Assert.IsFalse(ship.IsMoving);
            Assert.AreEqual(CompassDirection.W, ship.Heading);
        }

        [Test]
        public void SetDestination_RedirectsAShipUnderWay()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));
            ship.Advance(1f);

            ship.SetDestination(new Vector2(0.6f, 1f));
            ship.Advance(1f);

            Assert.AreEqual(CompassDirection.N, ship.Heading);
            TestAssert.AreEqual(new Vector2(4f, 4f), ship.WorldPosition);
        }

        [Test]
        public void SetDestination_ClampsToTheMap()
        {
            Ship ship = CreateShip();

            ship.SetDestination(new Vector2(2f, -1f));

            TestAssert.AreEqual(new Vector2(1f, 0f), ship.Destination.Value);
        }

        [Test]
        public void SetDestination_OnTheCurrentPosition_LeavesTheShipIdle_AndItsHeadingUnchanged()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(0.6f, 0.5f));
            ship.Advance(100f);

            ship.SetDestination(ship.Position);
            ship.Advance(1f);

            Assert.IsFalse(ship.IsMoving);
            Assert.AreEqual(CompassDirection.E, ship.Heading);
            TestAssert.AreEqual(new Vector2(0.6f, 0.5f), ship.Position);
        }

        [Test]
        public void SetDestination_Twice_KeepsSailingToTheSamePoint()
        {
            Ship ship = CreateShip();
            var destination = new Vector2(1f, 0.5f);
            ship.SetDestination(destination);
            ship.Advance(1f);

            ship.SetDestination(destination);
            ship.Advance(1f);

            TestAssert.AreEqual(new Vector2(8f, 0f), ship.WorldPosition);
            Assert.AreEqual(CompassDirection.E, ship.Heading);
        }

        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.NaN)]
        public void SetDestination_IgnoresANaNPoint(float x, float y)
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));

            ship.SetDestination(new Vector2(x, y));
            ship.Advance(1f);

            TestAssert.AreEqual(new Vector2(4f, 0f), ship.WorldPosition);
            TestAssert.AreEqual(new Vector2(1f, 0.5f), ship.Destination.Value);
        }

        [Test]
        public void AVeryShortTrip_StillTurnsTheShip()
        {
            Ship ship = CreateShip();

            // About 0.000004 world units to the west: Vector2 equality calls anything
            // shorter than 0.00001 zero.
            ship.SetDestination(new Vector2(0.4999999f, 0.5f));

            Assert.IsTrue(ship.IsMoving);
            Assert.AreEqual(CompassDirection.W, ship.Heading);
        }
    }
}
