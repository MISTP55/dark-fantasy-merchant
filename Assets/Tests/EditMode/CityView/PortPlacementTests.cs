using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PortPlacementTests
    {
        private static readonly PortFootprint Port = new PortFootprint(new Footprint(4, 3), 2);

        [Test]
        public void TryNear_FindsTheSiteNearestTheCentre()
        {
            var water = new List<Vector2Int>();
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 20; x++)
                {
                    water.Add(new Vector2Int(x, y));
                }
            }

            var grid = new CityGrid(20, 20, water);

            Assert.IsTrue(PortPlacement.TryNear(grid, Port, new Vector2(10f, 4f), out PortSite site));
            Assert.AreEqual(PortFacing.South, site.Facing);
            Assert.AreEqual(2, site.Origin.y);
            Assert.IsTrue(grid.CanPlacePort(Port, site));
        }

        [Test]
        public void TryNear_Fails_WithoutWater()
        {
            var grid = new CityGrid(20, 20, new Vector2Int[0]);

            Assert.IsFalse(PortPlacement.TryNear(grid, Port, new Vector2(10f, 10f), out _));
        }
    }
}
