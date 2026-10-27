using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WallSidesTests
    {
        private static readonly Footprint Cell = new Footprint(1, 1);

        private static void Place(CityGrid grid, BuildingKind kind, int x, int y)
        {
            grid.Place(kind, BuildingOwner.City, Cell, new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along));
        }

        [Test]
        public void SidesOf_GivesTheSidesWithAWall_AndNeverACorner()
        {
            var grid = new CityGrid(5, 5, new Vector2Int[0], RoadContact.SidesOnly);
            Place(grid, BuildingKind.Wall(), 2, 2);
            Place(grid, BuildingKind.Wall(), 3, 2);
            Place(grid, BuildingKind.Wall(), 2, 3);
            Place(grid, BuildingKind.Wall(), 1, 1);

            Assert.AreEqual(WallSides.East | WallSides.North, WallShapes.SidesOf(grid, new Vector2Int(2, 2)));
            Assert.AreEqual(WallSides.West, WallShapes.SidesOf(grid, new Vector2Int(3, 2)));
            Assert.AreEqual(WallSides.South, WallShapes.SidesOf(grid, new Vector2Int(2, 3)));
            Assert.AreEqual(WallSides.None, WallShapes.SidesOf(grid, new Vector2Int(1, 1)));
        }

        [Test]
        public void SidesOf_CountsOnlyWalls_NeitherOtherBuildingsNorWaterNorTheEdge()
        {
            var grid = new CityGrid(3, 3, new[] { new Vector2Int(1, 2) }, RoadContact.SidesOnly);
            Place(grid, BuildingKind.Wall(), 1, 1);
            Place(grid, BuildingKind.Road(), 0, 1);
            Place(grid, BuildingKind.Wall(), 0, 0);
            Place(grid, BuildingKind.Wall(), 2, 0);

            Assert.AreEqual(WallSides.None, WallShapes.SidesOf(grid, new Vector2Int(1, 1)));
            Assert.AreEqual(WallSides.None, WallShapes.SidesOf(grid, new Vector2Int(0, 0)));
        }

        [TestCase(WallSides.East | WallSides.West, true)]
        [TestCase(WallSides.North | WallSides.South, true)]
        [TestCase(WallSides.None, false)]
        [TestCase(WallSides.East, false)]
        [TestCase(WallSides.East | WallSides.North, false)]
        [TestCase(WallSides.East | WallSides.West | WallSides.North, false)]
        [TestCase(WallSides.East | WallSides.West | WallSides.North | WallSides.South, false)]
        public void IsStraight_OnlyForTwoOppositeSides(WallSides sides, bool straight)
        {
            Assert.AreEqual(straight, WallShapes.IsStraight(sides));
        }
    }
}
