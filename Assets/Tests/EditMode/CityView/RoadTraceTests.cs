using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class RoadTraceTests
    {
        private static List<Vector2Int> Trace(int x0, int y0, int x1, int y1)
        {
            var cells = new List<Vector2Int>();
            RoadTrace.Of(new Vector2Int(x0, y0), new Vector2Int(x1, y1), cells);
            return cells;
        }

        [Test]
        public void SameCell_IsOneCell()
        {
            CollectionAssert.AreEqual(new[] { new Vector2Int(2, 2) }, Trace(2, 2, 2, 2));
        }

        [Test]
        public void StraightAlongX()
        {
            CollectionAssert.AreEqual(
                new[] { new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 3) }, Trace(1, 3, 3, 3));
        }

        [Test]
        public void StraightAlongY()
        {
            CollectionAssert.AreEqual(
                new[] { new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(2, 3) }, Trace(2, 1, 2, 3));
        }

        [Test]
        public void LShape_GoesAlongXThenY()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1),
                    new Vector2Int(3, 2), new Vector2Int(3, 3), new Vector2Int(3, 4),
                },
                Trace(1, 1, 3, 4));
        }

        [Test]
        public void Backwards_WorksWithNegativeSteps()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector2Int(3, 3), new Vector2Int(2, 3), new Vector2Int(1, 3),
                    new Vector2Int(1, 2), new Vector2Int(1, 1),
                },
                Trace(3, 3, 1, 1));
        }
    }
}
