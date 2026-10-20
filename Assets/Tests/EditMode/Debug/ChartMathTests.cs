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

        [TestCase(0f, 97f, 5, 0f, 100f, 20f)]
        [TestCase(950f, 1230f, 4, 900f, 1300f, 100f)]
        [TestCase(-3f, 7f, 5, -4f, 8f, 2f)]
        [TestCase(0f, 0.8f, 4, 0f, 0.8f, 0.2f)]
        [TestCase(0f, 1f, 4, 0f, 1f, 0.25f)]
        public void NiceScale_WidensTheRangeToRoundSteps(float min, float max, int ticks, float niceMin, float niceMax, float step)
        {
            ChartMath.NiceScale(min, max, ticks, out float actualMin, out float actualMax, out float actualStep);

            Assert.AreEqual(niceMin, actualMin, 1e-4f);
            Assert.AreEqual(niceMax, actualMax, 1e-4f);
            Assert.AreEqual(step, actualStep, 1e-4f);
        }

        [Test]
        public void NiceScale_OfAFlatRange_StillHasAStep()
        {
            ChartMath.NiceScale(5f, 5f, 4, out float min, out float max, out float step);

            Assert.Greater(step, 0f);
            Assert.LessOrEqual(min, 5f);
            Assert.GreaterOrEqual(max, 5f);
            Assert.Greater(max, min);
        }

        [TestCase(12000f, 2000f, "12 000")]
        [TestCase(-4f, 2f, "-4")]
        [TestCase(0.4f, 0.2f, "0,4")]
        [TestCase(0.75f, 0.25f, "0,75")]
        [TestCase(0.6000001f, 0.2f, "0,6")]
        public void FormatTick_WritesTheDecimalsOfTheStep(float value, float step, string expected)
        {
            Assert.AreEqual(System.Text.RegularExpressions.Regex.Unescape(expected), ChartMath.FormatTick(value, step));
        }

        [TestCase(1f, 8f)]
        [TestCase(0.5f, 16f)]
        [TestCase(7f, 1f)]
        [TestCase(10f, 1f)]
        [TestCase(20f, 0.5f)]
        [TestCase(0f, 1f)]
        public void DashPeriodDays_IsAPowerOfTwo_OfAboutSevenPixels(float pixelsPerDay, float expected)
        {
            Assert.AreEqual(expected, ChartMath.DashPeriodDays(pixelsPerDay));
        }

        [Test]
        public void DashSpans_AreAnchoredToTheDays_NotToTheStartOfTheLine()
        {
            var spans = new System.Collections.Generic.List<(double, double)>();

            ChartMath.DashSpans(0d, 3d, 2d, 0.5d, spans);
            CollectionAssert.AreEqual(new[] { (0d, 1d), (2d, 3d) }, spans);

            // The same days drawn from another start: the dashes stay where they were.
            spans.Clear();
            ChartMath.DashSpans(1.5d, 2.5d, 2d, 0.5d, spans);
            CollectionAssert.AreEqual(new[] { (2d, 2.5d) }, spans);

            spans.Clear();
            ChartMath.DashSpans(3d, 4.6d, 1d, 0.5d, spans);
            CollectionAssert.AreEqual(new[] { (3d, 3.5d), (4d, 4.5d) }, spans);
        }
    }
}
