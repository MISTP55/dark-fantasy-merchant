using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipCrewTests
    {
        [Test]
        public void StartsWithTheGivenSailorsAndCapacity()
        {
            var crew = new ShipCrew(28, 12);

            Assert.AreEqual(28, crew.Capacity);
            Assert.AreEqual(12, crew.Count);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_RejectsACapacityWithoutRoom(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShipCrew(capacity, 0));
        }

        [TestCase(-1)]
        [TestCase(29)]
        public void Constructor_RejectsACountOutsideTheCapacity(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShipCrew(28, count));
        }

        [TestCase(28, 0, 0f)]
        [TestCase(28, 7, 0.25f)]
        [TestCase(28, 14, 0.5f)]
        [TestCase(28, 28, 1f)]
        public void Occupancy_IsTheShareOfTheCapacityThatIsManned(int capacity, int count, float expected)
        {
            Assert.AreEqual(expected, new ShipCrew(capacity, count).Occupancy, 1e-6f);
        }

        [Test]
        public void SpeedFactor_IsZero_WithoutASailor()
        {
            Assert.AreEqual(0f, new ShipCrew(28, 0).SpeedFactor);
        }

        // A tenth of the capacity or less: half speed.
        [TestCase(28, 1)]
        [TestCase(28, 2)]
        [TestCase(100, 10)]
        public void SpeedFactor_IsHalf_UpToATenthOfTheCapacity(int capacity, int count)
        {
            Assert.AreEqual(0.5f, new ShipCrew(capacity, count).SpeedFactor, 1e-6f);
        }

        // From a tenth to half of the capacity, the speed rises in a straight line.
        [TestCase(100, 20, 0.625f)]
        [TestCase(100, 30, 0.75f)]
        [TestCase(100, 40, 0.875f)]
        public void SpeedFactor_RisesWithTheOccupancy_BetweenATenthAndAHalf(int capacity, int count, float expected)
        {
            Assert.AreEqual(expected, new ShipCrew(capacity, count).SpeedFactor, 1e-6f);
        }

        [TestCase(28, 14)]
        [TestCase(28, 15)]
        [TestCase(28, 28)]
        [TestCase(1, 1)]
        public void SpeedFactor_IsFull_FromHalfTheCapacity(int capacity, int count)
        {
            Assert.AreEqual(1f, new ShipCrew(capacity, count).SpeedFactor);
        }

        [Test]
        public void SpeedFactor_OfTheStartingCrew_IsAlmostFull()
        {
            // 12 of 28 is 42.9 % of the capacity: 0.5 + 0.5 * (0.429 - 0.1) / 0.4.
            Assert.AreEqual(0.9107f, new ShipCrew(28, 12).SpeedFactor, 1e-4f);
        }
    }
}
