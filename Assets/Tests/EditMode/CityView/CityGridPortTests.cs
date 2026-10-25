using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityGridPortTests
    {
        private static readonly PortFootprint Port = new PortFootprint(new Footprint(4, 3), 2);
        private static readonly Vector2Int Origin = new Vector2Int(3, 3);

        private static GridPlace At(int x, int y)
        {
            return new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
        }

        // The cells of a rectangle, from (x, y) included, size on x and y.
        private static IEnumerable<Vector2Int> Cells(int x, int y, int sizeX, int sizeY)
        {
            for (int dy = 0; dy < sizeY; dy++)
            {
                for (int dx = 0; dx < sizeX; dx++)
                {
                    yield return new Vector2Int(x + dx, y + dy);
                }
            }
        }

        // The two water rows of a port at Origin facing this way, nothing else.
        private static IEnumerable<Vector2Int> WaterFor(PortFacing facing)
        {
            switch (facing)
            {
                case PortFacing.North:
                    return Cells(3, 4, 4, 2);
                case PortFacing.South:
                    return Cells(3, 3, 4, 2);
                case PortFacing.East:
                    return Cells(4, 3, 2, 4);
                default:
                    return Cells(3, 3, 2, 4);
            }
        }

        private static CityGrid GridWithWater(IEnumerable<Vector2Int> water, int size = 10)
        {
            return new CityGrid(size, size, water);
        }

        [TestCase(PortFacing.North)]
        [TestCase(PortFacing.East)]
        [TestCase(PortFacing.South)]
        [TestCase(PortFacing.West)]
        public void CanPlacePort_EachFacing_NeedsWaterOnItsSide(PortFacing facing)
        {
            CityGrid grid = GridWithWater(WaterFor(facing));

            Assert.IsTrue(grid.CanPlacePort(Port, new PortSite(Origin, facing)));

            foreach (PortFacing other in new[] { PortFacing.North, PortFacing.East, PortFacing.South, PortFacing.West })
            {
                if (other != facing)
                {
                    Assert.IsFalse(grid.CanPlacePort(Port, new PortSite(Origin, other)), other.ToString());
                }
            }
        }

        [Test]
        public void CanPlacePort_RefusesWaterInTheLandRow()
        {
            var water = new List<Vector2Int>(WaterFor(PortFacing.North)) { new Vector2Int(5, 3) };

            Assert.IsFalse(GridWithWater(water).CanPlacePort(Port, new PortSite(Origin, PortFacing.North)));
        }

        [Test]
        public void CanPlacePort_RefusesLandInAWaterRow()
        {
            var water = new List<Vector2Int>(WaterFor(PortFacing.North));
            water.Remove(new Vector2Int(6, 5));

            Assert.IsFalse(GridWithWater(water).CanPlacePort(Port, new PortSite(Origin, PortFacing.North)));
        }

        [Test]
        public void CanPlacePort_RefusesOffTheGridAndOnABuilding()
        {
            // Water on the two top rows of an 8 by 8 grid.
            CityGrid grid = GridWithWater(Cells(0, 6, 8, 2), 8);
            Assert.IsTrue(grid.CanPlacePort(Port, new PortSite(new Vector2Int(4, 5), PortFacing.North)));

            Assert.IsFalse(grid.CanPlacePort(Port, new PortSite(new Vector2Int(5, 5), PortFacing.North)), "off the grid on x");
            Assert.IsFalse(grid.CanPlacePort(Port, new PortSite(new Vector2Int(4, 6), PortFacing.North)), "off the grid on y");

            grid.Place(BuildingKind.Road(), BuildingOwner.City, new Footprint(1, 1), At(5, 5));
            Assert.IsFalse(grid.CanPlacePort(Port, new PortSite(new Vector2Int(4, 5), PortFacing.North)), "on a road");
        }

        [TestCase(PortFacing.North, 4, 3)]
        [TestCase(PortFacing.East, 3, 4)]
        public void PlacePort_SetsThePortAndItsFacing(PortFacing facing, int sizeX, int sizeY)
        {
            CityGrid grid = GridWithWater(WaterFor(facing));

            PlacedBuilding port = grid.PlacePort(Port, new PortSite(Origin, facing));

            Assert.AreSame(port, grid.Port);
            Assert.IsTrue(port.Kind.IsPort);
            Assert.AreEqual(new Vector2Int(sizeX, sizeY), port.Size);
            Assert.AreEqual(facing, port.PortFacing);
            Assert.AreEqual(BuildingOwner.City, port.Owner);
            Assert.IsFalse(port.IsConnected);

            foreach (Vector2Int cell in Cells(Origin.x, Origin.y, sizeX, sizeY))
            {
                Assert.AreSame(port, grid.BuildingAt(cell), cell.ToString());
            }
        }

        [Test]
        public void PlacePort_Twice_Throws()
        {
            CityGrid grid = GridWithWater(Cells(0, 8, 10, 2));
            grid.PlacePort(Port, new PortSite(new Vector2Int(0, 7), PortFacing.North));

            Assert.Throws<InvalidOperationException>(
                () => grid.PlacePort(Port, new PortSite(new Vector2Int(5, 7), PortFacing.North)));
        }

        [Test]
        public void PlacePort_WhereItCannotStand_Throws()
        {
            CityGrid grid = GridWithWater(new Vector2Int[0]);

            Assert.Throws<InvalidOperationException>(() => grid.PlacePort(Port, new PortSite(Origin, PortFacing.North)));
            Assert.IsNull(grid.Port);
        }

        [Test]
        public void Place_WithThePortKind_Throws()
        {
            CityGrid grid = GridWithWater(new Vector2Int[0]);

            Assert.Throws<ArgumentException>(
                () => grid.Place(BuildingKind.Port(), BuildingOwner.City, new Footprint(4, 3), At(0, 0)));
        }

        [Test]
        public void ARoadTouchingOnlyThePort_IsNotANetworkRoad()
        {
            // Port facing north on the top rows, land row y 7, x 0..3; town hall far away in the bottom right corner.
            CityGrid grid = new CityGrid(12, 12, Cells(0, 8, 12, 4));
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(2, 2), At(10, 0));
            grid.PlacePort(Port, new PortSite(new Vector2Int(0, 7), PortFacing.North));
            var connected = new List<PlacedBuilding>();
            grid.BuildingConnected += connected.Add;

            grid.Place(BuildingKind.Road(), BuildingOwner.City, new Footprint(1, 1), At(1, 6));
            PlacedBuilding house = grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, new Footprint(2, 1), At(1, 5));

            Assert.IsFalse(grid.IsNetworkRoad(new Vector2Int(1, 6)));
            Assert.IsFalse(grid.JoinsNetwork(new Vector2Int(2, 6)));
            Assert.IsFalse(house.IsConnected);
            CollectionAssert.IsEmpty(connected);
        }

        [Test]
        public void CanPlace_StillRefusesWater()
        {
            CityGrid grid = GridWithWater(new[] { new Vector2Int(2, 2) });

            Assert.IsFalse(grid.CanPlace(new Footprint(1, 1), At(2, 2)));
        }

        [TestCase(0)]
        [TestCase(3)]
        public void PortFootprint_RefusesWaterRowsOutsideOneToDepthMinusOne(int waterRows)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PortFootprint(new Footprint(4, 3), waterRows));
        }

        [Test]
        public void PortFacings_Turned_GoesRound()
        {
            Assert.AreEqual(PortFacing.East, PortFacings.Turned(PortFacing.North));
            Assert.AreEqual(PortFacing.South, PortFacings.Turned(PortFacing.East));
            Assert.AreEqual(PortFacing.West, PortFacings.Turned(PortFacing.South));
            Assert.AreEqual(PortFacing.North, PortFacings.Turned(PortFacing.West));
            Assert.AreEqual(BuildingOrientation.Along, PortFacings.OrientationOf(PortFacing.North));
            Assert.AreEqual(BuildingOrientation.Along, PortFacings.OrientationOf(PortFacing.South));
            Assert.AreEqual(BuildingOrientation.Across, PortFacings.OrientationOf(PortFacing.East));
            Assert.AreEqual(BuildingOrientation.Across, PortFacings.OrientationOf(PortFacing.West));
        }
    }
}
