using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class RandomPlacementTests
    {
        private readonly List<Vector2Int> roads = new List<Vector2Int>();

        [Test]
        public void TryChoose_ChoosesAPlaceThatFits()
        {
            var grid = new CityGrid(3, 3, new[] { new Vector2Int(1, 1) });
            var footprint = new Footprint(2, 1);
            roads.Add(new Vector2Int(0, 0));
            for (int seed = 0; seed < 50; seed++)
            {
                Assert.IsTrue(new RandomPlacement(new System.Random(seed)).TryChoose(grid, footprint, out GridPlace place, roads));
                Assert.IsTrue(grid.CanPlace(footprint, place), $"seed {seed}");
                Assert.AreEqual(0, roads.Count);
            }
        }

        [Test]
        public void TryChoose_IsTheSame_ForTheSameSeed()
        {
            var grid = new CityGrid(5, 5, new Vector2Int[0]);
            var footprint = new Footprint(2, 1);
            new RandomPlacement(new System.Random(7)).TryChoose(grid, footprint, out GridPlace first, roads);
            new RandomPlacement(new System.Random(7)).TryChoose(grid, footprint, out GridPlace second, roads);
            Assert.AreEqual(first.Origin, second.Origin);
            Assert.AreEqual(first.Orientation, second.Orientation);
        }

        [Test]
        public void TryChoose_ReachesEveryPlace()
        {
            var grid = new CityGrid(2, 2, new Vector2Int[0]);
            var footprint = new Footprint(2, 1);
            var all = new List<GridPlace>();
            grid.GetPlaces(footprint, all);
            Assert.AreEqual(4, all.Count);

            var reached = new HashSet<(Vector2Int, BuildingOrientation)>();
            for (int seed = 0; seed < 200; seed++)
            {
                new RandomPlacement(new System.Random(seed)).TryChoose(grid, footprint, out GridPlace place, roads);
                reached.Add((place.Origin, place.Orientation));
            }

            foreach (GridPlace place in all)
            {
                Assert.IsTrue(reached.Contains((place.Origin, place.Orientation)), $"{place.Origin} {place.Orientation}");
            }
        }

        [Test]
        public void TryChoose_IsFalse_WhenNothingFits()
        {
            var grid = new CityGrid(1, 1, new Vector2Int[0]);
            Assert.IsFalse(new RandomPlacement(new System.Random(1)).TryChoose(grid, new Footprint(2, 1), out _, roads));
        }

        [Test]
        public void Constructor_Throws_ForANullRandom() =>
            Assert.Throws<ArgumentNullException>(() => new RandomPlacement(null));
    }
}
