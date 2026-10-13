using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityPopulationTests
    {
        private const double Tolerance = 1e-6;

        [Test]
        public void StartsWithItsInhabitants()
        {
            var population = new CityPopulation(2100);

            Assert.AreEqual(2100, population.Count);
            Assert.AreEqual(2100, population.Inhabitants);
        }

        [Test]
        public void Inhabitants_AreTheNearestWholeNumber()
        {
            Assert.AreEqual(1999, new CityPopulation(1999.4).Inhabitants);
            Assert.AreEqual(2000, new CityPopulation(1999.5).Inhabitants);
            Assert.AreEqual(0, new CityPopulation(0).Inhabitants);
        }

        [TestCase(0.05, 2100)]
        [TestCase(-0.1, 1800)]
        [TestCase(0, 2000)]
        public void TheDaysOfAMonth_ChangeThePopulationByTheMonthlyRate(double monthlyRate, double expected)
        {
            var population = new CityPopulation(2000);

            for (int day = 0; day < 30; day++)
            {
                population.GrowForADay(monthlyRate, 30);
            }

            Assert.AreEqual(expected, population.Count, Tolerance);
        }

        [Test]
        public void OneDay_IsASmallShareOfTheMonth()
        {
            var population = new CityPopulation(2000);

            population.GrowForADay(0.05, 30);

            Assert.AreEqual(2000 * Math.Pow(1.05, 1d / 30), population.Count, Tolerance);
            Assert.Greater(population.Count, 2003d);
            Assert.Less(population.Count, 2004d);
        }

        [Test]
        public void AnEmptyCity_StaysEmpty()
        {
            var population = new CityPopulation(0);

            population.GrowForADay(0.05, 30);

            Assert.AreEqual(0, population.Count);
        }

        [Test]
        public void RejectsFiguresThatMakeNoSense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(double.PositiveInfinity));

            var population = new CityPopulation(2000);

            Assert.Throws<ArgumentOutOfRangeException>(() => population.GrowForADay(-1.5, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => population.GrowForADay(double.NaN, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => population.GrowForADay(0.05, 0));
        }
    }
}
