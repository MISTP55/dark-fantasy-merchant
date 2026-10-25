using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class FastForwardAwaitedShipTests
    {
        private Ship playerUnderWay;
        private Ship aiShip;
        private Ship viewShip;
        private HashSet<Ship> awaitable;

        [SetUp]
        public void SetUp()
        {
            var projection = new MapProjection(40f, 2f);
            playerUnderWay = new Ship(projection, Vector2.zero, 4f, TestCrew.Full);
            aiShip = new Ship(projection, Vector2.zero, 4f, TestCrew.Full);
            viewShip = new Ship(projection, Vector2.zero, 4f, TestCrew.Full);
            awaitable = new HashSet<Ship> { playerUnderWay, viewShip };
        }

        [Test]
        public void AwaitedShipOf_TheSelectedShipOfThePlayerUnderWay_WinsOverTheViewsShip()
        {
            Assert.AreSame(playerUnderWay, WorldMapInteraction.AwaitedShipOf(playerUnderWay, viewShip, awaitable.Contains));
        }

        [Test]
        public void AwaitedShipOf_AnAiShipSelectedInTheView_LeavesTheViewsShipAwaited()
        {
            Assert.AreSame(viewShip, WorldMapInteraction.AwaitedShipOf(aiShip, viewShip, awaitable.Contains));
        }

        [Test]
        public void AwaitedShipOf_NothingSelected_AwaitsTheViewsShip()
        {
            Assert.AreSame(viewShip, WorldMapInteraction.AwaitedShipOf(null, viewShip, awaitable.Contains));
        }

        [Test]
        public void AwaitedShipOf_NeitherAwaitable_IsNull()
        {
            awaitable.Remove(viewShip);

            Assert.IsNull(WorldMapInteraction.AwaitedShipOf(aiShip, viewShip, awaitable.Contains));
            Assert.IsNull(WorldMapInteraction.AwaitedShipOf(aiShip, null, awaitable.Contains));
            Assert.IsNull(WorldMapInteraction.AwaitedShipOf(null, null, awaitable.Contains));
        }
    }
}
