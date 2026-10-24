using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityLayoutEditingTests
    {
        private readonly List<Object> created = new List<Object>();

        private BuildingsDefinition buildings;
        private CityLayoutDefinition layout;

        [SetUp]
        public void SetUp()
        {
            buildings = Track(TestBuildings.CreateCatalogue());
            layout = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
            layout.SetContent(10, 10, new Vector2Int[0], new CityLayoutDefinition.StartingHouse[0]);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object instance in created)
            {
                Object.DestroyImmediate(instance);
            }

            created.Clear();
        }

        private T Track<T>(T instance) where T : Object
        {
            created.Add(instance);
            return instance;
        }

        private void Forbid(Vector2Int cell)
        {
            CityLayoutEditing.SetForbidden(layout, buildings, new[] { cell }, true);
        }

        private static GridPlace Along(int x, int y)
        {
            return new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
        }

        [Test]
        public void TryAddHouse_RefusesAPlaceThatIsTaken()
        {
            Assert.IsTrue(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(2, 2)));

            Assert.IsFalse(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(3, 2)));
            Assert.AreEqual(1, layout.StartingHouses.Count);

            Forbid(new Vector2Int(6, 6));
            Assert.IsFalse(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(5, 6)));
            Assert.IsFalse(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(9, 9)));
            Assert.AreEqual(1, layout.StartingHouses.Count);
        }

        [Test]
        public void SetForbidden_SkipsTheCellsUnderAHouseAndOutsideTheGrid()
        {
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(2, 2));

            CityLayoutEditing.SetForbidden(
                layout, buildings, new[] { new Vector2Int(3, 2), new Vector2Int(5, 5), new Vector2Int(10, 5) }, true);
            Assert.AreEqual(1, layout.ForbiddenCells.Count);
            Assert.AreEqual(new Vector2Int(5, 5), layout.ForbiddenCells[0]);

            // Forbidding again changes nothing: a cell is never listed twice.
            Forbid(new Vector2Int(5, 5));
            Assert.AreEqual(1, layout.ForbiddenCells.Count);

            CityLayoutEditing.SetForbidden(layout, buildings, new[] { new Vector2Int(5, 5), new Vector2Int(6, 6) }, false);
            Assert.AreEqual(0, layout.ForbiddenCells.Count);
        }

        [Test]
        public void BrushCells_SquareIsCutToTheGrid()
        {
            Assert.AreEqual(9, CityLayoutEditing.BrushCells(new Vector2Int(5, 5), 3, false, 10, 10).Count);
            Assert.AreEqual(4, CityLayoutEditing.BrushCells(new Vector2Int(0, 0), 3, false, 10, 10).Count);

            // Even: one cell more on the low side.
            List<Vector2Int> even = CityLayoutEditing.BrushCells(new Vector2Int(5, 5), 2, false, 10, 10);
            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(4, 5), new Vector2Int(5, 5) }, even);
        }

        [Test]
        public void BrushCells_RoundLeavesTheCornersOut()
        {
            List<Vector2Int> three = CityLayoutEditing.BrushCells(new Vector2Int(5, 5), 3, true, 10, 10);
            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(5, 5), new Vector2Int(4, 5), new Vector2Int(6, 5), new Vector2Int(5, 4), new Vector2Int(5, 6) },
                three);

            List<Vector2Int> five = CityLayoutEditing.BrushCells(new Vector2Int(10, 10), 5, true, 20, 20);
            Assert.AreEqual(21, five.Count);
            CollectionAssert.DoesNotContain(five, new Vector2Int(8, 8));
            CollectionAssert.Contains(five, new Vector2Int(8, 9));

            Assert.AreEqual(1, CityLayoutEditing.BrushCells(new Vector2Int(0, 0), 1, true, 10, 10).Count);
            Assert.AreEqual(4, CityLayoutEditing.BrushCells(new Vector2Int(5, 5), 2, true, 10, 10).Count);
        }

        [Test]
        public void Resize_DropsTheHousesThatNoLongerFit()
        {
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(0, 0));
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(7, 7));
            Forbid(new Vector2Int(9, 9));
            Forbid(new Vector2Int(1, 5));

            // The house at (7, 7) covers x 7 and 8: it no longer fits in 8 cells.
            CityLayoutEditing.Resize(layout, buildings, 8, 8);

            Assert.AreEqual(8, layout.Width);
            Assert.AreEqual(8, layout.Height);
            Assert.AreEqual(1, layout.StartingHouses.Count);
            Assert.AreEqual(Vector2Int.zero, layout.StartingHouses[0].origin);
            Assert.AreEqual(1, layout.ForbiddenCells.Count);
            Assert.AreEqual(new Vector2Int(1, 5), layout.ForbiddenCells[0]);
        }

        [Test]
        public void RemoveHouses_RemovesTheHousesCoveringTheCells()
        {
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(2, 2));
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(5, 5));
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(0, 8));

            CityLayoutEditing.RemoveHouses(layout, buildings, new[] { new Vector2Int(3, 2), new Vector2Int(1, 8) });

            Assert.AreEqual(1, layout.StartingHouses.Count);
            Assert.AreEqual(SocialClass.Rich, layout.StartingHouses[0].socialClass);

            CityLayoutEditing.RemoveHouses(layout, buildings, new[] { new Vector2Int(5, 5) });
            Assert.AreEqual(0, layout.StartingHouses.Count);
        }

        [Test]
        public void RemoveHouses_OfAClassLeavesTheOthers()
        {
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(2, 2));
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(4, 2));

            CityLayoutEditing.RemoveHouses(layout, buildings, new[] { new Vector2Int(3, 2), new Vector2Int(4, 2) }, SocialClass.Rich);

            Assert.AreEqual(1, layout.StartingHouses.Count);
            Assert.AreEqual(SocialClass.Poor, layout.StartingHouses[0].socialClass);
        }
    }
}
