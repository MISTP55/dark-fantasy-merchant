using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShoreSideTests
    {
        // A 100 by 100 mask, land everywhere but where the predicate says water.
        private static NavigationGrid Mask(System.Func<int, int, bool> water)
        {
            var mask = new NavigationGrid(100, 100);
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 100; x++)
                {
                    mask.SetNavigable(x, y, water(x, y));
                }
            }

            return mask;
        }

        [Test]
        public void Of_WaterAbove_IsNorth()
        {
            Assert.AreEqual(PortFacing.North, ShoreSide.Of(Mask((x, y) => y >= 55), new Vector2(0.5f, 0.52f), 10f));
        }

        [Test]
        public void Of_WaterBelow_IsSouth()
        {
            Assert.AreEqual(PortFacing.South, ShoreSide.Of(Mask((x, y) => y < 45), new Vector2(0.5f, 0.47f), 10f));
        }

        [Test]
        public void Of_WaterToTheRight_IsEast()
        {
            Assert.AreEqual(PortFacing.East, ShoreSide.Of(Mask((x, y) => x >= 55), new Vector2(0.52f, 0.5f), 10f));
        }

        [Test]
        public void Of_WaterToTheLeft_IsWest()
        {
            Assert.AreEqual(PortFacing.West, ShoreSide.Of(Mask((x, y) => x < 45), new Vector2(0.47f, 0.5f), 10f));
        }

        [Test]
        public void Of_ABayToTheUpperRight_TakesTheLargerSide()
        {
            // More water to the right than above.
            NavigationGrid mask = Mask((x, y) => x >= 53 && y >= 48);

            Assert.AreEqual(PortFacing.East, ShoreSide.Of(mask, new Vector2(0.5f, 0.5f), 10f));
        }

        [Test]
        public void Of_NoWaterNearby_IsSouth()
        {
            Assert.AreEqual(PortFacing.South, ShoreSide.Of(Mask((x, y) => x < 5), new Vector2(0.5f, 0.5f), 10f));
        }

        [Test]
        public void Of_WithoutAMask_IsSouth()
        {
            Assert.AreEqual(PortFacing.South, ShoreSide.Of(null, new Vector2(0.5f, 0.5f), 10f));
        }
    }
}
