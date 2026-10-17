using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class HomePortChoiceTests
    {
        [Test]
        public void Pick_TakesTheCityWithTheFewestHomedShips()
        {
            var homed = new Dictionary<int, int> { { 3, 2 }, { 5, 1 }, { 8, 2 } };
            Assert.AreEqual(5, HomePortChoice.Pick(new[] { 3, 5, 8 }, p => homed[p], n => 0));
        }

        [Test]
        public void Pick_DrawsAmongTies()
        {
            int asked = -1;
            Assert.AreEqual(8, HomePortChoice.Pick(new[] { 3, 5, 8 }, p => p == 5 ? 1 : 0, n => { asked = n; return 1; }));
            Assert.AreEqual(2, asked);
        }

        [Test]
        public void Pick_WithoutACandidate_IsNone()
        {
            Assert.AreEqual(-1, HomePortChoice.Pick(new int[0], p => 0, n => 0));
        }
    }
}
