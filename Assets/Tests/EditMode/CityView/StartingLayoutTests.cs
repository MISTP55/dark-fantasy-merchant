using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class StartingLayoutTests
    {
        private static readonly PortFootprint Port = new PortFootprint(new Footprint(4, 3), 2);
        private static readonly Footprint TownHall = new Footprint(3, 2);
        private static readonly PerClass<Footprint> Houses = new PerClass<Footprint>(new Footprint(2, 1), new Footprint(2, 1), new Footprint(2, 1));

        private static bool TryBuild(PortFacing shore, PerClass<int> houses, out CityGrid grid, out string problem, int size = 40)
        {
            return StartingLayout.TryBuild(
                size, size, shore, 4, Port, TownHall, houses, Houses, RoadContact.SidesOnly, new System.Random(3),
                out grid, out problem);
        }

        private static bool InBand(PortFacing shore, Vector2Int cell, int size)
        {
            switch (shore)
            {
                case PortFacing.North:
                    return cell.y >= size - 4;
                case PortFacing.South:
                    return cell.y < 4;
                case PortFacing.East:
                    return cell.x >= size - 4;
                default:
                    return cell.x < 4;
            }
        }

        [TestCase(PortFacing.North)]
        [TestCase(PortFacing.East)]
        [TestCase(PortFacing.South)]
        [TestCase(PortFacing.West)]
        public void TryBuild_PutsTheWaterThePortTheTownHallAndConnectedHouses(PortFacing shore)
        {
            Assert.IsTrue(TryBuild(shore, new PerClass<int>(2, 3, 5), out CityGrid grid, out string problem), problem);

            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Assert.AreEqual(InBand(shore, cell, 40), grid.IsForbidden(cell), cell.ToString());
                }
            }

            Assert.IsNotNull(grid.Port);
            Assert.AreEqual(shore, grid.Port.PortFacing);
            Assert.IsNotNull(grid.TownHall);
            Assert.AreEqual(2, grid.ConnectedHousesOf(SocialClass.Rich));
            Assert.AreEqual(3, grid.ConnectedHousesOf(SocialClass.Wealthy));
            Assert.AreEqual(5, grid.ConnectedHousesOf(SocialClass.Poor));
            Assert.IsNull(problem);
        }

        [Test]
        public void TryBuild_PutsThePortInTheMiddleOfTheShore()
        {
            Assert.IsTrue(TryBuild(PortFacing.South, new PerClass<int>(0, 0, 0), out CityGrid grid, out _));

            // Its land row on the first row of land, its centre on the middle of the edge.
            Assert.AreEqual(2, grid.Port.Place.Origin.y);
            Assert.AreEqual(18, grid.Port.Place.Origin.x);
        }

        [Test]
        public void TryBuild_Fails_WhenTheHousesDoNotFit()
        {
            Assert.IsFalse(TryBuild(PortFacing.South, new PerClass<int>(0, 0, 500), out CityGrid grid, out string problem, 12));
            Assert.IsNull(grid);
            StringAssert.Contains("houses", problem);
        }

        [Test]
        public void TryBuild_Fails_WhenThePortHasNoRoom()
        {
            // A grid narrower than the port.
            Assert.IsFalse(TryBuild(PortFacing.South, new PerClass<int>(0, 0, 0), out CityGrid grid, out string problem, 3));
            Assert.IsNull(grid);
            StringAssert.Contains("port", problem);
        }
    }
}
