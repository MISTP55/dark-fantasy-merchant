using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WeeklyBillTests
    {
        [Test]
        public void SailorWages_AreTheWageOfEverySailor()
        {
            var bill = new WeeklyBill(12, 10);

            Assert.AreEqual(120, bill.SailorWages);
        }

        [Test]
        public void Total_IsTheSailorWages()
        {
            var bill = new WeeklyBill(12, 10);

            Assert.AreEqual(120, bill.Total);
        }

        [TestCase(0, 10)]
        [TestCase(12, 0)]
        public void NothingIsOwed_WithoutASailorOrAWage(int sailorCount, long sailorWeeklyWage)
        {
            Assert.AreEqual(0, new WeeklyBill(sailorCount, sailorWeeklyWage).Total);
        }

        [Test]
        public void SailorWages_DoNotOverflowAnInt()
        {
            var bill = new WeeklyBill(100000, 100000);

            Assert.AreEqual(10000000000L, bill.SailorWages);
        }

        [Test]
        public void NegativeValues_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeeklyBill(-1, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeeklyBill(12, -1));
        }
    }
}
