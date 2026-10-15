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
            Assert.AreEqual(0, hold.UnitsOf(Wine));
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

            Assert.AreEqual(20, hold.UnitsOf(Wine));
            Assert.AreEqual(40, hold.UnitsOf(Wheat));
            Assert.AreEqual(60, hold.Used);
            Assert.AreEqual(140, hold.Free);
        }

        [Test]
        public void TryAdd_BeyondTheFreeRoom_IsRefusedWhole()
        {
            var hold = new CargoHold(50);
            hold.TryAdd(Wine, 30);

            Assert.IsFalse(hold.TryAdd(Wheat, 21));

            Assert.AreEqual(0, hold.UnitsOf(Wheat));
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

            Assert.AreEqual(15, hold.UnitsOf(Wine));
            Assert.AreEqual(15, hold.Used);
        }

        [Test]
        public void TryRemove_MoreThanAboard_IsRefusedWhole()
        {
            var hold = new CargoHold(200);
            hold.TryAdd(Wine, 20);

            Assert.IsFalse(hold.TryRemove(Wine, 21));
            Assert.IsFalse(hold.TryRemove(Wheat, 1));

            Assert.AreEqual(20, hold.UnitsOf(Wine));
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

            Assert.Throws<ArgumentOutOfRangeException>(() => hold.UnitsOf(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryAdd(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryRemove(-1, 1));
        }

        [Test]
        public void AHoldWithoutRoom_TakesNothing()
        {
            var hold = new CargoHold(0);

            Assert.IsFalse(hold.TryAdd(Wine, 1));
        }

        // Wheat in bushels of ten barrels, wine in barrels.
        private static GoodUnits Units => new GoodUnits(new[] { 10, 1, 1, 1, 1, 1, 1, 1, 1 });

        [Test]
        public void ABushel_TakesTenBarrelsOfRoom()
        {
            var hold = new CargoHold(200, Units);

            Assert.IsTrue(hold.TryAdd(Wheat, 3));
            Assert.IsTrue(hold.TryAdd(Wine, 20));

            Assert.AreEqual(3, hold.UnitsOf(Wheat));
            Assert.AreEqual(20, hold.UnitsOf(Wine));
            Assert.AreEqual(50, hold.Used);
            Assert.AreEqual(150, hold.Free);
        }

        [Test]
        public void RoomFor_IsTheWholeUnitsThatStillFit()
        {
            var hold = new CargoHold(200, Units);
            hold.TryAdd(Wine, 191);

            Assert.AreEqual(9, hold.Free);
            Assert.AreEqual(0, hold.RoomFor(Wheat));
            Assert.AreEqual(9, hold.RoomFor(Wine));
        }

        [Test]
        public void TryAdd_ABushelThatDoesNotFit_IsRefused()
        {
            var hold = new CargoHold(25, Units);

            Assert.IsFalse(hold.TryAdd(Wheat, 3));
            Assert.AreEqual(0, hold.Used);
            Assert.IsTrue(hold.TryAdd(Wheat, 2));
            Assert.AreEqual(5, hold.Free);
        }

        [Test]
        public void TryRemove_FreesTheRoomOfItsUnits()
        {
            var hold = new CargoHold(200, Units);
            hold.TryAdd(Wheat, 5);

            Assert.IsTrue(hold.TryRemove(Wheat, 2));

            Assert.AreEqual(3, hold.UnitsOf(Wheat));
            Assert.AreEqual(30, hold.Used);
            Assert.AreEqual(170, hold.Free);
            Assert.IsFalse(hold.TryRemove(Wheat, 4));
            Assert.AreEqual(30, hold.Used);
        }
    }
}
