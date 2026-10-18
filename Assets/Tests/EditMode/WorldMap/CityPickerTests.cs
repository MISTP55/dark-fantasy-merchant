using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityPickerTests
    {
        private static readonly List<Vector2> Positions = new List<Vector2>
        {
            new Vector2(0f, 0f),
            new Vector2(10f, 0f),
            new Vector2(10f, 3f),
        };

        [Test]
        public void ReturnsTheNearestPositionWithinTheRadius()
        {
            int index = CityPicker.PickNearest(Positions, new Vector2(10f, 2f), 5f);

            Assert.AreEqual(2, index);
        }

        [Test]
        public void ReturnsMinusOne_WhenNothingIsWithinTheRadius()
        {
            int index = CityPicker.PickNearest(Positions, new Vector2(5f, 20f), 1f);

            Assert.AreEqual(-1, index);
        }

        [Test]
        public void IncludesAPositionExactlyOnTheRadius()
        {
            int index = CityPicker.PickNearest(Positions, new Vector2(-2f, 0f), 2f);

            Assert.AreEqual(0, index);
        }

        [Test]
        public void ResolvesATieToTheFirstPositionInListOrder()
        {
            var positions = new List<Vector2> { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) };

            int index = CityPicker.PickNearest(positions, Vector2.zero, 5f);

            Assert.AreEqual(0, index);
        }

        [Test]
        public void ResolvesIdenticalPositionsToTheFirstOne()
        {
            var positions = new List<Vector2> { new Vector2(3f, 3f), new Vector2(3f, 3f) };

            int index = CityPicker.PickNearest(positions, new Vector2(3f, 3f), 0f);

            Assert.AreEqual(0, index);
        }

        [Test]
        public void ReturnsMinusOne_ForAnEmptyList()
        {
            Assert.AreEqual(-1, CityPicker.PickNearest(new List<Vector2>(), Vector2.zero, 5f));
        }

        [Test]
        public void ReturnsMinusOne_ForANullList()
        {
            Assert.AreEqual(-1, CityPicker.PickNearest(null, Vector2.zero, 5f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void ReturnsMinusOne_ForAnInvalidRadius(float radius)
        {
            Assert.AreEqual(-1, CityPicker.PickNearest(Positions, Vector2.zero, radius));
        }

        [Test]
        public void ReturnsMinusOne_ForANonFinitePoint()
        {
            Assert.AreEqual(-1, CityPicker.PickNearest(Positions, new Vector2(float.NaN, 0f), 5f));
        }
    }
}
