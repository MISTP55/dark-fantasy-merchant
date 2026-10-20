using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ChartMathTests
    {
        [Test]
        public void TryRange_IgnoresNaN()
        {
            bool found = ChartMath.TryRange(new[] { new[] { 3f, float.NaN, -2f }, new[] { 7f, 1f, float.NaN } }, 3, out float min, out float max);

            Assert.IsTrue(found);
            Assert.AreEqual(-2f, min);
            Assert.AreEqual(7f, max);
        }

        [Test]
        public void TryRange_ReadsOnlyTheCountedDays()
        {
            ChartMath.TryRange(new[] { new[] { 3f, 4f, 100f } }, 2, out float min, out float max);

            Assert.AreEqual(3f, min);
            Assert.AreEqual(4f, max);
        }

        [Test]
        public void TryRange_OfNothing_IsFalse()
        {
            Assert.IsFalse(ChartMath.TryRange(new[] { new[] { float.NaN } }, 1, out _, out _));
            Assert.IsFalse(ChartMath.TryRange(new float[0][], 0, out _, out _));
            Assert.IsFalse(ChartMath.TryRange(new[] { new[] { 5f } }, 0, out _, out _));
        }

        [Test]
        public void TryRange_Flat_Widens()
        {
            ChartMath.TryRange(new[] { new[] { 5f, 5f } }, 2, out float min, out float max);
            Assert.AreEqual(4f, min);
            Assert.AreEqual(6f, max);

            ChartMath.TryRange(new[] { new[] { 0f } }, 1, out min, out max);
            Assert.AreEqual(0f, min);
            Assert.AreEqual(1f, max);
        }

        [Test]
        public void DayIndexAt_IsTheNearestDay()
        {
            Assert.AreEqual(1, ChartMath.DayIndexAt(14f, 100f, 11));
            Assert.AreEqual(0, ChartMath.DayIndexAt(-5f, 100f, 11));
            Assert.AreEqual(10, ChartMath.DayIndexAt(500f, 100f, 11));
        }

        [Test]
        public void DayIndexAt_WithNoDay_IsMinusOne()
        {
            Assert.AreEqual(-1, ChartMath.DayIndexAt(14f, 100f, 0));
        }

        [Test]
        public void DayIndexAt_WithOneDay_IsIt()
        {
            Assert.AreEqual(0, ChartMath.DayIndexAt(80f, 100f, 1));
        }

        [Test]
        public void XOf_SpreadsTheDaysOverTheWidth()
        {
            Assert.AreEqual(0f, ChartMath.XOf(0, 11, 100f));
            Assert.AreEqual(100f, ChartMath.XOf(10, 11, 100f));
        }

        [Test]
        public void XOf_SinglePoint_IsCentred()
        {
            Assert.AreEqual(50f, ChartMath.XOf(0, 1, 100f));
        }

        [Test]
        public void YOf_MaxIsTop_MinIsBottom()
        {
            Assert.AreEqual(0f, ChartMath.YOf(10f, 2f, 10f, 60f));
            Assert.AreEqual(60f, ChartMath.YOf(2f, 2f, 10f, 60f));
            Assert.AreEqual(30f, ChartMath.YOf(6f, 2f, 10f, 60f));
        }
    }
}
