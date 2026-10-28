using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WarehouseTests
    {
        private const int Wine = 1;
        private const int Wheat = 0;

        [Test]
        public void StartsEmpty()
        {
            var warehouse = new Warehouse(2000);

            Assert.AreEqual(2000, warehouse.Capacity);
            Assert.AreEqual(0, warehouse.Used);
            Assert.AreEqual(0, warehouse.Excess);
            Assert.AreEqual(0, warehouse.UnitsOf(Wine));
            Assert.AreEqual(int.MaxValue, warehouse.RoomFor(Wine));
        }

        [Test]
        public void RejectsANegativeCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Warehouse(-1));
        }

        [Test]
        public void TryAdd_AboveCapacity_Succeeds_AndCountsExcess()
        {
            var warehouse = new Warehouse(2000);

            Assert.IsTrue(warehouse.TryAdd(Wine, 2150));

            Assert.AreEqual(2150, warehouse.UnitsOf(Wine));
            Assert.AreEqual(2150, warehouse.Used);
            Assert.AreEqual(150, warehouse.Excess);
        }

        [Test]
        public void Used_CountsBushelsAsSeveralBarrels()
        {
            var warehouse = new Warehouse(100, new GoodUnits(new[] { 3 }));

            warehouse.TryAdd(Wheat, 10);
            warehouse.TryAdd(Wine, 5);

            Assert.AreEqual(35, warehouse.Used);
            warehouse.TryRemove(Wheat, 4);
            Assert.AreEqual(23, warehouse.Used);
        }

        [Test]
        public void TryAdd_FarAboveIntRange_DoesNotOverflow()
        {
            var warehouse = new Warehouse(2000);

            warehouse.TryAdd(Wine, int.MaxValue);
            warehouse.TryAdd(Wine, int.MaxValue);

            Assert.AreEqual(2L * int.MaxValue, warehouse.Used);
            Assert.AreEqual(2L * int.MaxValue - 2000, warehouse.Excess);
        }

        [Test]
        public void TryRemove_RefusesMoreThanStored_AndChangesNothing()
        {
            var warehouse = new Warehouse(100);
            warehouse.TryAdd(Wine, 5);
            int changes = 0;
            warehouse.Changed += () => changes++;

            Assert.IsFalse(warehouse.TryRemove(Wine, 6));
            Assert.IsTrue(warehouse.TryRemove(Wine, 5));

            Assert.AreEqual(0, warehouse.UnitsOf(Wine));
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void WeeklyRentOf_SumsExcessTimesRent()
        {
            var over = new Warehouse(2000);
            over.TryAdd(Wine, 2150);
            var within = new Warehouse(2000);
            within.TryAdd(Wine, 10);

            Assert.AreEqual(150, Warehouse.WeeklyRentOf(new[] { over, within }, 1));
            Assert.AreEqual(450, Warehouse.WeeklyRentOf(new[] { over, within }, 3));
        }
    }
}
