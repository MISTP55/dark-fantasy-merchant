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

            CityLayoutEditing.ToggleForbidden(layout, new Vector2Int(6, 6));
            Assert.IsFalse(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(5, 6)));
            Assert.IsFalse(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(9, 9)));
            Assert.AreEqual(1, layout.StartingHouses.Count);
        }

        [Test]
        public void ToggleForbidden_IsRefusedUnderAHouse()
        {
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(2, 2));

            CityLayoutEditing.ToggleForbidden(layout, new Vector2Int(3, 2), buildings);
            Assert.AreEqual(0, layout.ForbiddenCells.Count);

            CityLayoutEditing.ToggleForbidden(layout, new Vector2Int(5, 5), buildings);
            Assert.AreEqual(1, layout.ForbiddenCells.Count);
            CityLayoutEditing.ToggleForbidden(layout, new Vector2Int(5, 5), buildings);
            Assert.AreEqual(0, layout.ForbiddenCells.Count);
        }

        [Test]
        public void Resize_DropsTheHousesThatNoLongerFit()
        {
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(0, 0));
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(7, 7));
            CityLayoutEditing.ToggleForbidden(layout, new Vector2Int(9, 9));
            CityLayoutEditing.ToggleForbidden(layout, new Vector2Int(1, 5));

            // The house at (7, 7) covers x 7 and 8: it no longer fits in 8 cells.
            CityLayoutEditing.Resize(layout, 8, 8, buildings);

            Assert.AreEqual(8, layout.Width);
            Assert.AreEqual(8, layout.Height);
            Assert.AreEqual(1, layout.StartingHouses.Count);
            Assert.AreEqual(Vector2Int.zero, layout.StartingHouses[0].origin);
            Assert.AreEqual(1, layout.ForbiddenCells.Count);
            Assert.AreEqual(new Vector2Int(1, 5), layout.ForbiddenCells[0]);
        }

        [Test]
        public void RemoveHouseAt_RemovesTheHouseCoveringTheCell()
        {
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(2, 2));
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Rich, Along(5, 5));

            CityLayoutEditing.RemoveHouseAt(layout, new Vector2Int(3, 2), buildings);

            Assert.AreEqual(1, layout.StartingHouses.Count);
            Assert.AreEqual(SocialClass.Rich, layout.StartingHouses[0].socialClass);

            CityLayoutEditing.RemoveHouseAt(layout, new Vector2Int(5, 5), buildings);
            Assert.AreEqual(0, layout.StartingHouses.Count);
        }
    }
}
