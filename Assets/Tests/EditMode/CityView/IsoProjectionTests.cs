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
                            var point = centre + new Vector2(dx * IsoProjection.CellWidth, dy * IsoProjection.CellHeight / 0.5f);
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
        public void SortingOrderOf_UsesTheFarCellOfAFootprint()
        {
            Assert.AreEqual(
                IsoProjection.SortingOrderOf(new Vector2Int(3, 3), Vector2Int.one),
                IsoProjection.SortingOrderOf(new Vector2Int(2, 3), new Vector2Int(2, 1)));
        }

        [Test]
        public void SortingOrderOf_StaysInTheSortingOrderRangeOnTheLargestGrid()
        {
            var order = IsoProjection.SortingOrderOf(new Vector2Int(199, 199), Vector2Int.one);
            Assert.LessOrEqual(order, short.MaxValue);
        }
    }
}
