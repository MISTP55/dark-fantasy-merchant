using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TradingShipChoiceTests
    {
        private readonly object p1 = new object();
        private readonly object p2 = new object();
        private readonly object ai1 = new object();
        private readonly object ai2 = new object();

        private List<object> playerShips;

        [SetUp]
        public void SetUp()
        {
            playerShips = new List<object> { p1, p2 };
        }

        private bool IsPlayerShip(object ship)
        {
            return playerShips.Contains(ship);
        }

        [Test]
        public void PlayerShipsAmong_KeepsOnlyThePlayersShips_InTheirOrder()
        {
            var results = new List<object>();

            TradingShipChoice.PlayerShipsAmong(new List<object> { ai1, p1, ai2, p2 }, IsPlayerShip, results);

            Assert.That(results, Is.EqualTo(new List<object> { p1, p2 }));
        }

        [Test]
        public void PlayerShipsAmong_ClearsTheResultsFirst()
        {
            var results = new List<object> { ai1 };

            TradingShipChoice.PlayerShipsAmong(new List<object> { p1 }, IsPlayerShip, results);

            Assert.That(results, Is.EqualTo(new List<object> { p1 }));
        }

        [Test]
        public void Choose_WithoutAChoice_TakesTheFirstPlayerShip_AfterOtherShips()
        {
            Assert.That(TradingShipChoice.Choose(new List<object> { ai1, p1, p2 }, IsPlayerShip, null), Is.SameAs(p1));
        }

        [Test]
        public void Choose_KeepsTheCurrentShip_WhileItIsInPort()
        {
            Assert.That(TradingShipChoice.Choose(new List<object> { p1, p2 }, IsPlayerShip, p2), Is.SameAs(p2));
        }

        [Test]
        public void Choose_WhenTheChosenShipLeft_TakesTheFirstOtherPlayerShip()
        {
            Assert.That(TradingShipChoice.Choose(new List<object> { ai1, p2 }, IsPlayerShip, p1), Is.SameAs(p2));
        }

        [Test]
        public void Choose_WhenNoPlayerShipIsLeft_ChoosesNone()
        {
            Assert.That(TradingShipChoice.Choose(new List<object> { ai1 }, IsPlayerShip, p1), Is.Null);
        }

        [Test]
        public void Choose_InAnEmptyPort_ChoosesNone()
        {
            Assert.That(TradingShipChoice.Choose(new List<object>(), IsPlayerShip, null), Is.Null);
        }

        [Test]
        public void Choose_NeverKeepsAShipThatIsNotThePlayers()
        {
            Assert.That(TradingShipChoice.Choose(new List<object> { ai1, p1 }, IsPlayerShip, ai1), Is.SameAs(p1));
        }

        [Test]
        public void Choose_ThrowsOnANullList()
        {
            Assert.Throws<ArgumentNullException>(() => TradingShipChoice.Choose<object>(null, IsPlayerShip, null));
        }

        [Test]
        public void PlayerShipsAmong_ThrowsOnNullResults()
        {
            Assert.Throws<ArgumentNullException>(
                () => TradingShipChoice.PlayerShipsAmong(new List<object> { p1 }, IsPlayerShip, null));
        }
    }
}
