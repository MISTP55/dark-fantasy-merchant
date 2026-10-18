using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WaterColorClassifierTests
    {
        private const float Threshold = 0.2f;

        [TestCase(128, 128, 128)]
        [TestCase(120, 125, 130)]
        [TestCase(200, 200, 190)]
        public void GreyishColours_AreWater(int r, int g, int b)
        {
            Assert.IsTrue(WaterColorClassifier.IsWater(Colour(r, g, b), Threshold));
        }

        [TestCase(150, 125, 70)]
        [TestCase(220, 140, 60)]
        [TestCase(90, 60, 30)]
        public void BrownAndOrangeColours_AreLand(int r, int g, int b)
        {
            Assert.IsFalse(WaterColorClassifier.IsWater(Colour(r, g, b), Threshold));
        }

        [Test]
        public void TheThreshold_IsInclusive()
        {
            // Saturation (200 - 100) / 200 = 0.5 exactly.
            Color32 colour = Colour(200, 100, 100);

            Assert.IsTrue(WaterColorClassifier.IsWater(colour, 0.5f));
            Assert.IsFalse(WaterColorClassifier.IsWater(colour, 0.49f));
        }

        [Test]
        public void BlackAndWhite_HaveNoSaturation()
        {
            Assert.IsTrue(WaterColorClassifier.IsWater(Colour(0, 0, 0), 0f));
            Assert.IsTrue(WaterColorClassifier.IsWater(Colour(255, 255, 255), 0f));
        }

        [Test]
        public void APureColour_IsWaterOnlyAtFullThreshold()
        {
            Assert.IsFalse(WaterColorClassifier.IsWater(Colour(255, 0, 0), 0.99f));
            Assert.IsTrue(WaterColorClassifier.IsWater(Colour(255, 0, 0), 1f));
        }

        [Test]
        public void Alpha_IsIgnored()
        {
            Assert.IsTrue(WaterColorClassifier.IsWater(new Color32(128, 128, 128, 0), Threshold));
            Assert.IsFalse(WaterColorClassifier.IsWater(new Color32(220, 140, 60, 0), Threshold));
        }

        [Test]
        public void ANaNThreshold_ClassifiesNothingAsWater()
        {
            Assert.IsFalse(WaterColorClassifier.IsWater(Colour(128, 128, 128), float.NaN));
        }

        private static Color32 Colour(int r, int g, int b)
        {
            return new Color32((byte)r, (byte)g, (byte)b, 255);
        }
    }
}
