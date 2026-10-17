using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityPopulationTests
    {
        [TestCase(252, 1008, 59, 1100)] // Patrician III's start: 4 × W, 4 × W ÷ 17, houses rounded up
        [TestCase(250, 1000, 58, 1000)]
        [TestCase(17, 68, 4, 100)]
        [TestCase(0, 0, 0, 0)]
        public void AtStart_SplitsTheWorkersFamilies(int workers, int housed, int beggars, int room)
        {
            CityPopulation population = CityPopulation.AtStart(workers, 4, 4d / 17d, 100);
            Assert.AreEqual(housed, population.Housed);
            Assert.AreEqual(beggars, population.Beggars);
            Assert.AreEqual(room, population.Room);
            Assert.AreEqual(housed + beggars, population.Inhabitants);
            Assert.AreEqual(housed + beggars, population.StartingInhabitants);
        }

        [Test]
        public void ConstructedWithTheHousedOnly_HasNoBeggarAndExactlyTheirRoom()
        {
            var population = new CityPopulation(2100);

            Assert.AreEqual(2100, population.Housed);
            Assert.AreEqual(0, population.Beggars);
            Assert.AreEqual(2100, population.Room);
            Assert.AreEqual(2100, population.Inhabitants);
            Assert.AreEqual(0, population.FreeRoom);
        }

        [Test] public void FreeRoom_IsNeverNegative() => Assert.AreEqual(0, new CityPopulation(1000, 0, 800).FreeRoom);

        [Test]
        public void Hire_TurnsFourBeggarsPerWorkerIntoHousedInhabitants()
        {
            var population = new CityPopulation(1000, 40, 1100);
            population.Hire(6, 4);
            Assert.AreEqual(1024, population.Housed);
            Assert.AreEqual(16, population.Beggars);
            Assert.AreEqual(76, population.FreeRoom);
            Assert.Throws<ArgumentOutOfRangeException>(() => population.Hire(5, 4));
        }

        [Test]
        public void SendToBeggars_GivesFourHousedInhabitantsPerWorkerBack()
        {
            var population = new CityPopulation(1000, 0, 1000);
            population.SendToBeggars(4, 4);
            Assert.AreEqual(984, population.Housed);
            Assert.AreEqual(16, population.Beggars);
            Assert.Throws<ArgumentOutOfRangeException>(() => population.SendToBeggars(1000, 4));
        }

        [Test]
        public void GrowthSinceStart_FollowsTheInhabitants()
        {
            var population = new CityPopulation(1000, 0, 1100);
            population.SetBeggars(100);
            Assert.AreEqual(0.1, population.GrowthSinceStart, 1e-9);
            Assert.AreEqual(0, new CityPopulation(0).GrowthSinceStart);
        }

        [Test]
        public void AddRoom_AddsPlaces()
        {
            var population = new CityPopulation(1000);
            population.AddRoom(100);
            Assert.AreEqual(1100, population.Room);
            Assert.Throws<ArgumentOutOfRangeException>(() => population.AddRoom(0));
        }

        [Test]
        public void TheConstructors_RejectNegativeCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(0, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(-1, 4, 0.2, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 0, 0.2, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 4, 0.2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 4, double.NaN, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 4, -0.1, 100));
        }
    }
}
