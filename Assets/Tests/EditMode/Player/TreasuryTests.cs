using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TreasuryTests
    {
        [Test]
        public void StartsWithTheGivenGold()
        {
            var treasury = new Treasury(10000);

            Assert.AreEqual(10000, treasury.Gold);
            Assert.IsFalse(treasury.IsInDebt);
        }

        [Test]
        public void Deposit_AddsGold()
        {
            var treasury = new Treasury(100);

            treasury.Deposit(250);

            Assert.AreEqual(350, treasury.Gold);
        }

        [Test]
        public void Withdraw_RemovesGold()
        {
            var treasury = new Treasury(100);

            treasury.Withdraw(40);

            Assert.AreEqual(60, treasury.Gold);
            Assert.IsFalse(treasury.IsInDebt);
        }

        [Test]
        public void Withdraw_BeyondTheGold_LeavesADebt()
        {
            var treasury = new Treasury(100);

            treasury.Withdraw(150);

            Assert.AreEqual(-50, treasury.Gold);
            Assert.IsTrue(treasury.IsInDebt);
        }

        [Test]
        public void Withdraw_DeepensADebt()
        {
            var treasury = new Treasury(-50);

            treasury.Withdraw(25);

            Assert.AreEqual(-75, treasury.Gold);
        }

        [Test]
        public void Deposit_PaysADebtBack()
        {
            var treasury = new Treasury(-50);

            treasury.Deposit(80);

            Assert.AreEqual(30, treasury.Gold);
            Assert.IsFalse(treasury.IsInDebt);
        }

        [Test]
        public void AnEmptyTreasury_IsNotInDebt()
        {
            Assert.IsFalse(new Treasury(0).IsInDebt);
        }

        [Test]
        public void CanAfford_UpToTheGold()
        {
            var treasury = new Treasury(100);

            Assert.IsTrue(treasury.CanAfford(0));
            Assert.IsTrue(treasury.CanAfford(100));
            Assert.IsFalse(treasury.CanAfford(101));
        }

        [Test]
        public void CanAfford_NothingButZero_InDebt()
        {
            var treasury = new Treasury(-1);

            Assert.IsTrue(treasury.CanAfford(0));
            Assert.IsFalse(treasury.CanAfford(1));
        }

        [Test]
        public void TryWithdraw_RemovesGoldItCanAfford()
        {
            var treasury = new Treasury(100);

            Assert.IsTrue(treasury.TryWithdraw(100));
            Assert.AreEqual(0, treasury.Gold);
        }

        [Test]
        public void TryWithdraw_BeyondTheGold_IsRefusedAndChangesNothing()
        {
            var treasury = new Treasury(100);
            int changes = 0;
            treasury.Changed += _ => changes++;

            Assert.IsFalse(treasury.TryWithdraw(101));

            Assert.AreEqual(100, treasury.Gold);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void Changed_IsRaisedWithTheNewGold()
        {
            var treasury = new Treasury(100);
            var raised = new List<long>();
            treasury.Changed += raised.Add;

            treasury.Deposit(50);
            treasury.Withdraw(200);
            treasury.TryWithdraw(0);
            treasury.Deposit(0);
            treasury.Withdraw(0);

            // An amount of zero changes nothing.
            CollectionAssert.AreEqual(new long[] { 150, -50 }, raised);
        }

        [Test]
        public void NegativeAmounts_AreRejected()
        {
            var treasury = new Treasury(100);

            Assert.Throws<ArgumentOutOfRangeException>(() => treasury.Deposit(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => treasury.Withdraw(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => treasury.TryWithdraw(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => treasury.CanAfford(-1));
            Assert.AreEqual(100, treasury.Gold);
        }
    }
}
