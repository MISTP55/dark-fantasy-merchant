using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityGridTradingPostTests
    {
        private static readonly Footprint Post = new Footprint(3, 2);
        private static readonly Footprint TownHall = new Footprint(3, 2);
        private static readonly Footprint House = new Footprint(2, 1);
        private static readonly Footprint Cell = new Footprint(1, 1);
        private static readonly Footprint PortSize = new Footprint(4, 3);

        private static GridPlace At(int x, int y, BuildingOrientation orientation = BuildingOrientation.Along)
        {
            return new GridPlace(new Vector2Int(x, y), orientation);
        }

        private static CityGrid Grid(IEnumerable<Vector2Int> water = null, int size = 12)
        {
            return new CityGrid(size, size, water ?? new Vector2Int[0], RoadContact.SidesOnly);
        }

        // A 10 by 10 grid, its town hall at (4, 4), walled by the outline of x 1..8, y 1..8.
        private static CityGrid Walled()
        {
            CityGrid grid = Grid(null, 10);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(4, 4));
            for (int i = 1; i <= 8; i++)
            {
                foreach (Vector2Int cell in new[] { new Vector2Int(i, 1), new Vector2Int(i, 8), new Vector2Int(1, i), new Vector2Int(8, i) })
                {
                    if (grid.BuildingAt(cell) == null)
                    {
                        grid.Place(BuildingKind.Wall(), BuildingOwner.City, Cell, new GridPlace(cell, BuildingOrientation.Along));
                    }
                }
            }

            return grid;
        }

        [Test]
        public void ReserveTradingPostSite_SetsSiteAndReservesItsCells()
        {
            CityGrid grid = Grid();
            GridPlace place = At(4, 4, BuildingOrientation.Across);

            grid.ReserveTradingPostSite(new Footprint(3, 2), place);

            Assert.AreEqual(place, grid.TradingPostSite);
            for (int y = 4; y <= 6; y++)
            {
                for (int x = 4; x <= 5; x++)
                {
                    Assert.IsTrue(grid.IsReserved(new Vector2Int(x, y)), $"({x}, {y})");
                }
            }

            Assert.IsFalse(grid.IsReserved(new Vector2Int(6, 4)));
            Assert.IsFalse(grid.IsReserved(new Vector2Int(4, 7)));
        }

        [Test]
        public void CanReserveTradingPostSite_RefusesWaterBuildingOffGridAndSecondSite()
        {
            CityGrid grid = Grid(new[] { new Vector2Int(2, 2) });
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, House, At(8, 8));

            Assert.IsFalse(grid.CanReserveTradingPostSite(Post, At(1, 1)), "water");
            Assert.IsFalse(grid.CanReserveTradingPostSite(Post, At(7, 7)), "building");
            Assert.IsFalse(grid.CanReserveTradingPostSite(Post, At(10, 10)), "off the grid");
            Assert.IsTrue(grid.CanReserveTradingPostSite(Post, At(4, 4)));

            grid.ReserveTradingPostSite(Post, At(4, 4));

            Assert.IsFalse(grid.CanReserveTradingPostSite(Post, At(0, 8)), "second site");
            Assert.Throws<InvalidOperationException>(() => grid.ReserveTradingPostSite(Post, At(0, 8)));
        }

        [Test]
        public void CanReserveTradingPostSite_RefusesOutsideWallsWhileKeepingInsideWalls()
        {
            CityGrid grid = Walled();
            Assert.IsTrue(grid.CanReserveTradingPostSite(Cell, At(0, 0)));

            grid.KeepsInsideWalls = true;

            Assert.IsFalse(grid.CanReserveTradingPostSite(Cell, At(0, 0)));
            Assert.IsTrue(grid.CanReserveTradingPostSite(Post, At(2, 2)));
        }

        [Test]
        public void IsBuildable_IsFalseOnReservedCells()
        {
            CityGrid grid = Grid();
            grid.ReserveTradingPostSite(Post, At(4, 4));

            Assert.IsFalse(grid.IsBuildable(new Vector2Int(4, 4)));
            Assert.IsFalse(grid.IsBuildable(new Vector2Int(6, 5)));
            Assert.IsTrue(grid.IsBuildable(new Vector2Int(7, 5)));
        }

        [Test]
        public void CanPlace_RefusesAnyFootprintCoveringAReservedCell()
        {
            CityGrid grid = Grid();
            grid.ReserveTradingPostSite(Post, At(4, 4));

            Assert.IsFalse(grid.CanPlace(House, At(3, 4)), "house overlapping the edge");
            Assert.IsFalse(grid.CanPlace(Cell, At(5, 5)), "road");
            Assert.IsTrue(grid.CanPlace(House, At(2, 4)));
        }

        [Test]
        public void Place_ThrowsForAWallOnAReservedCell()
        {
            CityGrid grid = Grid();
            grid.ReserveTradingPostSite(Post, At(4, 4));

            Assert.Throws<InvalidOperationException>(() =>
                grid.Place(BuildingKind.Wall(), BuildingOwner.City, Cell, At(5, 5)));
        }

        [Test]
        public void CanPlacePort_RefusesASiteCoveringAReservedCell()
        {
            var water = new List<Vector2Int>();
            for (int x = 3; x < 7; x++)
            {
                water.Add(new Vector2Int(x, 4));
                water.Add(new Vector2Int(x, 5));
            }

            CityGrid grid = Grid(water);
            var port = new PortFootprint(PortSize, 2);
            var site = new PortSite(new Vector2Int(3, 3), PortFacing.North);
            Assert.IsTrue(grid.CanPlacePort(port, site));

            grid.ReserveTradingPostSite(Cell, At(3, 3));

            Assert.IsFalse(grid.CanPlacePort(port, site));
        }

        [Test]
        public void GetPlaces_ListsNoPlaceOverlappingTheSite()
        {
            CityGrid grid = Grid();
            grid.ReserveTradingPostSite(Post, At(4, 4));
            var places = new List<GridPlace>();

            grid.GetPlaces(House, places);

            Assert.IsNotEmpty(places);
            foreach (GridPlace place in places)
            {
                Vector2Int size = House.SizeFor(place.Orientation);
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        Assert.IsFalse(grid.IsReserved(place.Origin + new Vector2Int(x, y)), place.Origin.ToString());
                    }
                }
            }
        }

        [Test]
        public void RoadPlanner_NeverRoutesThroughReservedCells()
        {
            CityGrid grid = Grid();
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(1, 5));
            grid.Place(BuildingKind.House(SocialClass.Poor), BuildingOwner.City, House, At(9, 5));
            grid.ReserveTradingPostSite(new Footprint(3, 8), At(5, 2));
            var roads = new List<Vector2Int>();
            var houseCells = new List<Vector2Int> { new Vector2Int(9, 5), new Vector2Int(10, 5) };

            Assert.IsTrue(RoadPlanner.TryFindRoads(grid, houseCells, roads));

            Assert.IsNotEmpty(roads);
            foreach (Vector2Int road in roads)
            {
                Assert.IsFalse(grid.IsReserved(road), road.ToString());
            }
        }

        [Test]
        public void PlaceTradingPost_PlacesThePlayersBuildingOnTheSiteAndRaisesBuildingPlaced()
        {
            CityGrid grid = Grid();
            GridPlace site = At(4, 4, BuildingOrientation.Across);
            grid.ReserveTradingPostSite(Post, site);
            var placed = new List<PlacedBuilding>();
            grid.BuildingPlaced += placed.Add;
            Assert.IsTrue(grid.CanPlaceTradingPost);

            PlacedBuilding post = grid.PlaceTradingPost();

            Assert.IsTrue(post.Kind.IsTradingPost);
            Assert.AreEqual(BuildingOwner.Player, post.Owner);
            Assert.AreEqual(site, post.Place);
            Assert.AreSame(post, grid.TradingPost);
            Assert.AreSame(post, grid.BuildingAt(new Vector2Int(5, 6)));
            CollectionAssert.AreEqual(new[] { post }, placed);
            Assert.IsFalse(grid.CanPlaceTradingPost);
            Assert.Throws<InvalidOperationException>(() => grid.PlaceTradingPost());
        }

        [Test]
        public void PlaceTradingPost_WithoutASite_Throws()
        {
            CityGrid grid = Grid();

            Assert.IsFalse(grid.CanPlaceTradingPost);
            Assert.Throws<InvalidOperationException>(() => grid.PlaceTradingPost());
        }

        [Test]
        public void PlaceTradingPost_TouchingANetworkRoad_IsNeverConnected()
        {
            CityGrid grid = Grid();
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, TownHall, At(1, 1));
            grid.Place(BuildingKind.Road(), BuildingOwner.City, Cell, At(4, 1));
            grid.Place(BuildingKind.Road(), BuildingOwner.City, Cell, At(5, 1));
            grid.ReserveTradingPostSite(Post, At(5, 2));
            var connected = new List<PlacedBuilding>();
            grid.BuildingConnected += connected.Add;
            Assert.IsTrue(grid.IsNetworkRoad(new Vector2Int(5, 1)));

            PlacedBuilding post = grid.PlaceTradingPost();

            Assert.IsEmpty(connected);
            Assert.IsFalse(post.IsConnected);
            grid.Place(BuildingKind.Road(), BuildingOwner.City, Cell, At(8, 3));
            Assert.IsFalse(grid.IsNetworkRoad(new Vector2Int(8, 3)), "a road touching only the post");
        }

        [Test]
        public void TryValidateWalls_ReportsASiteOutsideTheWalls()
        {
            CityGrid grid = Walled();
            grid.ReserveTradingPostSite(Cell, At(0, 0));

            Assert.IsFalse(grid.TryValidateWalls(out string problem));
            StringAssert.Contains("trading post", problem);
        }

        [Test]
        public void TryValidateWalls_AcceptsASiteInsideTheWalls()
        {
            CityGrid grid = Walled();
            grid.ReserveTradingPostSite(Post, At(2, 2));

            Assert.IsTrue(grid.TryValidateWalls(out string problem), problem);
        }
    }
}
