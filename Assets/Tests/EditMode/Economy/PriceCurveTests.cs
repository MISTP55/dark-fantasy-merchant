using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PriceCurveTests
    {
        private const double Tolerance = 1e-9;

        // The factors at which the player buys.
        private static PriceCurve Curve => new PriceCurve(4, 1.5, 1, 0.8, 0.6);

        private static readonly StockThresholds Thresholds = new StockThresholds(20, 40, 60, 80);

        // A good the city does not produce: T2 is T1.
        private static readonly StockThresholds Step = new StockThresholds(20, 60, 60, 80);

        [TestCase(0, 4)]
        [TestCase(10, 2.75)]
        [TestCase(20, 1.5)]
        [TestCase(30, 1.25)]
        [TestCase(40, 1)]
        [TestCase(50, 0.9)]
        [TestCase(60, 0.8)]
        [TestCase(70, 0.7)]
        [TestCase(80, 0.6)]
        public void FactorAt_IsAStraightLineBetweenThePoints(double stock, double expected)
        {
            Assert.AreEqual(expected, Curve.FactorAt(stock, Thresholds), Tolerance);
        }

        [Test]
        public void FactorAt_ANegativeStock_IsThatOfAnEmptyOne()
        {
            Assert.AreEqual(4, Curve.FactorAt(-5, Thresholds), Tolerance);
        }

        [TestCase(80.5)]
        [TestCase(500)]
        [TestCase(1e6)]
        public void FactorAt_AboveT3_StaysAtTheLastFactor(double stock)
        {
            Assert.AreEqual(0.6, Curve.FactorAt(stock, Thresholds), Tolerance);
        }

        [Test]
        public void AverageFactor_InsideOneSegment_IsTheMiddleOfItsEnds()
        {
            Assert.AreEqual(2.75, Curve.AverageFactor(0, 20, Thresholds), Tolerance);
            Assert.AreEqual(0.7, Curve.AverageFactor(60, 80, Thresholds), Tolerance);
        }

        [Test]
        public void AverageFactor_AcrossAThreshold_AddsTheSegmentsItCrosses()
        {
            // Ten barrels averaging 2.125 under T0, then ten averaging 1.375 above it.
            Assert.AreEqual(1.75, Curve.AverageFactor(10, 30, Thresholds), Tolerance);
        }

        [Test]
        public void AverageFactor_BelowAnEmptyStock_CountsTheEmptyFactor()
        {
            Assert.AreEqual(4, Curve.AverageFactor(-1, 0, Thresholds), Tolerance);

            // Half at 4, half averaging 3.9375.
            Assert.AreEqual(3.96875, Curve.AverageFactor(-1, 1, Thresholds), Tolerance);
        }

        [Test]
        public void AverageFactor_AboveT3_IsTheLastFactor()
        {
            Assert.AreEqual(0.6, Curve.AverageFactor(90, 95, Thresholds), Tolerance);
        }

        [Test]
        public void TwoEqualThresholds_MakeAStep()
        {
            // At T1 itself, the factor of T1; just above, already on the segment from T2 to T3.
            Assert.AreEqual(1, Curve.FactorAt(60, Step), Tolerance);
            Assert.AreEqual(0.795, Curve.FactorAt(60.5, Step), Tolerance);

            // One barrel averaging 1.00625 below the step, one averaging 0.795 above it.
            Assert.AreEqual(0.900625, Curve.AverageFactor(59, 61, Step), Tolerance);
        }

        [TestCase(5, 5)]
        [TestCase(6, 5)]
        [TestCase(double.NaN, 5)]
        [TestCase(5, double.PositiveInfinity)]
        public void AverageFactor_RejectsARangeThatIsNotOne(double from, double to)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Curve.AverageFactor(from, to, Thresholds));
        }

        [Test]
        public void AFlatCurve_IsOneFactorEverywhere()
        {
            var flat = new PriceCurve(1, 1, 1, 1, 1);

            Assert.AreEqual(1, flat.FactorAt(0, Thresholds), Tolerance);
            Assert.AreEqual(1, flat.FactorAt(55, Step), Tolerance);
            Assert.AreEqual(1, flat.AverageFactor(-3, 300, Thresholds), Tolerance);
        }

        [Test]
        public void ACurve_KeepsItsFactors()
        {
            PriceCurve curve = Curve;

            Assert.AreEqual(4, curve.Empty);
            Assert.AreEqual(1.5, curve.AtT0);
            Assert.AreEqual(1, curve.AtT1);
            Assert.AreEqual(0.8, curve.AtT2);
            Assert.AreEqual(0.6, curve.AtT3);
        }

        [TestCase(0, 1.5, 1, 0.8, 0.6)]
        [TestCase(4, 1.5, 1, 0.8, -0.1)]
        [TestCase(double.NaN, 1.5, 1, 0.8, 0.6)]
        [TestCase(double.PositiveInfinity, 1.5, 1, 0.8, 0.6)]
        [TestCase(4, 1.5, 1.6, 0.8, 0.6)]
        [TestCase(4, 1.5, 1, 0.8, 0.9)]
        [TestCase(1, 1.5, 1, 0.8, 0.6)]
        public void RejectsFactorsThatAreNotPositive_OrThatRise(double empty, double atT0, double atT1, double atT2, double atT3)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PriceCurve(empty, atT0, atT1, atT2, atT3));
        }

        [TestCase(1, 1.5, 1, 0.8, 0.6, "atT0", "the factor at T0 is above the one at an empty stock")]
        [TestCase(4, 1.5, 1.6, 0.8, 0.6, "atT1", "the factor at T1 is above the one at T0")]
        [TestCase(4, 1.5, 1, 0.8, 0.9, "atT3", "the factor at T3 is above the one at T2")]
        [TestCase(4, 1.5, 1, 0, 0.6, "atT2", "above 0")]
        public void ARejectedCurve_NamesTheFactorAtFault(
            double empty, double atT0, double atT1, double atT2, double atT3, string factor, string explanation)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PriceCurve(empty, atT0, atT1, atT2, atT3));

            Assert.AreEqual(factor, exception.ParamName);
            StringAssert.Contains(explanation, exception.Message);
        }

        [Test]
        public void Thresholds_CanBeEqual()
        {
            var thresholds = new StockThresholds(10, 10, 10, 10);

            Assert.AreEqual(10, thresholds.T0);
            Assert.AreEqual(10, thresholds.T3);
        }

        [TestCase(-1, 10, 15, 20)]
        [TestCase(20, 10, 30, 40)]
        [TestCase(5, 10, 15, double.PositiveInfinity)]
        [TestCase(double.NaN, 10, 15, 20)]
        public void Thresholds_RejectANegativeStock_OrAWrongOrder(double t0, double t1, double t2, double t3)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StockThresholds(t0, t1, t2, t3));
        }
    }
}
