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
        public void CenterOf_GoesRightAndUpAlongX()
        {
            Assert.AreEqual(new Vector2(0.5f, 0.25f), IsoProjection.CenterOf(new Vector2Int(1, 0)));
        }

        [Test]
        public void CenterOf_GoesLeftAndUpAlongY()
        {
            Assert.AreEqual(new Vector2(-0.5f, 0.25f), IsoProjection.CenterOf(new Vector2Int(0, 1)));
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
        public void DrawKeyOf_DrawsTheNearerLater()
        {
            var one = Vector2Int.one;
            Assert.Greater(
                IsoProjection.DrawKeyOf(new Vector2Int(4, 4), one),
                IsoProjection.DrawKeyOf(new Vector2Int(5, 5), one));
        }

        [Test]
        public void DrawKeyOf_A1x2IsInFrontOfAnAdjacent2x2()
        {
            Assert.Greater(
                IsoProjection.DrawKeyOf(new Vector2Int(3, 4), new Vector2Int(1, 2)),
                IsoProjection.DrawKeyOf(new Vector2Int(4, 3), new Vector2Int(2, 2)));
        }

        [Test]
        public void DrawKeyOf_A2x1IsInFrontOfAnAdjacent2x2()
        {
            Assert.Greater(
                IsoProjection.DrawKeyOf(new Vector2Int(4, 3), new Vector2Int(2, 1)),
                IsoProjection.DrawKeyOf(new Vector2Int(3, 4), new Vector2Int(2, 2)));
        }

        [Test]
        public void DrawKeyOf_OrdersEveryFrontBlockAfterTheOneBehindIt_NearTheOrigin()
        {
            AssertFrontBlocksDrawnLater(0, 19, 0, 19);
        }

        [Test]
        public void DrawKeyOf_OrdersEveryFrontBlockAfterTheOneBehindIt_AtTheFarCornersOfA200x200Grid()
        {
            AssertFrontBlocksDrawnLater(194, 198, 0, 4);
            AssertFrontBlocksDrawnLater(0, 4, 194, 198);
            AssertFrontBlocksDrawnLater(194, 198, 194, 198);
        }

        [Test]
        public void DrawKeyOf_KeepsItsOrderOnA1000x1000Grid()
        {
            var behind = IsoProjection.DrawKeyOf(new Vector2Int(999, 999), Vector2Int.one);
            var front = IsoProjection.DrawKeyOf(new Vector2Int(998, 998), Vector2Int.one);
            Assert.Greater(front, behind);
            Assert.Greater(behind, 0L);
        }

        /// <summary>
        /// Over every non-overlapping pair of 1x1, 2x1, 1x2, 2x2, 3x2 and 2x3 blocks with origins in the given ranges: when one
        /// is nearer than the other on an axis (of smaller x or y, beyond the other's extent on that axis) and they can
        /// be drawn over one another on screen, it has the larger key.
        /// </summary>
        private static void AssertFrontBlocksDrawnLater(int minX, int maxX, int minY, int maxY)
        {
            var sizes = new[]
            {
                new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2), new Vector2Int(2, 2),
                new Vector2Int(3, 2), new Vector2Int(2, 3),
            };
            var blocks = new System.Collections.Generic.List<(Vector2Int origin, Vector2Int size)>();
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
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
                    var aInFront = aMax.x < b.origin.x || aMax.y < b.origin.y;
                    var bInFront = bMax.x < a.origin.x || bMax.y < a.origin.y;
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
                        IsoProjection.DrawKeyOf(a.origin, a.size),
                        IsoProjection.DrawKeyOf(b.origin, b.size),
                        $"{a.origin} {a.size} should be drawn after {b.origin} {b.size}");
                }
            }
        }
    }
}
