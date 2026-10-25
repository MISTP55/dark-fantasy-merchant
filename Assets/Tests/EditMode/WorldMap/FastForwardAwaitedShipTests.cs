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

        [Test]
        public void ShipToSelectAgain_TheAwaitedShipQueued_IsTheAwaitedShipNotTheOneSelectedBefore()
        {
            Assert.AreSame(viewShip, WorldMapInteraction.ShipToSelectAgain(aiShip, viewShip, true));
        }

        [Test]
        public void ShipToSelectAgain_TheAwaitedShipNotQueued_IsTheOneSelectedBefore()
        {
            Assert.AreSame(aiShip, WorldMapInteraction.ShipToSelectAgain(aiShip, viewShip, false));
            Assert.IsNull(WorldMapInteraction.ShipToSelectAgain(null, viewShip, false));
        }

        [Test]
        public void ShipToSelectAgain_NoShipAwaited_IsTheOneSelectedBefore()
        {
            Assert.AreSame(aiShip, WorldMapInteraction.ShipToSelectAgain(aiShip, null, false));
        }

        [Test]
        public void KeepsShipSelected_AShipAtSeaWhenACityIsSelected_IsDeselected()
        {
            CityDefinition city = ScriptableObject.CreateInstance<CityDefinition>();

            try
            {
                Assert.IsFalse(WorldMapInteraction.KeepsShipSelected(city, false, null));
            }
            finally
            {
                Object.DestroyImmediate(city);
            }
        }

        [Test]
        public void KeepsShipSelected_AShipAtSeaWhenTheCityIsCleared_StaysSelected()
        {
            Assert.IsTrue(WorldMapInteraction.KeepsShipSelected(null, false, null));
        }

        [Test]
        public void KeepsShipSelected_AShipInACity_StaysSelectedOnlyWithThatCity()
        {
            CityDefinition city = ScriptableObject.CreateInstance<CityDefinition>();
            CityDefinition other = ScriptableObject.CreateInstance<CityDefinition>();

            try
            {
                Assert.IsTrue(WorldMapInteraction.KeepsShipSelected(city, true, city));
                Assert.IsFalse(WorldMapInteraction.KeepsShipSelected(other, true, city));
                Assert.IsFalse(WorldMapInteraction.KeepsShipSelected(null, true, city));
            }
            finally
            {
                Object.DestroyImmediate(city);
                Object.DestroyImmediate(other);
            }
        }
    }
}
