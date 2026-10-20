using System.Linq;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugIndicatorsTests
    {
        private const int Fish = 0;

        private static DebugIndicator Find(string name)
        {
            return DebugIndicators.All.First(indicator => indicator.Name == name);
        }

        // Two cities of one good, one day: fish in stock 10 and 5.
        private static SimulationHistory History(float first, float second)
        {
            var history = new SimulationHistory(2, 1, null);
            float[][] rows = history.BeginDay(0);
            rows[0][CitySnapshot.IndexOf(GoodIndicator.Stock, Fish)] = first;
            rows[1][CitySnapshot.IndexOf(GoodIndicator.Stock, Fish)] = second;
            rows[0][CitySnapshot.IndexOf(CityIndicator.PoorSatisfaction)] = 12;
            return history;
        }

        [Test]
        public void All_IsAlphabetical()
        {
            string[] names = DebugIndicators.All.Select(indicator => indicator.Name).ToArray();

            for (int i = 1; i < names.Length; i++)
            {
                Assert.LessOrEqual(NameOrder.Compare(names[i - 1], names[i]), 0, $"{names[i - 1]} before {names[i]}");
            }
        }

        [Test]
        public void All_HasEveryIndicatorOnce()
        {
            Assert.AreEqual(13, DebugIndicators.All.Count(indicator => !indicator.IsGood));
            Assert.AreEqual(15, DebugIndicators.All.Count(indicator => indicator.IsGood));
            Assert.AreEqual(28, DebugIndicators.All.Select(indicator => indicator.Name).Distinct().Count());
        }

        [Test]
        public void TheNames_AreFrench()
        {
            Assert.AreEqual(GoodIndicator.Missing, Find("Manque (jour)").Good);
            Assert.AreEqual(CityIndicator.BeggarTarget, Find("Cible des mendiants").City);
            Assert.AreEqual(GoodIndicator.BuyPrice, Find("Prix d'achat").Good);
        }

        [Test]
        public void ValueOf_ReadsTheHistory()
        {
            Assert.AreEqual(5f, DebugIndicators.ValueOf(History(10, 5), 0, 1, Find("Stock"), Fish));
            Assert.AreEqual(12f, DebugIndicators.ValueOf(History(10, 5), 0, 0, Find("Satisfaction des pauvres"), Fish));
        }

        [Test]
        public void WorldValue_SumsTheCities()
        {
            Assert.AreEqual(15f, DebugIndicators.WorldValueOf(History(10, 5), 0, 2, Find("Stock"), Fish));
        }

        [Test]
        public void WorldValue_OfASatisfaction_IsNaN()
        {
            Assert.IsFalse(Find("Satisfaction des pauvres").AddsUp);
            Assert.IsFalse(Find("Prix de vente").AddsUp);
            Assert.IsTrue(float.IsNaN(DebugIndicators.WorldValueOf(History(10, 5), 0, 2, Find("Satisfaction des pauvres"), Fish)));
        }

        [Test]
        public void WorldValue_SkipsNaN()
        {
            Assert.AreEqual(10f, DebugIndicators.WorldValueOf(History(10, float.NaN), 0, 2, Find("Stock"), Fish));
        }
    }
}
