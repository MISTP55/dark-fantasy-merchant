using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityDayReportTests
    {
        private const int Fish = 0;
        private const int Iron = 1;

        // Two barrels a day for 1 000 inhabitants; a workshop of up to 60 workers making a tenth each.
        // Near 1 000 inhabitants: T0 20, T1 60, T2 120, T3 140.
        private static MarketGood Good => new MarketGood(10, 2, 60, 0.1);

        // The workshop of a one-good market starts with 10 workers.
        private static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 10);

        private static CityPopulation Population(int rich, int wealthy, int poor, int beggars, int richRoom, int wealthyRoom, int poorRoom)
        {
            return new CityPopulation(new PerClass<int>(rich, wealthy, poor), beggars, new PerClass<int>(richRoom, wealthyRoom, poorRoom));
        }

        // Fish: 2 barrels a day for a thousand, not produced. Iron: 2 barrels a day, and a full workshop of 30 workers making 3.
        private static CityMarket FishAndIron(double startingDays)
        {
            return new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 30, 0.1) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                startingDays,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));
        }

        private static CityMarket LabourMarket(double startingDays, CityPopulation population, bool withSocial = false)
        {
            return new CityMarket(new[] { Good }, TestMarkets.Thresholds, Workforce, startingDays,
                TestMarkets.Pricing, 7, population, TestMarkets.Labour, null, withSocial ? TestMarkets.Social : null);
        }

        [Test]
        public void Shortage_IsWhatTheCityNeededAndDidNotGet()
        {
            CityMarket market = FishAndIron(0);

            market.StartDay();

            Assert.AreEqual(2d, market.LastDay.NeededOf(Fish), 1e-9);
            Assert.AreEqual(0d, market.LastDay.ConsumedOf(Fish), 1e-9);
            Assert.AreEqual(2d, market.LastDay.MissingOf(Fish), 1e-9);
        }

        [Test]
        public void Shortage_OfAProducedGood_IsSeenEvenWhenTheEveningStockIsNotEmpty()
        {
            CityMarket market = FishAndIron(0);

            market.StartDay();

            Assert.AreEqual(2d, market.LastDay.MissingOf(Iron), 1e-9);
            Assert.AreEqual(3d, market.LastDay.ProducedOf(Iron), 1e-9);
            Assert.AreEqual(3d, market.StockOf(Iron), 1e-9);
            Assert.IsFalse(market.LastDay.IsHaltedOf(Iron));
        }

        [Test]
        public void AFullStock_HaltsTheWorkshop()
        {
            // 100 days, 200 barrels: above T3.
            CityMarket market = FishAndIron(100);

            market.StartDay();

            Assert.IsTrue(market.LastDay.IsHaltedOf(Iron));
            Assert.AreEqual(0d, market.LastDay.ProducedOf(Iron), 1e-9);
            Assert.IsFalse(market.LastDay.IsHaltedOf(Fish));
        }

        [Test]
        public void Report_IsClearedEveryDay()
        {
            CityMarket market = FishAndIron(0);
            market.StartDay();

            market.Put(Fish, 100);
            market.StartDay();

            Assert.AreEqual(0d, market.LastDay.MissingOf(Fish), 1e-9);
            Assert.AreEqual(2d, market.LastDay.ConsumedOf(Fish), 1e-9);
        }

        [Test]
        public void Report_BeforeAnyDay_IsEmpty()
        {
            CityMarket market = FishAndIron(0);

            Assert.AreEqual(0d, market.LastDay.MissingOf(Fish));
            Assert.AreEqual(0, market.LastDay.HiredOf(Iron));
        }

        [Test]
        public void Hires_AndTheBeggarChange_AreCounted()
        {
            // An empty stock wants 26 workers; 24 places take 6 of the 40 beggars, then 7 come: the change
            // counts those who come or go, not those hired.
            CityMarket market = LabourMarket(0, new CityPopulation(1000, 40, 1024));

            market.StartDay();

            Assert.AreEqual(6, market.LastDay.HiredOf(0));
            Assert.AreEqual(0, market.LastDay.LaidOffOf(0));
            Assert.AreEqual(7, market.LastDay.BeggarChange);
        }

        [Test]
        public void LayOffs_AreCounted()
        {
            // 200 barrels, above T3: the workshop keeps one worker.
            CityMarket market = LabourMarket(100, new CityPopulation(1000));

            market.StartDay();

            Assert.AreEqual(9, market.LastDay.LaidOffOf(0));
            Assert.AreEqual(0, market.LastDay.HiredOf(0));
        }

        [Test]
        public void ClassMoves_AreCounted()
        {
            CityMarket market = LabourMarket(7, Population(60, 180, 600, 0, 200, 400, 700), true);

            market.StartDay();

            Assert.AreEqual(6, market.LastDay.ClassChangeOf(SocialClass.Rich));
            Assert.AreEqual(4, market.LastDay.ClassChangeOf(SocialClass.Wealthy));
            Assert.AreEqual(0, market.LastDay.ClassChangeOf(SocialClass.Poor));
        }

        [Test]
        public void ThePoorWhoLeave_AreCounted_WithTheirWorkers()
        {
            // One rich and one wealthy rise, then 5 poor want to leave: one worker's family.
            CityMarket market = LabourMarket(100, Population(0, 0, 1000, 0, 0, 0, 980), true);

            market.StartDay();

            Assert.AreEqual(4, market.LastDay.PoorLeft);
            Assert.AreEqual(1, market.LastDay.WorkersLeft);
        }

        [Test]
        public void AHouseOfTheCity_IsCounted()
        {
            var material = new MarketGood(10, 0, 0, 0);
            var housing = new CityHousingRule(SocialClass.Poor, 0.95, 100, new[] { new MaterialCost(0, 10), new MaterialCost(1, 10) });
            var market = new CityMarket(new[] { material, material }, TestMarkets.Thresholds, TestMarkets.Workforce, 0,
                TestMarkets.Pricing, 7, new CityPopulation(960, 0, 1000), TestMarkets.Labour, new[] { housing });
            market.Put(0, 25);
            market.Put(1, 12);

            market.StartDay();

            Assert.AreEqual(1, market.LastDay.HousesBuiltOf(SocialClass.Poor));
            Assert.AreEqual(0, market.LastDay.HousesBuiltOf(SocialClass.Rich));
        }

        [Test]
        public void Report_ChangesNoFigureOfTheSimulation()
        {
            CityMarket watched = LabourMarket(3, Population(60, 180, 600, 50, 200, 400, 700), true);
            CityMarket alone = LabourMarket(3, Population(60, 180, 600, 50, 200, 400, 700), true);
            double read = 0d;

            for (int day = 0; day < 60; day++)
            {
                watched.StartDay();
                alone.StartDay();
                read += watched.LastDay.MissingOf(0) + watched.LastDay.HiredOf(0) + watched.LastDay.BeggarChange;
            }

            Assert.AreEqual(alone.StockOf(0), watched.StockOf(0), 1e-9);
            Assert.AreEqual(alone.WorkersOf(0), watched.WorkersOf(0));
            Assert.AreEqual(alone.ReserveOf(0), watched.ReserveOf(0));
            Assert.AreEqual(alone.Population.Beggars, watched.Population.Beggars);

            foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor })
            {
                Assert.AreEqual(alone.Population.CountOf(housedClass), watched.Population.CountOf(housedClass));
                Assert.AreEqual(alone.Population.SatisfactionOf(housedClass), watched.Population.SatisfactionOf(housedClass));
            }

            Assert.IsFalse(double.IsNaN(read));
        }
    }
}
