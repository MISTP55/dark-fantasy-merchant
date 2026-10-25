using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class BuildingDrawOrderTests
    {
        private static readonly Vector2Int House = new Vector2Int(2, 1);

        [Test]
        public void Insert_GivesTheFirstBuildingRankZero()
        {
            var order = new BuildingDrawOrder();

            Assert.AreEqual(0, order.Insert(new Vector2Int(5, 5), House));
            Assert.AreEqual(1, order.Count);
        }

        [Test]
        public void Insert_PutsABuildingInFrontAfterTheOneBehindIt()
        {
            var order = new BuildingDrawOrder();
            order.Insert(new Vector2Int(4, 4), House);

            Assert.AreEqual(1, order.Insert(new Vector2Int(0, 0), House));
        }

        [Test]
        public void Insert_PutsABuildingBehindBeforeTheOnesInFront()
        {
            var order = new BuildingDrawOrder();
            order.Insert(new Vector2Int(4, 4), House);
            order.Insert(new Vector2Int(0, 0), House);

            Assert.AreEqual(0, order.Insert(new Vector2Int(8, 8), House));
            Assert.AreEqual(3, order.Count);
        }

        [Test]
        public void Insert_PutsAnEqualKeyAfterTheBuildingsPlacedBefore()
        {
            var order = new BuildingDrawOrder();
            order.Insert(new Vector2Int(2, 2), House);

            // Far apart on the same row of the screen: drawn in any order, so in the order placed.
            Assert.AreEqual(1, order.Insert(new Vector2Int(2, 2), House));
        }

        [Test]
        public void Insert_RanksByDrawKey()
        {
            var order = new BuildingDrawOrder();
            var places = new[]
            {
                (new Vector2Int(6, 1), new Vector2Int(2, 2)),
                (new Vector2Int(0, 3), new Vector2Int(1, 2)),
                (new Vector2Int(3, 3), House),
                (new Vector2Int(1, 0), House),
            };

            foreach ((Vector2Int origin, Vector2Int size) in places)
            {
                order.Insert(origin, size);
            }

            for (int rank = 1; rank < order.Count; rank++)
            {
                Assert.LessOrEqual(order.KeyAt(rank - 1), order.KeyAt(rank));
            }
        }

        [Test]
        public void RankOf_CountsSmallerKeys()
        {
            var order = new BuildingDrawOrder();
            order.Insert(new Vector2Int(8, 8), House);
            order.Insert(new Vector2Int(4, 4), House);
            order.Insert(new Vector2Int(4, 4), House);
            order.Insert(new Vector2Int(0, 0), House);
            long behind = IsoProjection.DrawKeyOf(new Vector2Int(8, 8), House);
            long middle = IsoProjection.DrawKeyOf(new Vector2Int(4, 4), House);
            long front = IsoProjection.DrawKeyOf(new Vector2Int(0, 0), House);

            Assert.AreEqual(0, order.RankOf(behind - 1));
            Assert.AreEqual(0, order.RankOf(behind));
            Assert.AreEqual(1, order.RankOf(middle));
            Assert.AreEqual(3, order.RankOf(middle + 1));
            Assert.AreEqual(3, order.RankOf(front));
            Assert.AreEqual(4, order.RankOf(front + 1));
            Assert.AreEqual(0, new BuildingDrawOrder().RankOf(middle));
        }
    }
}
