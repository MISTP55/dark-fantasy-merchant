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
            Assert.AreEqual(3d, market.StockOf(Iron), 1e-9);
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
            Assert.AreEqual(0d, market.LastDay.NeededOf(Iron));
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
                read += watched.LastDay.MissingOf(0) + watched.LastDay.NeededOf(0);
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
