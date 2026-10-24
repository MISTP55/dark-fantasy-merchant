using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TownHallPlacementTests
    {
        private static readonly Footprint TownHall = new Footprint(3, 2);

        [Test]
        public void TryNear_ChoosesTheNearestCentre_AndTheFirstOfATie()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0]);

            Assert.IsTrue(TownHallPlacement.TryNear(grid, TownHall, new Vector2(5, 5), out GridPlace place));

            // Four places have their centre half a cell from (5, 5); (3, 4) Along comes first by x.
            Assert.AreEqual(new Vector2Int(3, 4), place.Origin);
            Assert.AreEqual(BuildingOrientation.Along, place.Orientation);
        }

        [Test]
        public void TryNear_SkipsForbiddenCells()
        {
            var grid = new CityGrid(10, 10, new[] { new Vector2Int(5, 4), new Vector2Int(5, 5), new Vector2Int(4, 4), new Vector2Int(4, 5) });

            Assert.IsTrue(TownHallPlacement.TryNear(grid, TownHall, new Vector2(5, 5), out GridPlace place));

            Assert.IsTrue(grid.CanPlace(TownHall, place));
        }

        [Test]
        public void TryNear_BreaksATieByTheOrderOfGetPlaces()
        {
            var grid = new CityGrid(6, 6, new Vector2Int[0]);

            // The 3 by 2 block and the 2 by 3 one at (0, 0) have their centres, (1.5, 1) and (1, 1.5), equally far.
            Assert.IsTrue(TownHallPlacement.TryNear(grid, TownHall, new Vector2(1.25f, 1.25f), out GridPlace place));

            Assert.AreEqual(new Vector2Int(0, 0), place.Origin);
            Assert.AreEqual(BuildingOrientation.Along, place.Orientation);
        }

        [Test]
        public void TryNear_IsFalse_WhenItFitsNowhere()
        {
            var grid = new CityGrid(2, 2, new Vector2Int[0]);

            Assert.IsFalse(TownHallPlacement.TryNear(grid, TownHall, new Vector2(1, 1), out _));
        }
    }
}
