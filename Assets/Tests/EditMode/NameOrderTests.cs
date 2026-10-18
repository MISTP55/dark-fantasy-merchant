using System.Collections.Generic;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NameOrderTests
    {
        [Test]
        public void Of_SortsTheNames_AccentsAndCaseAside()
        {
            CollectionAssert.AreEqual(new[] { 3, 1, 2, 0 }, NameOrder.Of(new[] { "Vin", "Étoffe", "Fer", "céréales" }));
        }

        [Test]
        public void Of_KeepsEqualNamesInTheirOrder()
        {
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, NameOrder.Of(new[] { "Vin", "Bois", "vin" }));
        }

        [Test]
        public void Sort_SortsTheItemsByTheirNames_KeepingTiesInOrder()
        {
            var first = new KeyValuePair<string, int>("Navire marchand", 1);
            var second = new KeyValuePair<string, int>("Marchand indépendant 2", 2);
            var third = new KeyValuePair<string, int>("navire marchand", 3);
            var items = new List<KeyValuePair<string, int>> { first, second, third };

            NameOrder.Sort(items, item => item.Key);

            CollectionAssert.AreEqual(new[] { second, first, third }, items);
        }
    }
}
