using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class IsoProjectionTests
    {
        [Test]
        public void CenterOf_PutsTheFirstCellAtTheOrigin()
        {
            Assert.AreEqual(Vector2.zero, IsoProjection.CenterOf(Vector2Int.zero));
        }

        [Test]
        public void CenterOf_GoesRightAndDownAlongX()
        {
            Assert.AreEqual(new Vector2(0.5f, -0.25f), IsoProjection.CenterOf(new Vector2Int(1, 0)));
        }

        [Test]
        public void CenterOf_GoesLeftAndDownAlongY()
        {
            Assert.AreEqual(new Vector2(-0.5f, -0.25f), IsoProjection.CenterOf(new Vector2Int(0, 1)));
        }

        [Test]
        public void CenterOf_AFootprintIsTheMiddleOfItsCoveredCells()
        {
            var centre = IsoProjection.CenterOf(new Vector2Int(2, 3), new Vector2Int(2, 1));
            var expected = (IsoProjection.CenterOf(new Vector2Int(2, 3)) + IsoProjection.CenterOf(new Vector2Int(3, 3))) / 2f;
            Assert.AreEqual(expected.x, centre.x, 1e-5f);
            Assert.AreEqual(expected.y, centre.y, 1e-5f);
        }

        [Test]
        public void CellAt_IsTheInverseOfCenterOf()
        {
            for (var x = 0; x < 10; x++)
            {
                for (var y = 0; y < 10; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var centre = IsoProjection.CenterOf(cell);
                    foreach (var dx in new[] { -0.24f, 0f, 0.24f })
                    {
                        foreach (var dy in new[] { -0.11f, 0f, 0.11f })
                        {
                            var point = centre + new Vector2(dx * IsoProjection.CellWidth, dy);
                            Assert.AreEqual(cell, IsoProjection.CellAt(point), $"cell {cell}, offset ({dx}, {dy})");
                        }
                    }
                }
            }
        }

        [Test]
        public void SortingOrderOf_DrawsTheNearerLater()
        {
            var one = Vector2Int.one;
            Assert.Greater(
                IsoProjection.SortingOrderOf(new Vector2Int(5, 5), one),
                IsoProjection.SortingOrderOf(new Vector2Int(4, 4), one));
        }

        [Test]
        public void SortingOrderOf_A1x2IsInFrontOfAnAdjacent2x2()
        {
            Assert.Greater(
                IsoProjection.SortingOrderOf(new Vector2Int(2, 0), new Vector2Int(1, 2)),
                IsoProjection.SortingOrderOf(new Vector2Int(0, 1), new Vector2Int(2, 2)));
        }

        [Test]
        public void SortingOrderOf_A2x1IsInFrontOfAnAdjacent2x2()
        {
            Assert.Greater(
                IsoProjection.SortingOrderOf(new Vector2Int(0, 2), new Vector2Int(2, 1)),
                IsoProjection.SortingOrderOf(new Vector2Int(1, 0), new Vector2Int(2, 2)));
        }

        [Test]
        public void SortingOrderOf_NeverTiesOrInvertsAFrontBlockAndTheOneBehindIt()
        {
            var sizes = new[] { new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2), new Vector2Int(2, 2) };
            var blocks = new System.Collections.Generic.List<(Vector2Int origin, Vector2Int size)>();
            for (var x = 0; x <= 4; x++)
            {
                for (var y = 0; y <= 4; y++)
                {
                    foreach (var size in sizes)
                    {
                        blocks.Add((new Vector2Int(x, y), size));
                    }
                }
            }

            foreach (var a in blocks)
            {
                foreach (var b in blocks)
                {
                    var aMax = a.origin + a.size - Vector2Int.one;
                    var bMax = b.origin + b.size - Vector2Int.one;
                    var aInFront = a.origin.x > bMax.x || a.origin.y > bMax.y;
                    var bInFront = b.origin.x > aMax.x || b.origin.y > aMax.y;
                    if (!aInFront || bInFront)
                    {
                        continue; // not separated with a beyond, or separated both ways (not a plain front/behind pair)
                    }

                    // Screen x of a block spans (min x - max y - 1) .. (max x - min y + 1), in half cell widths.
                    var overlap = a.origin.x - aMax.y - 1 < bMax.x - b.origin.y + 1
                        && b.origin.x - bMax.y - 1 < aMax.x - a.origin.y + 1;
                    if (!overlap)
                    {
                        continue;
                    }

                    Assert.Greater(
                        IsoProjection.SortingOrderOf(a.origin, a.size),
                        IsoProjection.SortingOrderOf(b.origin, b.size),
                        $"{a.origin} {a.size} should be drawn after {b.origin} {b.size}");
                }
            }
        }

        [Test]
        public void SortingOrderOf_StaysInTheSortingOrderRangeOnTheLargestGrid()
        {
            var order = IsoProjection.SortingOrderOf(new Vector2Int(199, 199), Vector2Int.one);
            Assert.LessOrEqual(order, short.MaxValue);
        }
    }
}
