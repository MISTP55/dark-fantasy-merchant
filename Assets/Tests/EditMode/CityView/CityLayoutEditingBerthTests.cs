using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityLayoutEditingBerthTests
    {
        // Water on the two top rows of a 12 by 12 layout; the port faces north on x 0..3, y 9..11.
        private static readonly PortSite Port = new PortSite(new Vector2Int(0, 9), PortFacing.North);

        private readonly List<Object> created = new List<Object>();

        private BuildingsDefinition buildings;
        private CityLayoutDefinition layout;

        [SetUp]
        public void SetUp()
        {
            buildings = Track(TestBuildings.CreateCatalogue());
            layout = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
            layout.SetContent(12, 12, Water(), new CityLayoutDefinition.StartingHouse[0], Along(4, 0), new Vector2Int[0], Port, new Vector2Int[0]);
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

        private static List<Vector2Int> Water()
        {
            var water = new List<Vector2Int>();
            for (int y = 10; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    water.Add(new Vector2Int(x, y));
                }
            }

            return water;
        }

        private static GridPlace Along(int x, int y)
        {
            return new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
        }

        [Test]
        public void AddBerth_OnWater_AddsOne()
        {
            Assert.IsTrue(CityLayoutEditing.CanPlaceBerth(layout, buildings, new Vector2Int(6, 10)));
            Assert.IsTrue(CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(6, 10)));

            CollectionAssert.AreEqual(new[] { new Vector2Int(6, 10) }, layout.Berths);
        }

        [Test]
        public void AddBerth_OnLandOrUnderThePortOrTwice_IsRefused()
        {
            Assert.IsFalse(CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(6, 5)), "land");
            Assert.IsFalse(CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(1, 10)), "under the port");
            Assert.IsFalse(CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(12, 10)), "outside");
            Assert.IsTrue(CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(6, 10)));
            Assert.IsFalse(CityLayoutEditing.CanPlaceBerth(layout, buildings, new Vector2Int(6, 10)), "twice");
            Assert.IsFalse(CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(6, 10)), "twice");

            Assert.AreEqual(1, layout.Berths.Count);
        }

        [Test]
        public void RemoveBerth_RemovesIt()
        {
            CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(6, 10));

            Assert.IsFalse(CityLayoutEditing.RemoveBerth(layout, new Vector2Int(7, 10)));
            Assert.IsTrue(CityLayoutEditing.RemoveBerth(layout, new Vector2Int(6, 10)));

            CollectionAssert.IsEmpty(layout.Berths);
        }

        [Test]
        public void FreeingWater_UnderABerth_DropsIt()
        {
            CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(6, 10));
            CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(7, 10));

            CityLayoutEditing.SetForbidden(layout, buildings, new[] { new Vector2Int(6, 10) }, false);

            CollectionAssert.AreEqual(new[] { new Vector2Int(7, 10) }, layout.Berths);
        }

        [Test]
        public void MovingThePort_OverABerth_DropsIt()
        {
            CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(5, 10));
            CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(11, 10));

            Assert.IsTrue(CityLayoutEditing.SetPort(layout, buildings, new PortSite(new Vector2Int(4, 9), PortFacing.North)));

            CollectionAssert.AreEqual(new[] { new Vector2Int(11, 10) }, layout.Berths);
        }

        [Test]
        public void Resize_DropsBerthsOutside()
        {
            CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(6, 10));
            CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(11, 10));

            CityLayoutEditing.Resize(layout, buildings, 8, 12);

            CollectionAssert.AreEqual(new[] { new Vector2Int(6, 10) }, layout.Berths);
        }

        [Test]
        public void TryMakeGrid_PlacesTheBerths()
        {
            CityLayoutEditing.AddBerth(layout, buildings, new Vector2Int(6, 10));

            Assert.IsTrue(CityLayoutEditing.TryMakeGrid(layout, buildings, out CityGrid grid));
            Assert.IsTrue(grid.IsBerth(new Vector2Int(6, 10)));
        }

        [Test]
        public void TryMakeGrid_Fails_WhenABerthCannotStand()
        {
            layout.SetContent(12, 12, Water(), new CityLayoutDefinition.StartingHouse[0], Along(4, 0), new Vector2Int[0], Port, new[] { new Vector2Int(6, 3) });

            Assert.IsFalse(CityLayoutEditing.TryMakeGrid(layout, buildings, out _));
        }
    }
}
