using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TradingPostPlacementTests
    {
        private static readonly Footprint Post = new Footprint(2, 3);

        [Test]
        public void TryNear_PicksTheNearestPlaceWithAFreeRing()
        {
            // 4 cells wide: only the post lying Along at x 1 keeps a ring inside the grid. The road at (0, 0) is on
            // the ring of (1, 1), whose middle is the centre itself, so the nearest is the next one up.
            var grid = new CityGrid(4, 12, new Vector2Int[0]);
            grid.Place(BuildingKind.Road(), BuildingOwner.City, new Footprint(1, 1), new GridPlace(new Vector2Int(0, 0), BuildingOrientation.Along));
            Assert.IsTrue(grid.CanPlace(Post, new GridPlace(new Vector2Int(1, 1), BuildingOrientation.Along)));

            Assert.IsTrue(TradingPostPlacement.TryNear(grid, Post, new Vector2(2f, 2.5f), out GridPlace place));

            Assert.AreEqual(new Vector2Int(1, 2), place.Origin);
            Assert.AreEqual(BuildingOrientation.Along, place.Orientation);
        }

        [Test]
        public void TryNear_IsFalseWhenNoPlaceHasAFreeRing()
        {
            // The post fits on x 0..2, but every ring inside the grid reaches the water on x 3.
            var water = new List<Vector2Int>();
            for (int y = 0; y < 12; y++)
            {
                water.Add(new Vector2Int(3, y));
            }

            var grid = new CityGrid(4, 12, water);
            Assert.IsTrue(grid.CanPlace(Post, new GridPlace(new Vector2Int(1, 4), BuildingOrientation.Along)));

            Assert.IsFalse(TradingPostPlacement.TryNear(grid, Post, new Vector2(2f, 6f), out _));
        }
    }
}
