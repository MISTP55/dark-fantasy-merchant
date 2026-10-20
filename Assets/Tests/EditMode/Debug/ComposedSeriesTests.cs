using System.Linq;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ComposedSeriesTests
    {
        private static readonly string[] CityNames = { "Brumefort", "Corvaux" };
        private static readonly string[] GoodNames = { "Blé", "Vin" };

        private static DebugIndicator Find(string name)
        {
            return DebugIndicators.All.First(indicator => indicator.Name == name);
        }

        [Test]
        public void TryAdd_RefusesADuplicate()
        {
            var list = new ComposedSeriesList();

            Assert.IsTrue(list.TryAdd(new ComposedSeries(1, Find("Prix d'achat"), 0)));
            Assert.IsFalse(list.TryAdd(new ComposedSeries(1, Find("Prix d'achat"), 0)));
            Assert.IsTrue(list.TryAdd(new ComposedSeries(1, Find("Prix d'achat"), 1)));
            Assert.AreEqual(2, list.Items.Count);
        }

        [Test]
        public void ACityIndicator_IgnoresTheGood()
        {
            var list = new ComposedSeriesList();

            Assert.IsTrue(list.TryAdd(new ComposedSeries(0, Find("Habitants"), 0)));
            Assert.IsFalse(list.TryAdd(new ComposedSeries(0, Find("Habitants"), 1)));
        }

        [Test]
        public void Remove_KeepsTheOthersInOrder()
        {
            var list = new ComposedSeriesList();
            list.TryAdd(new ComposedSeries(0, Find("Stock"), 0));
            list.TryAdd(new ComposedSeries(1, Find("Stock"), 0));
            list.TryAdd(new ComposedSeries(0, Find("Stock"), 1));

            list.Remove(1);

            Assert.AreEqual(2, list.Items.Count);
            Assert.AreEqual(0, list.Items[0].City);
            Assert.AreEqual(1, list.Items[1].Good);
        }

        [Test]
        public void Colours_Cycle()
        {
            var list = new ComposedSeriesList();

            Assert.AreEqual(list.ColorOf(0), list.ColorOf(8));
            Assert.AreNotEqual(list.ColorOf(0), list.ColorOf(1));
        }

        [Test]
        public void Label_OfACityIndicator_HasNoGood()
        {
            Assert.AreEqual("Brumefort · Habitants",
                ComposedSeriesList.LabelOf(new ComposedSeries(0, Find("Habitants"), 1), CityNames, GoodNames));
        }

        [Test]
        public void Label_OfAGoodIndicator()
        {
            Assert.AreEqual("Corvaux · Blé · Prix d'achat",
                ComposedSeriesList.LabelOf(new ComposedSeries(1, Find("Prix d'achat"), 0), CityNames, GoodNames));
        }
    }
}
