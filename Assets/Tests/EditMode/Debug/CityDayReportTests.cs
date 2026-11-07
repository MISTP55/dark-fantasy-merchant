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

        // Fish: 2 barrels a day for a thousand, not produced. Iron: 2 barrels a day, and a full workshop of 30 workers making 3.
        private static City FishAndIron(double startingDays)
        {
            return new City(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 30, 0.1) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                startingDays,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));
        }

        private static City LabourCity(double startingDays, CityPopulation population, bool withLeaving = false)
        {
            return new City(new[] { Good }, TestMarkets.Thresholds, Workforce, startingDays,
                TestMarkets.Pricing, 7, population,
                new CityLabourRules(TestMarkets.Labour, withLeaving ? TestMarkets.Leaving : null));
        }

        [Test]
        public void Shortage_IsWhatTheCityNeededAndDidNotGet()
        {
            City city = FishAndIron(0);

            city.StartDay();

            Assert.AreEqual(2d, city.LastDay.NeededOf(Fish), 1e-9);
            Assert.AreEqual(0d, city.LastDay.ConsumedOf(Fish), 1e-9);
            Assert.AreEqual(2d, city.LastDay.MissingOf(Fish), 1e-9);
        }

        [Test]
        public void Shortage_OfAProducedGood_IsSeenEvenWhenTheEveningStockIsNotEmpty()
        {
            City city = FishAndIron(0);

            city.StartDay();

            Assert.AreEqual(2d, city.LastDay.MissingOf(Iron), 1e-9);
            Assert.AreEqual(3d, city.Market.StockOf(Iron), 1e-9);
        }

        [Test]
        public void Report_IsClearedEveryDay()
        {
            City city = FishAndIron(0);
            city.StartDay();

            city.Market.Put(Fish, 100);
            city.StartDay();

            Assert.AreEqual(0d, city.LastDay.MissingOf(Fish), 1e-9);
            Assert.AreEqual(2d, city.LastDay.ConsumedOf(Fish), 1e-9);
        }

        [Test]
        public void Report_BeforeAnyDay_IsEmpty()
        {
            City city = FishAndIron(0);

            Assert.AreEqual(0d, city.LastDay.MissingOf(Fish));
            Assert.AreEqual(0d, city.LastDay.NeededOf(Iron));
        }

        [Test]
        public void Report_ChangesNoFigureOfTheSimulation()
        {
            City watched = LabourCity(3, new CityPopulation(840, 50, 1300), true);
            City alone = LabourCity(3, new CityPopulation(840, 50, 1300), true);
            double read = 0d;

            for (int day = 0; day < 60; day++)
            {
                watched.StartDay();
                alone.StartDay();
                read += watched.LastDay.MissingOf(0) + watched.LastDay.NeededOf(0);
            }

            Assert.AreEqual(alone.Market.StockOf(0), watched.Market.StockOf(0), 1e-9);
            Assert.AreEqual(alone.WorkersOf(0), watched.WorkersOf(0));
            Assert.AreEqual(alone.ReserveOf(0), watched.ReserveOf(0));
            Assert.AreEqual(alone.Population.Beggars, watched.Population.Beggars);

            Assert.AreEqual(alone.Population.Housed, watched.Population.Housed);

            Assert.IsFalse(double.IsNaN(read));
        }
    }
}
