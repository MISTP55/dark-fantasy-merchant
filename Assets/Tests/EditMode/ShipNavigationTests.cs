using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>A ship that was given a pathfinder: it sails over water only.</summary>
    public class ShipNavigationTests
    {
        private const float Speed = 4f;
        private const float FirstLegLength = 15.8113883f;

        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;

        [SetUp]
        public void SetUp()
        {
            // 40 x 20 world units over 8 x 4 cells: a cell is 5 x 5 world units.
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#....",
                "...#....");
            navigation = new NavigationPathfinder(grid);
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(grid, x, y);
        }

        private Ship CreateShip()
        {
            return new Ship(projection, Cell(1, 0), Speed, navigation);
        }

        [Test]
        public void AShipCreatedOnWater_StaysWhereItIs()
        {
            Vector2 position = GridAssert.CellPoint(grid, 1.2f, 0.7f);

            var ship = new Ship(projection, position, Speed, navigation);

            TestAssert.AreEqual(position, ship.Position);
            Assert.IsFalse(ship.IsMoving);
        }

        [Test]
        public void AShipCreatedOnLand_StartsOnTheNearestWater()
        {
            // In the wall, nearer its west side.
            var ship = new Ship(projection, GridAssert.CellPoint(grid, 3.2f, 1.5f), Speed, navigation);

            TestAssert.AreEqual(Cell(2, 1), ship.Position);
            Assert.IsFalse(ship.IsMoving);
        }

        [Test]
        public void AShipCreatedOutsideTheMap_IsClampedBeforeLookingForWater()
        {
            var ship = new Ship(projection, new Vector2(-3f, 0.1f), Speed, navigation);

            TestAssert.AreEqual(new Vector2(0f, 0.1f), ship.Position);
        }

        [Test]
        public void AnOrderAcrossLand_BecomesARouteAroundIt()
        {
            Ship ship = CreateShip();

            ship.SetDestination(Cell(5, 0));

            Assert.IsTrue(ship.IsMoving);
            Assert.AreEqual(3, ship.RemainingWaypoints.Count);
            TestAssert.AreEqual(Cell(2, 3), ship.RemainingWaypoints[0]);
            TestAssert.AreEqual(Cell(4, 3), ship.RemainingWaypoints[1]);
            TestAssert.AreEqual(Cell(5, 0), ship.RemainingWaypoints[2]);
            TestAssert.AreEqual(Cell(5, 0), ship.Destination.Value);
            Assert.AreEqual(CompassDirection.N, ship.Heading, "towards the first waypoint");
        }

        [Test]
        public void AnIdleShip_HasNoWaypoint()
        {
            Ship ship = CreateShip();

            Assert.IsEmpty(ship.RemainingWaypoints);
            Assert.IsNull(ship.Destination);
        }

        [Test]
        public void Advance_FollowsTheFirstLeg()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.Advance(1f);

            // Four world units along (5, 15) from (-12.5, -7.5).
            float along = Speed / FirstLegLength;
            TestAssert.AreEqual(new Vector2(-12.5f + 5f * along, -7.5f + 15f * along), ship.WorldPosition);
            Assert.AreEqual(3, ship.RemainingWaypoints.Count);
        }

        [Test]
        public void Advance_PastAWaypoint_SpendsTheRestOfTheStepOnTheNextLeg()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.Advance(5f);

            // Twenty world units: the whole first leg, then the rest eastwards.
            Assert.AreEqual(-7.5f + (20f - FirstLegLength), ship.WorldPosition.x, 1e-3f);
            Assert.AreEqual(7.5f, ship.WorldPosition.y, 1e-3f);
            Assert.AreEqual(CompassDirection.E, ship.Heading);
            Assert.AreEqual(2, ship.RemainingWaypoints.Count);
            TestAssert.AreEqual(Cell(4, 3), ship.RemainingWaypoints[0]);
            TestAssert.AreEqual(Cell(5, 0), ship.Destination.Value);
        }

        [Test]
        public void Advance_InSmallSteps_CoversTheSameDistanceAsOneStep()
        {
            Ship inOneStep = CreateShip();
            Ship inSmallSteps = CreateShip();
            inOneStep.SetDestination(Cell(5, 0));
            inSmallSteps.SetDestination(Cell(5, 0));

            inOneStep.Advance(8f);

            for (int i = 0; i < 80; i++)
            {
                inSmallSteps.Advance(0.1f);
            }

            Assert.AreEqual(inOneStep.WorldPosition.x, inSmallSteps.WorldPosition.x, 1e-2f);
            Assert.AreEqual(inOneStep.WorldPosition.y, inSmallSteps.WorldPosition.y, 1e-2f);
            Assert.AreEqual(CompassDirection.S, inSmallSteps.Heading, "on the last leg");
        }

        [Test]
        public void TheShip_StopsOnItsLastWaypoint_AndKeepsItsHeading()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            // The route is about 41.6 world units long: 10.4 seconds.
            ship.Advance(11f);

            Assert.AreEqual(Cell(5, 0), ship.Position);
            Assert.IsFalse(ship.IsMoving);
            Assert.IsNull(ship.Destination);
            Assert.IsEmpty(ship.RemainingWaypoints);
            Assert.AreEqual(CompassDirection.S, ship.Heading);
        }

        [TestCase(1e6f)]
        [TestCase(float.PositiveInfinity)]
        public void Advance_WithAHugeDeltaTime_StopsExactlyOnTheLastWaypoint(float deltaTime)
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.Advance(deltaTime);

            Assert.AreEqual(Cell(5, 0), ship.Position);
            Assert.IsFalse(ship.IsMoving);
            Assert.AreEqual(CompassDirection.S, ship.Heading);
        }

        [Test]
        public void OverAWholeTrip_TheShipIsNeverOnLand()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            int steps = 0;

            while (ship.IsMoving)
            {
                ship.Advance(0.05f);
                steps++;

                Assert.IsTrue(grid.IsNavigable(ship.Position), $"on land at {ship.Position}, step {steps}");
                Assert.Less(steps, 1000, "the ship never arrives");
            }

            Assert.AreEqual(Cell(5, 0), ship.Position);
        }

        [Test]
        public void AnOrderOnLand_SendsTheShipToTheNearestWaterItCanReach()
        {
            Ship ship = CreateShip();

            // In the wall, nearer its west side.
            ship.SetDestination(GridAssert.CellPoint(grid, 3.2f, 0.5f));

            Assert.IsTrue(ship.IsMoving);
            TestAssert.AreEqual(Cell(2, 0), ship.Destination.Value);

            ship.Advance(100f);

            Assert.AreEqual(Cell(2, 0), ship.Position);
        }

        [Test]
        public void AnOrderUnderWay_ReplacesTheRoute_FromWhereTheShipIs()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));
            ship.Advance(1f);
            Vector2 turningPoint = ship.Position;

            ship.SetDestination(Cell(0, 3));

            Assert.AreEqual(1, ship.RemainingWaypoints.Count);
            TestAssert.AreEqual(Cell(0, 3), ship.Destination.Value);
            TestAssert.AreEqual(turningPoint, ship.Position);
            Assert.AreEqual(CompassDirection.NW, ship.Heading);

            int steps = 0;

            while (ship.IsMoving)
            {
                ship.Advance(0.05f);
                steps++;

                Assert.IsTrue(grid.IsNavigable(ship.Position), $"on land at {ship.Position}");
                Assert.Less(steps, 1000, "the ship never arrives");
            }

            Assert.AreEqual(Cell(0, 3), ship.Position);
        }

        [Test]
        public void AnIdleShip_HasSailedNothing()
        {
            Ship ship = CreateShip();

            Assert.IsEmpty(ship.SailedWaypoints);
        }

        [Test]
        public void AnOrder_StartsTheSailedRoute_WhereTheShipIs()
        {
            Ship ship = CreateShip();

            ship.SetDestination(Cell(5, 0));

            Assert.AreEqual(1, ship.SailedWaypoints.Count);
            TestAssert.AreEqual(Cell(1, 0), ship.SailedWaypoints[0]);
        }

        [Test]
        public void Advance_OnALeg_AddsNothingToTheSailedRoute()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.Advance(1f);

            Assert.AreEqual(1, ship.SailedWaypoints.Count);
            TestAssert.AreEqual(Cell(1, 0), ship.SailedWaypoints[0]);
        }

        [Test]
        public void Advance_PastAWaypoint_MovesItToTheSailedRoute()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            // Twenty world units: the whole first leg and part of the second.
            ship.Advance(5f);

            Assert.AreEqual(2, ship.SailedWaypoints.Count);
            TestAssert.AreEqual(Cell(1, 0), ship.SailedWaypoints[0]);
            TestAssert.AreEqual(Cell(2, 3), ship.SailedWaypoints[1]);
            Assert.AreEqual(2, ship.RemainingWaypoints.Count);
        }

        [Test]
        public void AShipThatHasArrived_HasSailedNothing()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.Advance(100f);

            Assert.IsEmpty(ship.SailedWaypoints);
        }

        [Test]
        public void AnOrderUnderWay_StartsTheSailedRouteAgain_FromWhereTheShipIs()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));
            ship.Advance(5f);
            Vector2 turningPoint = ship.Position;

            ship.SetDestination(Cell(0, 3));

            Assert.AreEqual(1, ship.SailedWaypoints.Count);
            TestAssert.AreEqual(turningPoint, ship.SailedWaypoints[0]);
        }

        [Test]
        public void AnIgnoredOrder_KeepsTheSailedRoute()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));
            ship.Advance(5f);

            ship.SetDestination(new Vector2(float.NaN, 0.5f));

            Assert.AreEqual(2, ship.SailedWaypoints.Count);
        }

        [Test]
        public void AnOrderToWhereTheShipIs_UnderWay_EndsTheSailedRoute()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));
            ship.Advance(5f);

            ship.SetDestination(ship.Position);

            Assert.IsFalse(ship.IsMoving);
            Assert.IsEmpty(ship.SailedWaypoints);
        }

        [Test]
        public void AnOrderToWhereTheShipIs_LeavesItIdle_AndItsHeadingUnchanged()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(2, 0));
            ship.Advance(100f);

            ship.SetDestination(ship.Position);

            Assert.IsFalse(ship.IsMoving);
            Assert.AreEqual(CompassDirection.E, ship.Heading);
        }

        [Test]
        public void ANaNOrder_KeepsTheCurrentRoute()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.SetDestination(new Vector2(float.NaN, 0.5f));

            Assert.AreEqual(3, ship.RemainingWaypoints.Count);
            TestAssert.AreEqual(Cell(5, 0), ship.Destination.Value);
        }

        [Test]
        public void SetDestination_TellsWhetherTheOrderWasTaken()
        {
            Ship ship = CreateShip();

            Assert.IsTrue(ship.SetDestination(Cell(5, 0)));
            Assert.IsTrue(ship.SetDestination(ship.Position), "an order to where the ship is");
            Assert.IsFalse(ship.SetDestination(new Vector2(float.NaN, 0.5f)));
        }

        [Test]
        public void AnchorageAt_IsThePointOnWater_AndTheNearestWaterOnLand()
        {
            Ship ship = CreateShip();
            Vector2 onWater = GridAssert.CellPoint(grid, 5.2f, 0.7f);

            TestAssert.AreEqual(onWater, ship.AnchorageAt(onWater));

            // In the wall, nearer its west side.
            TestAssert.AreEqual(Cell(2, 1), ship.AnchorageAt(GridAssert.CellPoint(grid, 3.2f, 1.5f)));
        }

        [Test]
        public void AnchorageAt_WithoutAPathfinder_IsThePointClampedToTheMap()
        {
            var ship = new Ship(projection, new Vector2(0.5f, 0.5f), Speed);

            TestAssert.AreEqual(new Vector2(1f, 0.25f), ship.AnchorageAt(new Vector2(1.5f, 0.25f)));
        }

        [Test]
        public void OnAGridWithoutWater_TheShipStaysWhereItIs_AndIgnoresOrders()
        {
            var noWater = new NavigationPathfinder(new NavigationGrid(8, 4));
            var ship = new Ship(projection, new Vector2(0.3f, 0.4f), Speed, noWater);

            Assert.IsFalse(ship.SetDestination(new Vector2(0.8f, 0.8f)));
            ship.Advance(1f);

            Assert.IsFalse(ship.IsMoving);
            TestAssert.AreEqual(new Vector2(0.3f, 0.4f), ship.Position);
        }
    }
}
