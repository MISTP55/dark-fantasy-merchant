using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CargoHoldTests
    {
        private const int Wine = 8;
        private const int Wheat = 0;

        [Test]
        public void StartsEmpty()
        {
            var hold = new CargoHold(200);

            Assert.AreEqual(200, hold.Capacity);
            Assert.AreEqual(0, hold.Used);
            Assert.AreEqual(200, hold.Free);
            Assert.AreEqual(0, hold.BarrelsOf(Wine));
        }

        [Test]
        public void RejectsANegativeCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CargoHold(-1));
        }

        [Test]
        public void TryAdd_StoresBarrelsOfEachGood()
        {
            var hold = new CargoHold(200);

            Assert.IsTrue(hold.TryAdd(Wine, 20));
            Assert.IsTrue(hold.TryAdd(Wheat, 40));

            Assert.AreEqual(20, hold.BarrelsOf(Wine));
            Assert.AreEqual(40, hold.BarrelsOf(Wheat));
            Assert.AreEqual(60, hold.Used);
            Assert.AreEqual(140, hold.Free);
        }

        [Test]
        public void TryAdd_BeyondTheFreeRoom_IsRefusedWhole()
        {
            var hold = new CargoHold(50);
            hold.TryAdd(Wine, 30);

            Assert.IsFalse(hold.TryAdd(Wheat, 21));

            Assert.AreEqual(0, hold.BarrelsOf(Wheat));
            Assert.AreEqual(30, hold.Used);
        }

        [Test]
        public void TryAdd_FillsTheHoldExactly()
        {
            var hold = new CargoHold(50);

            Assert.IsTrue(hold.TryAdd(Wine, 50));
            Assert.AreEqual(0, hold.Free);
        }

        [Test]
        public void TryRemove_TakesBarrelsOut()
        {
            var hold = new CargoHold(200);
            hold.TryAdd(Wine, 20);

            Assert.IsTrue(hold.TryRemove(Wine, 5));

            Assert.AreEqual(15, hold.BarrelsOf(Wine));
            Assert.AreEqual(15, hold.Used);
        }

        [Test]
        public void TryRemove_MoreThanAboard_IsRefusedWhole()
        {
            var hold = new CargoHold(200);
            hold.TryAdd(Wine, 20);

            Assert.IsFalse(hold.TryRemove(Wine, 21));
            Assert.IsFalse(hold.TryRemove(Wheat, 1));

            Assert.AreEqual(20, hold.BarrelsOf(Wine));
        }

        [Test]
        public void Changed_IsRaisedOncePerMove_AndNotByARefusal()
        {
            var hold = new CargoHold(50);
            int changes = 0;
            hold.Changed += () => changes++;

            hold.TryAdd(Wine, 30);
            hold.TryRemove(Wine, 10);
            Assert.AreEqual(2, changes);

            hold.TryAdd(Wine, 100);
            hold.TryRemove(Wine, 100);
            Assert.AreEqual(2, changes);
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void RejectsAQuantityThatIsNotPositive(int barrels)
        {
            var hold = new CargoHold(50);

            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryAdd(Wine, barrels));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryRemove(Wine, barrels));
        }

        [Test]
        public void RejectsANegativeGood()
        {
            var hold = new CargoHold(50);

            Assert.Throws<ArgumentOutOfRangeException>(() => hold.BarrelsOf(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryAdd(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryRemove(-1, 1));
        }

        [Test]
        public void AHoldWithoutRoom_TakesNothing()
        {
            var hold = new CargoHold(0);

            Assert.IsFalse(hold.TryAdd(Wine, 1));
        }
    }
}
