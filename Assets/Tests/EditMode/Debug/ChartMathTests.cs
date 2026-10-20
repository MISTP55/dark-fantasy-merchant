using System;
using System.Collections.Generic;
using System.Linq;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

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

        [TestCase(1f, 32)]
        [TestCase(0.5f, 64)]
        [TestCase(0.7f, 64)]
        [TestCase(24f, 1)]
        [TestCase(30f, 1)]
        [TestCase(0f, 1)]
        public void AnchorPeriodDays_IsAPowerOfTwo_OfAtLeast24Pixels(float pixelsPerDay, int expected)
        {
            Assert.AreEqual(expected, ChartMath.AnchorPeriodDays(pixelsPerDay));
        }

        // The dashes of a line, as spans of x, in the order of x.
        private static List<(float, float)> SpansOf(Vector2[] points, bool[] anchors)
        {
            var dashes = new List<(Vector2, Vector2)>();
            ChartMath.DashPath(points, anchors, 4f, 3f, dashes);
            return dashes.Select(dash => ((float)Math.Round(Math.Min(dash.Item1.x, dash.Item2.x), 3), (float)Math.Round(Math.Max(dash.Item1.x, dash.Item2.x), 3)))
                .OrderBy(span => span.Item1).ToList();
        }

        private static Vector2[] Flat(params float[] xs)
        {
            return xs.Select(x => new Vector2(x, 0f)).ToArray();
        }

        [Test]
        public void DashPath_StartsADashAtTheAnchor_AndAGapBeforeIt()
        {
            List<(float, float)> spans = SpansOf(Flat(0f, 10f, 20f, 30f), new[] { false, false, true, false });

            CollectionAssert.AreEqual(new[] { (0f, 3f), (6f, 10f), (13f, 17f), (20f, 24f), (27f, 30f) }, spans);
        }

        [Test]
        public void DashPath_DoesNotMove_WhenTheLineLosesItsFirstPoints()
        {
            // The same line from x = 5: the dashes it still has are where they were.
            List<(float, float)> spans = SpansOf(Flat(5f, 10f, 20f, 30f), new[] { false, false, true, false });

            CollectionAssert.AreEqual(new[] { (6f, 10f), (13f, 17f), (20f, 24f), (27f, 30f) }, spans);
        }

        [Test]
        public void DashPath_MeasuresTheDashesAlongTheLine_HoweverSteep()
        {
            var dashes = new List<(Vector2, Vector2)>();

            ChartMath.DashPath(new[] { new Vector2(0f, 0f), new Vector2(1f, 20f), new Vector2(2f, 0f) }, new[] { true, false, false }, 4f, 3f, dashes);

            Assert.Greater(dashes.Count, 4);

            // Every dash is 4 pixels long, but the one cut by the end and the one that turns the corner.
            Assert.AreEqual(4f, Vector2.Distance(dashes[0].Item1, dashes[0].Item2), 1e-3f);
            Assert.AreEqual(4f, Vector2.Distance(dashes[1].Item1, dashes[1].Item2), 1e-3f);
            Assert.IsTrue(dashes.All(dash => Vector2.Distance(dash.Item1, dash.Item2) <= 4f + 1e-3f));
        }

        [Test]
        public void DashPath_StartsAgainAtEveryAnchor()
        {
            List<(float, float)> spans = SpansOf(Flat(0f, 10f, 20f), new[] { true, true, false });

            CollectionAssert.AreEqual(new[] { (0f, 4f), (7f, 10f), (10f, 14f), (17f, 20f) }, spans);
        }

        [Test]
        public void DashPath_WithoutAnAnchor_StartsAtTheFirstPoint()
        {
            List<(float, float)> spans = SpansOf(Flat(0f, 10f), new[] { false, false });

            CollectionAssert.AreEqual(new[] { (0f, 4f), (7f, 10f) }, spans);
        }
    }
}
