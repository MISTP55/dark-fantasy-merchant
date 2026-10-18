using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MapProjectionTests
    {
        [Test]
        public void WorldRect_IsCenteredOnOrigin_WithHeightFromAspectRatio()
        {
            var projection = new MapProjection(40f, 2f);

            Assert.AreEqual(-20f, projection.WorldRect.xMin, 1e-4f);
            Assert.AreEqual(-10f, projection.WorldRect.yMin, 1e-4f);
            Assert.AreEqual(40f, projection.WorldRect.width, 1e-4f);
            Assert.AreEqual(20f, projection.WorldRect.height, 1e-4f);
        }

        [Test]
        public void NormalizedToWorld_MapsCornersAndCenter()
        {
            var projection = new MapProjection(40f, 2f);

            TestAssert.AreEqual(new Vector2(-20f, -10f), projection.NormalizedToWorld(new Vector2(0f, 0f)));
            TestAssert.AreEqual(new Vector2(20f, 10f), projection.NormalizedToWorld(new Vector2(1f, 1f)));
            TestAssert.AreEqual(Vector2.zero, projection.NormalizedToWorld(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void WorldToNormalized_IsTheInverseOfNormalizedToWorld()
        {
            var projection = new MapProjection(2048f / 100f, 2048f / 1758f);
            var normalized = new Vector2(0.115f, 0.645f);

            Vector2 roundTrip = projection.WorldToNormalized(projection.NormalizedToWorld(normalized));

            TestAssert.AreEqual(normalized, roundTrip);
        }

        [Test]
        public void NormalizedPosition_DoesNotDependOnWorldWidth()
        {
            var small = new MapProjection(10f, 1.5f);
            var large = new MapProjection(80f, 1.5f);
            var normalized = new Vector2(0.25f, 0.8f);

            Vector2 smallWorld = small.NormalizedToWorld(normalized);
            Vector2 largeWorld = large.NormalizedToWorld(normalized);

            TestAssert.AreEqual(smallWorld * 8f, largeWorld);
        }

        [Test]
        public void NormalizedToWorld_AcceptsPositionsOutsideTheMap()
        {
            var projection = new MapProjection(40f, 2f);

            TestAssert.AreEqual(new Vector2(60f, -30f), projection.NormalizedToWorld(new Vector2(2f, -1f)));
        }

        [TestCase(0f, 2f)]
        [TestCase(-1f, 2f)]
        [TestCase(float.NaN, 2f)]
        [TestCase(float.PositiveInfinity, 2f)]
        [TestCase(40f, 0f)]
        [TestCase(40f, -2f)]
        [TestCase(40f, float.NaN)]
        public void Constructor_RejectsInvalidArguments(float worldWidth, float aspectRatio)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapProjection(worldWidth, aspectRatio));
        }

        [TestCase(0f, 0f, true)]
        [TestCase(1f, 1f, true)]
        [TestCase(0.5f, 0.5f, true)]
        [TestCase(-0.01f, 0.5f, false)]
        [TestCase(0.5f, 1.01f, false)]
        [TestCase(float.NaN, 0.5f, false)]
        public void IsInsideMap_ChecksTheUnitSquare(float x, float y, bool expected)
        {
            Assert.AreEqual(expected, MapProjection.IsInsideMap(new Vector2(x, y)));
        }
    }
}
