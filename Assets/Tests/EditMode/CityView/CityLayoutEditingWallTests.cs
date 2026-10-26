using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityLayoutEditingWallTests
    {
        // Water on the two top rows of a 12 by 12 layout; the port faces north on x 0..3, its land row y 9; a berth
        // beside it; the town hall on x 4..6, y 0..1.
        private static readonly PortSite Port = new PortSite(new Vector2Int(0, 9), PortFacing.North);
        private static readonly Vector2Int Berth = new Vector2Int(6, 11);

        private readonly List<Object> created = new List<Object>();

        private BuildingsDefinition buildings;
        private CityLayoutDefinition layout;

        [SetUp]
        public void SetUp()
        {
            buildings = Track(TestBuildings.CreateCatalogue());
            layout = Track(ScriptableObject.CreateInstance<CityLayoutDefinition>());
            layout.SetContent(12, 12, Water(), new CityLayoutDefinition.StartingHouse[0], Along(4, 0), new Vector2Int[0], Port, new[] { Berth });
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
        public void SetWalls_AddsAndRemoves()
        {
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(8, 5), new Vector2Int(9, 5) }, true);

            CollectionAssert.AreEquivalent(new[] { new Vector2Int(8, 5), new Vector2Int(9, 5) }, layout.Walls);

            // Adding again changes nothing: a cell is never listed twice.
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(8, 5) }, true);
            Assert.AreEqual(2, layout.Walls.Count);

            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(8, 5), new Vector2Int(1, 1) }, false);
            CollectionAssert.AreEqual(new[] { new Vector2Int(9, 5) }, layout.Walls);
        }

        [Test]
        public void SetWalls_IsOneUndoStep()
        {
            Undo.IncrementCurrentGroup();
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(8, 5), new Vector2Int(9, 5) }, true);
            Undo.PerformUndo();

            Assert.AreEqual(0, layout.Walls.Count);
        }

        [Test]
        public void SetWalls_SkipsWaterBuildingsPortBerthsAndOutside()
        {
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(1, 3));
            CityLayoutEditing.SetRoads(layout, buildings, new[] { new Vector2Int(8, 0) }, true);

            CityLayoutEditing.SetWalls(layout, buildings, new[]
            {
                new Vector2Int(5, 10), // water
                Berth,
                new Vector2Int(1, 9), // the port's land row
                new Vector2Int(5, 1), // the town hall
                new Vector2Int(2, 3), // the house
                new Vector2Int(8, 0), // the road
                new Vector2Int(12, 4), // outside
            }, true);

            Assert.AreEqual(0, layout.Walls.Count);
        }

        [Test]
        public void SetForbidden_SkipsWalls()
        {
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(8, 5) }, true);

            CityLayoutEditing.SetForbidden(layout, buildings, new[] { new Vector2Int(8, 5) }, true);

            CollectionAssert.DoesNotContain(layout.ForbiddenCells, new Vector2Int(8, 5));
            CollectionAssert.Contains(layout.Walls, new Vector2Int(8, 5));
        }

        [Test]
        public void TheOtherBuildings_RefuseAWallsCell()
        {
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(8, 5) }, true);

            CityLayoutEditing.SetRoads(layout, buildings, new[] { new Vector2Int(8, 5) }, true);
            Assert.AreEqual(0, layout.Roads.Count);

            Assert.IsFalse(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(7, 5)));
            Assert.IsFalse(CityLayoutEditing.SetTownHall(layout, buildings, Along(7, 4)));
            Assert.AreEqual(new Vector2Int(4, 0), layout.TownHallPlace.Origin);

            // A port at (4, 9) would stand on the wall at (5, 9), on its land row.
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(5, 9) }, true);
            Assert.IsFalse(CityLayoutEditing.SetPort(layout, buildings, new PortSite(new Vector2Int(4, 9), PortFacing.North)));
            Assert.AreEqual(Port.Origin, layout.PortSite.Origin);
        }

        [Test]
        public void TryAddHouse_OutsideTheWalls_IsAllowed()
        {
            // A closed ring around the town hall, the house beyond it.
            var ring = new List<Vector2Int>();
            for (int x = 3; x <= 7; x++)
            {
                ring.Add(new Vector2Int(x, 2));
            }

            for (int y = 0; y < 2; y++)
            {
                ring.Add(new Vector2Int(3, y));
                ring.Add(new Vector2Int(7, y));
            }

            CityLayoutEditing.SetWalls(layout, buildings, ring, true);

            Assert.IsTrue(CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(8, 6)));
            Assert.AreEqual(1, layout.StartingHouses.Count);
        }

        [Test]
        public void Resize_DropsTheWallsOutside()
        {
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(8, 5), new Vector2Int(11, 5) }, true);

            CityLayoutEditing.Resize(layout, buildings, 10, 12);

            CollectionAssert.AreEqual(new[] { new Vector2Int(8, 5) }, layout.Walls);
        }

        [Test]
        public void TheOtherEdits_KeepTheWalls()
        {
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(8, 5) }, true);

            CityLayoutEditing.SetRoads(layout, buildings, new[] { new Vector2Int(8, 0) }, true);
            CityLayoutEditing.TryAddHouse(layout, buildings, SocialClass.Poor, Along(1, 3));
            CityLayoutEditing.SetForbidden(layout, buildings, new[] { new Vector2Int(9, 9) }, true);
            CityLayoutEditing.SetTownHall(layout, buildings, Along(4, 1));
            CityLayoutEditing.RemoveTownHall(layout);
            CityLayoutEditing.RemovePort(layout);
            CityLayoutEditing.RemoveBerth(layout, Berth);

            CollectionAssert.AreEqual(new[] { new Vector2Int(8, 5) }, layout.Walls);
        }

        [Test]
        public void TryMakeGrid_PlacesTheWalls()
        {
            CityLayoutEditing.SetWalls(layout, buildings, new[] { new Vector2Int(8, 5) }, true);

            Assert.IsTrue(CityLayoutEditing.TryMakeGrid(layout, buildings, out CityGrid grid));
            Assert.IsTrue(grid.BuildingAt(new Vector2Int(8, 5)).Kind.IsWall);
            Assert.IsFalse(grid.KeepsInsideWalls);
        }

        [Test]
        public void TryGenerate_WallsTheDefaultGrid()
        {
            EconomyDefinition economy = AssetDatabase.LoadAssetAtPath<EconomyDefinition>("Assets/Data/Economy/Economy.asset");
            CityDefinition city = Track(ScriptableObject.CreateInstance<CityDefinition>());

            Assert.IsTrue(CityLayoutEditing.TryGenerate(
                city, economy, buildings, null, 1, CityLayoutDefinition.DefaultSize, CityLayoutDefinition.DefaultSize,
                out CityGrid grid, out string problem), problem);

            int walls = 0;
            foreach (PlacedBuilding building in grid.Buildings)
            {
                walls += building.Kind.IsWall ? 1 : 0;
            }

            // The square's three land sides, less the cells where they meet the water.
            Assert.Greater(walls, 3 * (CityLayoutEditing.GeneratedWallSide - 1) - 2 * CityLayoutEditing.GeneratedWaterRows - 4);
            Assert.IsTrue(grid.TryValidateWalls(out string wallProblem), wallProblem);
        }
    }
}
