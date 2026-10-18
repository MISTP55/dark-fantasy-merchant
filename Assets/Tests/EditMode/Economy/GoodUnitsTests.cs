using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GoodUnitsTests
    {
        [Test]
        public void RoomOf_IsTheBarrelsAUnitTakes()
        {
            var units = new GoodUnits(new[] { 10, 1, 10 });

            Assert.AreEqual(3, units.Count);
            Assert.AreEqual(10, units.RoomOf(0));
            Assert.AreEqual(1, units.RoomOf(1));
            Assert.AreEqual(10, units.RoomOf(2));
        }

        [Test]
        public void AGoodBeyondTheList_TakesOneBarrel()
        {
            Assert.AreEqual(1, new GoodUnits(new[] { 10 }).RoomOf(5));
            Assert.AreEqual(0, GoodUnits.AllBarrels.Count);
            Assert.AreEqual(1, GoodUnits.AllBarrels.RoomOf(3));
        }

        [Test]
        public void TheRooms_AreCopied()
        {
            var rooms = new[] { 10 };
            var units = new GoodUnits(rooms);

            rooms[0] = 3;

            Assert.AreEqual(10, units.RoomOf(0));
        }

        [Test]
        public void RejectsARoomUnderOneBarrel_AMissingList_AndANegativeGood()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GoodUnits(new[] { 10, 0 }));
            Assert.Throws<ArgumentNullException>(() => new GoodUnits(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => GoodUnits.AllBarrels.RoomOf(-1));
        }
    }
}
