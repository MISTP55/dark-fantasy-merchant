using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WeeklyBillingTests
    {
        private Treasury treasury;
        private int sailors;
        private List<long> paid;
        private int elapsedDays;

        [SetUp]
        public void SetUp()
        {
            treasury = new Treasury(1000);
            sailors = 12;
            paid = new List<long>();
            elapsedDays = 0;
        }

        [Test]
        public void NothingIsPaid_BeforeTheWeekEnds()
        {
            WeeklyBilling billing = CreateBilling();

            StartDays(billing, 6);

            Assert.AreEqual(1000, treasury.Gold);
            CollectionAssert.IsEmpty(paid);
        }

        [Test]
        public void TheWages_AreTakenFromTheTreasury_WhenTheWeekEnds()
        {
            WeeklyBilling billing = CreateBilling();

            StartDays(billing, 7);

            Assert.AreEqual(880, treasury.Gold);
            CollectionAssert.AreEqual(new long[] { 120 }, paid);
        }

        [Test]
        public void EveryWeek_IsPaid()
        {
            WeeklyBilling billing = CreateBilling();

            StartDays(billing, 21);

            Assert.AreEqual(640, treasury.Gold);
            CollectionAssert.AreEqual(new long[] { 120, 120, 120 }, paid);
        }

        [Test]
        public void TheNextWeek_StartsAfterAPayment()
        {
            WeeklyBilling billing = CreateBilling();

            StartDays(billing, 13);

            CollectionAssert.AreEqual(new long[] { 120 }, paid);
        }

        [Test]
        public void TheBill_IsForTheSailorsAboardWhenTheWeekEnds()
        {
            WeeklyBilling billing = CreateBilling();

            StartDays(billing, 6);
            sailors = 20;
            StartDays(billing, 1);

            CollectionAssert.AreEqual(new long[] { 200 }, paid);
        }

        [Test]
        public void TheWages_ArePaidWithoutTheGold_LeavingADebt()
        {
            treasury = new Treasury(100);
            WeeklyBilling billing = CreateBilling();

            StartDays(billing, 7);

            Assert.AreEqual(-20, treasury.Gold);
            Assert.IsTrue(treasury.IsInDebt);
            CollectionAssert.AreEqual(new long[] { 120 }, paid);
        }

        [Test]
        public void Paid_IsRaisedAfterTheGoldIsTaken()
        {
            WeeklyBilling billing = CreateBilling();
            long goldWhenPaid = 0;
            billing.Paid += _ => goldWhenPaid = treasury.Gold;

            StartDays(billing, 7);

            Assert.AreEqual(880, goldWhenPaid);
        }

        [Test]
        public void AWeekInWhichNothingIsOwed_IsNotPaid_AndTheNextOneIs()
        {
            sailors = 0;
            WeeklyBilling billing = CreateBilling();
            int changes = 0;
            treasury.Changed += _ => changes++;

            StartDays(billing, 7);

            Assert.AreEqual(0, changes);
            CollectionAssert.IsEmpty(paid);

            sailors = 5;
            StartDays(billing, 7);

            CollectionAssert.AreEqual(new long[] { 50 }, paid);
        }

        [Test]
        public void Weeks_AreCountedFromTheFirstDayOfTheGame_WhateverTheDaysReported()
        {
            WeeklyBilling billing = CreateBilling();

            // Days 3 to 12 are not reported: the first week is left unpaid.
            billing.StartDay(1);
            billing.StartDay(2);
            billing.StartDay(13);
            CollectionAssert.IsEmpty(paid);

            billing.StartDay(14);
            CollectionAssert.AreEqual(new long[] { 120 }, paid);
        }

        [Test]
        public void AClockStepThatCrossesSeveralWeeks_PaysEachOfThem()
        {
            WeeklyBilling billing = CreateBilling();
            var clock = new GameClock(30f, 60f, 932, 12, 30);
            clock.DayStarted += _ => billing.StartDay(clock.ElapsedDays);

            clock.Advance(30f * 21);

            Assert.AreEqual(640, treasury.Gold);
            CollectionAssert.AreEqual(new long[] { 120, 120, 120 }, paid);
        }

        [Test]
        public void AWeek_LastsTheGivenDays()
        {
            WeeklyBilling billing = CreateBilling(daysPerWeek: 1);

            StartDays(billing, 3);

            CollectionAssert.AreEqual(new long[] { 120, 120, 120 }, paid);
        }

        [Test]
        public void InvalidValues_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new WeeklyBilling(null, 7, 10, () => sailors));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeeklyBilling(treasury, 0, 10, () => sailors));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeeklyBilling(treasury, 7, -1, () => sailors));
            Assert.Throws<ArgumentNullException>(() => new WeeklyBilling(treasury, 7, 10, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateBilling().StartDay(0));
        }

        private WeeklyBilling CreateBilling(int daysPerWeek = 7)
        {
            var billing = new WeeklyBilling(treasury, daysPerWeek, 10, () => sailors);
            billing.Paid += bill => paid.Add(bill.Total);
            return billing;
        }

        // Starts the days that follow the last one started.
        private void StartDays(WeeklyBilling billing, int days)
        {
            for (int i = 0; i < days; i++)
            {
                elapsedDays++;
                billing.StartDay(elapsedDays);
            }
        }
    }
}
