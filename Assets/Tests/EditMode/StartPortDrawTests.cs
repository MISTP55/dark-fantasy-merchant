using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class StartPortDrawTests
    {
        private List<int> results;

        [SetUp]
        public void SetUp()
        {
            results = new List<int> { 99 };
        }

        [Test]
        public void Draw_GivesDifferentPorts_WhileThereAreEnough()
        {
            var random = new Random(7);

            StartPortDraw.Draw(4, new[] { 3, 5, 8, 9, 12 }, random.Next, results);

            Assert.AreEqual(4, results.Count);
            CollectionAssert.AllItemsAreUnique(results);
            CollectionAssert.IsSubsetOf(results, new[] { 3, 5, 8, 9, 12 });
        }

        [Test]
        public void Draw_RepeatsPorts_OnlyOnceAllWereDrawn()
        {
            var random = new Random(7);

            StartPortDraw.Draw(5, new[] { 3, 5 }, random.Next, results);

            Assert.AreEqual(5, results.Count);
            Assert.AreEqual(3, results.FindAll(port => port == results[0]).Count);
            Assert.AreNotEqual(results[0], results[1]);
        }

        [Test]
        public void Draw_FollowsTheNumbersItIsGiven()
        {
            // Always the first of what is left to shuffle: the order is reversed.
            StartPortDraw.Draw(3, new[] { 1, 2, 3 }, n => 0, results);

            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, results);
        }

        [Test]
        public void Draw_WithoutCandidates_DrawsNothing()
        {
            StartPortDraw.Draw(5, new int[0], n => 0, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Draw_OfNoShip_DrawsNothing()
        {
            StartPortDraw.Draw(0, new[] { 1, 2 }, n => 0, results);

            Assert.AreEqual(0, results.Count);
        }
    }
}
