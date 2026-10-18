using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipmentQuantityTests
    {
        private static readonly StockThresholds Usual = new StockThresholds(10, 30, 30, 40);

        [TestCase(20)]
        [TestCase(15)]
        public void ASourceAtOrUnderTheMiddleOfT0AndT1_GivesNothing(double sourceStock)
        {
            Assert.AreEqual(0d, ShipmentQuantity.Of(sourceStock, Usual, 0, Usual));
        }

        [TestCase(30)]
        [TestCase(45)]
        public void ADestinationAtOrAboveT1_GetsNothing(double projected)
        {
            Assert.AreEqual(0d, ShipmentQuantity.Of(100, Usual, projected, Usual));
        }

        [Test]
        public void ASurplusAboveT1ThatCoversTheNeed_GivesTheNeed()
        {
            Assert.AreEqual(10d, ShipmentQuantity.Of(60, Usual, 20, Usual), 1e-9);
        }

        [Test]
        public void UpToTheTargetPoint_TheSourceGivesDownToItsMiddle()
        {
            // No surplus; 8 from 28 down to 20, short of the target 17.5.
            Assert.AreEqual(8d, ShipmentQuantity.Of(28, Usual, 0, Usual), 1e-9);
        }

        [Test]
        public void TheThreeSteps_SurplusTargetAndEqualRelativePosition()
        {
            // 10 above T1, then 7.5 up to the target, then 1.3889 so that both end at the same share of their range.
            Assert.AreEqual(18.888888889, ShipmentQuantity.Of(40, Usual, 0, Usual), 1e-6);
        }

        [Test]
        public void AboveTheTargetPoint_TheRestIsShared()
        {
            // 20 of surplus, then 4.444: the source ends at 25.556 and the destination at 24.444, both at 55.6 %.
            Assert.AreEqual(24.444444444, ShipmentQuantity.Of(50, Usual, 0, Usual), 1e-6);
        }

        [Test]
        public void DegenerateThresholds_GiveAFiniteQuantity()
        {
            var flat = new StockThresholds(10, 10, 10, 10);

            Assert.AreEqual(0d, ShipmentQuantity.Of(10, flat, 0, Usual));
            Assert.AreEqual(10d, ShipmentQuantity.Of(20, flat, 0, flat), 1e-9);
            // 5 above T1, then nothing: both ranges are empty, and nothing is divided by them.
            double quantity = ShipmentQuantity.Of(15, flat, 0, flat);
            Assert.IsFalse(double.IsNaN(quantity));
            Assert.AreEqual(5d, quantity, 1e-9);
        }

        [TestCase(1, 5)]
        [TestCase(10, 3)]
        public void TheSmallestLot_IsFiveBarrelsOrThreeBushels(int unitRoom, int lot)
        {
            Assert.AreEqual(lot, ShipmentQuantity.SmallestLotFor(unitRoom));
        }
    }
}
