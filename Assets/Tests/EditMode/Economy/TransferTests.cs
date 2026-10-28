using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TransferTests
    {
        private const int Good = 0;

        [Test]
        public void Move_HoldToWarehouse_IgnoresCapacity()
        {
            var hold = new CargoHold(100);
            var warehouse = new Warehouse(10);
            hold.TryAdd(Good, 80);

            int moved = Transfer.Move(hold, warehouse, Good, 80);

            Assert.AreEqual(80, moved);
            Assert.AreEqual(0, hold.UnitsOf(Good));
            Assert.AreEqual(80, warehouse.UnitsOf(Good));
            Assert.AreEqual(70, warehouse.Excess);
        }

        [Test]
        public void Move_WarehouseToHold_StopsWhenTheHoldIsFull()
        {
            var hold = new CargoHold(30);
            var warehouse = new Warehouse(1000);
            warehouse.TryAdd(Good, 100);

            int moved = Transfer.Move(warehouse, hold, Good, 100);

            Assert.AreEqual(30, moved);
            Assert.AreEqual(30, hold.UnitsOf(Good));
            Assert.AreEqual(70, warehouse.UnitsOf(Good));
        }

        [Test]
        public void Move_MovesNoMoreThanTheSourceHolds()
        {
            var hold = new CargoHold(100);
            var warehouse = new Warehouse(1000);
            warehouse.TryAdd(Good, 12);

            int moved = Transfer.Move(warehouse, hold, Good, 50);

            Assert.AreEqual(12, moved);
            Assert.AreEqual(0, warehouse.UnitsOf(Good));
        }

        [Test]
        public void Move_NothingToMove_RaisesNoChanged()
        {
            var hold = new CargoHold(0);
            var warehouse = new Warehouse(1000);
            int changes = 0;
            hold.Changed += () => changes++;
            warehouse.Changed += () => changes++;

            Assert.AreEqual(0, Transfer.Move(hold, warehouse, Good, 5));
            warehouse.TryAdd(Good, 5);
            changes = 0;
            Assert.AreEqual(0, Transfer.Move(warehouse, hold, Good, 5));

            Assert.AreEqual(0, changes);
            Assert.AreEqual(5, warehouse.UnitsOf(Good));
        }
    }
}
