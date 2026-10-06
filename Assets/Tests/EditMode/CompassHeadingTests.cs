using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CompassHeadingTests
    {
        [TestCase(0f, 1f, CompassDirection.N)]
        [TestCase(1f, 1f, CompassDirection.NE)]
        [TestCase(1f, 0f, CompassDirection.E)]
        [TestCase(1f, -1f, CompassDirection.SE)]
        [TestCase(0f, -1f, CompassDirection.S)]
        [TestCase(-1f, -1f, CompassDirection.SW)]
        [TestCase(-1f, 0f, CompassDirection.W)]
        [TestCase(-1f, 1f, CompassDirection.NW)]
        public void FromVector_ReturnsTheExactDirection(float x, float y, CompassDirection expected)
        {
            Assert.AreEqual(expected, CompassHeading.FromVector(new Vector2(x, y), CompassDirection.S));
        }

        // Angles are clockwise from north. Sector boundaries sit at 22.5 + 45 * n degrees.
        [TestCase(21.5f, CompassDirection.N)]
        [TestCase(23.5f, CompassDirection.NE)]
        [TestCase(66.5f, CompassDirection.NE)]
        [TestCase(68.5f, CompassDirection.E)]
        [TestCase(111.5f, CompassDirection.E)]
        [TestCase(113.5f, CompassDirection.SE)]
        [TestCase(156.5f, CompassDirection.SE)]
        [TestCase(158.5f, CompassDirection.S)]
        [TestCase(201.5f, CompassDirection.S)]
        [TestCase(203.5f, CompassDirection.SW)]
        [TestCase(246.5f, CompassDirection.SW)]
        [TestCase(248.5f, CompassDirection.W)]
        [TestCase(291.5f, CompassDirection.W)]
        [TestCase(293.5f, CompassDirection.NW)]
        [TestCase(336.5f, CompassDirection.NW)]
        [TestCase(338.5f, CompassDirection.N)]
        public void FromVector_SwitchesDirectionAtTheSectorBoundaries(float degrees, CompassDirection expected)
        {
            float radians = degrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));

            Assert.AreEqual(expected, CompassHeading.FromVector(direction, CompassDirection.S));
        }

        [TestCase(1e-20f)]
        [TestCase(1f)]
        [TestCase(1e20f)]
        public void FromVector_IgnoresMagnitude(float magnitude)
        {
            var direction = new Vector2(-magnitude, 0f);

            Assert.AreEqual(CompassDirection.W, CompassHeading.FromVector(direction, CompassDirection.S));
        }

        [TestCase(0f, 0f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(1f, float.NaN)]
        public void FromVector_ReturnsTheFallback_WhenThereIsNoDirection(float x, float y)
        {
            Assert.AreEqual(CompassDirection.NW, CompassHeading.FromVector(new Vector2(x, y), CompassDirection.NW));
        }
    }
}
