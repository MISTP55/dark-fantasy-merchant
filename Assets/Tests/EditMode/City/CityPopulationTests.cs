using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityPopulationTests
    {
        // A house shelters 140.
        private const int HouseRoom = 140;

        [TestCase(252, 1008, 59, 1100)] // 4 × W, 4 × W ÷ 17, houses rounded up
        [TestCase(250, 1000, 58, 1000)]
        [TestCase(17, 68, 4, 100)]
        [TestCase(0, 0, 0, 0)]
        public void AtStart_HousesTheWorkersFamilies(int workers, int housed, int beggars, int room)
        {
            CityPopulation population = CityPopulation.AtStart(workers, 4, 4d / 17d, 100);
            Assert.AreEqual(housed, population.Housed);
            Assert.AreEqual(beggars, population.Beggars);
            Assert.AreEqual(room, population.Room);
            Assert.AreEqual(housed + beggars, population.Inhabitants);
            Assert.AreEqual(housed + beggars, population.StartingInhabitants);
        }

        [Test]
        public void AtStart_HousesEveryFamily_InWholeHouses()
        {
            CityPopulation population = CityPopulation.AtStart(250, 4, 4d / 17d, HouseRoom);

            Assert.AreEqual(1000, population.Housed);
            Assert.AreEqual(58, population.Beggars);
            Assert.AreEqual(1120, population.Room);
        }

        [Test]
        public void RoomAtStart_IsWholeHouses()
        {
            Assert.AreEqual(1120, CityPopulation.RoomAtStart(250, 4, HouseRoom));
            Assert.AreEqual(0, CityPopulation.RoomAtStart(0, 4, HouseRoom));
        }

        [Test]
        public void AtStart_WithRoom_KeepsTheInhabitants_AndTakesTheRoom()
        {
            CityPopulation population = CityPopulation.AtStart(275, 4, 4d / 17d, HouseRoom, 280);

            Assert.AreEqual(1100, population.Housed);
            Assert.AreEqual(64, population.Beggars);
            Assert.AreEqual(280, population.Room);
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

        [Test]
        public void FreeRoom_IsWhatIsLeft_NeverNegative()
        {
            Assert.AreEqual(100, new CityPopulation(900, 0, 1000).FreeRoom);
            Assert.AreEqual(0, new CityPopulation(1000, 0, 800).FreeRoom);
        }

        [Test]
        public void Hire_TurnsBeggarsIntoHoused()
        {
            var population = new CityPopulation(130, 20, 200);
            population.Hire(2, 4);

            Assert.AreEqual(12, population.Beggars);
            Assert.AreEqual(138, population.Housed);
            Assert.Throws<ArgumentOutOfRangeException>(() => population.Hire(4, 4));
        }

        [Test]
        public void SendToBeggars_GivesFourHousedInhabitantsPerWorkerBack()
        {
            var population = new CityPopulation(1000, 0, 1000);
            Assert.AreEqual(0, population.SendToBeggars(4, 4));
            Assert.AreEqual(984, population.Housed);
            Assert.AreEqual(16, population.Beggars);
            Assert.Throws<ArgumentOutOfRangeException>(() => population.SendToBeggars(1000, 4));
        }

        [Test]
        public void SendToBeggars_TakesEveryFamilyAsked_WhenItCan()
        {
            var population = new CityPopulation(100, 0, 200);

            Assert.AreEqual(0, population.SendToBeggars(5, 4));
            Assert.AreEqual(80, population.Housed);
            Assert.AreEqual(20, population.Beggars);
        }

        [Test]
        public void SendToBeggars_NeverTakesTheLastHousedInhabitant()
        {
            // Two families out of nine: one is left.
            var population = new CityPopulation(9, 0, 200);

            Assert.AreEqual(0, population.SendToBeggars(2, 4));
            Assert.AreEqual(1, population.Housed);
            Assert.AreEqual(8, population.Beggars);

            var whole = new CityPopulation(8, 0, 200);

            Assert.AreEqual(1, whole.SendToBeggars(2, 4));
            Assert.AreEqual(4, whole.Housed);
            Assert.AreEqual(4, whole.Beggars);
        }

        [Test]
        public void SendToBeggars_KeepsTheWorkersWhoseFamiliesCannotBeTaken()
        {
            // The housed never fall to nothing: the four cannot all come, so the worker stays with his whole
            // family, still four housed per worker.
            var population = new CityPopulation(4, 0, 200);

            Assert.AreEqual(1, population.SendToBeggars(1, 4));
            Assert.AreEqual(4, population.Housed);
            Assert.AreEqual(0, population.Beggars);

            // The same worker can leave later without a throw.
            Assert.DoesNotThrow(() => population.SendToBeggars(1, 4));
        }

        [Test]
        public void Leave_TakesInhabitantsOutOfTheCity()
        {
            var population = new CityPopulation(130, 5, 200);

            population.Leave(8);
            Assert.AreEqual(122, population.Housed);
            Assert.AreEqual(127, population.Inhabitants);
            Assert.AreEqual(5, population.Beggars);
        }

        [Test]
        public void Leave_MoreThanTheHoused_Throws()
        {
            var population = new CityPopulation(130, 0, 200);

            Assert.Throws<ArgumentOutOfRangeException>(() => population.Leave(131));
            Assert.Throws<ArgumentOutOfRangeException>(() => population.Leave(-1));
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
        public void AddRoom_AddsToTheRoom()
        {
            var population = new CityPopulation(1000);
            population.AddRoom(140);

            Assert.AreEqual(1140, population.Room);
            Assert.Throws<ArgumentOutOfRangeException>(() => population.AddRoom(0));
        }

        [Test]
        public void TheConstructors_RejectNegativeCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(0, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(-1, 4, 0.2, HouseRoom));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 0, 0.2, HouseRoom));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 4, 0.2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 4, double.NaN, HouseRoom));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 4, -0.1, HouseRoom));
        }
    }
}
