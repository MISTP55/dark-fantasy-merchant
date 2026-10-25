using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class HarbourPathsTests
    {
        private const string Sea = "~~~~~~~~";

        // The port at (1, 3) faces the sea; pondRow is the row y 6 (a pond is a cell of water in it).
        private static CityGrid OpenHarbour(string pondRow)
        {
            CityGrid grid = TestHarbours.FromRows(
                "........",
                pondRow,
                "........",
                ".~~~~...",
                ".~~~~...",
                Sea,
                Sea,
                Sea);
            TestHarbours.PortSouth(grid, 1, 3);
            return grid;
        }

        [Test]
        public void TryFindExit_StraightToTheNearestEdge()
        {
            CityGrid grid = TestHarbours.FromRows(
                "~~~~~~~",
                "~~~~~~~",
                "~~~~~~~",
                "~~~~~~~",
                "~~~~~~~");
            var path = new List<Vector2Int>();

            Assert.IsTrue(HarbourPaths.TryFindExit(grid, new Vector2Int(3, 1), path));

            CollectionAssert.AreEqual(new[] { new Vector2Int(3, 1), new Vector2Int(3, 0) }, path);
            Assert.AreEqual(1f, HarbourPaths.LengthOf(path), 1e-4f);
        }

        [Test]
        public void TryFindExit_GoesAroundLand()
        {
            CityGrid grid = TestHarbours.FromRows(
                "...~.",
                "..~~.",
                ".~~~.",
                ".....",
                ".....");
            var path = new List<Vector2Int>();

            Assert.IsTrue(HarbourPaths.TryFindExit(grid, new Vector2Int(1, 2), path));

            Assert.AreEqual(new Vector2Int(1, 2), path[0]);
            Vector2Int last = path[path.Count - 1];
            Assert.IsTrue(last.x == 0 || last.y == 0 || last.x == 4 || last.y == 4);
            for (int i = 0; i < path.Count; i++)
            {
                Assert.IsTrue(grid.IsNavigable(path[i]), path[i].ToString());
                if (i > 0)
                {
                    Assert.LessOrEqual(Mathf.Abs(path[i].x - path[i - 1].x), 1);
                    Assert.LessOrEqual(Mathf.Abs(path[i].y - path[i - 1].y), 1);
                }
            }

            // (1,2) to (2,2), a diagonal to (3,3), then up to the edge at (3,4): 2 + sqrt 2.
            Assert.AreEqual(2f + Mathf.Sqrt(2f), HarbourPaths.LengthOf(path), 1e-4f);
        }

        [Test]
        public void TryFindExit_DoesNotCutCorners()
        {
            // The edge cell (0,0) is joined to (2,2) only through the corner of two land cells.
            CityGrid grid = TestHarbours.FromRows(
                ".....",
                ".....",
                "..~..",
                ".~...",
                "~....");
            var path = new List<Vector2Int>();

            Assert.IsFalse(HarbourPaths.TryFindExit(grid, new Vector2Int(2, 2), path));
            CollectionAssert.IsEmpty(path);
        }

        [Test]
        public void TryFindExit_FromAnEdgeCell_IsThatCellAlone()
        {
            CityGrid grid = TestHarbours.FromRows(
                "~~~",
                "~~~",
                "~~~");
            var path = new List<Vector2Int>();

            Assert.IsTrue(HarbourPaths.TryFindExit(grid, new Vector2Int(2, 1), path));

            CollectionAssert.AreEqual(new[] { new Vector2Int(2, 1) }, path);
            Assert.AreEqual(0f, HarbourPaths.LengthOf(path));
        }

        [Test]
        public void TryFindExit_LakeWithNoEdge_IsFalse()
        {
            CityGrid grid = TestHarbours.FromRows(
                ".....",
                ".~~~.",
                ".~~~.",
                ".....");
            var path = new List<Vector2Int> { new Vector2Int(9, 9) };

            Assert.IsFalse(HarbourPaths.TryFindExit(grid, new Vector2Int(2, 1), path));
            CollectionAssert.IsEmpty(path);
        }

        [Test]
        public void TryFindExit_FromLand_IsFalse()
        {
            CityGrid grid = TestHarbours.FromRows(
                "~~~",
                ".~~",
                "~~~");
            var path = new List<Vector2Int> { new Vector2Int(9, 9) };

            Assert.IsFalse(HarbourPaths.TryFindExit(grid, new Vector2Int(0, 1), path));
            CollectionAssert.IsEmpty(path);
        }

        [Test]
        public void TryValidate_NoBerth_Fails()
        {
            CityGrid grid = OpenHarbour("........");

            Assert.IsFalse(HarbourPaths.TryValidate(grid, out string problem));
            StringAssert.Contains("no berth", problem);
        }

        [Test]
        public void TryValidate_PortNotJoinedToTheEdge_Fails()
        {
            // A lake that touches no edge; the port covers its two upper rows.
            CityGrid grid = TestHarbours.FromRows(
                "........",
                "........",
                "........",
                "........",
                "..~~~~..",
                "..~~~~..",
                "..~~~~..",
                "........");
            TestHarbours.PortSouth(grid, 2, 2);
            grid.PlaceBerth(new Vector2Int(3, 1));

            Assert.IsFalse(HarbourPaths.IsJoinedToEdge(grid));
            Assert.IsFalse(HarbourPaths.TryValidate(grid, out string problem));
            StringAssert.Contains("not joined", problem);
        }

        [Test]
        public void TryValidate_UnreachableBerth_Fails()
        {
            CityGrid grid = OpenHarbour("......~.");
            grid.PlaceBerth(new Vector2Int(2, 1));
            grid.PlaceBerth(new Vector2Int(6, 6));

            Assert.IsFalse(HarbourPaths.TryValidate(grid, out string problem));
            StringAssert.Contains("(6, 6)", problem);
        }

        [Test]
        public void TryValidate_JoinedPortAndReachableBerths_Passes()
        {
            CityGrid grid = OpenHarbour("........");
            grid.PlaceBerth(new Vector2Int(2, 1));
            grid.PlaceBerth(new Vector2Int(5, 2));

            Assert.IsTrue(HarbourPaths.IsJoinedToEdge(grid));
            Assert.IsTrue(HarbourPaths.TryValidate(grid, out string problem), problem);
            Assert.IsNull(problem);
        }
    }
}
