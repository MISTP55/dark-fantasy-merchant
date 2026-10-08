using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationPathfinderTests
    {
        [Test]
        public void ANullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NavigationPathfinder(null));
        }

        [Test]
        public void HasNavigableCells_TellsWhetherTheGridHasWater()
        {
            Assert.IsFalse(new NavigationPathfinder(new NavigationGrid(4, 3)).HasNavigableCells);
            Assert.IsTrue(new NavigationPathfinder(GridAssert.FromRows("###", "#.#")).HasNavigableCells);
        }

        [Test]
        public void NearestNavigable_OfAPointOnWater_IsThePointItself()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#..",
                "###");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 point = GridAssert.CellPoint(grid, 1.2f, 1.7f);

            bool found = pathfinder.TryGetNearestNavigable(point, out Vector2 nearest);

            Assert.IsTrue(found);
            Assert.AreEqual(point.x, nearest.x);
            Assert.AreEqual(point.y, nearest.y);
        }

        [Test]
        public void NearestNavigable_OfAPointOnLand_IsTheCenterOfTheNearestWaterCell()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#..",
                "###");
            var pathfinder = new NavigationPathfinder(grid);

            bool found = pathfinder.TryGetNearestNavigable(
                GridAssert.CellPoint(grid, 0.8f, 1.5f), out Vector2 nearest);

            Assert.IsTrue(found);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 1), nearest);
        }

        [Test]
        public void NearestNavigable_IsMeasuredInCells_NotInNormalizedSpace()
        {
            // Eight cells wide, two high: one cell up is 0.5 away in normalized space,
            // three cells to the right only 0.375.
            NavigationGrid grid = GridAssert.FromRows(
                ".#######",
                "###.####");
            var pathfinder = new NavigationPathfinder(grid);

            pathfinder.TryGetNearestNavigable(GridAssert.CellCenter(grid, 0, 0), out Vector2 nearest);

            TestAssert.AreEqual(GridAssert.CellCenter(grid, 0, 1), nearest);
        }

        [Test]
        public void NearestNavigable_OfAPointOutsideTheMap_StartsFromTheClampedPoint()
        {
            NavigationGrid grid = GridAssert.FromRows("#.");
            var pathfinder = new NavigationPathfinder(grid);

            bool found = pathfinder.TryGetNearestNavigable(new Vector2(5f, 0.25f), out Vector2 nearest);

            Assert.IsTrue(found);
            TestAssert.AreEqual(new Vector2(1f, 0.25f), nearest);
        }

        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.NaN)]
        public void NearestNavigable_OfANaNPoint_IsNotFound(float x, float y)
        {
            var pathfinder = new NavigationPathfinder(GridAssert.FromRows(".."));

            Assert.IsFalse(pathfinder.TryGetNearestNavigable(new Vector2(x, y), out _));
        }

        [Test]
        public void NearestNavigable_OnAGridWithoutWater_IsNotFound_AndGivesTheClampedPoint()
        {
            var pathfinder = new NavigationPathfinder(new NavigationGrid(4, 3));

            bool found = pathfinder.TryGetNearestNavigable(new Vector2(-2f, 0.5f), out Vector2 nearest);

            Assert.IsFalse(found);
            TestAssert.AreEqual(new Vector2(0f, 0.5f), nearest);
        }
    }
}
