using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityPopulationTests
    {
        private static readonly SocialClass[] Classes = { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };

        // Patrician III's houses: 40 rich, 70 wealthy, 140 poor.
        private static PerClass<int> HouseRooms => new PerClass<int>(40, 70, 140);

        private static void SetSatisfactions(CityPopulation population, int rich, int wealthy, int poor)
        {
            population.SetSatisfaction(SocialClass.Rich, rich);
            population.SetSatisfaction(SocialClass.Wealthy, wealthy);
            population.SetSatisfaction(SocialClass.Poor, poor);
        }

        [TestCase(252, 1008, 59, 1100)] // Patrician III's start: 4 × W, 4 × W ÷ 17, houses rounded up
        [TestCase(250, 1000, 58, 1000)]
        [TestCase(17, 68, 4, 300)]
        [TestCase(0, 0, 0, 0)]
        public void AtStart_SplitsTheWorkersFamilies(int workers, int housed, int beggars, int room)
        {
            CityPopulation population = CityPopulation.AtStart(workers, 4, 4d / 17d, PerClass<int>.All(100));
            Assert.AreEqual(housed, population.Housed);
            Assert.AreEqual(beggars, population.Beggars);
            Assert.AreEqual(room, population.Room);
            Assert.AreEqual(housed + beggars, population.Inhabitants);
            Assert.AreEqual(housed + beggars, population.StartingInhabitants);
        }

        [Test]
        public void AtStart_SplitsTheHousedAsPatricianIII()
        {
            CityPopulation population = CityPopulation.AtStart(275, 4, 4d / 17d, HouseRooms);

            Assert.AreEqual(110, population.CountOf(SocialClass.Rich));
            Assert.AreEqual(330, population.CountOf(SocialClass.Wealthy));
            Assert.AreEqual(660, population.CountOf(SocialClass.Poor));
            Assert.AreEqual(64, population.Beggars);
            Assert.AreEqual(120, population.RoomOf(SocialClass.Rich));
            Assert.AreEqual(350, population.RoomOf(SocialClass.Wealthy));
            Assert.AreEqual(700, population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(1100, population.Housed);
            Assert.AreEqual(1170, population.Room);
        }

        [Test]
        public void ConstructedWithTheHousedOnly_HasNoBeggarAndExactlyTheirRoom()
        {
            var population = new CityPopulation(2100);

            Assert.AreEqual(2100, population.Housed);
            Assert.AreEqual(2100, population.CountOf(SocialClass.Poor));
            Assert.AreEqual(0, population.CountOf(SocialClass.Rich));
            Assert.AreEqual(0, population.CountOf(SocialClass.Wealthy));
            Assert.AreEqual(0, population.Beggars);
            Assert.AreEqual(2100, population.Room);
            Assert.AreEqual(2100, population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(2100, population.Inhabitants);
            Assert.AreEqual(0, population.FreeRoomOf(SocialClass.Poor));
        }

        [Test]
        public void TheSatisfactions_StartAtZero()
        {
            var population = new CityPopulation(100);

            foreach (SocialClass housedClass in Classes)
            {
                Assert.AreEqual(0, population.SatisfactionOf(housedClass));
            }

            population.SetSatisfaction(SocialClass.Wealthy, -12);
            Assert.AreEqual(-12, population.SatisfactionOf(SocialClass.Wealthy));
        }

        [Test] public void FreeRoom_IsNeverNegative() => Assert.AreEqual(0, new CityPopulation(1000, 0, 800).FreeRoomOf(SocialClass.Poor));

        [Test]
        public void Hire_TurnsBeggarsIntoPoor()
        {
            var population = new CityPopulation(new PerClass<int>(10, 20, 100), 20, HouseRooms);
            population.Hire(2, 4);

            Assert.AreEqual(12, population.Beggars);
            Assert.AreEqual(108, population.CountOf(SocialClass.Poor));
            Assert.AreEqual(10, population.CountOf(SocialClass.Rich));
            Assert.AreEqual(20, population.CountOf(SocialClass.Wealthy));
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
        public void SendToBeggars_SharesTheFamiliesByCountAndSatisfaction()
        {
            var population = new CityPopulation(new PerClass<int>(11, 31, 61), 0, HouseRooms);
            SetSatisfactions(population, 50, 50, 50);

            Assert.AreEqual(0, population.SendToBeggars(1, 4));
            Assert.AreEqual(11, population.CountOf(SocialClass.Rich));
            Assert.AreEqual(30, population.CountOf(SocialClass.Wealthy));
            Assert.AreEqual(58, population.CountOf(SocialClass.Poor));
            Assert.AreEqual(4, population.Beggars);
        }

        [Test]
        public void SendToBeggars_TakesMoreFromAnUnhappyClass()
        {
            var population = new CityPopulation(new PerClass<int>(11, 31, 61), 0, HouseRooms);
            SetSatisfactions(population, 50, 50, -10);

            Assert.AreEqual(0, population.SendToBeggars(1, 4));
            Assert.AreEqual(11, population.CountOf(SocialClass.Rich));
            Assert.AreEqual(31, population.CountOf(SocialClass.Wealthy));
            Assert.AreEqual(57, population.CountOf(SocialClass.Poor));
            Assert.AreEqual(4, population.Beggars);
        }

        [Test]
        public void SendToBeggars_KeepsTheWorkersWhoseFamiliesCannotBeTaken()
        {
            // No class goes below one inhabitant: one of the four comes, the worker stays.
            var population = new CityPopulation(new PerClass<int>(1, 1, 2), 0, HouseRooms);

            Assert.AreEqual(1, population.SendToBeggars(1, 4));
            Assert.AreEqual(3, population.Housed);
            Assert.AreEqual(1, population.Beggars);
        }

        [Test]
        public void Move_AndLeave_ChangeTheClasses()
        {
            var population = new CityPopulation(new PerClass<int>(10, 20, 100), 5, HouseRooms);

            population.Move(SocialClass.Poor, SocialClass.Rich, 5);
            Assert.AreEqual(15, population.CountOf(SocialClass.Rich));
            Assert.AreEqual(95, population.CountOf(SocialClass.Poor));
            Assert.AreEqual(130, population.Housed);

            population.Leave(SocialClass.Poor, 8);
            Assert.AreEqual(87, population.CountOf(SocialClass.Poor));
            Assert.AreEqual(127, population.Inhabitants);
            Assert.AreEqual(5, population.Beggars);
        }

        [Test]
        public void Move_MoreThanTheClassHas_Throws()
        {
            var population = new CityPopulation(new PerClass<int>(10, 20, 100), 0, HouseRooms);

            Assert.Throws<ArgumentOutOfRangeException>(() => population.Move(SocialClass.Rich, SocialClass.Poor, 11));
            Assert.Throws<ArgumentOutOfRangeException>(() => population.Leave(SocialClass.Wealthy, 21));
            Assert.Throws<ArgumentOutOfRangeException>(() => population.Move(SocialClass.Poor, SocialClass.Rich, -1));
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
        public void AddRoom_AddsToTheClassOnly()
        {
            var population = new CityPopulation(1000);
            population.AddRoom(SocialClass.Rich, 40);

            Assert.AreEqual(40, population.RoomOf(SocialClass.Rich));
            Assert.AreEqual(1000, population.RoomOf(SocialClass.Poor));
            Assert.AreEqual(1040, population.Room);
            Assert.Throws<ArgumentOutOfRangeException>(() => population.AddRoom(SocialClass.Poor, 0));
        }

        [Test]
        public void TheConstructors_RejectNegativeCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(0, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(new PerClass<int>(0, -1, 0), 0, HouseRooms));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityPopulation(new PerClass<int>(0, 0, 0), 0, new PerClass<int>(-1, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(-1, 4, 0.2, HouseRooms));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 0, 0.2, HouseRooms));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 4, 0.2, new PerClass<int>(40, 0, 140)));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 4, double.NaN, HouseRooms));
            Assert.Throws<ArgumentOutOfRangeException>(() => CityPopulation.AtStart(10, 4, -0.1, HouseRooms));
        }
    }
}
