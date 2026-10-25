using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class HarbourViewTests
    {
        private static Ship NewShip()
        {
            return new Ship(new MapProjection(40f, 2f), Vector2.zero, 4f, TestCrew.Full);
        }

        [Test]
        public void TryPick_NearestWithinHalfACell()
        {
            Ship far = NewShip();
            Ship near = NewShip();
            Ship other = NewShip();
            var ships = new[] { far, near, other };
            var points = new[] { new Vector2(3f, 4f), new Vector2(3.6f, 4f), new Vector2(10f, 10f) };

            Assert.IsTrue(HarbourView.TryPick(ships, points, new Vector2(3.4f, 4.1f), out Ship picked));
            Assert.AreSame(near, picked);

            Assert.IsTrue(HarbourView.TryPick(ships, points, new Vector2(2.7f, 4.2f), out picked));
            Assert.AreSame(far, picked);
        }

        [Test]
        public void TryPick_TooFar_IsFalse()
        {
            var ships = new[] { NewShip() };
            var points = new[] { new Vector2(3f, 4f) };

            Assert.IsFalse(HarbourView.TryPick(ships, points, new Vector2(3.4f, 4.4f), out Ship picked));
            Assert.IsNull(picked);
            Assert.IsFalse(HarbourView.TryPick(new Ship[0], new Vector2[0], new Vector2(3f, 4f), out picked));
            Assert.IsNull(picked);
        }
    }
}
