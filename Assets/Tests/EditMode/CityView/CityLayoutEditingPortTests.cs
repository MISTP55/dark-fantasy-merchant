using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityLayoutEditingPortTests
    {
        // Water on the two top rows of a 12 by 12 layout; the port faces north on x 0..3, its land row y 9.
        private static readonly PortSite Port = new PortSite(new Vector2Int(0, 9), PortFacing.North);

        private readonly List<Object> created = new List<Object>();

        private BuildingsDefinition buildings;
        private CityLayoutDefinition layout;

        [SetUp]
        public void SetUp()
        {
            buildings = Track(TestBuildings.CreateCatalogue());
            layout = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
            layout.SetContent(12, 12, Water(), new CityLayoutDefinition.StartingHouse[0], Along(4, 0), new Vector2Int[0], null);
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

        private void AssertPort(PortSite expected)
        {
            Assert.IsTrue(layout.HasPort);
            Assert.AreEqual(expected.Origin, layout.PortSite.Origin);
            Assert.AreEqual(expected.Facing, layout.PortSite.Facing);
        }

        [Test]
        public void SetPort_PlacesAndMovesThePort()
        {
            Assert.IsTrue(CityLayoutEditing.SetPort(layout, buildings, Port));
            AssertPort(Port);

            var moved = new PortSite(new Vector2Int(6, 9), PortFacing.North);
            Assert.IsTrue(CityLayoutEditing.SetPort(layout, buildings, moved));
            AssertPort(moved);
        }

        [Test]
        public void SetPort_RefusesASiteThatCannotStand()
        {
            CityLayoutEditing.SetPort(layout, buildings, Port);

            Assert.IsFalse(CityLayoutEditing.SetPort(layout, buildings, new PortSite(new Vector2Int(6, 9), PortFacing.South)));
            Assert.IsFalse(CityLayoutEditing.SetPort(layout, buildings, new PortSite(new Vector2Int(3, 0), PortFacing.North)), "on the town hall");
            AssertPort(Port);
        }

        [Test]
        public void RemovePort_TakesItAway()
        {
            CityLayoutEditing.SetPort(layout, buildings, Port);

            CityLayoutEditing.RemovePort(layout);

            Assert.IsFalse(layout.HasPort);
        }

        [Test]
        public void CanPlacePort_IgnoresThePortsOwnPlace()
        {
            CityLayoutEditing.SetPort(layout, buildings, Port);

            Assert.IsTrue(CityLayoutEditing.CanPlacePort(layout, buildings, new PortSite(new Vector2Int(1, 9), PortFacing.North)));
        }

        [Test]
        public void IsUnderPort_CoversItsTwelveCells()
        {
            CityLayoutEditing.SetPort(layout, buildings, Port);

            Assert.IsTrue(CityLayoutEditing.IsUnderPort(layout, buildings, new Vector2Int(0, 9)));
            Assert.IsTrue(CityLayoutEditing.IsUnderPort(layout, buildings, new Vector2Int(3, 11)));
            Assert.IsFalse(CityLayoutEditing.IsUnderPort(layout, buildings, new Vector2Int(4, 9)));
            Assert.IsFalse(CityLayoutEditing.IsUnderPort(layout, buildings, new Vector2Int(0, 8)));
        }

        [Test]
        public void SetForbidden_LeavesTheCellsUnderThePort()
        {
            CityLayoutEditing.SetPort(layout, buildings, Port);

            CityLayoutEditing.SetForbidden(layout, buildings, new[] { new Vector2Int(1, 10) }, false);
            CityLayoutEditing.SetForbidden(layout, buildings, new[] { new Vector2Int(1, 9) }, true);

            CollectionAssert.Contains(layout.ForbiddenCells, new Vector2Int(1, 10));
            CollectionAssert.DoesNotContain(layout.ForbiddenCells, new Vector2Int(1, 9));
            Assert.IsTrue(CityLayoutEditing.TryMakeGrid(layout, buildings, out _));
        }

        [Test]
        public void SetRoadsAndTownHall_StayOffThePort()
        {
            CityLayoutEditing.SetPort(layout, buildings, Port);

            CityLayoutEditing.SetRoads(layout, buildings, new[] { new Vector2Int(2, 9) }, true);
            Assert.IsFalse(CityLayoutEditing.CanPlaceTownHall(layout, buildings, Along(2, 8)));

            CollectionAssert.IsEmpty(layout.Roads);
        }

        [Test]
        public void Resize_DropsAPortThatFallsOutside()
        {
            CityLayoutEditing.SetPort(layout, buildings, Port);

            CityLayoutEditing.Resize(layout, buildings, 12, 11);

            Assert.IsFalse(layout.HasPort);
        }

        [Test]
        public void EveryEdit_KeepsThePort()
        {
            CityLayoutEditing.SetPort(layout, buildings, Port);

            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(8, 4));
            AssertPort(Port);
            CityLayoutEditing.SetRoads(layout, buildings, new[] { new Vector2Int(8, 5) }, true);
            AssertPort(Port);
            CityLayoutEditing.SetForbidden(layout, buildings, new[] { new Vector2Int(11, 0) }, true);
            AssertPort(Port);
            CityLayoutEditing.SetTownHall(layout, buildings, Along(4, 2));
            AssertPort(Port);
            CityLayoutEditing.RemoveTownHall(layout);
            AssertPort(Port);
            CityLayoutEditing.RemoveHouses(layout, buildings, new[] { new Vector2Int(8, 4) });
            AssertPort(Port);
            CityLayoutEditing.Resize(layout, buildings, 14, 12);
            AssertPort(Port);
        }

        [Test]
        public void ACatalogueWithoutAPort_MakesNoGridAndThrowsNothing()
        {
            CityLayoutEditing.SetPort(layout, buildings, Port);
            Object.DestroyImmediate(buildings.Port);
            TestBuildings.SetPort(buildings, null);

            Assert.IsFalse(CityLayoutEditing.TryMakeGrid(layout, buildings, out _));
            Assert.IsFalse(CityLayoutEditing.CanPlacePort(layout, buildings, Port));
            Assert.IsFalse(CityLayoutEditing.IsUnderPort(layout, buildings, new Vector2Int(0, 9)));
            CityLayoutEditing.SetRoads(layout, buildings, new[] { new Vector2Int(6, 6) }, true);
            CollectionAssert.Contains(layout.Roads, new Vector2Int(6, 6));
        }

        [Test]
        public void TryMakeGrid_Fails_ForAPortThatCannotStand()
        {
            layout.SetContent(12, 12, Water(), new CityLayoutDefinition.StartingHouse[0], Along(4, 0), new Vector2Int[0],
                new PortSite(new Vector2Int(0, 9), PortFacing.South));

            Assert.IsFalse(CityLayoutEditing.TryMakeGrid(layout, buildings, out _));
        }
    }
}
