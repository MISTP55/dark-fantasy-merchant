using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityGridTests
    {
        private static GridPlace At(int x, int y, BuildingOrientation orientation = BuildingOrientation.Along)
        {
            return new GridPlace(new Vector2Int(x, y), orientation);
        }

        private static CityGrid Empty(int size)
        {
            return new CityGrid(size, size, new Vector2Int[0]);
        }

        [Test]
        public void Footprint_IsTurnedAcross() =>
            Assert.AreEqual(new Vector2Int(1, 2), new Footprint(2, 1).SizeFor(BuildingOrientation.Across));

        [Test]
        public void Footprint_Throws_ForANullSide()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Footprint(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Footprint(1, 0));
        }

        [Test]
        public void CanPlace_AcceptsAFreePlace() =>
            Assert.IsTrue(Empty(4).CanPlace(new Footprint(2, 1), At(2, 3)));

        [Test]
        public void CanPlace_RefusesAFootprintThatLeavesTheGrid()
        {
            var grid = Empty(4);
            Assert.IsFalse(grid.CanPlace(new Footprint(2, 1), At(3, 0)));
            Assert.IsFalse(grid.CanPlace(new Footprint(2, 1), At(0, 3, BuildingOrientation.Across)));
            Assert.IsFalse(grid.CanPlace(new Footprint(2, 1), At(-1, 0)));
        }

        [Test]
        public void CanPlace_RefusesOneForbiddenOrTakenCell()
        {
            var grid = new CityGrid(4, 4, new[] { new Vector2Int(1, 1) });
            Assert.IsFalse(grid.CanPlace(new Footprint(2, 2), At(0, 0)));
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, new Footprint(2, 1), At(2, 2));
            Assert.IsFalse(grid.CanPlace(new Footprint(2, 2), At(1, 2)));
            Assert.IsTrue(grid.CanPlace(new Footprint(2, 2), At(2, 0)));
        }

        [Test]
        public void Place_CoversEveryCellOfItsFootprint()
        {
            var grid = Empty(4);
            PlacedBuilding building = grid.Place(BuildingKind.Workshop(3), BuildingOwner.Player, new Footprint(2, 1), At(0, 0, BuildingOrientation.Across));
            Assert.AreSame(building, grid.BuildingAt(new Vector2Int(0, 0)));
            Assert.AreSame(building, grid.BuildingAt(new Vector2Int(0, 1)));
            Assert.IsNull(grid.BuildingAt(new Vector2Int(1, 0)));
            Assert.IsNull(grid.BuildingAt(new Vector2Int(-1, 0)));
            Assert.AreEqual(new Vector2Int(1, 2), building.Size);
            Assert.AreEqual(3, building.Kind.Good);
            Assert.AreEqual(BuildingOwner.Player, building.Owner);
        }

        [Test]
        public void Place_Throws_WhenThePlaceIsTaken()
        {
            var grid = Empty(4);
            grid.Place(BuildingKind.House(SocialClass.Rich), BuildingOwner.City, new Footprint(2, 2), At(0, 0));
            Assert.Throws<InvalidOperationException>(() =>
                grid.Place(BuildingKind.House(SocialClass.Rich), BuildingOwner.City, new Footprint(1, 1), At(1, 1)));
            Assert.AreEqual(1, grid.Buildings.Count);
        }

        [Test]
        public void Place_RaisesBuildingPlaced()
        {
            var grid = Empty(4);
            var raised = new List<PlacedBuilding>();
            grid.BuildingPlaced += raised.Add;
            PlacedBuilding building = grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, new Footprint(1, 1), At(1, 1));
            Assert.AreEqual(1, raised.Count);
            Assert.AreSame(building, raised[0]);
        }

        [Test]
        public void GetPlaces_ListsBothOrientations_ButOnceForASquare()
        {
            var grid = Empty(2);
            var places = new List<GridPlace> { At(9, 9) };
            grid.GetPlaces(new Footprint(2, 1), places);
            Assert.AreEqual(4, places.Count);
            Assert.AreEqual(2, places.FindAll(p => p.Orientation == BuildingOrientation.Along).Count);
            Assert.AreEqual(BuildingOrientation.Along, places[0].Orientation);
            grid.GetPlaces(new Footprint(2, 2), places);
            Assert.AreEqual(1, places.Count);
        }

        [Test]
        public void HousesOf_CountsTheHousesOfAClass()
        {
            var grid = Empty(4);
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, new Footprint(1, 1), At(0, 0));
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.Player, new Footprint(1, 1), At(1, 0));
            grid.Place(BuildingKind.House(SocialClass.Rich), BuildingOwner.City, new Footprint(1, 1), At(2, 0));
            grid.Place(BuildingKind.Workshop(0), BuildingOwner.City, new Footprint(1, 1), At(3, 0));
            Assert.AreEqual(2, grid.HousesOf(SocialClass.Poor));
            Assert.AreEqual(1, grid.HousesOf(SocialClass.Rich));
            Assert.AreEqual(0, grid.HousesOf(SocialClass.Wealthy));
        }

        [Test]
        public void Constructor_Throws_ForAForbiddenCellOutside() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityGrid(4, 4, new[] { new Vector2Int(4, 0) }));
    }
}
